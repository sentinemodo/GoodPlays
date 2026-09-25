using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Nintendo;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Infrastructure.Services;

public sealed class SwitchSyncService(
    GoodPlaysDbContext dbContext,
    INintendoClient nintendoClient,
    IGameCatalogService gameCatalogService,
    ITokenEncryptionService tokenEncryption,
    ILogger<SwitchSyncService> logger) : ISwitchSyncService
{
    public async Task<SwitchSyncResultDto> SyncAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
        string? onlyExternalId = null)
    {
        var connection = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Platform == PlatformConnectionPlatform.Switch && x.SyncEnabled,
                cancellationToken);
        if (connection is null)
        {
            throw new InvalidOperationException("No active Nintendo Switch connection found for this user.");
        }

        if (string.IsNullOrWhiteSpace(connection.AccessTokenEnc))
        {
            throw new InvalidOperationException("Nintendo Switch connection is missing stored credentials.");
        }

        var sessionToken = tokenEncryption.Decrypt(connection.AccessTokenEnc);
        await ReportAsync(reportProgress, "Fetching from Nintendo Switch", 0, 0, 0, 0, 0, 0, cancellationToken);
        var titles = await nintendoClient.GetPlayHistoryAsync(sessionToken, cancellationToken);
        string? warning = null;
        if (titles.Count == 0)
        {
            warning = "Nintendo returned no played games. Play Activity includes games you have launched, not every purchase.";
        }

        titles = await OrderTitlesAsync(userId, titles, cancellationToken);
        if (!string.IsNullOrWhiteSpace(onlyExternalId))
        {
            titles = titles.Where(title => title.TitleId == onlyExternalId).ToList();
            if (titles.Count == 0)
            {
                throw new InvalidOperationException("This game was not found in the connected Nintendo Switch library.");
            }
        }

        var added = 0;
        var updated = 0;
        var skipped = 0;
        var unmatched = 0;
        foreach (var title in titles)
        {
            var outcome = await ProcessTitleAsync(userId, title, cancellationToken);
            switch (outcome)
            {
                case SyncOutcome.Added:
                    added++;
                    break;
                case SyncOutcome.Updated:
                    updated++;
                    break;
                case SyncOutcome.Skipped:
                    skipped++;
                    break;
                default:
                    unmatched++;
                    break;
            }

            await ReportAsync(
                reportProgress,
                "Updating Nintendo Switch library",
                added + updated + skipped + unmatched,
                titles.Count,
                added,
                updated,
                skipped,
                unmatched,
                cancellationToken);
        }

        var syncedAt = DateTimeOffset.UtcNow;
        connection.LastSyncAt = syncedAt;
        connection.UpdatedAt = syncedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Switch sync for user {UserId}: added={Added}, updated={Updated}, skipped={Skipped}, unmatched={Unmatched}",
            userId,
            added,
            updated,
            skipped,
            unmatched);
        return new SwitchSyncResultDto(added, updated, skipped, unmatched, syncedAt, warning);
    }

    private async Task<IReadOnlyList<NintendoPlayedTitle>> OrderTitlesAsync(
        Guid userId,
        IReadOnlyList<NintendoPlayedTitle> titles,
        CancellationToken cancellationToken)
    {
        var known = await dbContext.LibraryEntries
            .Where(e => e.UserId == userId && e.Source == LibraryEntrySource.SwitchSync && e.PlatformExternalId != null)
            .Select(e => new { e.PlatformExternalId, e.UpdatedAt })
            .ToListAsync(cancellationToken);
        var updatedAt = known.ToDictionary(e => e.PlatformExternalId!, e => e.UpdatedAt);
        return SyncQueue.Order(titles, title => title.TitleId, updatedAt);
    }

    private async Task<SyncOutcome> ProcessTitleAsync(
        Guid userId,
        NintendoPlayedTitle title,
        CancellationToken cancellationToken)
    {
        var game = await gameCatalogService.ResolveForExternalTitleAsync(
            ExternalIdSource.Switch,
            title.TitleId,
            title.Name,
            cancellationToken);
        if (GameBlacklist.IsNonGameApplication(title.Name) && !game.IsHiddenFromCatalog)
        {
            game.IsHiddenFromCatalog = true;
            game.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var lastPlayed = title.LastPlayedAt is not null
            ? DateOnly.FromDateTime(title.LastPlayedAt.Value.UtcDateTime)
            : (DateOnly?)null;
        var entry = await dbContext.LibraryEntries.FirstOrDefaultAsync(
            e => e.UserId == userId &&
                 e.Source == LibraryEntrySource.SwitchSync &&
                 e.PlatformExternalId == title.TitleId,
            cancellationToken);
        if (entry is null)
        {
            var now = DateTimeOffset.UtcNow;
            entry = new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = game.Id,
                Status = LibraryStatusRules.InferFromPlayActivity(title.PlaytimeHours, lastPlayed, LibraryStatus.Owned),
                HoursPlayed = title.PlaytimeHours,
                HoursPlayedSource = HoursPlayedSource.Switch,
                Source = LibraryEntrySource.SwitchSync,
                PlatformExternalId = title.TitleId,
                StartedAt = lastPlayed,
                Visibility = Visibility.Public,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.LibraryEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
            await ProfileActivityRules.RecordPlaytimeAsync(
                dbContext, userId, game.Title, null, entry.HoursPlayed, cancellationToken);
            return SyncOutcome.Added;
        }

        var previousHours = entry.HoursPlayed;
        var changed = false;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        if (entry.GameId != game.Id)
        {
            entry.GameId = game.Id;
            changed = true;
        }

        if (!entry.HoursPlayedLocked && entry.HoursPlayed != title.PlaytimeHours)
        {
            entry.HoursPlayed = title.PlaytimeHours;
            entry.HoursPlayedSource = HoursPlayedSource.Switch;
            changed = true;
        }

        var latest = LibraryStatusRules.PickLatestLastPlayed(entry.StartedAt, lastPlayed);
        if (latest != entry.StartedAt)
        {
            entry.StartedAt = latest;
            changed = true;
        }

        var inferred = LibraryStatusRules.InferFromPlayActivity(entry.HoursPlayed, entry.StartedAt, entry.Status);
        if (inferred != entry.Status)
        {
            entry.Status = inferred;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await ProfileActivityRules.RecordPlaytimeAsync(
                dbContext, userId, game.Title, previousHours, entry.HoursPlayed, cancellationToken);
            return SyncOutcome.Updated;
        }

        return SyncOutcome.Skipped;
    }

    private static Task ReportAsync(
        Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress,
        string phase,
        int processed,
        int total,
        int added,
        int updated,
        int skipped,
        int unmatched,
        CancellationToken cancellationToken)
    {
        if (reportProgress is null)
        {
            return Task.CompletedTask;
        }

        return reportProgress(
            new SyncProgressUpdate(phase, processed, total, added, updated, skipped, unmatched),
            cancellationToken);
    }

    private enum SyncOutcome
    {
        Added,
        Updated,
        Skipped,
        Unmatched
    }
}

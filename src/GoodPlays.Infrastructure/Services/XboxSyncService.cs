using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Xbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Infrastructure.Services;

public sealed class XboxSyncService(
    GoodPlaysDbContext dbContext,
    IXboxClient xboxClient,
    IGameCatalogService gameCatalogService,
    ITokenEncryptionService tokenEncryption,
    ILogger<XboxSyncService> logger) : IXboxSyncService
{
    public async Task<XboxSyncResultDto> SyncAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
        string? onlyExternalId = null)
    {
        var connection = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Platform == PlatformConnectionPlatform.Xbox && x.SyncEnabled,
                cancellationToken);
        if (connection is null)
        {
            throw new InvalidOperationException("No active Xbox connection found for this user.");
        }

        if (string.IsNullOrWhiteSpace(connection.RefreshTokenEnc))
        {
            throw new InvalidOperationException("Xbox connection is missing stored credentials.");
        }

        await ReportAsync(reportProgress, "Fetching from Xbox", 0, 0, 0, 0, 0, 0, cancellationToken);
        var library = await xboxClient.GetLibraryAsync(tokenEncryption.Decrypt(connection.RefreshTokenEnc), cancellationToken);
        connection.RefreshTokenEnc = tokenEncryption.Encrypt(library.RefreshToken);
        connection.ExternalAccountId = library.Xuid;
        connection.DisplayName = library.Gamertag ?? connection.DisplayName;

        string? warning = null;
        if (library.Titles.Count == 0)
        {
            warning = "Xbox returned no played games. Title history includes games you have launched, not every purchase or Game Pass title.";
        }

        var titles = await OrderTitlesAsync(userId, library.Titles, cancellationToken);
        if (!string.IsNullOrWhiteSpace(onlyExternalId))
        {
            titles = titles.Where(title => title.TitleId == onlyExternalId).ToList();
            if (titles.Count == 0)
            {
                throw new InvalidOperationException("This game was not found in the connected Xbox library.");
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
                "Updating Xbox library",
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
            "Xbox sync for user {UserId}: added={Added}, updated={Updated}, skipped={Skipped}, unmatched={Unmatched}",
            userId,
            added,
            updated,
            skipped,
            unmatched);
        return new XboxSyncResultDto(added, updated, skipped, unmatched, syncedAt, warning);
    }

    private async Task<IReadOnlyList<XboxPlayedTitle>> OrderTitlesAsync(
        Guid userId,
        IReadOnlyList<XboxPlayedTitle> titles,
        CancellationToken cancellationToken)
    {
        var known = await dbContext.LibraryEntries
            .Where(e => e.UserId == userId && e.Source == LibraryEntrySource.XboxSync && e.PlatformExternalId != null)
            .Select(e => new { e.PlatformExternalId, e.UpdatedAt })
            .ToListAsync(cancellationToken);
        var updatedAt = known.ToDictionary(e => e.PlatformExternalId!, e => e.UpdatedAt);
        return SyncQueue.Order(titles, title => title.TitleId, updatedAt);
    }

    private async Task<SyncOutcome> ProcessTitleAsync(Guid userId, XboxPlayedTitle title, CancellationToken cancellationToken)
    {
        var game = await gameCatalogService.ResolveForExternalTitleAsync(
            ExternalIdSource.Xbox,
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
                 e.Source == LibraryEntrySource.XboxSync &&
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
                Status = LibraryStatus.Owned,
                HoursPlayed = null,
                HoursPlayedSource = HoursPlayedSource.Manual,
                Source = LibraryEntrySource.XboxSync,
                PlatformExternalId = title.TitleId,
                StartedAt = lastPlayed,
                Visibility = Visibility.Public,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.LibraryEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
            return SyncOutcome.Added;
        }

        var changed = false;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        if (entry.GameId != game.Id)
        {
            entry.GameId = game.Id;
            changed = true;
        }

        var latest = LibraryStatusRules.PickLatestLastPlayed(entry.StartedAt, lastPlayed);
        if (latest != entry.StartedAt)
        {
            entry.StartedAt = latest;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
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

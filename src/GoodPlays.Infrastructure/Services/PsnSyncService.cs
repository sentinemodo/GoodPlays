using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Psn;
using GoodPlays.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Infrastructure.Services;

public sealed class PsnSyncService(
    GoodPlaysDbContext dbContext,
    IPsnClient psnClient,
    IGameCatalogService gameCatalogService,
    ITokenEncryptionService tokenEncryption,
    ILogger<PsnSyncService> logger) : IPsnSyncService
{
    public async Task<PsnSyncResultDto> SyncAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
        string? onlyExternalId = null)
    {
        var connection = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Platform == PlatformConnectionPlatform.Psn && x.SyncEnabled,
                cancellationToken);

        if (connection is null)
        {
            throw new InvalidOperationException("No active PlayStation connection found for this user.");
        }

        if (string.IsNullOrWhiteSpace(connection.AccessTokenEnc) ||
            string.IsNullOrWhiteSpace(connection.RefreshTokenEnc))
        {
            throw new InvalidOperationException("PlayStation connection is missing stored credentials.");
        }

        var accessToken = tokenEncryption.Decrypt(connection.AccessTokenEnc);
        var refreshToken = tokenEncryption.Decrypt(connection.RefreshTokenEnc);
        await ReportAsync(reportProgress, "Fetching from PlayStation", 0, 0, 0, 0, 0, 0, cancellationToken);

        if (connection.TokenExpiresAt is not null && connection.TokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            var refreshed = await psnClient.RefreshTokenAsync(refreshToken, cancellationToken);
            await PersistTokensAsync(connection, refreshed, cancellationToken);
            accessToken = refreshed.AccessToken;
        }

        IReadOnlyList<PsnTitleStat> psnTitles;
        try
        {
            psnTitles = await psnClient.GetPlayedTitlesAsync(
                accessToken,
                connection.ExternalAccountId,
                cancellationToken);
        }
        catch (PsnApiException ex) when (ex.ErrorCode is PsnApiErrorCode.Unauthorized or PsnApiErrorCode.TokenExpired)
        {
            var refreshed = await psnClient.RefreshTokenAsync(refreshToken, cancellationToken);
            await PersistTokensAsync(connection, refreshed, cancellationToken);
            accessToken = refreshed.AccessToken;
            psnTitles = await psnClient.GetPlayedTitlesAsync(
                accessToken,
                connection.ExternalAccountId,
                cancellationToken);
        }

        psnTitles = await OrderTitlesAsync(userId, psnTitles, cancellationToken);
        if (!string.IsNullOrWhiteSpace(onlyExternalId))
        {
            psnTitles = psnTitles.Where(title => title.TitleId == onlyExternalId).ToList();
            if (psnTitles.Count == 0)
            {
                throw new InvalidOperationException("This game was not found in the connected PlayStation library.");
            }
        }

        string? warning = null;
        if (psnTitles.Count == 0)
        {
            warning = "PlayStation returned no played games. Verify your NPSSO token is current and privacy settings allow activity sharing.";
        }

        var added = 0;
        var updated = 0;
        var skipped = 0;
        var unmatched = 0;

        foreach (var psnTitle in psnTitles)
        {
            var outcome = await ProcessTitleAsync(userId, psnTitle, cancellationToken);
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
                "Updating PlayStation library",
                added + updated + skipped + unmatched,
                psnTitles.Count,
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
            "PSN sync for user {UserId}: added={Added}, updated={Updated}, skipped={Skipped}, unmatched={Unmatched}",
            userId,
            added,
            updated,
            skipped,
            unmatched);

        return new PsnSyncResultDto(added, updated, skipped, unmatched, syncedAt, warning);
    }

    private async Task PersistTokensAsync(
        PlatformConnection connection,
        PsnTokens tokens,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        connection.AccessTokenEnc = tokenEncryption.Encrypt(tokens.AccessToken);
        connection.RefreshTokenEnc = tokenEncryption.Encrypt(tokens.RefreshToken);
        connection.TokenExpiresAt = now.AddSeconds(tokens.ExpiresInSeconds);
        connection.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<SyncOutcome> ProcessTitleAsync(
        Guid userId,
        PsnTitleStat psnTitle,
        CancellationToken cancellationToken)
    {
        var game = await gameCatalogService.ResolveForPsnSyncAsync(
            psnTitle.TitleId,
            psnTitle.Name,
            cancellationToken);

        game = await gameCatalogService.EnrichFromIgdbForPsnAsync(game, psnTitle.TitleId, psnTitle.Name, cancellationToken)
            ?? game;

        if (GameBlacklist.IsNonGameApplication(psnTitle.Name) && !game.IsHiddenFromCatalog)
        {
            game.IsHiddenFromCatalog = true;
            game.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var previousHours = (decimal?)null;
        var entry = await dbContext.LibraryEntries
            .FirstOrDefaultAsync(
                e => e.UserId == userId &&
                     e.Source == LibraryEntrySource.PsnSync &&
                     e.PlatformExternalId == psnTitle.TitleId,
                cancellationToken);

        var lastPlayed = psnTitle.LastPlayedAt is not null
            ? DateOnly.FromDateTime(psnTitle.LastPlayedAt.Value.UtcDateTime)
            : (DateOnly?)null;

        if (entry is null)
        {
            var now = DateTimeOffset.UtcNow;
            entry = new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = game.Id,
                Status = LibraryStatusRules.InferFromPlayActivity(
                    psnTitle.PlaytimeHours,
                    lastPlayed,
                    LibraryStatus.Owned),
                HoursPlayed = psnTitle.PlaytimeHours,
                HoursPlayedSource = HoursPlayedSource.Psn,
                Source = LibraryEntrySource.PsnSync,
                PlatformExternalId = psnTitle.TitleId,
                StartedAt = lastPlayed,
                Visibility = Visibility.Public,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.LibraryEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
            await ProfileActivityRules.RecordPlaytimeAsync(
                dbContext, userId, game.Title, previousHours, entry.HoursPlayed, cancellationToken);
            return SyncOutcome.Added;
        }

        previousHours = entry.HoursPlayed;

        var changed = false;
        entry.PlatformExternalId = psnTitle.TitleId;
        entry.UpdatedAt = DateTimeOffset.UtcNow;

        if (entry.GameId != game.Id)
        {
            entry.GameId = game.Id;
            changed = true;
        }

        if (!entry.HoursPlayedLocked)
        {
            if (entry.HoursPlayed != psnTitle.PlaytimeHours)
            {
                entry.HoursPlayed = psnTitle.PlaytimeHours;
                entry.HoursPlayedSource = HoursPlayedSource.Psn;
                changed = true;
            }
        }

        var latestLastPlayed = LibraryStatusRules.PickLatestLastPlayed(entry.StartedAt, lastPlayed);
        if (latestLastPlayed != entry.StartedAt)
        {
            entry.StartedAt = latestLastPlayed;
            changed = true;
        }

        var inferredStatus = LibraryStatusRules.InferFromPlayActivity(
            entry.HoursPlayed,
            entry.StartedAt,
            entry.Status);
        if (inferredStatus != entry.Status)
        {
            entry.Status = inferredStatus;
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

    private async Task<IReadOnlyList<PsnTitleStat>> OrderTitlesAsync(
        Guid userId,
        IReadOnlyList<PsnTitleStat> titles,
        CancellationToken cancellationToken)
    {
        var known = await dbContext.LibraryEntries
            .Where(e => e.UserId == userId && e.Source == LibraryEntrySource.PsnSync && e.PlatformExternalId != null)
            .Select(e => new { e.PlatformExternalId, e.UpdatedAt })
            .ToListAsync(cancellationToken);
        var updatedAt = known.ToDictionary(e => e.PlatformExternalId!, e => e.UpdatedAt);
        return SyncQueue.Order(titles, title => title.TitleId, updatedAt);
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

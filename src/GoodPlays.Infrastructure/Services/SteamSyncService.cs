using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Steam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Infrastructure.Services;

public sealed class SteamSyncService(
    GoodPlaysDbContext dbContext,
    ISteamClient steamClient,
    IGameCatalogService gameCatalogService,
    ITokenEncryptionService tokenEncryption,
    ILogger<SteamSyncService> logger) : ISteamSyncService
{
    public async Task<SteamSyncResultDto> SyncAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
        string? onlyExternalId = null)
    {
        var connection = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Platform == PlatformConnectionPlatform.Steam && x.SyncEnabled,
                cancellationToken);

        if (connection is null)
        {
            throw new InvalidOperationException("No active Steam connection found for this user.");
        }

        if (string.IsNullOrWhiteSpace(connection.AccessTokenEnc))
        {
            throw new InvalidOperationException("Steam connection is missing stored credentials.");
        }

        var apiKey = tokenEncryption.Decrypt(connection.AccessTokenEnc);
        await ReportAsync(reportProgress, "Fetching from Steam", 0, 0, 0, 0, 0, 0, cancellationToken);
        var steamGames = await steamClient.GetOwnedGamesAsync(connection.ExternalAccountId, apiKey, cancellationToken);
        steamGames = await OrderOwnedGamesAsync(userId, steamGames, cancellationToken);
        if (!string.IsNullOrWhiteSpace(onlyExternalId))
        {
            steamGames = steamGames.Where(game => game.AppId.ToString() == onlyExternalId).ToList();
            if (steamGames.Count == 0)
            {
                throw new InvalidOperationException("This game was not found in the connected Steam library.");
            }
        }

        string? warning = null;
        if (steamGames.Count == 0)
        {
            var summary = await steamClient.GetPlayerSummaryAsync(connection.ExternalAccountId, apiKey, cancellationToken);
            if (summary is not null)
            {
                warning =
                    "Steam returned no owned games. Set Game details to Public in Steam privacy settings, or verify your API key.";
            }
        }

        var added = 0;
        var updated = 0;
        var skipped = 0;
        var unmatched = 0;

        foreach (var steamGame in steamGames)
        {
            var outcome = await ProcessGameAsync(userId, steamGame, cancellationToken);
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
                "Updating Steam library",
                added + updated + skipped + unmatched,
                steamGames.Count,
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
            "Steam sync for user {UserId}: added={Added}, updated={Updated}, skipped={Skipped}, unmatched={Unmatched}",
            userId,
            added,
            updated,
            skipped,
            unmatched);

        return new SteamSyncResultDto(added, updated, skipped, unmatched, syncedAt, warning);
    }

    private async Task<SyncOutcome> ProcessGameAsync(
        Guid userId,
        SteamOwnedGame steamGame,
        CancellationToken cancellationToken)
    {
        var game = await gameCatalogService.ResolveForSteamSyncAsync(steamGame.AppId, steamGame.Name, cancellationToken);

        game = await gameCatalogService.EnrichFromIgdbAsync(game, steamGame.AppId, cancellationToken) ?? game;

        if (GameBlacklist.IsNonGameApplication(steamGame.Name) && !game.IsHiddenFromCatalog)
        {
            game.IsHiddenFromCatalog = true;
            game.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var appIdStr = steamGame.AppId.ToString();
        var previousHours = (decimal?)null;
        var entry = await dbContext.LibraryEntries
            .FirstOrDefaultAsync(
                e => e.UserId == userId &&
                     e.Source == LibraryEntrySource.SteamSync &&
                     e.PlatformExternalId == appIdStr,
                cancellationToken);

        var lastPlayed = steamGame.LastPlayedUnix is > 0
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(steamGame.LastPlayedUnix.Value).UtcDateTime)
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
                    steamGame.PlaytimeHours,
                    lastPlayed,
                    LibraryStatus.Owned),
                HoursPlayed = steamGame.PlaytimeHours,
                HoursPlayedSource = HoursPlayedSource.Steam,
                Source = LibraryEntrySource.SteamSync,
                PlatformExternalId = appIdStr,
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
        entry.PlatformExternalId = appIdStr;
        entry.UpdatedAt = DateTimeOffset.UtcNow;

        if (!entry.HoursPlayedLocked)
        {
            if (entry.HoursPlayed != steamGame.PlaytimeHours)
            {
                entry.HoursPlayed = steamGame.PlaytimeHours;
                entry.HoursPlayedSource = HoursPlayedSource.Steam;
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

    private async Task<IReadOnlyList<SteamOwnedGame>> OrderOwnedGamesAsync(
        Guid userId,
        IReadOnlyList<SteamOwnedGame> steamGames,
        CancellationToken cancellationToken)
    {
        var known = await dbContext.LibraryEntries
            .Where(e => e.UserId == userId && e.Source == LibraryEntrySource.SteamSync && e.PlatformExternalId != null)
            .Select(e => new { e.PlatformExternalId, e.UpdatedAt })
            .ToListAsync(cancellationToken);
        var updatedAt = known.ToDictionary(e => e.PlatformExternalId!, e => e.UpdatedAt);
        return SyncQueue.Order(steamGames, game => game.AppId.ToString(), updatedAt);
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

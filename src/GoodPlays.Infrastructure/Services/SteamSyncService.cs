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
    public async Task<SteamSyncResultDto> SyncAsync(Guid userId, CancellationToken cancellationToken)
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
        var steamGames = await steamClient.GetOwnedGamesAsync(connection.ExternalAccountId, apiKey, cancellationToken);

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

        if (game.MetadataStatus == MetadataStatus.Pending || string.IsNullOrEmpty(game.CoverUrl))
        {
            game = await gameCatalogService.EnrichFromIgdbAsync(game, steamGame.AppId, cancellationToken) ?? game;
        }

        var appIdStr = steamGame.AppId.ToString();
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
                Status = steamGame.PlaytimeForeverMinutes > 0 ? LibraryStatus.Playing : LibraryStatus.Owned,
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
            return SyncOutcome.Added;
        }

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

        if (lastPlayed is not null && entry.StartedAt is null)
        {
            entry.StartedAt = lastPlayed;
            changed = true;
        }

        if (steamGame.PlaytimeForeverMinutes > 0 && entry.Status == LibraryStatus.Backlog)
        {
            entry.Status = LibraryStatus.Playing;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return SyncOutcome.Updated;
        }

        return SyncOutcome.Skipped;
    }

    private enum SyncOutcome
    {
        Added,
        Updated,
        Skipped,
        Unmatched
    }
}

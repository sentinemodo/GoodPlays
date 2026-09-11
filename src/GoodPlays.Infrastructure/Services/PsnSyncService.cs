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
    public async Task<PsnSyncResultDto> SyncAsync(Guid userId, CancellationToken cancellationToken)
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
        game = await ReconcileWithExistingLibraryGameAsync(userId, game, psnTitle, cancellationToken);

        if (game.MetadataStatus == MetadataStatus.Pending)
        {
            game = await gameCatalogService.EnrichFromIgdbForPsnAsync(game, psnTitle.TitleId, psnTitle.Name, cancellationToken)
                ?? game;
        }

        var entry = await dbContext.LibraryEntries
            .FirstOrDefaultAsync(e => e.UserId == userId && e.GameId == game.Id, cancellationToken);

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
                Status = psnTitle.PlaytimeHours > 0 ? LibraryStatus.Playing : LibraryStatus.Owned,
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
            return SyncOutcome.Added;
        }

        var changed = false;
        entry.PlatformExternalId = psnTitle.TitleId;
        entry.UpdatedAt = DateTimeOffset.UtcNow;

        if (!entry.HoursPlayedLocked)
        {
            if (entry.HoursPlayed != psnTitle.PlaytimeHours)
            {
                entry.HoursPlayed = psnTitle.PlaytimeHours;
                entry.HoursPlayedSource = HoursPlayedSource.Psn;
                changed = true;
            }
        }

        if (lastPlayed is not null && entry.StartedAt is null)
        {
            entry.StartedAt = lastPlayed;
            changed = true;
        }

        if (psnTitle.PlaytimeHours > 0 && entry.Status == LibraryStatus.Backlog)
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

    private async Task<Game> ReconcileWithExistingLibraryGameAsync(
        Guid userId,
        Game resolvedGame,
        PsnTitleStat psnTitle,
        CancellationToken cancellationToken)
    {
        var normalizedTitle = PsnTitleNormalizer.Normalize(psnTitle.Name);
        var userEntries = await dbContext.LibraryEntries
            .Include(e => e.Game)
            .ThenInclude(g => g.ExternalIds)
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        var titleMatch = userEntries.FirstOrDefault(e =>
            PsnTitleNormalizer.Normalize(e.Game.Title) == normalizedTitle);

        if (titleMatch is null || titleMatch.GameId == resolvedGame.Id)
        {
            return resolvedGame;
        }

        var existingGame = titleMatch.Game;
        var preferred = HasIgdbMetadata(existingGame) && !HasIgdbMetadata(resolvedGame)
            ? existingGame
            : resolvedGame.MetadataStatus == MetadataStatus.Complete && !HasIgdbMetadata(existingGame)
                ? resolvedGame
                : HasIgdbMetadata(existingGame)
                    ? existingGame
                    : resolvedGame;

        if (preferred.MetadataStatus == MetadataStatus.Pending)
        {
            preferred = await gameCatalogService.EnrichFromIgdbForPsnAsync(
                    preferred,
                    psnTitle.TitleId,
                    psnTitle.Name,
                    cancellationToken)
                ?? preferred;
        }

        if (titleMatch.GameId != preferred.Id)
        {
            var targetExists = userEntries.Any(e => e.GameId == preferred.Id);
            if (!targetExists)
            {
                titleMatch.GameId = preferred.Id;
                titleMatch.UpdatedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                MergeLibraryEntryData(userEntries.First(e => e.GameId == preferred.Id), titleMatch, psnTitle);
                dbContext.LibraryEntries.Remove(titleMatch);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return preferred;
    }

    private static bool HasIgdbMetadata(Game game) =>
        game.MetadataStatus == MetadataStatus.Complete ||
        game.ExternalIds.Any(x => x.Source == ExternalIdSource.Igdb);

    private static void MergeLibraryEntryData(
        LibraryEntry target,
        LibraryEntry duplicate,
        PsnTitleStat psnTitle)
    {
        if (!target.HoursPlayedLocked && duplicate.HoursPlayed is not null)
        {
            target.HoursPlayed = duplicate.HoursPlayed;
            target.HoursPlayedSource = duplicate.HoursPlayedSource;
        }

        target.StartedAt ??= duplicate.StartedAt;
        target.Rating ??= duplicate.Rating;
        target.PlatformExternalId = psnTitle.TitleId;
        target.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private enum SyncOutcome
    {
        Added,
        Updated,
        Skipped,
        Unmatched
    }
}

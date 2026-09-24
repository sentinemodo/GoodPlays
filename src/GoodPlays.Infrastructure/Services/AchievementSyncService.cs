using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Steam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Infrastructure.Services;

public sealed class AchievementSyncService(
    GoodPlaysDbContext dbContext,
    ISteamClient steamClient,
    ITokenEncryptionService encryption,
    IGameEnrichmentService enrichmentService,
    ILogger<AchievementSyncService> logger) : IAchievementSyncService
{
    public async Task SyncUserSteamAchievementsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.PlatformConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Platform == PlatformConnectionPlatform.Steam, cancellationToken);

        if (connection is null || string.IsNullOrWhiteSpace(connection.AccessTokenEnc))
        {
            return;
        }

        var apiKey = encryption.Decrypt(connection.AccessTokenEnc);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var steamId = connection.ExternalAccountId;
        var libraryGames = await dbContext.LibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Source == LibraryEntrySource.SteamSync)
            .Select(e => new { e.GameId, e.PlatformExternalId })
            .ToListAsync(cancellationToken);

        foreach (var entry in libraryGames)
        {
            if (!uint.TryParse(entry.PlatformExternalId, out var appId))
            {
                continue;
            }

            try
            {
                await enrichmentService.EnrichAchievementsAsync(entry.GameId, cancellationToken);

                var result = await steamClient.GetPlayerAchievementsAsync(steamId, appId, apiKey, cancellationToken);
                if (result?.Achievements is null)
                {
                    continue;
                }

                var achievementMap = await dbContext.Achievements
                    .Where(a => a.GameId == entry.GameId)
                    .ToDictionaryAsync(a => a.ExternalId, a => a.Id, cancellationToken);

                foreach (var unlocked in result.Achievements.Where(a => a.Achieved))
                {
                    if (!achievementMap.TryGetValue(unlocked.ApiName, out var achievementId))
                    {
                        continue;
                    }

                    var exists = await dbContext.UserAchievements.AnyAsync(
                        ua => ua.UserId == userId && ua.AchievementId == achievementId,
                        cancellationToken);
                    if (exists)
                    {
                        continue;
                    }

                    dbContext.UserAchievements.Add(new UserAchievement
                    {
                        UserId = userId,
                        AchievementId = achievementId,
                        UnlockedAt = unlocked.UnlockTimeUnix is null
                            ? DateTimeOffset.UtcNow
                            : DateTimeOffset.FromUnixTimeSeconds(unlocked.UnlockTimeUnix.Value)
                    });
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Achievement sync failed for user {UserId} app {AppId}", userId, appId);
            }
        }
    }
}

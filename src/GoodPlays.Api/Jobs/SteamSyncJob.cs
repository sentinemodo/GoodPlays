using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Jobs;

public sealed class SteamSyncJob(
    ISteamSyncService steamSyncService,
    IAchievementSyncService achievementSyncService)
{
    [Queue("default")]
    public async Task<SteamSyncResultDto> RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await steamSyncService.SyncAsync(userId, cancellationToken);
        await achievementSyncService.SyncUserSteamAchievementsAsync(userId, cancellationToken);
        return result;
    }
}

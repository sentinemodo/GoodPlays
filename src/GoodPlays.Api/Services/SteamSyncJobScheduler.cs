using GoodPlays.Api.Jobs;
using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Services;

public interface ISteamSyncJobScheduler
{
    Task<SteamSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record SteamSyncScheduleResult(bool Queued, SteamSyncResultDto? Result);

public sealed class HangfireSteamSyncJobScheduler(IBackgroundJobClient backgroundJobClient) : ISteamSyncJobScheduler
{
    public Task<SteamSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        backgroundJobClient.Enqueue<SteamSyncJob>(job => job.RunAsync(userId, CancellationToken.None));
        return Task.FromResult(new SteamSyncScheduleResult(true, null));
    }
}

public sealed class InlineSteamSyncJobScheduler(
    ISteamSyncService steamSyncService,
    IAchievementSyncService achievementSyncService) : ISteamSyncJobScheduler
{
    public async Task<SteamSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await steamSyncService.SyncAsync(userId, cancellationToken);
        await achievementSyncService.SyncUserSteamAchievementsAsync(userId, cancellationToken);
        return new SteamSyncScheduleResult(false, result);
    }
}

using GoodPlays.Api.Jobs;
using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Services;

public interface ISwitchSyncJobScheduler
{
    Task<SwitchSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record SwitchSyncScheduleResult(bool Queued, SwitchSyncResultDto? Result);

public sealed class HangfireSwitchSyncJobScheduler(IBackgroundJobClient backgroundJobClient) : ISwitchSyncJobScheduler
{
    public Task<SwitchSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        backgroundJobClient.Enqueue<SwitchSyncJob>(job => job.RunAsync(userId, CancellationToken.None));
        return Task.FromResult(new SwitchSyncScheduleResult(true, null));
    }
}

public sealed class InlineSwitchSyncJobScheduler(ISwitchSyncService switchSyncService) : ISwitchSyncJobScheduler
{
    public async Task<SwitchSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await switchSyncService.SyncAsync(userId, cancellationToken);
        return new SwitchSyncScheduleResult(false, result);
    }
}

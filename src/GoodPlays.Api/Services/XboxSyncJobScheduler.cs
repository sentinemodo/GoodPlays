using GoodPlays.Api.Jobs;
using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Services;

public interface IXboxSyncJobScheduler
{
    Task<XboxSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record XboxSyncScheduleResult(bool Queued, XboxSyncResultDto? Result);

public sealed class HangfireXboxSyncJobScheduler(IBackgroundJobClient backgroundJobClient) : IXboxSyncJobScheduler
{
    public Task<XboxSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        backgroundJobClient.Enqueue<XboxSyncJob>(job => job.RunAsync(userId, CancellationToken.None));
        return Task.FromResult(new XboxSyncScheduleResult(true, null));
    }
}

public sealed class InlineXboxSyncJobScheduler(IXboxSyncService xboxSyncService) : IXboxSyncJobScheduler
{
    public async Task<XboxSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await xboxSyncService.SyncAsync(userId, cancellationToken);
        return new XboxSyncScheduleResult(false, result);
    }
}

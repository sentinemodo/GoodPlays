using GoodPlays.Api.Jobs;
using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Services;

public interface IPsnSyncJobScheduler
{
    Task<PsnSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record PsnSyncScheduleResult(bool Queued, PsnSyncResultDto? Result);

public sealed class HangfirePsnSyncJobScheduler(IBackgroundJobClient backgroundJobClient) : IPsnSyncJobScheduler
{
    public Task<PsnSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        backgroundJobClient.Enqueue<PsnSyncJob>(job => job.RunAsync(userId, CancellationToken.None));
        return Task.FromResult(new PsnSyncScheduleResult(true, null));
    }
}

public sealed class InlinePsnSyncJobScheduler(IPsnSyncService psnSyncService) : IPsnSyncJobScheduler
{
    public async Task<PsnSyncScheduleResult> ScheduleSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await psnSyncService.SyncAsync(userId, cancellationToken);
        return new PsnSyncScheduleResult(false, result);
    }
}

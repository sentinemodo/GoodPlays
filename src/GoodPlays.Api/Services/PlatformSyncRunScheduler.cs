using GoodPlays.Api.Jobs;
using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Services;

public interface IPlatformSyncRunScheduler
{
    void Schedule(Guid runId);
}

public sealed class HangfirePlatformSyncRunScheduler(IBackgroundJobClient backgroundJobClient) : IPlatformSyncRunScheduler
{
    public void Schedule(Guid runId) =>
        backgroundJobClient.Enqueue<PlatformSyncRunJob>(job => job.RunAsync(runId, CancellationToken.None));
}

public sealed class InlinePlatformSyncRunScheduler(IServiceScopeFactory scopeFactory) : IPlatformSyncRunScheduler
{
    public void Schedule(Guid runId)
    {
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IPlatformSyncRunService>();
            await service.ExecuteAsync(runId, CancellationToken.None);
        });
    }
}

using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Jobs;

public sealed class PlatformSyncRunJob(IPlatformSyncRunService platformSyncRunService)
{
    [Queue("default")]
    public Task RunAsync(Guid runId, CancellationToken cancellationToken) =>
        platformSyncRunService.ExecuteAsync(runId, cancellationToken);
}

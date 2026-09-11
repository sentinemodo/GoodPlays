using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Jobs;

public sealed class PsnSyncJob(IPsnSyncService psnSyncService)
{
    [Queue("default")]
    public Task<PsnSyncResultDto> RunAsync(Guid userId, CancellationToken cancellationToken) =>
        psnSyncService.SyncAsync(userId, cancellationToken);
}

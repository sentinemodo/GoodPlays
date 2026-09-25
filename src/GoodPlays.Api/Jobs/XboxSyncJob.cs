using GoodPlays.Infrastructure.Services;

namespace GoodPlays.Api.Jobs;

public sealed class XboxSyncJob(IXboxSyncService xboxSyncService)
{
    public Task<XboxSyncResultDto> RunAsync(Guid userId, CancellationToken cancellationToken) =>
        xboxSyncService.SyncAsync(userId, cancellationToken);
}

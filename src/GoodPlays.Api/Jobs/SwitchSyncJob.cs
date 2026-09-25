using GoodPlays.Infrastructure.Services;

namespace GoodPlays.Api.Jobs;

public sealed class SwitchSyncJob(ISwitchSyncService switchSyncService)
{
    public Task<SwitchSyncResultDto> RunAsync(Guid userId, CancellationToken cancellationToken) =>
        switchSyncService.SyncAsync(userId, cancellationToken);
}

using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Jobs;

public sealed class SteamSyncJob(ISteamSyncService steamSyncService)
{
    [Queue("default")]
    public Task<SteamSyncResultDto> RunAsync(Guid userId, CancellationToken cancellationToken) =>
        steamSyncService.SyncAsync(userId, cancellationToken);
}

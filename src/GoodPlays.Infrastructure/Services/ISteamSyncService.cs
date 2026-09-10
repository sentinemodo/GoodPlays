namespace GoodPlays.Infrastructure.Services;

public interface ISteamSyncService
{
    Task<SteamSyncResultDto> SyncAsync(Guid userId, CancellationToken cancellationToken);
}

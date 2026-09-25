namespace GoodPlays.Infrastructure.Services;

public interface ISteamSyncService
{
    Task<SteamSyncResultDto> SyncAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
        string? onlyExternalId = null);
}

namespace GoodPlays.Infrastructure.Services;

public interface ISwitchSyncService
{
    Task<SwitchSyncResultDto> SyncAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
        string? onlyExternalId = null);
}

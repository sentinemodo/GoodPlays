namespace GoodPlays.Infrastructure.Services;

public interface IXboxSyncService
{
    Task<XboxSyncResultDto> SyncAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
        string? onlyExternalId = null);
}

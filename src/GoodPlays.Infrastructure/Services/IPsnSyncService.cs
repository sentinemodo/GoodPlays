namespace GoodPlays.Infrastructure.Services;

public interface IPsnSyncService
{
    Task<PsnSyncResultDto> SyncAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
        string? onlyExternalId = null);
}

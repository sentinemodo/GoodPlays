namespace GoodPlays.Infrastructure.Services;

public interface IPsnSyncService
{
    Task<PsnSyncResultDto> SyncAsync(Guid userId, CancellationToken cancellationToken);
}

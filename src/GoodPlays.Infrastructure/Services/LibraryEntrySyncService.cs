using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public interface ILibraryEntrySyncService
{
    Task SyncAsync(Guid userId, Guid entryId, CancellationToken cancellationToken);
}

public sealed class LibraryEntrySyncService(
    GoodPlaysDbContext dbContext,
    ISteamSyncService steamSyncService,
    IPsnSyncService psnSyncService,
    IXboxSyncService xboxSyncService,
    ISwitchSyncService switchSyncService) : ILibraryEntrySyncService
{
    public async Task SyncAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await dbContext.LibraryEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Library entry was not found.");

        if (!LibrarySourceLabels.IsPlatformSync(entry.Source))
        {
            throw new InvalidOperationException("Manually added games cannot be synced from a platform.");
        }

        if (string.IsNullOrWhiteSpace(entry.PlatformExternalId))
        {
            throw new InvalidOperationException("This game has no platform id to sync.");
        }

        switch (entry.Source)
        {
            case LibraryEntrySource.SteamSync:
                await steamSyncService.SyncAsync(userId, cancellationToken, onlyExternalId: entry.PlatformExternalId);
                break;
            case LibraryEntrySource.PsnSync:
                await psnSyncService.SyncAsync(userId, cancellationToken, onlyExternalId: entry.PlatformExternalId);
                break;
            case LibraryEntrySource.XboxSync:
                await xboxSyncService.SyncAsync(userId, cancellationToken, onlyExternalId: entry.PlatformExternalId);
                break;
            case LibraryEntrySource.SwitchSync:
                await switchSyncService.SyncAsync(userId, cancellationToken, onlyExternalId: entry.PlatformExternalId);
                break;
        }
    }
}

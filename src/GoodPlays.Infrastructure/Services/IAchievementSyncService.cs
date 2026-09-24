namespace GoodPlays.Infrastructure.Services;

public interface IAchievementSyncService
{
    Task SyncUserSteamAchievementsAsync(Guid userId, CancellationToken cancellationToken);
}

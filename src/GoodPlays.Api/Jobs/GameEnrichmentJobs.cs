using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Jobs;

public sealed class GameEnrichmentJobs(IGameEnrichmentService enrichmentService)
{
    public Task RunWeeklyEnrichmentAsync(CancellationToken cancellationToken) =>
        enrichmentService.RunWeeklyStaleEnrichmentAsync(cancellationToken);

    public static void RegisterWeekly(IRecurringJobManager recurringJobs)
    {
        recurringJobs.AddOrUpdate<GameEnrichmentJobs>(
            "weekly-game-enrichment",
            job => job.RunWeeklyEnrichmentAsync(CancellationToken.None),
            Cron.Weekly(DayOfWeek.Monday, 4));
    }
}

public sealed class AchievementSyncJob(IAchievementSyncService achievementSyncService)
{
    public Task SyncUserAsync(Guid userId, CancellationToken cancellationToken) =>
        achievementSyncService.SyncUserSteamAchievementsAsync(userId, cancellationToken);
}

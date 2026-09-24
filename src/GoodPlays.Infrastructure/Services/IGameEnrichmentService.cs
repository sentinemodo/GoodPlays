namespace GoodPlays.Infrastructure.Services;

public interface IGameEnrichmentService
{
    Task EnrichIfStaleAsync(Guid gameId, CancellationToken cancellationToken);

    Task EnrichRatingsAsync(Guid gameId, CancellationToken cancellationToken);

    Task EnrichNewsAsync(Guid gameId, CancellationToken cancellationToken);

    Task EnrichAchievementsAsync(Guid gameId, CancellationToken cancellationToken);

    Task RunWeeklyStaleEnrichmentAsync(CancellationToken cancellationToken);
}

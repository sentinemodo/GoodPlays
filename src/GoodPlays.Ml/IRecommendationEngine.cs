namespace GoodPlays.Ml;

public interface IRecommendationEngine
{
    Task<IReadOnlyList<RecommendationResult>> GetRecommendationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record RecommendationResult(Guid GameId, double Score, string? Reason);

public sealed record HealthStatus(bool IsHealthy, string Message);

namespace GoodPlays.Ml;

public interface IRecommendationEngine
{
    Task<IReadOnlyList<RecommendationResult>> GetRecommendationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record RecommendationResult(
    Guid GameId,
    string Title,
    string? CoverUrl,
    double Score,
    string? Reason);

public sealed record HealthStatus(bool IsHealthy, string Message);

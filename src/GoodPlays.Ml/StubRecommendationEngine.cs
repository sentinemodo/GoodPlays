using Microsoft.Extensions.Logging;

namespace GoodPlays.Ml;

public sealed class StubRecommendationEngine(ILogger<StubRecommendationEngine> logger) : IRecommendationEngine
{
    public Task<IReadOnlyList<RecommendationResult>> GetRecommendationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // TODO(architecture): ml-recommendations.md — ML.NET hybrid inference (Phase 3)
        logger.LogDebug("Stub recommendation engine invoked for user {UserId}", userId);
        return Task.FromResult<IReadOnlyList<RecommendationResult>>(Array.Empty<RecommendationResult>());
    }

    public Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HealthStatus(true, "ML.NET stub ready — no model loaded (Phase 0)"));
    }
}

using GoodPlays.Ml;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/recommendations")]
public class RecommendationsController(IRecommendationEngine recommendationEngine) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecommendations(CancellationToken cancellationToken)
    {
        // TODO(architecture): ml-recommendations.md — research agent reco (Phase 1)
        var userId = Guid.Empty;
        var results = await recommendationEngine.GetRecommendationsAsync(userId, cancellationToken);
        return Ok(results);
    }
}

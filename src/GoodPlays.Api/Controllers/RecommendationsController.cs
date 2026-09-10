using GoodPlays.Api.Services;
using GoodPlays.Ml;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/recommendations")]
public class RecommendationsController(
    IRecommendationEngine recommendationEngine,
    ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecommendations(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var results = await recommendationEngine.GetRecommendationsAsync(user.Id, cancellationToken);
        return Ok(results);
    }
}

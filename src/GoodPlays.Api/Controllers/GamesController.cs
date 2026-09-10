using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/games")]
public class GamesController(IGameCatalogService gameCatalogService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var results = await gameCatalogService.SearchAsync(q ?? string.Empty, cancellationToken);
        return Ok(results);
    }

    [HttpPost]
    public async Task<IActionResult> ImportGame([FromBody] ImportGameRequest request, CancellationToken cancellationToken)
    {
        if (request.IgdbId is null or <= 0)
        {
            return BadRequest(new { message = "igdbId is required." });
        }

        var game = await gameCatalogService.ImportFromIgdbAsync(request.IgdbId.Value, cancellationToken);
        if (game is null)
        {
            return NotFound(new { message = "Game not found or IGDB is not configured." });
        }

        return Ok(new
        {
            game.Id,
            game.Title,
            game.Slug,
            game.CoverUrl
        });
    }

    public sealed record ImportGameRequest(long? IgdbId);
}

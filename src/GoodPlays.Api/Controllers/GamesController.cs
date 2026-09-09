using GoodPlays.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/games")]
public class GamesController(GoodPlaysDbContext dbContext) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        // TODO(architecture): enrichment-pipeline — IGDB search + pg_trgm fallback (Phase 0)
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<object>());
        }

        var normalized = q.Trim().ToLowerInvariant();
        var results = await dbContext.Games
            .AsNoTracking()
            .Where(g => EF.Functions.ILike(g.SortTitle, $"%{normalized}%") || EF.Functions.ILike(g.Title, $"%{normalized}%"))
            .OrderBy(g => g.SortTitle)
            .Select(g => new { g.Id, g.Title, g.Slug, g.CoverUrl })
            .Take(25)
            .ToListAsync(cancellationToken);

        return Ok(results);
    }
}

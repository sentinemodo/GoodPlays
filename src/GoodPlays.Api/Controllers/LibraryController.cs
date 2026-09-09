using GoodPlays.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/library")]
public class LibraryController(GoodPlaysDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLibrary(CancellationToken cancellationToken)
    {
        // TODO(architecture): data-model.md — library CRUD with Clerk user context (Phase 0)
        var entries = await dbContext.LibraryEntries
            .AsNoTracking()
            .Select(e => new
            {
                e.Id,
                e.GameId,
                e.Status,
                e.Rating,
                e.HoursPlayed
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }

    [HttpPost]
    public IActionResult CreateLibraryEntry()
    {
        // TODO(architecture): modules-and-integrations — manual add flow (Phase 0)
        return StatusCode(StatusCodes.Status501NotImplemented, new { message = "Library create not implemented (Phase 0 stub)." });
    }
}

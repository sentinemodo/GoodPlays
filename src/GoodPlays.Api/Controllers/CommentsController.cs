using GoodPlays.Api.Services;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/games/{slug}/comments")]
public class CommentsController(
    GoodPlaysDbContext dbContext,
    ICommentService commentService,
    ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string slug, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var gameId = await ResolveGameIdAsync(slug, cancellationToken);
        if (gameId is null)
        {
            return NotFound();
        }

        var comments = await commentService.GetForGameAsync(gameId.Value, page, pageSize, cancellationToken);
        return Ok(comments);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string slug, [FromBody] CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var gameId = await ResolveGameIdAsync(slug, cancellationToken);
        if (gameId is null)
        {
            return NotFound();
        }

        var comment = await commentService.CreateAsync(user.Id, gameId.Value, request, cancellationToken);
        if (comment is null)
        {
            return BadRequest(new { message = "Could not create comment." });
        }

        return Created($"/api/v1/games/{slug}/comments/{comment.Id}", comment);
    }

    private async Task<Guid?> ResolveGameIdAsync(string slug, CancellationToken cancellationToken) =>
        await dbContext.Games.AsNoTracking().Where(g => g.Slug == slug).Select(g => g.Id).FirstOrDefaultAsync(cancellationToken) is var id && id != Guid.Empty
            ? id
            : null;
}

using GoodPlays.Api.Services;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/tags")]
public class TagsController(
    ITagService tagService,
    ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        var tags = await tagService.GetTagsAsync(user?.Id, cancellationToken);
        return Ok(tags);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request, CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var tag = await tagService.CreateTagAsync(user.Id, request, cancellationToken);
        if (tag is null)
        {
            return Conflict(new { message = "Tag already exists or name is invalid." });
        }

        return Created($"/api/v1/tags/{tag.Slug}", tag);
    }

    [HttpPost("{tagSlug}/games/{gameId:guid}")]
    public async Task<IActionResult> TagGame(string tagSlug, Guid gameId, CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var added = await tagService.AddTagToGameAsync(user.Id, gameId, tagSlug, cancellationToken);
        if (!added)
        {
            return NotFound(new { message = "Tag or game not found." });
        }

        return NoContent();
    }

    [HttpDelete("{tagSlug}/games/{gameId:guid}")]
    public async Task<IActionResult> UntagGame(string tagSlug, Guid gameId, CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var removed = await tagService.RemoveTagFromGameAsync(user.Id, gameId, tagSlug, cancellationToken);
        if (!removed)
        {
            return NotFound(new { message = "Tag or game not found." });
        }

        return NoContent();
    }
}

[ApiController]
[Route("api/v1/shelves")]
public class ShelvesController(
    ITagService tagService,
    ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var shelves = await tagService.GetShelvesAsync(user.Id, cancellationToken);
        return Ok(shelves);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShelfRequest request, CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var shelf = await tagService.CreateShelfAsync(user.Id, request, cancellationToken);
        if (shelf is null)
        {
            return Conflict(new { message = "Shelf already exists or name is invalid." });
        }

        return Created($"/api/v1/shelves/{shelf.Slug}", shelf);
    }

    [HttpPost("{shelfSlug}/entries/{gameId:guid}")]
    public async Task<IActionResult> AddEntry(string shelfSlug, Guid gameId, CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var added = await tagService.AddGameToShelfAsync(user.Id, shelfSlug, gameId, cancellationToken);
        if (!added)
        {
            return NotFound(new { message = "Shelf or game not found." });
        }

        return NoContent();
    }
}

using GoodPlays.Api.Services;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/library")]
public class LibraryController(
    ILibraryService libraryService,
    ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLibrary(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var entries = await libraryService.GetForUserAsync(user.Id, cancellationToken);
        return Ok(entries);
    }

    [HttpPost]
    public async Task<IActionResult> CreateLibraryEntry(
        [FromBody] CreateLibraryEntryRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (request.GameId == Guid.Empty)
        {
            return BadRequest(new { message = "gameId is required." });
        }

        var entry = await libraryService.CreateAsync(user.Id, request, cancellationToken);
        if (entry is null)
        {
            return Conflict(new { message = "Game not found or already in library." });
        }

        return Created($"/api/v1/library/{entry.Id}", entry);
    }

    [HttpPut("{entryId:guid}")]
    public async Task<IActionResult> UpdateLibraryEntry(
        Guid entryId,
        [FromBody] UpdateLibraryEntryRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var entry = await libraryService.UpdateAsync(user.Id, entryId, request, cancellationToken);
        if (entry is null)
        {
            return NotFound();
        }

        return Ok(entry);
    }

    [HttpDelete("{entryId:guid}")]
    public async Task<IActionResult> DeleteLibraryEntry(Guid entryId, CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var removed = await libraryService.DeleteAsync(user.Id, entryId, cancellationToken);
        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPut("{entryId:guid}/loved")]
    public async Task<IActionResult> SetLoved(
        Guid entryId,
        [FromBody] SetLovedRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var updated = await libraryService.SetLovedAsync(user.Id, entryId, request.Loved, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    public sealed record SetLovedRequest(bool Loved);
}

using GoodPlays.Api.Services;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
public class ProfileController(IProfileService profileService, ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProfile(
        [FromQuery] Guid? gameId,
        [FromQuery] TrophyClass? trophyClass,
        [FromQuery] decimal? maxRarity,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var profile = await profileService.GetAsync(user.Id, gameId, trophyClass, maxRarity, cancellationToken);
        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var profile = await profileService.UpdateAsync(user.Id, request, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("trophies/{achievementId:guid}/featured")]
    public async Task<IActionResult> SetFeaturedTrophy(
        Guid achievementId,
        [FromBody] SetFeaturedTrophyRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var updated = await profileService.SetFeaturedTrophyAsync(user.Id, achievementId, request.Featured, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    public sealed record SetFeaturedTrophyRequest(bool Featured);
}

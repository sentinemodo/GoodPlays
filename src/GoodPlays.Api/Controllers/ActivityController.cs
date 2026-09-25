using GoodPlays.Api.Services;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/activity")]
public class ActivityController(IActivityLogService activityLogService, ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 40, CancellationToken cancellationToken = default)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        await activityLogService.BackfillProfileMilestonesAsync(user.Id, cancellationToken);
        var entries = await activityLogService.ListAsync(user.Id, limit, cancellationToken);
        return Ok(entries);
    }
}

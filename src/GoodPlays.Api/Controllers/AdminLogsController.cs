using GoodPlays.Api.Services;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/admin/logs")]
public class AdminLogsController(
    IActivityLogService activityLogService,
    ICurrentUserAccessor currentUserAccessor,
    IProfileService profileService,
    IPlatformSyncRunService platformSyncRunService,
    IPlatformSyncRunScheduler platformSyncRunScheduler,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("session")]
    public async Task<IActionResult> Session(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!IsAdmin(user.Email))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Admin access required." });
        }

        return Ok(new { email = user.Email, isAdmin = true });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? category,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? q,
        [FromQuery] string? nickname,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!IsAdmin(user.Email))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Admin access required." });
        }

        var page = await activityLogService.SearchAdminAsync(
            new AdminActivityQuery(category, from, to, q, limit, offset, nickname),
            cancellationToken);
        return Ok(new
        {
            items = page.Items,
            total = page.Total,
            categories = new[] { "Login", "Sync" }.Concat(ProfileActivityRules.ProfileCategories)
        });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncForUser([FromBody] AdminSyncRequest request, CancellationToken cancellationToken)
    {
        var admin = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (admin is null)
        {
            return Unauthorized();
        }

        if (!IsAdmin(admin.Email))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Admin access required." });
        }

        var userId = await profileService.FindUserIdByUsernameAsync(request.Nickname, cancellationToken);
        if (userId is null)
        {
            return NotFound(new { message = "No player with that nickname." });
        }

        try
        {
            var start = await platformSyncRunService.StartAsync(userId.Value, cancellationToken);
            if (start.Created)
            {
                platformSyncRunScheduler.Schedule(start.Run.Id);
            }

            return Accepted(start.Run);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    public sealed record AdminSyncRequest(string Nickname);

    private bool IsAdmin(string email) =>
        AdminAccess.IsAllowed(
            email,
            AdminAccess.Parse(configuration["Admin:Emails"]),
            environment.IsDevelopment());
}

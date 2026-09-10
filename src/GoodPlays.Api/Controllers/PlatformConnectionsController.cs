using GoodPlays.Api.Services;
using GoodPlays.Infrastructure.Services;
using GoodPlays.Infrastructure.Steam;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/platforms")]
public class PlatformConnectionsController(
    IPlatformConnectionService platformConnectionService,
    ISteamSyncJobScheduler steamSyncJobScheduler,
    ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListConnections(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var connections = await platformConnectionService.ListAsync(user.Id, cancellationToken);
        return Ok(connections);
    }

    [HttpPost("steam/connect")]
    public async Task<IActionResult> ConnectSteam(
        [FromBody] ConnectSteamRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.SteamIdOrUrl))
        {
            return BadRequest(new { message = "steamIdOrUrl is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { message = "apiKey is required." });
        }

        try
        {
            var connection = await platformConnectionService.ConnectSteamAsync(
                user.Id,
                request.SteamIdOrUrl,
                request.ApiKey,
                cancellationToken);
            return Ok(connection);
        }
        catch (SteamApiException ex)
        {
            return ex.ErrorCode switch
            {
                SteamApiErrorCode.InvalidApiKey => Unauthorized(new { message = ex.Message }),
                SteamApiErrorCode.VanityNotFound => BadRequest(new { message = ex.Message }),
                SteamApiErrorCode.PrivateProfile => BadRequest(new { message = ex.Message }),
                SteamApiErrorCode.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message })
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("steam/sync")]
    public async Task<IActionResult> SyncSteam(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var schedule = await steamSyncJobScheduler.ScheduleSyncAsync(user.Id, cancellationToken);
            if (schedule.Queued)
            {
                return Accepted(new { message = "Steam sync queued." });
            }

            return Ok(schedule.Result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (SteamApiException ex)
        {
            return ex.ErrorCode switch
            {
                SteamApiErrorCode.InvalidApiKey => Unauthorized(new { message = ex.Message }),
                SteamApiErrorCode.PrivateProfile => BadRequest(new { message = ex.Message }),
                SteamApiErrorCode.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message })
            };
        }
    }

    [HttpDelete("steam")]
    public async Task<IActionResult> DisconnectSteam(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var removed = await platformConnectionService.DisconnectSteamAsync(user.Id, cancellationToken);
        if (!removed)
        {
            return NotFound(new { message = "No Steam connection found." });
        }

        return NoContent();
    }

    public sealed record ConnectSteamRequest(string SteamIdOrUrl, string ApiKey);
}

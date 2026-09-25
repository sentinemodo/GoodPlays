using GoodPlays.Api.Services;
using GoodPlays.Infrastructure.Services;
using GoodPlays.Infrastructure.Nintendo;
using GoodPlays.Infrastructure.Psn;
using GoodPlays.Infrastructure.Steam;
using GoodPlays.Infrastructure.Xbox;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/platforms")]
public class PlatformConnectionsController(
    IPlatformConnectionService platformConnectionService,
    ISteamSyncJobScheduler steamSyncJobScheduler,
    IPsnSyncJobScheduler psnSyncJobScheduler,
    IXboxSyncJobScheduler xboxSyncJobScheduler,
    ISwitchSyncJobScheduler switchSyncJobScheduler,
    IPlatformSyncRunService platformSyncRunService,
    IPlatformSyncRunScheduler platformSyncRunScheduler,
    IActivityLogService activityLogService,
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

    [HttpPost("sync-all")]
    public async Task<IActionResult> SyncAll(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var start = await platformSyncRunService.StartAsync(user.Id, cancellationToken);
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

    [HttpGet("sync-all/current")]
    public async Task<IActionResult> GetCurrentSync(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var current = await platformSyncRunService.GetCurrentAsync(user.Id, cancellationToken);
        if (current.ScheduleRestart && current.Run is not null)
        {
            platformSyncRunScheduler.Schedule(current.Run.Id);
        }

        if (current.Run is null)
        {
            return NoContent();
        }

        return Ok(current.Run);
    }

    [HttpPost("sync-all/stop")]
    public async Task<IActionResult> StopSync(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var run = await platformSyncRunService.StopAsync(user.Id, cancellationToken);
        if (run is null)
        {
            return NotFound(new { message = "No sync is running." });
        }

        return Ok(run);
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

    [HttpPost("psn/connect")]
    public async Task<IActionResult> ConnectPsn(
        [FromBody] ConnectPsnRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Npsso))
        {
            return BadRequest(new { message = "npsso is required." });
        }

        try
        {
            var connection = await platformConnectionService.ConnectPsnAsync(
                user.Id,
                request.Npsso,
                cancellationToken);
            return Ok(connection);
        }
        catch (PsnApiException ex)
        {
            return ex.ErrorCode switch
            {
                PsnApiErrorCode.InvalidNpsso => BadRequest(new { message = ex.Message }),
                PsnApiErrorCode.Unauthorized => Unauthorized(new { message = ex.Message }),
                PsnApiErrorCode.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message })
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("psn/sync")]
    public async Task<IActionResult> SyncPsn(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var schedule = await psnSyncJobScheduler.ScheduleSyncAsync(user.Id, cancellationToken);
            if (schedule.Queued)
            {
                return Accepted(new { message = "PlayStation sync queued." });
            }

            return Ok(schedule.Result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (PsnApiException ex)
        {
            await activityLogService.RecordAsync(user.Id, "Sync", $"PlayStation sync failed: {ex.Message}", cancellationToken);
            return ex.ErrorCode switch
            {
                PsnApiErrorCode.Unauthorized => Unauthorized(new { message = ex.Message }),
                PsnApiErrorCode.InvalidNpsso => BadRequest(new { message = ex.Message }),
                PsnApiErrorCode.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message })
            };
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("library_entries", StringComparison.OrdinalIgnoreCase) == true)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            var schemaHint = detail.Contains("IX_library_entries_user_id_game_id", StringComparison.Ordinal)
                ? "The API database is missing the platform-specific library migration. Run: dotnet ef database update --project src/GoodPlays.Infrastructure --startup-project src/GoodPlays.Api (Development uses the local Docker Postgres)."
                : $"Library database conflict: {detail}";
            await activityLogService.RecordAsync(user.Id, "Sync", $"PlayStation sync failed: {schemaHint}", cancellationToken);
            return Conflict(new { message = schemaHint });
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            await activityLogService.RecordAsync(user.Id, "Sync", $"PlayStation sync failed: {message}", cancellationToken);
            return StatusCode(StatusCodes.Status502BadGateway, new { message });
        }
    }

    [HttpDelete("psn")]
    public async Task<IActionResult> DisconnectPsn(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var removed = await platformConnectionService.DisconnectPsnAsync(user.Id, cancellationToken);
        if (!removed)
        {
            return NotFound(new { message = "No PlayStation connection found." });
        }

        return NoContent();
    }

    [HttpGet("xbox/login")]
    public IActionResult XboxLogin() => Ok(platformConnectionService.CreateXboxLogin());

    [HttpPost("xbox/connect")]
    public async Task<IActionResult> ConnectXbox(
        [FromBody] ConnectXboxRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            return BadRequest(new { message = "callbackUrl is required." });
        }

        try
        {
            var connection = await platformConnectionService.ConnectXboxAsync(user.Id, request.CallbackUrl, cancellationToken);
            return Ok(connection);
        }
        catch (XboxApiException ex)
        {
            return ex.ErrorCode switch
            {
                XboxApiErrorCode.InvalidCode => BadRequest(new { message = ex.Message }),
                XboxApiErrorCode.Unauthorized => Unauthorized(new { message = ex.Message }),
                XboxApiErrorCode.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message })
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("xbox/sync")]
    public async Task<IActionResult> SyncXbox(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var schedule = await xboxSyncJobScheduler.ScheduleSyncAsync(user.Id, cancellationToken);
            if (schedule.Queued)
            {
                return Accepted(new { message = "Xbox sync queued." });
            }

            return Ok(schedule.Result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (XboxApiException ex)
        {
            return ex.ErrorCode switch
            {
                XboxApiErrorCode.Unauthorized => Unauthorized(new { message = ex.Message }),
                XboxApiErrorCode.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message })
            };
        }
    }

    [HttpDelete("xbox")]
    public async Task<IActionResult> DisconnectXbox(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var removed = await platformConnectionService.DisconnectXboxAsync(user.Id, cancellationToken);
        if (!removed)
        {
            return NotFound(new { message = "No Xbox connection found." });
        }

        return NoContent();
    }

    [HttpGet("switch/login")]
    public IActionResult SwitchLogin() => Ok(platformConnectionService.CreateSwitchLogin());

    [HttpPost("switch/connect")]
    public async Task<IActionResult> ConnectSwitch(
        [FromBody] ConnectSwitchRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            return BadRequest(new { message = "callbackUrl is required." });
        }

        try
        {
            var connection = await platformConnectionService.ConnectSwitchAsync(
                user.Id,
                request.CallbackUrl,
                request.CodeVerifier,
                cancellationToken);
            return Ok(connection);
        }
        catch (NintendoApiException ex)
        {
            return ex.ErrorCode switch
            {
                NintendoApiErrorCode.InvalidSession => BadRequest(new { message = ex.Message }),
                NintendoApiErrorCode.Unauthorized => Unauthorized(new { message = ex.Message }),
                NintendoApiErrorCode.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message })
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("switch/sync")]
    public async Task<IActionResult> SyncSwitch(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var schedule = await switchSyncJobScheduler.ScheduleSyncAsync(user.Id, cancellationToken);
            if (schedule.Queued)
            {
                return Accepted(new { message = "Nintendo Switch sync queued." });
            }

            return Ok(schedule.Result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (NintendoApiException ex)
        {
            return ex.ErrorCode switch
            {
                NintendoApiErrorCode.Unauthorized or NintendoApiErrorCode.InvalidSession => Unauthorized(new { message = ex.Message }),
                NintendoApiErrorCode.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message })
            };
        }
    }

    [HttpDelete("switch")]
    public async Task<IActionResult> DisconnectSwitch(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var removed = await platformConnectionService.DisconnectSwitchAsync(user.Id, cancellationToken);
        if (!removed)
        {
            return NotFound(new { message = "No Nintendo Switch connection found." });
        }

        return NoContent();
    }

    public sealed record ConnectSteamRequest(string SteamIdOrUrl, string ApiKey);

    public sealed record ConnectPsnRequest(string Npsso);

    public sealed record ConnectXboxRequest(string CallbackUrl);

    public sealed record ConnectSwitchRequest(string CallbackUrl, string? CodeVerifier);
}

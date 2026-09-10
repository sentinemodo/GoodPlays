using System.Text.Json;
using GoodPlays.Api.Services;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController(
    IUserService userService,
    IConfiguration configuration,
    ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost("clerk")]
    public async Task<IActionResult> ClerkWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        var webhookSecret = configuration["Clerk:WebhookSecret"];
        if (!ClerkWebhookVerifier.TryVerify(Request.Headers, payload, webhookSecret))
        {
            return Unauthorized();
        }

        ClerkWebhookEvent? webhookEvent;
        try
        {
            webhookEvent = JsonSerializer.Deserialize<ClerkWebhookEvent>(
                payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        if (webhookEvent?.Data?.Id is null)
        {
            return BadRequest();
        }

        var email = webhookEvent.Data.EmailAddresses?.FirstOrDefault()?.EmailAddress
            ?? $"{webhookEvent.Data.Id}@users.clerk";

        var displayName = webhookEvent.Data.FirstName is null && webhookEvent.Data.LastName is null
            ? null
            : $"{webhookEvent.Data.FirstName} {webhookEvent.Data.LastName}".Trim();

        switch (webhookEvent.Type)
        {
            case "user.created":
            case "user.updated":
                await userService.SyncFromWebhookAsync(
                    webhookEvent.Data.Id,
                    email,
                    displayName,
                    deleted: false,
                    cancellationToken);
                break;
            case "user.deleted":
                await userService.SyncFromWebhookAsync(
                    webhookEvent.Data.Id,
                    email,
                    displayName,
                    deleted: true,
                    cancellationToken);
                break;
            default:
                logger.LogDebug("Ignoring Clerk webhook type {Type}", webhookEvent.Type);
                break;
        }

        return Ok();
    }

    private sealed class ClerkWebhookEvent
    {
        public string? Type { get; set; }
        public ClerkWebhookUser? Data { get; set; }
    }

    private sealed class ClerkWebhookUser
    {
        public string? Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public List<ClerkEmailAddress>? EmailAddresses { get; set; }
    }

    private sealed class ClerkEmailAddress
    {
        public string? EmailAddress { get; set; }
    }
}

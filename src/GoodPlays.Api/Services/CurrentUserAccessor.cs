using System.Security.Claims;
using GoodPlays.Api.Extensions;
using GoodPlays.Domain.Entities;
using GoodPlays.Infrastructure.Services;

namespace GoodPlays.Api.Services;

public sealed class CurrentUserAccessor(
    IHttpContextAccessor httpContextAccessor,
    IUserService userService,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ICurrentUserAccessor
{
    private const string DevClerkId = "dev_local_user";
    private const string DevEmail = "dev@localhost";

    public bool IsAuthEnabled => ClerkAuthenticationExtensions.IsClerkConfigured(configuration);

    public async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated == true)
        {
            var clerkId = httpContext.User.FindFirstValue("sub")
                ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(clerkId))
            {
                return null;
            }

            var email = httpContext.User.FindFirstValue("email")
                ?? httpContext.User.FindFirstValue(ClaimTypes.Email)
                ?? $"{clerkId}@users.clerk";

            var displayName = httpContext.User.FindFirstValue("name")
                ?? httpContext.User.FindFirstValue(ClaimTypes.Name);

            return await userService.EnsureUserAsync(clerkId, email, displayName, cancellationToken);
        }

        if (!IsAuthEnabled && environment.IsDevelopment())
        {
            return await userService.EnsureUserAsync(DevClerkId, DevEmail, "Local Dev User", cancellationToken);
        }

        return null;
    }
}

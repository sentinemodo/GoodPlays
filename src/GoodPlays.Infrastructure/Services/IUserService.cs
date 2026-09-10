using GoodPlays.Domain.Entities;

namespace GoodPlays.Infrastructure.Services;

public interface IUserService
{
    Task<User?> GetByClerkIdAsync(string clerkId, CancellationToken cancellationToken);

    Task<User> EnsureUserAsync(string clerkId, string email, string? displayName, CancellationToken cancellationToken);

    Task<User?> SyncFromWebhookAsync(
        string clerkId,
        string email,
        string? displayName,
        bool deleted,
        CancellationToken cancellationToken);
}

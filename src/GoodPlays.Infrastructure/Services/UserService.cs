using GoodPlays.Domain.Entities;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed class UserService(GoodPlaysDbContext dbContext) : IUserService
{
    public Task<User?> GetByClerkIdAsync(string clerkId, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.ClerkId == clerkId, cancellationToken);

    public async Task<User> EnsureUserAsync(
        string clerkId,
        string email,
        string? displayName,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.ClerkId == clerkId, cancellationToken);
        if (user is not null)
        {
            var changed = false;
            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = email;
                changed = true;
            }

            if (displayName is not null && user.DisplayName != displayName)
            {
                user.DisplayName = displayName;
                changed = true;
            }

            if (user.DeletedAt is not null)
            {
                user.DeletedAt = null;
                changed = true;
            }

            if (changed)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return user;
        }

        var now = DateTimeOffset.UtcNow;
        user = new User
        {
            Id = Guid.NewGuid(),
            ClerkId = clerkId,
            Email = email,
            DisplayName = displayName,
            CreatedAt = now
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User?> SyncFromWebhookAsync(
        string clerkId,
        string email,
        string? displayName,
        bool deleted,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.ClerkId == clerkId, cancellationToken);
        if (user is null)
        {
            if (deleted)
            {
                return null;
            }

            return await EnsureUserAsync(clerkId, email, displayName, cancellationToken);
        }

        user.Email = email;
        user.DisplayName = displayName;
        user.DeletedAt = deleted ? DateTimeOffset.UtcNow : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }
}

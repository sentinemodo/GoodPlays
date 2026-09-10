using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task EnsureUserAsync_CreatesThenUpdatesUser()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new GoodPlaysDbContext(options);
        var service = new UserService(context);

        var created = await service.EnsureUserAsync("user_123", "first@example.com", "First", CancellationToken.None);
        var updated = await service.EnsureUserAsync("user_123", "second@example.com", "Second", CancellationToken.None);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("second@example.com", updated.Email);
        Assert.Equal("Second", updated.DisplayName);
        Assert.Equal(1, await context.Users.CountAsync());
    }
}

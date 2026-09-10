using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class LibraryServiceTests
{
    [Fact]
    public async Task CreateAsync_AddsEntryForExistingGame()
    {
        var (context, userId, gameId) = await SeedUserAndGameAsync();
        await using (context)
        {
            var service = new LibraryService(context);
            var entry = await service.CreateAsync(
                userId,
                new CreateLibraryEntryRequest(gameId, LibraryStatus.Playing, null, null),
                CancellationToken.None);

            Assert.NotNull(entry);
            Assert.Equal("Hades", entry.GameTitle);
            Assert.Equal(LibraryStatus.Playing, entry.Status);
        }
    }

    [Fact]
    public async Task CreateAsync_ReturnsNullWhenDuplicate()
    {
        var (context, userId, gameId) = await SeedUserAndGameAsync();
        await using (context)
        {
            var service = new LibraryService(context);
            await service.CreateAsync(
                userId,
                new CreateLibraryEntryRequest(gameId, LibraryStatus.Owned, null, null),
                CancellationToken.None);

            var duplicate = await service.CreateAsync(
                userId,
                new CreateLibraryEntryRequest(gameId, LibraryStatus.Backlog, null, null),
                CancellationToken.None);

            Assert.Null(duplicate);
        }
    }

    private static async Task<(GoodPlaysDbContext Context, Guid UserId, Guid GameId)> SeedUserAndGameAsync()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = "user_test",
            Email = "test@example.com",
            CreatedAt = now
        });

        context.Games.Add(new Game
        {
            Id = gameId,
            Slug = "hades",
            Title = "Hades",
            SortTitle = "hades",
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return (context, userId, gameId);
    }
}

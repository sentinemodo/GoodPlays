using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class DbContextTests
{
    [Fact]
    public async Task DbContext_PersistsCoreEntities()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new GoodPlaysDbContext(options);

        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = "user_test",
            Email = "test@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Games.Add(new Game
        {
            Id = gameId,
            Slug = "hades",
            Title = "Hades",
            SortTitle = "hades",
            GameType = GameType.Base,
            MetadataStatus = MetadataStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        context.LibraryEntries.Add(new LibraryEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            Status = LibraryStatus.Playing,
            Source = LibraryEntrySource.Manual,
            Visibility = Visibility.Public,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync();

        var entryCount = await context.LibraryEntries.CountAsync();
        Assert.Equal(1, entryCount);
    }
}

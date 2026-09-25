using GoodPlays.Domain.Entities;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class ProfileActivityTests
{
    [Fact]
    public void PlaytimeMessages_FirstSessionAndHundredHours()
    {
        var first = ProfileActivityRules.PlaytimeMessages(null, 2m, "Hades");
        var hundred = ProfileActivityRules.PlaytimeMessages(99m, 100m, "Hades");
        var neither = ProfileActivityRules.PlaytimeMessages(100m, 140m, "Hades");

        Assert.Equal(["Played Hades for the first time."], first);
        Assert.Equal(["Spent 100 hours in Hades."], hundred);
        Assert.Empty(neither);
    }

    [Fact]
    public void RankingMessage_UsesOrdinalAndBoardName()
    {
        Assert.Equal(
            "Reached 50th on Best RPG players.",
            ProfileActivityRules.RankingMessage(50, "RPG"));
        Assert.Equal(
            "Reached 1st on Best RPG players.",
            ProfileActivityRules.RankingMessage(1, "RPG"));
        Assert.Null(ProfileActivityRules.RankingMessage(51, "RPG"));
    }

    [Fact]
    public void AdminAccess_MatchesAllowlistIgnoringCase()
    {
        var allowlist = AdminAccess.Parse(" Admin@GoodPlays.dev ; dev@localhost ");

        Assert.True(AdminAccess.IsAllowed("admin@goodplays.dev", allowlist));
        Assert.False(AdminAccess.IsAllowed("player@example.com", allowlist));
        Assert.True(AdminAccess.IsAllowed("user_abc@users.clerk", allowlist, allowLocalClerkFallback: true));
        Assert.False(AdminAccess.IsAllowed("user_abc@users.clerk", allowlist));
    }

    [Fact]
    public async Task ListForProfile_ReturnsOnlyMilestoneEvents()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = "user_1",
            Email = "player@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        });
        var now = DateTimeOffset.Parse("2026-09-25T10:00:00Z");
        context.ActivityLogs.AddRange(
            Log(userId, "Login", "Signed in as player@example.com", now),
            Log(userId, "Sync", "Sync started.", now.AddMinutes(1)),
            Log(userId, ProfileActivityRules.FirstPlay, "Played Hades for the first time.", now.AddMinutes(2)),
            Log(userId, ProfileActivityRules.Trophy, "Acquired Platinum in Hades.", now.AddMinutes(3)),
            Log(userId, ProfileActivityRules.Playtime, "Spent 100 hours in Hades.", now.AddMinutes(4)),
            Log(userId, ProfileActivityRules.Ranking, "Reached 50th on Best RPG players.", now.AddMinutes(5)));
        await context.SaveChangesAsync();
        var service = new ActivityLogService(context);

        var entries = await service.ListAsync(userId, 40, CancellationToken.None);

        Assert.Equal(
            [ProfileActivityRules.Ranking, ProfileActivityRules.Playtime, ProfileActivityRules.Trophy, ProfileActivityRules.FirstPlay],
            entries.Select(entry => entry.Category).ToArray());
    }

    [Fact]
    public async Task SearchAdmin_FiltersByTimeCategoryAndText()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = "user_1",
            Email = "player@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        });
        var start = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
        context.ActivityLogs.AddRange(
            Log(userId, "Sync", "Sync started.", start),
            Log(userId, "Sync", "Sync failed: timeout", start.AddDays(1)),
            Log(userId, ProfileActivityRules.Trophy, "Acquired Gold in Hades.", start.AddDays(2)));
        await context.SaveChangesAsync();
        var service = new ActivityLogService(context);

        var matches = await service.SearchAdminAsync(
            new AdminActivityQuery("Sync", start.AddHours(12), start.AddDays(2), "timeout", 20, 0),
            CancellationToken.None);

        var match = Assert.Single(matches.Items);
        Assert.Equal("Sync failed: timeout", match.Message);
        Assert.Equal("player@example.com", match.UserEmail);
        Assert.Equal(1, matches.Total);
    }

    [Fact]
    public async Task SearchAdmin_FiltersByNickname()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            ClerkId = "user_1",
            Email = "player@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            Profile = new UserProfile { UserId = userId, Username = "nightowl" }
        };
        context.Users.Add(user);
        context.ActivityLogs.Add(Log(userId, "Sync", "Sync started.", DateTimeOffset.Parse("2026-09-25T10:00:00Z")));
        var otherId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = otherId,
            ClerkId = "user_2",
            Email = "other@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            Profile = new UserProfile { UserId = otherId, Username = "other" }
        });
        context.ActivityLogs.Add(Log(otherId, "Sync", "Sync started.", DateTimeOffset.Parse("2026-09-25T11:00:00Z")));
        await context.SaveChangesAsync();
        var service = new ActivityLogService(context);

        var matches = await service.SearchAdminAsync(
            new AdminActivityQuery(null, null, null, null, 20, 0, "NightOwl"),
            CancellationToken.None);

        var match = Assert.Single(matches.Items);
        Assert.Equal(userId, match.UserId);
        Assert.Equal("nightowl", match.Username);
    }

    [Fact]
    public async Task RecordRankingMilestones_LogsFiftiethPlaceOnce()
    {
        await using var context = CreateContext();
        var genreId = Guid.NewGuid();
        context.Genres.Add(new Genre { Id = genreId, Name = "RPG", Slug = "rpg" });
        var targetId = Guid.NewGuid();
        context.Users.Add(User(targetId, "target@example.com"));
        for (var index = 0; index < 49; index++)
        {
            var rivalId = Guid.NewGuid();
            context.Users.Add(User(rivalId, $"rival{index}@example.com"));
            AddHours(context, rivalId, genreId, 200 - index, $"Rival {index}");
        }

        AddHours(context, targetId, genreId, 10, "Hades");
        await context.SaveChangesAsync();
        var service = new ActivityLogService(context);

        await service.RecordRankingMilestonesAsync(targetId, CancellationToken.None);
        await service.RecordRankingMilestonesAsync(targetId, CancellationToken.None);

        var logs = await context.ActivityLogs.Where(entry => entry.UserId == targetId).ToListAsync();
        var log = Assert.Single(logs);
        Assert.Equal(ProfileActivityRules.Ranking, log.Category);
        Assert.Equal("Reached 50th on Best RPG players.", log.Message);
    }

    private static void AddHours(GoodPlaysDbContext context, Guid userId, Guid genreId, decimal hours, string title)
    {
        var gameId = Guid.NewGuid();
        context.Games.Add(new Game
        {
            Id = gameId,
            Title = title,
            Slug = $"{title}-{gameId:N}",
            SortTitle = title,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        context.GameGenres.Add(new GameGenre { GameId = gameId, GenreId = genreId });
        context.LibraryEntries.Add(new LibraryEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            HoursPlayed = hours,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private static User User(Guid id, string email) => new()
    {
        Id = id,
        ClerkId = id.ToString(),
        Email = email,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static ActivityLog Log(Guid userId, string category, string message, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Category = category,
        Message = message,
        CreatedAt = createdAt
    };

    private static GoodPlaysDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GoodPlaysDbContext(options);
    }
}

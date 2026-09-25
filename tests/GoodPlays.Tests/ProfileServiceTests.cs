using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class ProfileServiceTests
{
    [Fact]
    public async Task GetAsync_UsesStarredGameOtherwiseMostHours()
    {
        var (context, userId) = CreateContext();
        var shortGame = AddGame(context, "Short", "short");
        var longGame = AddGame(context, "Long", "long");
        context.LibraryEntries.AddRange(
            Entry(userId, shortGame.Id, 10, loved: true),
            Entry(userId, longGame.Id, 80, loved: false));
        await context.SaveChangesAsync();

        var starred = await new ProfileService(context).GetAsync(userId, null, null, null, CancellationToken.None);
        Assert.Equal("Short", starred.MostLovedGame!.Title);
        Assert.True(starred.MostLovedIsStarred);

        var loved = context.LibraryEntries.Single(e => e.GameId == shortGame.Id);
        loved.IsLoved = false;
        await context.SaveChangesAsync();

        var fallback = await new ProfileService(context).GetAsync(userId, null, null, null, CancellationToken.None);
        Assert.Equal("Long", fallback.MostLovedGame!.Title);
        Assert.False(fallback.MostLovedIsStarred);
    }

    [Fact]
    public async Task GetAsync_GroupsHoursByPlatformAndCategory()
    {
        var (context, userId) = CreateContext();
        var rpg = new Genre { Id = Guid.NewGuid(), Name = "RPG", Slug = "rpg" };
        context.Genres.Add(rpg);
        var steam = AddGame(context, "Steam RPG", "steam-rpg");
        context.GameGenres.Add(new GameGenre { GameId = steam.Id, GenreId = rpg.Id });
        var psn = AddGame(context, "Psn Game", "psn-game");
        context.LibraryEntries.AddRange(
            Entry(userId, steam.Id, 12, source: LibraryEntrySource.SteamSync),
            Entry(userId, psn.Id, 3, source: LibraryEntrySource.PsnSync));
        await context.SaveChangesAsync();

        var profile = await new ProfileService(context).GetAsync(userId, null, null, null, CancellationToken.None);

        Assert.Contains(profile.Platforms, p => p.Label == "Steam" && p.GameCount == 1 && p.Hours == 12);
        Assert.Contains(profile.Platforms, p => p.Label == "PlayStation" && p.Hours == 3);
        Assert.Contains(profile.Categories, c => c.Label == "RPG" && c.Hours == 12);
        Assert.Contains(profile.Categories, c => c.Label == "Uncategorized" && c.GameCount == 1);
    }

    [Fact]
    public async Task GetAsync_FiltersTrophiesAndKeepsFeaturedAndRecent()
    {
        var (context, userId) = CreateContext();
        var game = AddGame(context, "Trophy Game", "trophy-game");
        var other = AddGame(context, "Other", "other");
        var platinum = Achievement(game.Id, "Platinum", 2);
        var silver = Achievement(game.Id, "Silver", 30);
        var otherTrophy = Achievement(other.Id, "Elsewhere", 2);
        context.Achievements.AddRange(platinum, silver, otherTrophy);
        context.UserAchievements.AddRange(
            new UserAchievement
            {
                UserId = userId,
                AchievementId = platinum.Id,
                UnlockedAt = DateTimeOffset.UtcNow.AddDays(-2),
                IsFeatured = true
            },
            new UserAchievement
            {
                UserId = userId,
                AchievementId = silver.Id,
                UnlockedAt = DateTimeOffset.UtcNow
            },
            new UserAchievement
            {
                UserId = userId,
                AchievementId = otherTrophy.Id,
                UnlockedAt = DateTimeOffset.UtcNow.AddDays(-1)
            });
        await context.SaveChangesAsync();

        var profile = await new ProfileService(context).GetAsync(
            userId,
            game.Id,
            TrophyClass.Platinum,
            5,
            CancellationToken.None);

        Assert.Equal("Platinum", profile.MostImportantTrophy!.Name);
        Assert.Equal("Silver", profile.MostRecentTrophy!.Name);
        Assert.Single(profile.Trophies);
        Assert.Equal("Platinum", profile.Trophies[0].Name);
        Assert.Equal(TrophyClass.Platinum, profile.Trophies[0].TrophyClass);
    }

    [Fact]
    public void FromRarity_MapsPlatinumGoldSilver()
    {
        Assert.Equal(TrophyClass.Platinum, TrophyClassRules.FromRarity(4));
        Assert.Equal(TrophyClass.Gold, TrophyClassRules.FromRarity(12));
        Assert.Equal(TrophyClass.Silver, TrophyClassRules.FromRarity(40));
        Assert.Equal(TrophyClass.Bronze, TrophyClassRules.FromRarity(80));
    }

    private static (GoodPlaysDbContext Context, Guid UserId) CreateContext()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var user = new User
        {
            Id = Guid.NewGuid(),
            ClerkId = "clerk",
            Email = "player@example.com",
            DisplayName = "Player",
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Users.Add(user);
        context.SaveChanges();
        return (context, user.Id);
    }

    private static Game AddGame(GoodPlaysDbContext context, string title, string slug)
    {
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = title,
            SortTitle = title.ToLowerInvariant(),
            Slug = slug,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Games.Add(game);
        return game;
    }

    private static LibraryEntry Entry(
        Guid userId,
        Guid gameId,
        decimal hours,
        bool loved = false,
        LibraryEntrySource source = LibraryEntrySource.Manual) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            HoursPlayed = hours,
            IsLoved = loved,
            Source = source,
            StartedAt = DateOnly.FromDateTime(DateTime.UtcNow),
            Visibility = Visibility.Public,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static Achievement Achievement(Guid gameId, string name, decimal rarity) =>
        new()
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            ExternalId = name.ToLowerInvariant(),
            Name = name,
            RarityPercent = rarity,
            FetchedAt = DateTimeOffset.UtcNow
        };
}

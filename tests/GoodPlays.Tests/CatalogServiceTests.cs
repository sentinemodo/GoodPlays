using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class CatalogServiceTests
{
    [Fact]
    public async Task BrowseAsync_FiltersByGenreAndPaginates()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);

        var rpg = new Genre { Id = Guid.NewGuid(), Name = "RPG", Slug = "rpg" };
        var fps = new Genre { Id = Guid.NewGuid(), Name = "Shooter", Slug = "shooter" };
        context.Genres.AddRange(rpg, fps);

        var game1 = CreateGame("Alpha Quest", "alpha-quest");
        var game2 = CreateGame("Beta Shooter", "beta-shooter");
        context.Games.AddRange(game1, game2);
        context.GameGenres.AddRange(
            new GameGenre { GameId = game1.Id, GenreId = rpg.Id },
            new GameGenre { GameId = game2.Id, GenreId = fps.Id });
        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var result = await service.BrowseAsync(null, new CatalogQuery(GenreSlug: "rpg", PageSize: 10), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Alpha Quest", result.Items[0].Title);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task BrowseAsync_InLibraryFilter_ReturnsOnlyUserGames()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();

        var owned = CreateGame("Owned Game", "owned-game");
        var other = CreateGame("Other Game", "other-game");
        context.Games.AddRange(owned, other);
        context.LibraryEntries.Add(new LibraryEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = owned.Id,
            Status = LibraryStatus.Playing,
            Source = LibraryEntrySource.Manual,
            Visibility = Visibility.Public,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var result = await service.BrowseAsync(
            userId,
            new CatalogQuery(InLibrary: true),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(owned.Id, result.Items[0].Id);
        Assert.Equal(LibraryStatus.Playing, result.Items[0].LibraryStatus);
    }

    [Fact]
    public async Task BrowseAsync_HoursSort_TreatsMissingHoursAsZero()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();

        var manyHours = CreateGame("Many Hours", "many-hours");
        var zeroHours = CreateGame("Helldivers 2", "helldivers-2");
        context.Games.AddRange(manyHours, zeroHours);
        context.LibraryEntries.AddRange(
            new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = manyHours.Id,
                Status = LibraryStatus.Playing,
                HoursPlayed = 120,
                Source = LibraryEntrySource.SteamSync,
                Visibility = Visibility.Public,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = zeroHours.Id,
                Status = LibraryStatus.Owned,
                HoursPlayed = null,
                Source = LibraryEntrySource.PsnSync,
                Visibility = Visibility.Public,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var result = await service.BrowseAsync(
            userId,
            new CatalogQuery(InLibrary: true, Sort: CatalogSortField.Hours),
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Many Hours", result.Items[0].Title);
        Assert.Equal("Helldivers 2", result.Items[1].Title);
        Assert.Null(result.Items[1].UserHours);
    }

    [Fact]
    public async Task BrowseAsync_InLibraryGroupByPlatform_UsesLibrarySource()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();

        var steamGame = CreateGame("Steam Game", "steam-game");
        var psnGame = CreateGame("Psn Game", "psn-game");
        context.Games.AddRange(steamGame, psnGame);
        context.LibraryEntries.AddRange(
            new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = steamGame.Id,
                Status = LibraryStatus.Owned,
                Source = LibraryEntrySource.SteamSync,
                Visibility = Visibility.Public,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = psnGame.Id,
                Status = LibraryStatus.Owned,
                Source = LibraryEntrySource.PsnSync,
                Visibility = Visibility.Public,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var result = await service.BrowseAsync(
            userId,
            new CatalogQuery(InLibrary: true, GroupBy: CatalogGroupBy.Platform, PageSize: 10),
            CancellationToken.None);

        Assert.NotNull(result.Groups);
        Assert.Equal(2, result.Groups!.Count);
        Assert.Contains(result.Groups, g => g.Label == "Steam");
        Assert.Contains(result.Groups, g => g.Label == "PlayStation");
    }

    [Fact]
    public async Task BrowseAsync_GroupByPlatform_PaginatesGamesNotGroups()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);

        var pc = new Platform { Id = Guid.NewGuid(), Name = "PC", Slug = "pc" };
        context.Platforms.Add(pc);

        for (var i = 0; i < 25; i++)
        {
            var game = CreateGame($"Game {i:D2}", $"game-{i:D2}");
            context.Games.Add(game);
            context.GamePlatforms.Add(new GamePlatform { GameId = game.Id, PlatformId = pc.Id });
        }

        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var page1 = await service.BrowseAsync(
            null,
            new CatalogQuery(GroupBy: CatalogGroupBy.Platform, Page: 1, PageSize: 10),
            CancellationToken.None);
        var page2 = await service.BrowseAsync(
            null,
            new CatalogQuery(GroupBy: CatalogGroupBy.Platform, Page: 2, PageSize: 10),
            CancellationToken.None);

        Assert.NotNull(page1.Groups);
        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(10, page1.Groups!.SelectMany(g => g.Items).Count());
        Assert.Equal(10, page2.Groups!.SelectMany(g => g.Items).Count());
        Assert.NotEqual(
            page1.Groups.SelectMany(g => g.Items).Select(i => i.Id).First(),
            page2.Groups.SelectMany(g => g.Items).Select(i => i.Id).First());
    }

    [Fact]
    public async Task BrowseAsync_GroupByTag_SplitsTaggedAndUntagged()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();

        var tagged = CreateGame("Tagged Game", "tagged-game");
        var untagged = CreateGame("Untagged Game", "untagged-game");
        context.Games.AddRange(tagged, untagged);

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Favorites",
            Slug = "favorites",
            Scope = TagScope.User
        };
        context.Tags.Add(tag);
        context.GameTags.Add(new GameTag { GameId = tagged.Id, TagId = tag.Id });
        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var result = await service.BrowseAsync(
            userId,
            new CatalogQuery(GroupBy: CatalogGroupBy.Tag, PageSize: 10),
            CancellationToken.None);

        Assert.NotNull(result.Groups);
        Assert.Contains(result.Groups!, g => g.Key == "favorites" && g.Items.Any(i => i.Title == "Tagged Game"));
        Assert.Contains(result.Groups!, g => g.Key == "untagged" && g.Items.Any(i => i.Title == "Untagged Game"));
    }

    [Fact]
    public async Task BrowseAsync_ExcludesHiddenGames()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);

        var visible = CreateGame("Visible Game", "visible-game");
        var hidden = CreateGame("Netflix", "netflix");
        hidden.IsHiddenFromCatalog = true;
        context.Games.AddRange(visible, hidden);
        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var result = await service.BrowseAsync(null, new CatalogQuery(), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Visible Game", result.Items[0].Title);
    }

    [Fact]
    public async Task BrowseAsync_ExcludesStreamingAppsEvenWhenNotFlaggedHidden()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();
        var netflix = CreateGame("Netflix", "netflix");
        var prime = CreateGame("Prime Video", "prime-video");
        var game = CreateGame("Hades", "hades");
        context.Games.AddRange(netflix, prime, game);
        context.LibraryEntries.AddRange(
            new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = netflix.Id,
                Source = LibraryEntrySource.PsnSync,
                Visibility = Visibility.Public,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = game.Id,
                Source = LibraryEntrySource.PsnSync,
                Visibility = Visibility.Public,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var result = await service.BrowseAsync(userId, new CatalogQuery(InLibrary: true), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Hades", result.Items[0].Title);
    }

    [Fact]
    public async Task BrowseAsync_ReleaseDateSort_RespectsAscending()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var older = CreateGame("Older", "older");
        older.ReleaseDate = new DateOnly(2001, 1, 1);
        var newer = CreateGame("Newer", "newer");
        newer.ReleaseDate = new DateOnly(2020, 1, 1);
        context.Games.AddRange(older, newer);
        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var ascending = await service.BrowseAsync(
            null,
            new CatalogQuery(Sort: CatalogSortField.ReleaseDate, Descending: false),
            CancellationToken.None);

        Assert.Equal("Older", ascending.Items[0].Title);
    }

    [Fact]
    public async Task BrowseAsync_LastPlayedSort_PutsMostRecentFirst()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();
        var recent = CreateGame("Recent Run", "recent-run");
        var older = CreateGame("Older Run", "older-run");
        context.Games.AddRange(recent, older);
        context.LibraryEntries.AddRange(
            new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = older.Id,
                Source = LibraryEntrySource.SteamSync,
                StartedAt = new DateOnly(2020, 1, 1),
                Visibility = Visibility.Public,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = recent.Id,
                Source = LibraryEntrySource.SteamSync,
                StartedAt = new DateOnly(2024, 6, 1),
                Visibility = Visibility.Public,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        var service = new CatalogService(context);
        var result = await service.BrowseAsync(
            userId,
            new CatalogQuery(InLibrary: true, Sort: CatalogSortField.LastPlayed),
            CancellationToken.None);

        Assert.Equal("Recent Run", result.Items[0].Title);
    }

    private static Game CreateGame(string title, string slug)
    {
        var now = DateTimeOffset.UtcNow;
        return new Game
        {
            Id = Guid.NewGuid(),
            Title = title,
            SortTitle = title.ToLowerInvariant(),
            Slug = slug,
            MetadataStatus = MetadataStatus.Complete,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}

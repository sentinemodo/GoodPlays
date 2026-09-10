using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class GameCatalogServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsLocalMatchesWhenIgdbDisabled()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new GoodPlaysDbContext(options);
        var now = DateTimeOffset.UtcNow;
        context.Games.Add(new Game
        {
            Id = Guid.NewGuid(),
            Slug = "hollow-knight",
            Title = "Hollow Knight",
            SortTitle = "hollow knight",
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();

        var service = new GameCatalogService(context, new StubIgdbClient());
        var results = await service.SearchAsync("hollow", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Hollow Knight", results[0].Title);
        Assert.Equal("local", results[0].Source);
    }

    [Fact]
    public async Task ImportFromIgdbAsync_PersistsGameAndExternalId()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new GoodPlaysDbContext(options);
        var igdbClient = new StubIgdbClient
        {
            Game = new IgdbSearchResult(42, "Celeste", "celeste", null, "Climb", null)
        };

        var service = new GameCatalogService(context, igdbClient);
        var game = await service.ImportFromIgdbAsync(42, CancellationToken.None);

        Assert.NotNull(game);
        Assert.Equal("Celeste", game!.Title);
        Assert.Equal(MetadataStatus.Complete, game.MetadataStatus);
        Assert.Equal("42", await context.GameExternalIds
            .Where(x => x.GameId == game.Id)
            .Select(x => x.ExternalId)
            .SingleAsync());
    }

    private sealed class StubIgdbClient : IIgdbClient
    {
        public bool IsConfigured => Game is not null;

        public IgdbSearchResult? Game { get; init; }

        public Task<IReadOnlyList<IgdbSearchResult>> SearchGamesAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IgdbSearchResult>>(Game is null ? [] : [Game]);

        public Task<IgdbSearchResult?> GetGameAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult(Game?.IgdbId == igdbId ? Game : null);
    }
}

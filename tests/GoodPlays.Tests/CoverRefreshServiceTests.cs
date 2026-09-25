using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class CoverRefreshServiceTests
{
    [Fact]
    public async Task RefreshMissingAsync_SetsCoverFromExistingIgdbId()
    {
        var (context, gameId) = await SeedGameAsync(coverUrl: null, igdbId: "42");
        var igdb = new StubIgdbClient(new IgdbGameDetails(
            42, "Darkest Dungeon", "darkest-dungeon",
            "https://images.igdb.com/igdb/image/upload/t_cover_big/co1.jpg",
            null, null, null, null, GameType.Base, null, [], [], []));

        var updated = await new CoverRefreshService(context, igdb)
            .RefreshMissingAsync([gameId], CancellationToken.None);

        Assert.Equal(1, updated);
        var game = await context.Games.SingleAsync(g => g.Id == gameId);
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/co1.jpg", game.CoverUrl);
    }

    [Fact]
    public async Task RefreshMissingAsync_SkipsGamesThatAlreadyHaveACover()
    {
        var (context, gameId) = await SeedGameAsync(coverUrl: "https://cdn.example/cover.jpg", igdbId: "42");
        var igdb = new StubIgdbClient(details: null);

        var updated = await new CoverRefreshService(context, igdb)
            .RefreshMissingAsync([gameId], CancellationToken.None);

        Assert.Equal(0, updated);
        Assert.Equal(0, igdb.DetailLookups);
    }

    [Fact]
    public async Task RefreshMissingAsync_ResolvesCoverFromSteamAppId()
    {
        var (context, gameId) = await SeedGameAsync(coverUrl: null, steamAppId: "1145360");
        var igdb = new StubIgdbClient(
            new IgdbGameDetails(
                99, "Hades", "hades",
                "https://images.igdb.com/hades.jpg",
                null, null, null, null, GameType.Base, null, [], [], []),
            steamMap: new Dictionary<uint, long> { [1145360] = 99 });

        var updated = await new CoverRefreshService(context, igdb)
            .RefreshMissingAsync([gameId], CancellationToken.None);

        Assert.Equal(1, updated);
        Assert.Contains(
            context.GameExternalIds,
            x => x.GameId == gameId && x.Source == ExternalIdSource.Igdb && x.ExternalId == "99");
    }

    [Fact]
    public async Task RefreshMissingAsync_DoesNotCallIgdbAgainBeforeCooldown()
    {
        var (context, gameId) = await SeedGameAsync(coverUrl: null, igdbId: "42");
        context.GameEnrichmentRuns.Add(new GameEnrichmentRun
        {
            GameId = gameId,
            Kind = EnrichmentKind.Cover,
            LastRunAt = DateTimeOffset.UtcNow,
            NextRunAt = DateTimeOffset.UtcNow.AddDays(7),
            Status = "missing"
        });
        await context.SaveChangesAsync();

        var igdb = new StubIgdbClient(details: null);
        var updated = await new CoverRefreshService(context, igdb)
            .RefreshMissingAsync([gameId], CancellationToken.None);

        Assert.Equal(0, updated);
        Assert.Equal(0, igdb.DetailLookups);
    }

    private static async Task<(GoodPlaysDbContext Context, Guid GameId)> SeedGameAsync(
        string? coverUrl,
        string? igdbId = null,
        string? steamAppId = null)
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = "Darkest Dungeon®",
            SortTitle = "darkest dungeon",
            Slug = "darkest-dungeon",
            CoverUrl = coverUrl,
            MetadataStatus = MetadataStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (igdbId is not null)
        {
            game.ExternalIds.Add(new GameExternalId
            {
                GameId = game.Id,
                Source = ExternalIdSource.Igdb,
                ExternalId = igdbId
            });
        }

        if (steamAppId is not null)
        {
            game.ExternalIds.Add(new GameExternalId
            {
                GameId = game.Id,
                Source = ExternalIdSource.Steam,
                ExternalId = steamAppId
            });
        }

        context.Games.Add(game);
        await context.SaveChangesAsync();
        return (context, game.Id);
    }

    private sealed class StubIgdbClient : IIgdbClient
    {
        private readonly IgdbGameDetails? _details;
        private readonly Dictionary<uint, long> _steamMap;

        public StubIgdbClient(IgdbGameDetails? details, Dictionary<uint, long>? steamMap = null)
        {
            _details = details;
            _steamMap = steamMap ?? [];
        }

        public int DetailLookups { get; private set; }

        public bool IsConfigured => true;

        public Task<IReadOnlyList<IgdbSearchResult>> SearchGamesAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IgdbSearchResult>>([]);

        public Task<IgdbSearchResult?> GetGameAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult<IgdbSearchResult?>(null);

        public Task<IgdbGameDetails?> GetGameDetailsAsync(long igdbId, CancellationToken cancellationToken)
        {
            DetailLookups++;
            return Task.FromResult(_details is not null && _details.IgdbId == igdbId ? _details : null);
        }

        public Task<long?> FindIgdbIdBySteamAppIdAsync(uint steamAppId, CancellationToken cancellationToken) =>
            Task.FromResult(_steamMap.TryGetValue(steamAppId, out var id) ? id : (long?)null);
    }
}

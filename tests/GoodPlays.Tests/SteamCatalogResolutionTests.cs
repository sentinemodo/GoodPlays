using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class SteamCatalogResolutionTests
{
    [Fact]
    public async Task ResolveForSteamSyncAsync_ImportsFromIgdbBySteamAppId()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new GoodPlaysDbContext(options);
        const uint appId = 1145360u;
        var igdbClient = new MappingIgdbClient(appId, 114536L, new IgdbSearchResult(
            114536,
            "Hades",
            "hades",
            "https://images.igdb.com/igdb/image/upload/t_cover_big/co1xxy.jpg",
            "Roguelike",
            null));

        var service = new GameCatalogService(context, igdbClient);
        var game = await service.ResolveForSteamSyncAsync(appId, "Hades", CancellationToken.None);

        Assert.Equal("Hades", game.Title);
        Assert.Equal(MetadataStatus.Complete, game.MetadataStatus);
        Assert.NotNull(game.CoverUrl);
        Assert.Contains(
            context.GameExternalIds,
            x => x.Source == ExternalIdSource.Steam && x.ExternalId == appId.ToString());
        Assert.Contains(
            context.GameExternalIds,
            x => x.Source == ExternalIdSource.Igdb && x.ExternalId == "114536");
    }

    [Fact]
    public async Task EnrichFromIgdbAsync_UpgradesPendingSteamStub()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new GoodPlaysDbContext(options);
        const uint appId = 553850u;
        var now = DateTimeOffset.UtcNow;
        var stub = new Game
        {
            Id = Guid.NewGuid(),
            Title = "HELLDIVERS™ 2",
            SortTitle = "helldivers 2",
            Slug = "helldivers-2-steam",
            MetadataStatus = MetadataStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        stub.ExternalIds.Add(new GameExternalId
        {
            GameId = stub.Id,
            Source = ExternalIdSource.Steam,
            ExternalId = appId.ToString()
        });
        context.Games.Add(stub);
        await context.SaveChangesAsync();

        var igdbClient = new MappingIgdbClient(appId, 290987L, new IgdbSearchResult(
            290987,
            "Helldivers 2",
            "helldivers-2",
            "https://images.igdb.com/igdb/image/upload/t_cover_big/co7d9j.jpg",
            "Co-op shooter",
            null));

        var service = new GameCatalogService(context, igdbClient);
        var enriched = await service.EnrichFromIgdbAsync(stub, appId, CancellationToken.None);

        Assert.NotNull(enriched);
        Assert.Equal("Helldivers 2", enriched!.Title);
        Assert.Equal(MetadataStatus.Complete, enriched.MetadataStatus);
        Assert.NotNull(enriched.CoverUrl);
    }

    private sealed class MappingIgdbClient(uint steamAppId, long igdbId, IgdbSearchResult game) : IIgdbClient
    {
        public bool IsConfigured => true;

        public Task<IReadOnlyList<IgdbSearchResult>> SearchGamesAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IgdbSearchResult>>([game]);

        public Task<IgdbSearchResult?> GetGameAsync(long requestedIgdbId, CancellationToken cancellationToken) =>
            Task.FromResult(requestedIgdbId == igdbId ? game : null);

        public Task<long?> FindIgdbIdBySteamAppIdAsync(uint requestedAppId, CancellationToken cancellationToken) =>
            Task.FromResult(requestedAppId == steamAppId ? igdbId : (long?)null);
    }
}

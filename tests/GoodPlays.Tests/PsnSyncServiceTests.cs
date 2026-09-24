using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Psn;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoodPlays.Tests;

public class PsnSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_AddsNewLibraryEntriesFromPsnTitles()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        var (context, userId, connectionId) = await SeedPsnConnectionAsync(encryption);
        await using (context)
        {
            var existingGame = new Game
            {
                Id = Guid.NewGuid(),
                Title = "Hades",
                SortTitle = "hades",
                Slug = "hades",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            existingGame.ExternalIds.Add(new GameExternalId
            {
                GameId = existingGame.Id,
                Source = ExternalIdSource.Psn,
                ExternalId = "CUSA27300_00"
            });
            context.Games.Add(existingGame);
            await context.SaveChangesAsync();

            var service = CreateService(context, encryption, new FakePsnClient([
                new PsnTitleStat("CUSA27300_00", "Hades", 10m, null, null, 5, "ps5_native_game")
            ]));

            var result = await service.SyncAsync(userId, CancellationToken.None);

            Assert.Equal(1, result.AddedCount);
            var entry = await context.LibraryEntries.SingleAsync();
            Assert.Equal(LibraryEntrySource.PsnSync, entry.Source);
            Assert.Equal(HoursPlayedSource.Psn, entry.HoursPlayedSource);
            Assert.Equal(10m, entry.HoursPlayed);
            Assert.Equal("CUSA27300_00", entry.PlatformExternalId);

            var connection = await context.PlatformConnections.FindAsync(connectionId);
            Assert.NotNull(connection?.LastSyncAt);
        }
    }

    [Fact]
    public async Task SyncAsync_ReusesExistingGameWhenMultiplePsnTitleIdsMatchSameTitle()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        var (context, userId, _) = await SeedPsnConnectionAsync(encryption);
        await using (context)
        {
            var existingGame = new Game
            {
                Id = Guid.NewGuid(),
                Title = "Hades",
                SortTitle = "hades",
                Slug = "hades",
                MetadataStatus = MetadataStatus.Complete,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            existingGame.ExternalIds.Add(new GameExternalId
            {
                GameId = existingGame.Id,
                Source = ExternalIdSource.Psn,
                ExternalId = "CUSA27300_00"
            });
            context.Games.Add(existingGame);
            await context.SaveChangesAsync();

            var service = CreateService(context, encryption, new FakePsnClient([
                new PsnTitleStat("CUSA27300_00", "Hades", 10m, null, null, 5, "ps5_native_game"),
                new PsnTitleStat("CUSA15081_00", "Hades", 4m, null, null, 2, "ps4_game")
            ]));

            var result = await service.SyncAsync(userId, CancellationToken.None);

            Assert.Equal(2, result.AddedCount);
            Assert.Equal(0, result.UpdatedCount);
            Assert.Equal(2, await context.LibraryEntries.CountAsync());
            Assert.Contains(
                await context.LibraryEntries.ToListAsync(),
                e => e.Source == LibraryEntrySource.PsnSync && e.PlatformExternalId == "CUSA27300_00");
            Assert.Contains(
                await context.LibraryEntries.ToListAsync(),
                e => e.Source == LibraryEntrySource.PsnSync && e.PlatformExternalId == "CUSA15081_00");
        }
    }

    [Fact]
    public async Task SyncAsync_BackfillsCoverWhenPsnGameHasGenresButNoArt()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        var (context, userId, _) = await SeedPsnConnectionAsync(encryption);
        await using (context)
        {
            var genre = new Genre { Id = Guid.NewGuid(), Name = "Action", Slug = "action" };
            context.Genres.Add(genre);

            var existingGame = new Game
            {
                Id = Guid.NewGuid(),
                Title = "Hades",
                SortTitle = "hades",
                Slug = "hades",
                MetadataStatus = MetadataStatus.Complete,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            existingGame.ExternalIds.Add(new GameExternalId
            {
                GameId = existingGame.Id,
                Source = ExternalIdSource.Psn,
                ExternalId = "CUSA27300_00"
            });
            context.Games.Add(existingGame);
            context.GameGenres.Add(new GameGenre { GameId = existingGame.Id, GenreId = genre.Id });
            context.LibraryEntries.Add(new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = existingGame.Id,
                Source = LibraryEntrySource.PsnSync,
                PlatformExternalId = "CUSA27300_00",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            const string coverUrl = "https://images.igdb.com/igdb/image/upload/t_cover_big/co1xxy.jpg";
            var igdb = new ConfigurableIgdbClient(
                new IgdbSearchResult(42, "Hades", "hades", coverUrl, "Roguelike", null),
                new IgdbGameDetails(
                    42,
                    "Hades",
                    "hades",
                    coverUrl,
                    "Roguelike",
                    null,
                    null,
                    null,
                    GameType.Base,
                    null,
                    [new IgdbNamedRef(1, "Action")],
                    [],
                    []));

            var service = new PsnSyncService(
                context,
                new FakePsnClient([new PsnTitleStat("CUSA27300_00", "Hades", 10m, null, null, 5, "ps5_native_game")]),
                new GameCatalogService(context, igdb),
                encryption,
                NullLogger<PsnSyncService>.Instance);

            await service.SyncAsync(userId, CancellationToken.None);

            var game = await context.Games.SingleAsync();
            Assert.Equal(coverUrl, game.CoverUrl);
        }
    }

    [Fact]
    public async Task SyncAsync_SkipsPlaytimeUpdateWhenLocked()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        var (context, userId, _) = await SeedPsnConnectionAsync(encryption);
        await using (context)
        {
            var game = new Game
            {
                Id = Guid.NewGuid(),
                Title = "Hades",
                SortTitle = "hades",
                Slug = "hades",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            game.ExternalIds.Add(new GameExternalId
            {
                GameId = game.Id,
                Source = ExternalIdSource.Psn,
                ExternalId = "CUSA27300_00"
            });
            context.Games.Add(game);
            context.LibraryEntries.Add(new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = game.Id,
                HoursPlayed = 1m,
                HoursPlayedLocked = true,
                HoursPlayedSource = HoursPlayedSource.Psn,
                Source = LibraryEntrySource.PsnSync,
                PlatformExternalId = "CUSA27300_00",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            var service = CreateService(context, encryption, new FakePsnClient([
                new PsnTitleStat("CUSA27300_00", "Hades", 10m, null, null, 5, "ps5_native_game")
            ]));

            var result = await service.SyncAsync(userId, CancellationToken.None);

            Assert.Equal(1, result.SkippedCount);
            var entry = await context.LibraryEntries.SingleAsync();
            Assert.Equal(1m, entry.HoursPlayed);
            Assert.Equal(HoursPlayedSource.Psn, entry.HoursPlayedSource);
        }
    }

    private static PsnSyncService CreateService(
        GoodPlaysDbContext context,
        ITokenEncryptionService encryption,
        IPsnClient psnClient)
    {
        var igdb = new FakeIgdbClient();
        var catalog = new GameCatalogService(context, igdb);
        return new PsnSyncService(
            context,
            psnClient,
            catalog,
            encryption,
            NullLogger<PsnSyncService>.Instance);
    }

    private static async Task<(GoodPlaysDbContext Context, Guid UserId, Guid ConnectionId)> SeedPsnConnectionAsync(
        ITokenEncryptionService encryption)
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = $"clerk_{userId:N}",
            Email = "psn-sync@test.local",
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.PlatformConnections.Add(new PlatformConnection
        {
            Id = connectionId,
            UserId = userId,
            Platform = PlatformConnectionPlatform.Psn,
            ExternalAccountId = "me",
            AccessTokenEnc = encryption.Encrypt("access-token"),
            RefreshTokenEnc = encryption.Encrypt("refresh-token"),
            TokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            SyncEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return (context, userId, connectionId);
    }

    private sealed class FakePsnClient(IReadOnlyList<PsnTitleStat> titles) : IPsnClient
    {
        public Task<PsnTokens> ExchangeNpssoAsync(string npsso, CancellationToken cancellationToken) =>
            Task.FromResult(new PsnTokens("access", "refresh", 3600, 5184000, null));

        public Task<PsnTokens> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) =>
            Task.FromResult(new PsnTokens("access", refreshToken, 3600, 5184000, null));

        public Task<PsnUserProfile> GetProfileAsync(string accessToken, string accountId, CancellationToken cancellationToken) =>
            Task.FromResult(new PsnUserProfile(accountId, "TestGamer"));

        public Task<IReadOnlyList<PsnTitleStat>> GetPlayedTitlesAsync(
            string accessToken,
            string accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult(titles);
    }

    private sealed class FakeIgdbClient : IIgdbClient
    {
        public bool IsConfigured => false;

        public Task<IReadOnlyList<IgdbSearchResult>> SearchGamesAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IgdbSearchResult>>([]);

        public Task<IgdbSearchResult?> GetGameAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult<IgdbSearchResult?>(null);

        public Task<IgdbGameDetails?> GetGameDetailsAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult<IgdbGameDetails?>(null);

        public Task<long?> FindIgdbIdBySteamAppIdAsync(uint steamAppId, CancellationToken cancellationToken) =>
            Task.FromResult<long?>(null);
    }

    private sealed class ConfigurableIgdbClient(IgdbSearchResult game, IgdbGameDetails details) : IIgdbClient
    {
        public bool IsConfigured => true;

        public Task<IReadOnlyList<IgdbSearchResult>> SearchGamesAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IgdbSearchResult>>([game]);

        public Task<IgdbSearchResult?> GetGameAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult(igdbId == game.IgdbId ? game : null);

        public Task<IgdbGameDetails?> GetGameDetailsAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult(igdbId == details.IgdbId ? details : null);

        public Task<long?> FindIgdbIdBySteamAppIdAsync(uint steamAppId, CancellationToken cancellationToken) =>
            Task.FromResult<long?>(null);
    }
}

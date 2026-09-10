using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Services;
using GoodPlays.Infrastructure.Steam;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoodPlays.Tests;

public class SteamSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_AddsNewLibraryEntriesFromSteamGames()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        var (context, userId, connectionId) = await SeedSteamConnectionAsync(encryption);
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
                Source = ExternalIdSource.Steam,
                ExternalId = "1145360"
            });
            context.Games.Add(existingGame);
            await context.SaveChangesAsync();

            var service = CreateService(context, encryption, new FakeSteamClient([
                new SteamOwnedGame(1145360, "Hades", 600, 0, 1700000000, 10m)
            ]));

            var result = await service.SyncAsync(userId, CancellationToken.None);

            Assert.Equal(1, result.AddedCount);
            var entry = await context.LibraryEntries.SingleAsync();
            Assert.Equal(LibraryEntrySource.SteamSync, entry.Source);
            Assert.Equal(HoursPlayedSource.Steam, entry.HoursPlayedSource);
            Assert.Equal(10m, entry.HoursPlayed);
            Assert.Equal("1145360", entry.PlatformExternalId);

            var connection = await context.PlatformConnections.FindAsync(connectionId);
            Assert.NotNull(connection?.LastSyncAt);
        }
    }

    [Fact]
    public async Task SyncAsync_UpdatesPlaytimeWhenNotLocked()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        var (context, userId, _) = await SeedSteamConnectionAsync(encryption);
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
                Source = ExternalIdSource.Steam,
                ExternalId = "1145360"
            });
            context.Games.Add(game);
            context.LibraryEntries.Add(new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = game.Id,
                HoursPlayed = 1m,
                HoursPlayedSource = HoursPlayedSource.Manual,
                Source = LibraryEntrySource.Manual,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            var service = CreateService(context, encryption, new FakeSteamClient([
                new SteamOwnedGame(1145360, "Hades", 600, 0, null, 10m)
            ]));

            var result = await service.SyncAsync(userId, CancellationToken.None);

            Assert.Equal(1, result.UpdatedCount);
            var entry = await context.LibraryEntries.SingleAsync();
            Assert.Equal(10m, entry.HoursPlayed);
            Assert.Equal(HoursPlayedSource.Steam, entry.HoursPlayedSource);
        }
    }

    [Fact]
    public async Task SyncAsync_ReconcilesExistingLibraryEntryByTitle()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        var (context, userId, _) = await SeedSteamConnectionAsync(encryption);
        await using (context)
        {
            var igdbGame = new Game
            {
                Id = Guid.NewGuid(),
                Title = "Helldivers 2",
                SortTitle = "helldivers 2",
                Slug = "helldivers-2",
                CoverUrl = "https://images.igdb.com/co7d9j.jpg",
                MetadataStatus = MetadataStatus.Complete,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            igdbGame.ExternalIds.Add(new GameExternalId
            {
                GameId = igdbGame.Id,
                Source = ExternalIdSource.Igdb,
                ExternalId = "290987"
            });
            context.Games.Add(igdbGame);
            context.LibraryEntries.Add(new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = igdbGame.Id,
                Source = LibraryEntrySource.Manual,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            var service = CreateService(context, encryption, new FakeSteamClient([
                new SteamOwnedGame(553850, "HELLDIVERS™ 2", 1200, 0, null, 20m)
            ]));

            var result = await service.SyncAsync(userId, CancellationToken.None);

            Assert.Equal(1, result.UpdatedCount);
            var entry = await context.LibraryEntries.SingleAsync();
            Assert.Equal(igdbGame.Id, entry.GameId);
            Assert.Equal(20m, entry.HoursPlayed);
            Assert.Contains(
                context.GameExternalIds,
                x => x.GameId == igdbGame.Id && x.Source == ExternalIdSource.Steam && x.ExternalId == "553850");
        }
    }

    [Fact]
    public async Task SyncAsync_SkipsPlaytimeUpdateWhenLocked()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        var (context, userId, _) = await SeedSteamConnectionAsync(encryption);
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
                Source = ExternalIdSource.Steam,
                ExternalId = "1145360"
            });
            context.Games.Add(game);
            context.LibraryEntries.Add(new LibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = game.Id,
                HoursPlayed = 1m,
                HoursPlayedLocked = true,
                HoursPlayedSource = HoursPlayedSource.Manual,
                Source = LibraryEntrySource.Manual,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            var service = CreateService(context, encryption, new FakeSteamClient([
                new SteamOwnedGame(1145360, "Hades", 600, 0, null, 10m)
            ]));

            var result = await service.SyncAsync(userId, CancellationToken.None);

            Assert.Equal(1, result.SkippedCount);
            var entry = await context.LibraryEntries.SingleAsync();
            Assert.Equal(1m, entry.HoursPlayed);
            Assert.Equal(HoursPlayedSource.Manual, entry.HoursPlayedSource);
        }
    }

    private static SteamSyncService CreateService(
        GoodPlaysDbContext context,
        ITokenEncryptionService encryption,
        ISteamClient steamClient)
    {
        var igdb = new FakeIgdbClient();
        var catalog = new GameCatalogService(context, igdb);
        return new SteamSyncService(
            context,
            steamClient,
            catalog,
            encryption,
            NullLogger<SteamSyncService>.Instance);
    }

    private static async Task<(GoodPlaysDbContext Context, Guid UserId, Guid ConnectionId)> SeedSteamConnectionAsync(
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
            Email = "steam-sync@test.local",
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.PlatformConnections.Add(new PlatformConnection
        {
            Id = connectionId,
            UserId = userId,
            Platform = PlatformConnectionPlatform.Steam,
            ExternalAccountId = "76561198000000000",
            AccessTokenEnc = encryption.Encrypt("test-api-key"),
            SyncEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return (context, userId, connectionId);
    }

    private sealed class FakeSteamClient(IReadOnlyList<SteamOwnedGame> ownedGames) : ISteamClient
    {
        public Task<string?> ResolveVanityUrlAsync(string vanityOrUrl, string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("76561198000000000");

        public Task<SteamPlayerSummary?> GetPlayerSummaryAsync(string steamId64, string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult<SteamPlayerSummary?>(new SteamPlayerSummary(steamId64, "TestPlayer", null, null, 3));

        public Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string steamId64, string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult(ownedGames);

        public Task<IReadOnlyList<SteamRecentlyPlayedGame>> GetRecentlyPlayedGamesAsync(
            string steamId64,
            string apiKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SteamRecentlyPlayedGame>>([]);

        public Task<SteamPlayerAchievementsResult?> GetPlayerAchievementsAsync(
            string steamId64,
            uint appId,
            string apiKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<SteamPlayerAchievementsResult?>(null);
    }

    private sealed class FakeIgdbClient : GoodPlays.Infrastructure.Metadata.IIgdbClient
    {
        public bool IsConfigured => false;

        public Task<IReadOnlyList<GoodPlays.Infrastructure.Metadata.IgdbSearchResult>> SearchGamesAsync(
            string query,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GoodPlays.Infrastructure.Metadata.IgdbSearchResult>>([]);

        public Task<GoodPlays.Infrastructure.Metadata.IgdbSearchResult?> GetGameAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult<GoodPlays.Infrastructure.Metadata.IgdbSearchResult?>(null);

        public Task<long?> FindIgdbIdBySteamAppIdAsync(uint steamAppId, CancellationToken cancellationToken) =>
            Task.FromResult<long?>(null);
    }
}

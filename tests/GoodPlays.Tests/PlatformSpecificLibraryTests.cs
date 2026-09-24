using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Services;
using GoodPlays.Infrastructure.Steam;
using GoodPlays.Infrastructure.Psn;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoodPlays.Tests;

public class PlatformSpecificLibraryTests
{
    [Fact]
    public async Task SyncAsync_KeepsSteamAndPlayStationEntriesSeparateForSameGame()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = $"clerk_{userId:N}",
            Email = "platform@test.local",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var game = new Game
        {
            Id = gameId,
            Title = "Helldivers 2",
            SortTitle = "helldivers 2",
            Slug = "helldivers-2",
            MetadataStatus = MetadataStatus.Complete,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        game.ExternalIds.Add(new GameExternalId
        {
            GameId = gameId,
            Source = ExternalIdSource.Steam,
            ExternalId = "553850"
        });
        game.ExternalIds.Add(new GameExternalId
        {
            GameId = gameId,
            Source = ExternalIdSource.Psn,
            ExternalId = "PPSA01234_00"
        });
        context.Games.Add(game);

        context.PlatformConnections.AddRange(
            new PlatformConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = PlatformConnectionPlatform.Steam,
                ExternalAccountId = "76561198000000000",
                AccessTokenEnc = encryption.Encrypt("steam-key"),
                SyncEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new PlatformConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = PlatformConnectionPlatform.Psn,
                ExternalAccountId = "psn-account",
                AccessTokenEnc = encryption.Encrypt("access"),
                RefreshTokenEnc = encryption.Encrypt("refresh"),
                TokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                SyncEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        await context.SaveChangesAsync();

        await using (context)
        {
            var catalog = new GameCatalogService(context, new DisabledIgdbClient());
            var steamService = new SteamSyncService(
                context,
                new FakeSteamClient([new SteamOwnedGame(553850, "Helldivers 2", 1200, 0, null, 20m)]),
                catalog,
                encryption,
                NullLogger<SteamSyncService>.Instance);
            var psnService = new PsnSyncService(
                context,
                new FakePsnClient([new PsnTitleStat("PPSA01234_00", "Helldivers 2", 8m, null, null, 3, "ps5_native_game")]),
                catalog,
                encryption,
                NullLogger<PsnSyncService>.Instance);

            await steamService.SyncAsync(userId, CancellationToken.None);
            await psnService.SyncAsync(userId, CancellationToken.None);

            var entries = await context.LibraryEntries
                .Where(e => e.UserId == userId)
                .OrderBy(e => e.Source)
                .ToListAsync();

            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, e => e.Source == LibraryEntrySource.SteamSync && e.HoursPlayed == 20m);
            Assert.Contains(entries, e => e.Source == LibraryEntrySource.PsnSync && e.HoursPlayed == 8m);
            Assert.Equal(gameId, entries[0].GameId);
            Assert.Equal(gameId, entries[1].GameId);
        }
    }

    private sealed class DisabledIgdbClient : GoodPlays.Infrastructure.Metadata.IIgdbClient
    {
        public bool IsConfigured => false;

        public Task<IReadOnlyList<GoodPlays.Infrastructure.Metadata.IgdbSearchResult>> SearchGamesAsync(
            string query,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GoodPlays.Infrastructure.Metadata.IgdbSearchResult>>([]);

        public Task<GoodPlays.Infrastructure.Metadata.IgdbSearchResult?> GetGameAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult<GoodPlays.Infrastructure.Metadata.IgdbSearchResult?>(null);

        public Task<GoodPlays.Infrastructure.Metadata.IgdbGameDetails?> GetGameDetailsAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult<GoodPlays.Infrastructure.Metadata.IgdbGameDetails?>(null);

        public Task<long?> FindIgdbIdBySteamAppIdAsync(uint steamAppId, CancellationToken cancellationToken) =>
            Task.FromResult<long?>(null);
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

        public Task<SteamGameSchema?> GetGameSchemaAsync(uint appId, string? apiKey, CancellationToken cancellationToken) =>
            Task.FromResult<SteamGameSchema?>(null);

        public Task<IReadOnlyList<SteamNewsItem>> GetNewsForAppAsync(uint appId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SteamNewsItem>>([]);
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
}

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

public class PlatformConnectionServiceTests
{
    [Fact]
    public async Task ConnectSteamAsync_PersistsEncryptedApiKeyAndProfile()
    {
        var (context, userId) = await SeedUserAsync();
        await using (context)
        {
            var steamClient = new FakeSteamClient();
            var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
            var service = new PlatformConnectionService(
                context,
                steamClient,
                encryption,
                NullLogger<PlatformConnectionService>.Instance);

            var connection = await service.ConnectSteamAsync(
                userId,
                "76561198000000000",
                "user-api-key",
                CancellationToken.None);

            Assert.Equal(PlatformConnectionPlatform.Steam, connection.Platform);
            Assert.Equal("76561198000000000", connection.ExternalAccountId);
            Assert.Equal("TestPlayer", connection.DisplayName);
            Assert.True(connection.SyncEnabled);

            var stored = await context.PlatformConnections.SingleAsync();
            Assert.NotEqual("user-api-key", stored.AccessTokenEnc);
            Assert.Equal("user-api-key", encryption.Decrypt(stored.AccessTokenEnc!));
        }
    }

    [Fact]
    public async Task DisconnectSteamAsync_RemovesConnection()
    {
        var (context, userId) = await SeedUserAsync();
        await using (context)
        {
            context.PlatformConnections.Add(new PlatformConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = PlatformConnectionPlatform.Steam,
                ExternalAccountId = "76561198000000000",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new PlatformConnectionService(
                context,
                new FakeSteamClient(),
                new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider()),
                NullLogger<PlatformConnectionService>.Instance);

            var removed = await service.DisconnectSteamAsync(userId, CancellationToken.None);

            Assert.True(removed);
            Assert.Empty(await context.PlatformConnections.ToListAsync());
        }
    }

    private static async Task<(GoodPlaysDbContext Context, Guid UserId)> SeedUserAsync()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = $"clerk_{userId:N}",
            Email = "steam@test.local",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return (context, userId);
    }

    private sealed class FakeSteamClient : ISteamClient
    {
        public Task<string?> ResolveVanityUrlAsync(string vanityOrUrl, string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("76561198000000000");

        public Task<SteamPlayerSummary?> GetPlayerSummaryAsync(string steamId64, string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult<SteamPlayerSummary?>(new SteamPlayerSummary(
                steamId64,
                "TestPlayer",
                null,
                "https://steamcommunity.com/profiles/" + steamId64,
                3));

        public Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string steamId64, string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SteamOwnedGame>>([]);

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
}

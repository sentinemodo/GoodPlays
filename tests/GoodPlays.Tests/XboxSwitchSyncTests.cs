using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Nintendo;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Services;
using GoodPlays.Infrastructure.Xbox;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoodPlays.Tests;

public class XboxSwitchSyncTests
{
    [Fact]
    public void XboxAuth_ReadsCodeFromDesktopRedirect()
    {
        var code = XboxAuth.ReadAuthorizationCode("https://login.live.com/oauth20_desktop.srf?code=abc%2B123&lc=1033");
        Assert.Equal("abc+123", code);
    }

    [Fact]
    public void NintendoLogin_ReadsSessionTokenCodeFromFragment()
    {
        var code = NintendoLogin.ReadSessionTokenCode("npf5c38e31cd085304b://auth#session_token_code=hello&state=xyz");
        Assert.Equal("hello", code);
        var login = NintendoLogin.Create();
        Assert.Contains("session_token_code_challenge", login.Url);
        Assert.False(string.IsNullOrWhiteSpace(login.CodeVerifier));
    }

    [Fact]
    public void XboxRequestSigner_ProducesVersionedSignature()
    {
        var signer = new XboxRequestSigner();
        var signature = signer.Sign("POST", "/device/authenticate", "{}"u8.ToArray());
        var bytes = Convert.FromBase64String(signature);
        Assert.True(bytes.Length > 12);
        Assert.Equal(1, bytes[3]);
    }

    [Fact]
    public async Task XboxSync_AddsPlayedTitleWithoutHours()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        await using var context = await SeedAsync(encryption, PlatformConnectionPlatform.Xbox, refresh: true);
        var userId = context.Users.Single().Id;
        var service = new XboxSyncService(
            context,
            new FakeXboxClient(),
            new GameCatalogService(context, new OfflineIgdb()),
            encryption,
            NullLogger<XboxSyncService>.Instance);

        var result = await service.SyncAsync(userId, CancellationToken.None);

        Assert.Equal(1, result.AddedCount);
        var entry = await context.LibraryEntries.SingleAsync();
        Assert.Equal(LibraryEntrySource.XboxSync, entry.Source);
        Assert.Null(entry.HoursPlayed);
        Assert.Equal(HoursPlayedSource.Manual, entry.HoursPlayedSource);
        Assert.Equal("1234", entry.PlatformExternalId);
        Assert.Equal(LibraryStatus.Owned, entry.Status);
    }

    [Fact]
    public async Task SwitchSync_AddsPlayedTitleWithMinutesAsHours()
    {
        var encryption = new DataProtectionTokenEncryptionService(new EphemeralDataProtectionProvider());
        await using var context = await SeedAsync(encryption, PlatformConnectionPlatform.Switch, refresh: false);
        var userId = context.Users.Single().Id;
        var service = new SwitchSyncService(
            context,
            new FakeNintendoClient(),
            new GameCatalogService(context, new OfflineIgdb()),
            encryption,
            NullLogger<SwitchSyncService>.Instance);

        var result = await service.SyncAsync(userId, CancellationToken.None);

        Assert.Equal(1, result.AddedCount);
        var entry = await context.LibraryEntries.SingleAsync();
        Assert.Equal(LibraryEntrySource.SwitchSync, entry.Source);
        Assert.Equal(2.5m, entry.HoursPlayed);
        Assert.Equal(HoursPlayedSource.Switch, entry.HoursPlayedSource);
        Assert.Equal("0100", entry.PlatformExternalId);
    }

    private static async Task<GoodPlaysDbContext> SeedAsync(
        ITokenEncryptionService encryption,
        PlatformConnectionPlatform platform,
        bool refresh)
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
            Email = "sync@test.local",
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.PlatformConnections.Add(new PlatformConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = platform,
            ExternalAccountId = "account",
            AccessTokenEnc = encryption.Encrypt("session"),
            RefreshTokenEnc = refresh ? encryption.Encrypt("refresh") : null,
            SyncEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class FakeXboxClient : IXboxClient
    {
        public XboxLoginRequest CreateLogin() => new("https://login.live.com");

        public Task<XboxAccount> ConnectAsync(string callbackUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<XboxLibrary> GetLibraryAsync(string refreshToken, CancellationToken cancellationToken) =>
            Task.FromResult(new XboxLibrary(
                "rotated",
                "xuid",
                "Gamer",
                [new XboxPlayedTitle("1234", "Halo Infinite", DateTimeOffset.Parse("2026-01-02T00:00:00Z"))]));
    }

    private sealed class FakeNintendoClient : INintendoClient
    {
        public Task<string> ExchangeSessionTokenAsync(string sessionTokenCode, string codeVerifier, CancellationToken cancellationToken) =>
            Task.FromResult("session");

        public Task<NintendoAccount> GetAccountAsync(string sessionToken, CancellationToken cancellationToken) =>
            Task.FromResult(new NintendoAccount("na", "Player"));

        public Task<IReadOnlyList<NintendoPlayedTitle>> GetPlayHistoryAsync(string sessionToken, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NintendoPlayedTitle>>([
                new NintendoPlayedTitle("0100", "The Legend of Zelda", 2.5m, null, DateTimeOffset.Parse("2026-02-01T00:00:00Z"), "Switch")
            ]);
    }

    private sealed class OfflineIgdb : IIgdbClient
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
}

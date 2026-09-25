using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Nintendo;
using GoodPlays.Infrastructure.Psn;
using GoodPlays.Infrastructure.Steam;
using GoodPlays.Infrastructure.Xbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Infrastructure.Services;

public sealed class PlatformConnectionService(
    GoodPlaysDbContext dbContext,
    ISteamClient steamClient,
    IPsnClient psnClient,
    IXboxClient xboxClient,
    INintendoClient nintendoClient,
    ITokenEncryptionService tokenEncryption,
    ILogger<PlatformConnectionService> logger) : IPlatformConnectionService
{
    public async Task<IReadOnlyList<PlatformConnectionDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.PlatformConnections
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Platform)
            .Select(x => new PlatformConnectionDto(
                x.Platform,
                x.ExternalAccountId,
                x.DisplayName,
                x.LastSyncAt,
                x.SyncEnabled,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlatformConnectionDto> ConnectSteamAsync(
        Guid userId,
        string steamIdOrUrl,
        string apiKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(steamIdOrUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var steamId64 = SteamIdParser.TryExtractSteamId64(steamIdOrUrl)
            ?? await steamClient.ResolveVanityUrlAsync(steamIdOrUrl, apiKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(steamId64))
        {
            throw new ArgumentException("Could not resolve a Steam ID from the provided value.", nameof(steamIdOrUrl));
        }

        var summary = await steamClient.GetPlayerSummaryAsync(steamId64, apiKey, cancellationToken);
        if (summary is null)
        {
            throw new SteamApiException(SteamApiErrorCode.PrivateProfile, "Steam profile was not found for the resolved ID.");
        }

        var now = DateTimeOffset.UtcNow;
        var encryptedKey = tokenEncryption.Encrypt(apiKey.Trim());

        var existing = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Platform == PlatformConnectionPlatform.Steam, cancellationToken);

        if (existing is null)
        {
            existing = new PlatformConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = PlatformConnectionPlatform.Steam,
                ExternalAccountId = steamId64,
                CreatedAt = now
            };
            dbContext.PlatformConnections.Add(existing);
        }

        existing.ExternalAccountId = steamId64;
        existing.DisplayName = summary.PersonaName;
        existing.AccessTokenEnc = encryptedKey;
        existing.RefreshTokenEnc = null;
        existing.TokenExpiresAt = null;
        existing.SyncEnabled = true;
        existing.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Connected Steam account {SteamId} for user {UserId}", steamId64, userId);

        return new PlatformConnectionDto(
            existing.Platform,
            existing.ExternalAccountId,
            existing.DisplayName,
            existing.LastSyncAt,
            existing.SyncEnabled,
            existing.CreatedAt);
    }

    public async Task<bool> DisconnectSteamAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Platform == PlatformConnectionPlatform.Steam, cancellationToken);

        if (connection is null)
        {
            return false;
        }

        dbContext.PlatformConnections.Remove(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Disconnected Steam account for user {UserId}", userId);
        return true;
    }

    public async Task<PlatformConnectionDto> ConnectPsnAsync(
        Guid userId,
        string npsso,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(npsso);

        var tokens = await psnClient.ExchangeNpssoAsync(NpssoParser.Normalize(npsso), cancellationToken);
        var accountId = PsnJwtHelper.TryGetSubject(tokens.IdToken);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new PsnApiException(
                PsnApiErrorCode.Unauthorized,
                "PSN token response did not include an account ID.");
        }

        var profile = await psnClient.GetProfileAsync(tokens.AccessToken, accountId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Platform == PlatformConnectionPlatform.Psn, cancellationToken);

        if (existing is null)
        {
            existing = new PlatformConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = PlatformConnectionPlatform.Psn,
                ExternalAccountId = profile.AccountId,
                CreatedAt = now
            };
            dbContext.PlatformConnections.Add(existing);
        }

        existing.ExternalAccountId = profile.AccountId;
        existing.DisplayName = profile.OnlineId;
        existing.AccessTokenEnc = tokenEncryption.Encrypt(tokens.AccessToken);
        existing.RefreshTokenEnc = tokenEncryption.Encrypt(tokens.RefreshToken);
        existing.TokenExpiresAt = now.AddSeconds(tokens.ExpiresInSeconds);
        existing.SyncEnabled = true;
        existing.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Connected PSN account {AccountId} for user {UserId}", profile.AccountId, userId);

        return new PlatformConnectionDto(
            existing.Platform,
            existing.ExternalAccountId,
            existing.DisplayName,
            existing.LastSyncAt,
            existing.SyncEnabled,
            existing.CreatedAt);
    }

    public async Task<bool> DisconnectPsnAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Platform == PlatformConnectionPlatform.Psn, cancellationToken);

        if (connection is null)
        {
            return false;
        }

        dbContext.PlatformConnections.Remove(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Disconnected PSN account for user {UserId}", userId);
        return true;
    }

    public XboxLoginRequest CreateXboxLogin() => xboxClient.CreateLogin();

    public async Task<PlatformConnectionDto> ConnectXboxAsync(
        Guid userId,
        string callbackUrl,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);
        var account = await xboxClient.ConnectAsync(callbackUrl, cancellationToken);
        return await UpsertAsync(
            userId,
            PlatformConnectionPlatform.Xbox,
            account.Xuid,
            account.Gamertag,
            accessToken: null,
            refreshToken: account.RefreshToken,
            expiresAt: null,
            cancellationToken);
    }

    public Task<bool> DisconnectXboxAsync(Guid userId, CancellationToken cancellationToken) =>
        DisconnectAsync(userId, PlatformConnectionPlatform.Xbox, cancellationToken);

    public NintendoLoginRequest CreateSwitchLogin() => NintendoLogin.Create();

    public async Task<PlatformConnectionDto> ConnectSwitchAsync(
        Guid userId,
        string callbackUrl,
        string? codeVerifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);
        var sessionToken = callbackUrl.Contains("session_token_code", StringComparison.Ordinal) ||
                           callbackUrl.Contains("npf", StringComparison.OrdinalIgnoreCase)
            ? await nintendoClient.ExchangeSessionTokenAsync(
                NintendoLogin.ReadSessionTokenCode(callbackUrl),
                codeVerifier ?? throw new ArgumentException("codeVerifier is required for a Nintendo login link.", nameof(codeVerifier)),
                cancellationToken)
            : NintendoLogin.ReadSessionTokenCode(callbackUrl);
        var account = await nintendoClient.GetAccountAsync(sessionToken, cancellationToken);
        return await UpsertAsync(
            userId,
            PlatformConnectionPlatform.Switch,
            account.AccountId,
            account.DisplayName ?? "Nintendo Account",
            accessToken: sessionToken,
            refreshToken: null,
            expiresAt: null,
            cancellationToken);
    }

    public Task<bool> DisconnectSwitchAsync(Guid userId, CancellationToken cancellationToken) =>
        DisconnectAsync(userId, PlatformConnectionPlatform.Switch, cancellationToken);

    private async Task<PlatformConnectionDto> UpsertAsync(
        Guid userId,
        PlatformConnectionPlatform platform,
        string externalAccountId,
        string? displayName,
        string? accessToken,
        string? refreshToken,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Platform == platform, cancellationToken);
        if (existing is null)
        {
            existing = new PlatformConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = platform,
                ExternalAccountId = externalAccountId,
                CreatedAt = now
            };
            dbContext.PlatformConnections.Add(existing);
        }

        existing.ExternalAccountId = externalAccountId;
        existing.DisplayName = displayName;
        existing.AccessTokenEnc = accessToken is null ? null : tokenEncryption.Encrypt(accessToken);
        existing.RefreshTokenEnc = refreshToken is null ? null : tokenEncryption.Encrypt(refreshToken);
        existing.TokenExpiresAt = expiresAt;
        existing.SyncEnabled = true;
        existing.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Connected {Platform} account {AccountId} for user {UserId}", platform, externalAccountId, userId);
        return new PlatformConnectionDto(
            existing.Platform,
            existing.ExternalAccountId,
            existing.DisplayName,
            existing.LastSyncAt,
            existing.SyncEnabled,
            existing.CreatedAt);
    }

    private async Task<bool> DisconnectAsync(
        Guid userId,
        PlatformConnectionPlatform platform,
        CancellationToken cancellationToken)
    {
        var connection = await dbContext.PlatformConnections
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Platform == platform, cancellationToken);
        if (connection is null)
        {
            return false;
        }

        dbContext.PlatformConnections.Remove(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Disconnected {Platform} account for user {UserId}", platform, userId);
        return true;
    }
}

using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Steam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Infrastructure.Services;

public sealed class PlatformConnectionService(
    GoodPlaysDbContext dbContext,
    ISteamClient steamClient,
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
}

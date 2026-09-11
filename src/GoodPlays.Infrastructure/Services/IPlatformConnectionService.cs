using GoodPlays.Domain.Enums;

namespace GoodPlays.Infrastructure.Services;

public sealed record PlatformConnectionDto(
    PlatformConnectionPlatform Platform,
    string ExternalAccountId,
    string? DisplayName,
    DateTimeOffset? LastSyncAt,
    bool SyncEnabled,
    DateTimeOffset ConnectedAt);

public interface IPlatformConnectionService
{
    Task<IReadOnlyList<PlatformConnectionDto>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<PlatformConnectionDto> ConnectSteamAsync(
        Guid userId,
        string steamIdOrUrl,
        string apiKey,
        CancellationToken cancellationToken);

    Task<bool> DisconnectSteamAsync(Guid userId, CancellationToken cancellationToken);

    Task<PlatformConnectionDto> ConnectPsnAsync(
        Guid userId,
        string npsso,
        CancellationToken cancellationToken);

    Task<bool> DisconnectPsnAsync(Guid userId, CancellationToken cancellationToken);
}

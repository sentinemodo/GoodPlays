using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;

namespace GoodPlays.Infrastructure.Services;

public sealed record GameSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    string? CoverUrl,
    long? IgdbId,
    string Source);

public interface IGameCatalogService
{
    Task<IReadOnlyList<GameSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken);

    Task<Game?> ImportFromIgdbAsync(long igdbId, CancellationToken cancellationToken);

    Task<Game?> FindBySteamAppIdAsync(uint appId, CancellationToken cancellationToken);

    Task<Game> ImportFromSteamAppAsync(uint appId, string title, CancellationToken cancellationToken);

    Task<Game> ResolveForSteamSyncAsync(uint appId, string steamTitle, CancellationToken cancellationToken);

    Task<Game?> EnrichFromIgdbAsync(Game game, uint steamAppId, CancellationToken cancellationToken);

    Task<Game> ResolveForPsnSyncAsync(string titleId, string psnTitle, CancellationToken cancellationToken);

    Task<Game?> EnrichFromIgdbForPsnAsync(
        Game game,
        string titleId,
        string psnTitle,
        CancellationToken cancellationToken);

    Task<Game> ResolveForExternalTitleAsync(
        ExternalIdSource source,
        string titleId,
        string title,
        CancellationToken cancellationToken);
}

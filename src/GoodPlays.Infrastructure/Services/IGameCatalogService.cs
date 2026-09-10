using GoodPlays.Domain.Entities;
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
}

namespace GoodPlays.Infrastructure.Metadata;

public interface IIgdbClient
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<IgdbSearchResult>> SearchGamesAsync(string query, CancellationToken cancellationToken);

    Task<IgdbSearchResult?> GetGameAsync(long igdbId, CancellationToken cancellationToken);

    Task<long?> FindIgdbIdBySteamAppIdAsync(uint steamAppId, CancellationToken cancellationToken);
}

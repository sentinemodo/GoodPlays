namespace GoodPlays.Infrastructure.Steam;

public interface ISteamClient
{
    Task<string?> ResolveVanityUrlAsync(string vanityOrUrl, string apiKey, CancellationToken cancellationToken);

    Task<SteamPlayerSummary?> GetPlayerSummaryAsync(string steamId64, string apiKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string steamId64, string apiKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<SteamRecentlyPlayedGame>> GetRecentlyPlayedGamesAsync(
        string steamId64,
        string apiKey,
        CancellationToken cancellationToken);

    Task<SteamPlayerAchievementsResult?> GetPlayerAchievementsAsync(
        string steamId64,
        uint appId,
        string apiKey,
        CancellationToken cancellationToken);
}

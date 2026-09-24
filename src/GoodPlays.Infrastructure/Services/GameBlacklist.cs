namespace GoodPlays.Infrastructure.Services;

/// <summary>
/// Detects non-game applications synced from platforms (streaming apps, etc.).
/// These may be imported but are hidden from catalog/library browse.
/// </summary>
public static class GameBlacklist
{
    private static readonly string[] BlockedTitleFragments =
    [
        "netflix",
        "amazon prime",
        "prime video",
        "disney+",
        "disney plus",
        "hulu",
        "spotify",
        "youtube",
        "twitch",
        "apple tv",
        "crunchyroll",
        "plex",
        "media player",
        "blu-ray player",
        "dvd player",
        "hbo max",
        "max app",
        "peacock",
        "paramount+",
        "apple music",
        "tidal"
    ];

    public static bool IsNonGameApplication(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized = title.Trim().ToLowerInvariant();
        return BlockedTitleFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
    }
}

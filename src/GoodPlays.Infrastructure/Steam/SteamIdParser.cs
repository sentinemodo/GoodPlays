using System.Text.RegularExpressions;

namespace GoodPlays.Infrastructure.Steam;

public static partial class SteamIdParser
{
    [GeneratedRegex(@"^\d{17}$", RegexOptions.Compiled)]
    private static partial Regex SteamId64Regex();

    [GeneratedRegex(@"steamcommunity\.com/profiles/(?<id>\d{17})", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ProfileUrlRegex();

    [GeneratedRegex(@"steamcommunity\.com/id/(?<vanity>[^/?#]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex VanityUrlRegex();

    public static bool IsSteamId64(string input) =>
        !string.IsNullOrWhiteSpace(input) && SteamId64Regex().IsMatch(input.Trim());

    public static string? TryExtractSteamId64(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (IsSteamId64(trimmed))
        {
            return trimmed;
        }

        var profileMatch = ProfileUrlRegex().Match(trimmed);
        if (profileMatch.Success)
        {
            return profileMatch.Groups["id"].Value;
        }

        return null;
    }

    public static string? TryExtractVanityName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        var vanityMatch = VanityUrlRegex().Match(trimmed);
        if (vanityMatch.Success)
        {
            return vanityMatch.Groups["vanity"].Value;
        }

        if (IsSteamId64(trimmed) || ProfileUrlRegex().IsMatch(trimmed))
        {
            return null;
        }

        return trimmed;
    }
}

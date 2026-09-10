using System.Text.RegularExpressions;

namespace GoodPlays.Infrastructure.Steam;

public static partial class SteamTitleNormalizer
{
    [GeneratedRegex(@"[™®]", RegexOptions.Compiled)]
    private static partial Regex TrademarkRegex();

    [GeneratedRegex(@"\s*\((demo|beta|soundtrack|dlc|pack|edition|bundle)[^)]*\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SuffixRegex();

    public static string Normalize(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var normalized = TrademarkRegex().Replace(title.Trim(), string.Empty);
        normalized = SuffixRegex().Replace(normalized, string.Empty);
        return normalized.Trim();
    }
}

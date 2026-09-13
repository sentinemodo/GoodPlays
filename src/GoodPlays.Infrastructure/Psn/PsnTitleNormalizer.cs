using System.Text.RegularExpressions;

namespace GoodPlays.Infrastructure.Psn;

public static partial class PsnTitleNormalizer
{
    [GeneratedRegex(@"[™®]", RegexOptions.Compiled)]
    private static partial Regex TrademarkRegex();

    [GeneratedRegex(@"\s*\((ps4|ps5|ps3|vita|pc)[^)]*\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PlatformSuffixRegex();

    [GeneratedRegex(@"\s*\((?:[^)]*\s)?(?:demo|beta|soundtrack|dlc|pack|edition|bundle|complete)[^)]*\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EditionSuffixRegex();

    public static string Normalize(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var normalized = TrademarkRegex().Replace(title.Trim(), string.Empty);
        normalized = PlatformSuffixRegex().Replace(normalized, string.Empty);
        normalized = EditionSuffixRegex().Replace(normalized, string.Empty);
        return normalized.Trim();
    }
}

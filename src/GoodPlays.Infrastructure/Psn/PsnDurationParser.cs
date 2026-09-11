using System.Text.RegularExpressions;

namespace GoodPlays.Infrastructure.Psn;

public static partial class PsnDurationParser
{
    [GeneratedRegex(@"^PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex IsoDurationRegex();

    public static decimal ParseToHours(string? playDuration)
    {
        if (string.IsNullOrWhiteSpace(playDuration))
        {
            return 0m;
        }

        var match = IsoDurationRegex().Match(playDuration.Trim());
        if (!match.Success)
        {
            return 0m;
        }

        var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        var seconds = match.Groups[3].Success ? decimal.Parse(match.Groups[3].Value) : 0m;

        var totalHours = hours + (minutes / 60m) + (seconds / 3600m);
        return Math.Round(totalHours, 2);
    }
}

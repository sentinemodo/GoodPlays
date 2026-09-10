using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GoodPlays.Infrastructure.Services;

internal static partial class SlugHelper
{
    public static string CreateSlug(string title, long? externalId = null)
    {
        var normalized = title.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (ch is ' ' or '-' or '_' or '.')
            {
                builder.Append('-');
            }
        }

        var slug = HyphenCollapse().Replace(builder.ToString(), "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "game";
        }

        return externalId is null ? slug : $"{slug}-{externalId}";
    }

    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex HyphenCollapse();
}

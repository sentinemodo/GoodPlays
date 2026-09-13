using System.Text.Json;

namespace GoodPlays.Infrastructure.Psn;

public static class NpssoParser
{
    public static string Normalize(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var trimmed = input.Trim();

        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.TryGetProperty("npsso", out var npssoProperty))
                {
                    var fromJson = npssoProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(fromJson))
                    {
                        return fromJson.Trim();
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to treat the value as a raw token.
            }
        }

        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            return trimmed[1..^1].Trim();
        }

        return trimmed;
    }
}

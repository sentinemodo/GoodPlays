using System.Text.Json;

namespace GoodPlays.Infrastructure.Psn;

public static class PsnErrorParser
{
    public static string? TryReadMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("error_description", out var description) &&
                description.ValueKind == JsonValueKind.String)
            {
                var text = description.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }

                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}

using System.Security.Cryptography;
using System.Text;

namespace GoodPlays.Api.Services;

public static class ClerkWebhookVerifier
{
    public static bool TryVerify(IHeaderDictionary headers, string payload, string? webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return false;
        }

        var messageId = headers["svix-id"].FirstOrDefault();
        var timestamp = headers["svix-timestamp"].FirstOrDefault();
        var signatureHeader = headers["svix-signature"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(messageId)
            || string.IsNullOrWhiteSpace(timestamp)
            || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        if (!long.TryParse(timestamp, out var timestampSeconds))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestampSeconds) > 300)
        {
            return false;
        }

        var secretBytes = DecodeSecret(webhookSecret);
        var signedContent = $"{messageId}.{timestamp}.{payload}";
        var expected = ComputeSignature(secretBytes, signedContent);

        foreach (var part in signatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split(',', 2);
            if (pieces.Length != 2)
            {
                continue;
            }

            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(pieces[1]),
                    Encoding.UTF8.GetBytes(expected)))
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] DecodeSecret(string webhookSecret)
    {
        var normalized = webhookSecret.StartsWith("whsec_", StringComparison.Ordinal)
            ? webhookSecret["whsec_".Length..]
            : webhookSecret;

        return Convert.FromBase64String(normalized);
    }

    private static string ComputeSignature(byte[] secretBytes, string signedContent)
    {
        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        return Convert.ToBase64String(hash);
    }
}

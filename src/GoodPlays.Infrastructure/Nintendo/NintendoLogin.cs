using System.Security.Cryptography;
using System.Text;

namespace GoodPlays.Infrastructure.Nintendo;

public static class NintendoLogin
{
    public const string ClientId = "5c38e31cd085304b";
    public const string AccountsUrl = "https://accounts.nintendo.com";
    public const string AppUrl = "https://app-api.znej.nintendo.com";
    public const string UserAgent = "com.nintendo.znej/3.0.3 (iOS/26.0.1)";
    public const string Locale = "en-GB";

    public static NintendoLoginRequest Create()
    {
        var verifier = Base64Url(RandomBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomBytes(36));
        var query = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = $"npf{ClientId}://auth",
            ["response_type"] = "session_token_code",
            ["scope"] = "openid user user.mii user.email user.links[].id",
            ["session_token_code_challenge"] = challenge,
            ["session_token_code_challenge_method"] = "S256",
            ["state"] = state,
            ["theme"] = "login_form"
        };
        var url = $"{AccountsUrl}/connect/1.0.0/authorize?{string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))}";
        return new NintendoLoginRequest(url, verifier);
    }

    public static string ReadSessionTokenCode(string callbackUrl)
    {
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            throw new NintendoApiException(NintendoApiErrorCode.InvalidSession, "Paste the Nintendo login redirect link.");
        }

        var trimmed = callbackUrl.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var hash = trimmed.IndexOf('#');
        if (hash < 0 || hash == trimmed.Length - 1)
        {
            throw new NintendoApiException(
                NintendoApiErrorCode.InvalidSession,
                "That link has no session_token_code. Copy the full npf:// address from the browser.");
        }

        var fragment = trimmed[(hash + 1)..];
        foreach (var part in fragment.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = part.Split('=', 2);
            if (split.Length == 2 && split[0] == "session_token_code" && !string.IsNullOrWhiteSpace(split[1]))
            {
                return Uri.UnescapeDataString(split[1]);
            }
        }

        throw new NintendoApiException(
            NintendoApiErrorCode.InvalidSession,
            "That link has no session_token_code. Copy the full npf:// address from the browser.");
    }

    private static byte[] RandomBytes(int length)
    {
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

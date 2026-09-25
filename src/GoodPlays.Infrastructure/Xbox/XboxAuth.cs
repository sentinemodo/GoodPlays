namespace GoodPlays.Infrastructure.Xbox;

public static class XboxAuth
{
    public const string ClientId = "000000004C12AE6F";
    public const string RedirectUri = "https://login.live.com/oauth20_desktop.srf";
    public const string Scope = "XboxLive.signin XboxLive.offline_access";

    public static string CreateLoginUrl()
    {
        var query = $"client_id={Uri.EscapeDataString(ClientId)}" +
                    "&response_type=code" +
                    $"&scope={Uri.EscapeDataString(Scope)}" +
                    $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}";
        return $"https://login.live.com/oauth20_authorize.srf?{query}";
    }

    public static string ReadAuthorizationCode(string callbackUrl)
    {
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            throw new XboxApiException(XboxApiErrorCode.InvalidCode, "Paste the Xbox login redirect link.");
        }

        var trimmed = callbackUrl.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal) && !trimmed.Contains('?', StringComparison.Ordinal))
        {
            return trimmed;
        }

        var queryIndex = trimmed.IndexOf('?');
        if (queryIndex < 0 || queryIndex == trimmed.Length - 1)
        {
            throw new XboxApiException(
                XboxApiErrorCode.InvalidCode,
                "That link has no authorization code. Copy the full address after Microsoft signs you in.");
        }

        foreach (var part in trimmed[(queryIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = part.Split('=', 2);
            if (split.Length == 2 && split[0] == "code" && !string.IsNullOrWhiteSpace(split[1]))
            {
                return Uri.UnescapeDataString(split[1]);
            }
        }

        throw new XboxApiException(
            XboxApiErrorCode.InvalidCode,
            "That link has no authorization code. Copy the full address after Microsoft signs you in.");
    }
}

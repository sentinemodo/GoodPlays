using System.Net;
using System.Text;
using System.Text.Json;

namespace GoodPlays.Infrastructure.Xbox;

public sealed class XboxClient(HttpClient httpClient) : IXboxClient
{
    public XboxLoginRequest CreateLogin() => new(XboxAuth.CreateLoginUrl());

    public async Task<XboxAccount> ConnectAsync(string callbackUrl, CancellationToken cancellationToken)
    {
        var code = XboxAuth.ReadAuthorizationCode(callbackUrl);
        var tokens = await ExchangeCodeAsync(code, cancellationToken);
        var session = await AuthorizeAsync(tokens.AccessToken, cancellationToken);
        var account = await GetProfileAsync(session, cancellationToken);
        return new XboxAccount(account.Xuid, account.Gamertag, tokens.RefreshToken);
    }

    public async Task<XboxLibrary> GetLibraryAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokens = await RefreshAsync(refreshToken, cancellationToken);
        var session = await AuthorizeAsync(tokens.AccessToken, cancellationToken);
        var account = await GetProfileAsync(session, cancellationToken);
        var titles = await GetTitleHistoryAsync(session, account.Xuid, cancellationToken);
        return new XboxLibrary(tokens.RefreshToken, account.Xuid, account.Gamertag, titles);
    }

    private async Task<XboxTokens> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = XboxAuth.ClientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = XboxAuth.RedirectUri,
            ["scope"] = XboxAuth.Scope
        };
        return await TokenRequestAsync(form, cancellationToken);
    }

    private async Task<XboxTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = XboxAuth.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["scope"] = XboxAuth.Scope
        };
        return await TokenRequestAsync(form, cancellationToken);
    }

    private async Task<XboxTokens> TokenRequestAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://login.live.com/oauth20_token.srf")
        {
            Content = new FormUrlEncodedContent(form)
        };
        var body = await SendAsync(request, sign: false, signer: null, cancellationToken);
        var access = ReadString(body, "access_token");
        var refresh = ReadString(body, "refresh_token");
        if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh))
        {
            throw new XboxApiException(XboxApiErrorCode.Unauthorized, "Microsoft did not return Xbox tokens.");
        }

        var expires = 3600;
        if (TryReadInt(body, "expires_in", out var parsed))
        {
            expires = parsed;
        }

        return new XboxTokens(access, refresh, expires);
    }

    private async Task<XboxSession> AuthorizeAsync(string accessToken, CancellationToken cancellationToken)
    {
        var signer = new XboxRequestSigner();
        var deviceToken = await DeviceTokenAsync(signer, cancellationToken);
        var userToken = await UserTokenAsync(signer, accessToken, cancellationToken);
        return await XstsTokenAsync(signer, userToken, deviceToken, cancellationToken);
    }

    private async Task<string> DeviceTokenAsync(XboxRequestSigner signer, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
            Properties = new
            {
                AuthMethod = "ProofOfPossession",
                Id = "{" + Guid.NewGuid() + "}",
                DeviceType = "Win32",
                SerialNumber = "{" + Guid.NewGuid() + "}",
                Version = "10.0",
                ProofKey = signer.ProofKey
            }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://device.auth.xboxlive.com/device/authenticate")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var response = await SendAsync(request, sign: true, signer, cancellationToken);
        return RequiredToken(response, "Xbox device authentication failed.");
    }

    private async Task<string> UserTokenAsync(XboxRequestSigner signer, string accessToken, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = "d=" + accessToken
            }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://user.auth.xboxlive.com/user/authenticate")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var response = await SendAsync(request, sign: true, signer, cancellationToken);
        return RequiredToken(response, "Xbox user authentication failed.");
    }

    private async Task<XboxSession> XstsTokenAsync(
        XboxRequestSigner signer,
        string userToken,
        string deviceToken,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            RelyingParty = "http://xboxlive.com",
            TokenType = "JWT",
            Properties = new
            {
                UserTokens = new[] { userToken },
                DeviceToken = deviceToken,
                SandboxId = "RETAIL"
            }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://xsts.auth.xboxlive.com/xsts/authorize")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var response = await SendAsync(request, sign: true, signer, cancellationToken);
        var token = RequiredToken(response, "Xbox XSTS authorization failed.");
        var userHash = ReadUserHash(response);
        if (string.IsNullOrWhiteSpace(userHash))
        {
            throw new XboxApiException(XboxApiErrorCode.Unauthorized, "Xbox authorization did not include a user hash.");
        }

        return new XboxSession(signer, token, userHash);
    }

    private async Task<(string Xuid, string? Gamertag)> GetProfileAsync(XboxSession session, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://profile.xboxlive.com/users/me/profile/settings?settings=Gamertag");
        AddXboxAuth(request, session);
        var body = await SendAsync(request, sign: true, session.Signer, cancellationToken);
        using var document = JsonDocument.Parse(body);
        var user = document.RootElement.GetProperty("profileUsers")[0];
        var xuid = user.GetProperty("id").GetString() ?? throw new XboxApiException(
            XboxApiErrorCode.Unauthorized,
            "Xbox profile did not include an XUID.");
        string? gamertag = null;
        if (user.TryGetProperty("settings", out var settings))
        {
            foreach (var setting in settings.EnumerateArray())
            {
                if (setting.GetProperty("id").GetString() == "Gamertag")
                {
                    gamertag = setting.GetProperty("value").GetString();
                }
            }
        }

        return (xuid, gamertag);
    }

    private async Task<IReadOnlyList<XboxPlayedTitle>> GetTitleHistoryAsync(
        XboxSession session,
        string xuid,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://titlehub.xboxlive.com/users/xuid({xuid})/titles/titlehistory/decoration/detail");
        request.Headers.TryAddWithoutValidation("x-xbl-contract-version", "2");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US");
        AddXboxAuth(request, session);
        var body = await SendAsync(request, sign: true, session.Signer, cancellationToken);
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("titles", out var titles) || titles.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<XboxPlayedTitle>();
        foreach (var title in titles.EnumerateArray())
        {
            var name = ReadString(title, "name");
            var titleId = ReadString(title, "titleId") ?? ReadModernTitleId(title);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(titleId))
            {
                continue;
            }

            results.Add(new XboxPlayedTitle(titleId, name, ReadLastPlayed(title)));
        }

        return results;
    }

    private static void AddXboxAuth(HttpRequestMessage request, XboxSession session)
    {
        request.Headers.TryAddWithoutValidation("Authorization", $"XBL3.0 x={session.UserHash};{session.Token}");
        request.Headers.TryAddWithoutValidation("x-xbl-contract-version", "2");
    }

    private async Task<string> SendAsync(
        HttpRequestMessage request,
        bool sign,
        XboxRequestSigner? signer,
        CancellationToken cancellationToken)
    {
        if (sign && signer is not null)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Xbox request is missing a URL.");
            var path = uri.PathAndQuery;
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var authorization = request.Headers.TryGetValues("Authorization", out var values)
                ? string.Join(", ", values)
                : "";
            request.Headers.TryAddWithoutValidation("Signature", signer.Sign(request.Method.Method, path, body, authorization));
            if (request.Content is not null)
            {
                request.Content = new ByteArrayContent(body);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            }
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return text;
        }

        var message = ReadString(text, "error_description")
                      ?? ReadXErr(text)
                      ?? "Xbox request failed.";
        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new XboxApiException(XboxApiErrorCode.Unauthorized, message),
            HttpStatusCode.TooManyRequests =>
                new XboxApiException(XboxApiErrorCode.RateLimited, message),
            _ when message.Contains("code", StringComparison.OrdinalIgnoreCase) =>
                new XboxApiException(XboxApiErrorCode.InvalidCode, message),
            _ => new XboxApiException(XboxApiErrorCode.Upstream, message)
        };
    }

    private static string RequiredToken(string json, string fallback)
    {
        var token = ReadString(json, "Token");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new XboxApiException(XboxApiErrorCode.Unauthorized, fallback);
        }

        return token;
    }

    private static string? ReadUserHash(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("DisplayClaims", out var claims) ||
            !claims.TryGetProperty("xui", out var xui) ||
            xui.GetArrayLength() == 0)
        {
            return null;
        }

        return ReadString(xui[0], "uhs");
    }

    private static string? ReadModernTitleId(JsonElement title)
    {
        if (title.TryGetProperty("modernTitleId", out var modern) && modern.TryGetInt64(out var id))
        {
            return id.ToString();
        }

        return null;
    }

    private static DateTimeOffset? ReadLastPlayed(JsonElement title)
    {
        if (title.TryGetProperty("titleHistory", out var history))
        {
            var fromHistory = ReadTime(history, "lastTimePlayed");
            if (fromHistory is not null)
            {
                return fromHistory;
            }
        }

        return ReadTime(title, "lastTimePlayed");
    }

    private static DateTimeOffset? ReadTime(JsonElement element, string name)
    {
        var text = ReadString(element, name);
        return DateTimeOffset.TryParse(text, out var parsed) ? parsed : null;
    }

    private static string? ReadString(string json, string name)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ReadString(document.RootElement, name);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryReadInt(string json, string name, out int value)
    {
        value = 0;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(name, out var property) && property.TryGetInt32(out value))
            {
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static string? ReadXErr(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("XErr", out var xerr))
            {
                return $"Xbox rejected the sign-in ({xerr}).";
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private sealed record XboxSession(XboxRequestSigner Signer, string Token, string UserHash);
}

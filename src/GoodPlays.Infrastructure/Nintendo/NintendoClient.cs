using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GoodPlays.Infrastructure.Nintendo;

public sealed class NintendoClient(HttpClient httpClient) : INintendoClient
{
    public async Task<string> ExchangeSessionTokenAsync(
        string sessionTokenCode,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = NintendoLogin.ClientId,
            ["session_token_code"] = sessionTokenCode,
            ["session_token_code_verifier"] = codeVerifier
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{NintendoLogin.AccountsUrl}/connect/1.0.0/api/session_token")
        {
            Content = form
        };
        request.Headers.TryAddWithoutValidation("User-Agent", NintendoLogin.UserAgent);
        var body = await SendAsync(request, cancellationToken);
        var token = ReadString(body, "session_token");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new NintendoApiException(NintendoApiErrorCode.InvalidSession, "Nintendo did not return a session token.");
        }

        return token;
    }

    public async Task<NintendoAccount> GetAccountAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var access = await AccessTokenAsync(sessionToken, cancellationToken);
        var accountId = ReadJwtString(access.IdToken, "sub") ?? "nintendo";
        var displayName = ReadJwtString(access.IdToken, "nickname")
                          ?? ReadJwtString(access.IdToken, "name");
        return new NintendoAccount(accountId, displayName);
    }

    public async Task<IReadOnlyList<NintendoPlayedTitle>> GetPlayHistoryAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var access = await AccessTokenAsync(sessionToken, cancellationToken);
        var history = await PlayHistoryAsync(access.AccessToken, cancellationToken)
                      ?? (string.IsNullOrWhiteSpace(access.IdToken) ? null : await PlayHistoryAsync(access.IdToken, cancellationToken));
        if (history is null)
        {
            throw new NintendoApiException(NintendoApiErrorCode.Unauthorized, "Nintendo rejected the play history request.");
        }

        using var document = JsonDocument.Parse(history);
        if (!document.RootElement.TryGetProperty("playHistories", out var titles) || titles.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<NintendoPlayedTitle>();
        foreach (var title in titles.EnumerateArray())
        {
            var titleId = ReadProperty(title, "titleId");
            var name = ReadProperty(title, "titleName");
            if (string.IsNullOrWhiteSpace(titleId) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var minutes = title.TryGetProperty("totalPlayedMinutes", out var mins) && mins.TryGetInt32(out var value)
                ? value
                : 0;
            var system = ReadProperty(title, "platform") ?? ReadProperty(title, "deviceType");
            results.Add(new NintendoPlayedTitle(
                titleId,
                name,
                Math.Round(minutes / 60m, 2),
                ReadTime(title, "firstPlayedAt"),
                ReadTime(title, "lastPlayedAt"),
                system));
        }

        return results;
    }

    private async Task<NintendoAccess> AccessTokenAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["client_id"] = NintendoLogin.ClientId,
            ["session_token"] = sessionToken,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer-session-token"
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{NintendoLogin.AccountsUrl}/connect/1.0.0/api/token")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("User-Agent", NintendoLogin.UserAgent);
        var body = await SendAsync(request, cancellationToken);
        var accessToken = ReadString(body, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new NintendoApiException(NintendoApiErrorCode.Unauthorized, "Nintendo did not return an access token.");
        }

        return new NintendoAccess(accessToken, ReadString(body, "id_token"));
    }

    private async Task<string?> PlayHistoryAsync(string bearer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{NintendoLogin.AppUrl}/api/v2.0/users/me/play_histories");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.TryAddWithoutValidation("User-Agent", NintendoLogin.UserAgent);
        request.Headers.TryAddWithoutValidation("Gentry-Locale", NintendoLogin.Locale);
        var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return body;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        throw MapStatus(response.StatusCode, body);
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return body;
        }

        throw MapStatus(response.StatusCode, body);
    }

    private static NintendoApiException MapStatus(HttpStatusCode status, string body)
    {
        var message = ReadString(body, "error_description") ?? ReadString(body, "error") ?? "Nintendo request failed.";
        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new NintendoApiException(NintendoApiErrorCode.Unauthorized, message),
            HttpStatusCode.TooManyRequests =>
                new NintendoApiException(NintendoApiErrorCode.RateLimited, message),
            _ => new NintendoApiException(NintendoApiErrorCode.Upstream, message)
        };
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
            return ReadProperty(document.RootElement, name);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? ReadTime(JsonElement element, string name)
    {
        var text = ReadProperty(element, name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, out var parsed) ? parsed : null;
    }

    private static string? ReadJwtString(string? jwt, string claim)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2:
                payload += "==";
                break;
            case 3:
                payload += "=";
                break;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            return ReadString(json, claim);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private sealed record NintendoAccess(string AccessToken, string? IdToken);
}

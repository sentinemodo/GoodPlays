using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoodPlays.Infrastructure.Psn;

public sealed class PsnClient(HttpClient httpClient, IOptions<PsnOptions> options, ILogger<PsnClient> logger)
    : IPsnClient
{
    private const string AuthBaseUrl = "https://ca.account.sony.com/api/authz/v3/oauth";
    private const string ProfileBaseUrl = "https://m.np.playstation.com/api/userProfile/v1/internal/users";
    private const string GamesBaseUrl = "https://m.np.playstation.com/api/gamelist/v2/users";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly PsnOptions _options = ConfigureOptions(options.Value);

    public async Task<PsnTokens> ExchangeNpssoAsync(string npsso, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(npsso);

        var accessCode = await ExchangeNpssoForAccessCodeAsync(npsso.Trim(), cancellationToken);
        return await ExchangeAccessCodeForTokensAsync(accessCode, cancellationToken);
    }

    public Task<PsnTokens> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) =>
        ExchangeRefreshTokenForTokensAsync(refreshToken, cancellationToken);

    public async Task<PsnUserProfile> GetProfileAsync(
        string accessToken,
        string accountId,
        CancellationToken cancellationToken)
    {
        var url = $"{ProfileBaseUrl}/{Uri.EscapeDataString(accountId)}/profiles";
        using var request = CreateAuthorizedRequest(HttpMethod.Get, url, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccessOrThrow(response, body);

        var onlineId = TryReadOnlineId(body);
        if (string.IsNullOrWhiteSpace(onlineId))
        {
            throw new PsnApiException(PsnApiErrorCode.Unauthorized, "PSN profile did not include an online ID.");
        }

        return new PsnUserProfile(accountId, onlineId);
    }

    public async Task<IReadOnlyList<PsnTitleStat>> GetPlayedTitlesAsync(
        string accessToken,
        string accountId,
        CancellationToken cancellationToken)
    {
        var allTitles = new List<PsnTitleStat>();
        var offset = 0;
        const int limit = 200;

        while (true)
        {
            var url =
                $"{GamesBaseUrl}/{Uri.EscapeDataString(accountId)}/titles?limit={limit}&offset={offset}";
            using var request = CreateAuthorizedRequest(HttpMethod.Get, url, accessToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccessOrThrow(response, body);

            var payload = JsonSerializer.Deserialize<PlayedGamesResponse>(body, JsonOptions);
            if (payload?.Titles is null || payload.Titles.Count == 0)
            {
                break;
            }

            foreach (var title in payload.Titles)
            {
                if (string.IsNullOrWhiteSpace(title.TitleId) || string.IsNullOrWhiteSpace(title.Name))
                {
                    continue;
                }

                allTitles.Add(new PsnTitleStat(
                    title.TitleId,
                    title.Name,
                    PsnDurationParser.ParseToHours(title.PlayDuration),
                    ParseDateTime(title.LastPlayedDateTime),
                    ParseDateTime(title.FirstPlayedDateTime),
                    title.PlayCount,
                    title.Category));
            }

            if (payload.Titles.Count < limit)
            {
                break;
            }

            offset += limit;
        }

        return allTitles;
    }

    private async Task<string> ExchangeNpssoForAccessCodeAsync(string npsso, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>
        {
            ["access_type"] = "offline",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = _options.Scope
        };

        var queryString = string.Join(
            "&",
            query.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        var url = $"{AuthBaseUrl}/authorize?{queryString}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"npsso={npsso}");
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            "com.sony.snei.np.android.sso.share.oauth.versa.USER_AGENT");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!IsRedirectStatusCode(response.StatusCode))
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "PSN NPSSO exchange returned unexpected status {StatusCode}: {Body}",
                response.StatusCode,
                body);
            throw new PsnApiException(
                PsnApiErrorCode.InvalidNpsso,
                "Could not exchange NPSSO token. Verify the token from ca.account.sony.com/api/v1/ssocookie is current.");
        }

        var location = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new PsnApiException(
                PsnApiErrorCode.InvalidNpsso,
                "Could not exchange NPSSO token. Verify the token from ca.account.sony.com/api/v1/ssocookie is current.");
        }

        var redirectPart = location.Contains("redirect/", StringComparison.Ordinal)
            ? location[(location.IndexOf("redirect/", StringComparison.Ordinal) + "redirect/".Length)..]
            : location;

        var errorCode = ParseQueryParameter(redirectPart, "error_code");
        if (!string.IsNullOrWhiteSpace(ParseQueryParameter(redirectPart, "error")) ||
            !string.IsNullOrWhiteSpace(errorCode))
        {
            logger.LogWarning("PSN NPSSO exchange redirect contained error {ErrorCode}", errorCode);
            throw new PsnApiException(
                PsnApiErrorCode.InvalidNpsso,
                errorCode is "4165"
                    ? "NPSSO token expired. Sign in at playstation.com and fetch a fresh token from ca.account.sony.com/api/v1/ssocookie."
                    : "Could not exchange NPSSO token. Verify the token from ca.account.sony.com/api/v1/ssocookie is current.");
        }

        var code = ParseQueryParameter(redirectPart, "code");
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new PsnApiException(
                PsnApiErrorCode.InvalidNpsso,
                "Could not exchange NPSSO token. Verify the token from ca.account.sony.com/api/v1/ssocookie is current.");
        }

        return code;
    }

    private async Task<PsnTokens> ExchangeAccessCodeForTokensAsync(string accessCode, CancellationToken cancellationToken)
    {
        using var request = CreateTokenRequest(
            new Dictionary<string, string>
            {
                ["code"] = accessCode,
                ["redirect_uri"] = _options.RedirectUri,
                ["grant_type"] = "authorization_code",
                ["token_format"] = "jwt"
            });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ParseTokenResponseAsync(response, cancellationToken);
    }

    private async Task<PsnTokens> ExchangeRefreshTokenForTokensAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateTokenRequest(
            new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
                ["token_format"] = "jwt",
                ["scope"] = _options.Scope
            });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ParseTokenResponseAsync(response, cancellationToken);
    }

    private HttpRequestMessage CreateTokenRequest(Dictionary<string, string> formFields)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{AuthBaseUrl}/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BuildBasicAuthCredentials());
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            "com.sony.snei.np.android.sso.share.oauth.versa.USER_AGENT");
        request.Content = new FormUrlEncodedContent(formFields);
        return request;
    }

    private static string BuildBasicAuthCredentials() => PsnOAuthDefaults.BasicAuthParameter;

    private async Task<PsnTokens> ParseTokenResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccessOrThrow(response, body);

        var payload = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions);
        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.AccessToken) ||
            string.IsNullOrWhiteSpace(payload.RefreshToken))
        {
            throw new PsnApiException(PsnApiErrorCode.Unauthorized, "PSN token response was incomplete.");
        }

        return new PsnTokens(
            payload.AccessToken,
            payload.RefreshToken,
            payload.ExpiresIn,
            payload.RefreshTokenExpiresIn,
            payload.IdToken);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        return request;
    }

    private void EnsureSuccessOrThrow(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        logger.LogWarning("PSN API request failed with {StatusCode}: {Body}", response.StatusCode, body);
        var oauthMessage = PsnErrorParser.TryReadMessage(body);

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest => new PsnApiException(
                PsnApiErrorCode.Unauthorized,
                oauthMessage ??
                "PSN authorization failed. Fetch a fresh NPSSO token from ca.account.sony.com/api/v1/ssocookie and try again."),
            HttpStatusCode.Forbidden => new PsnApiException(
                PsnApiErrorCode.Unauthorized,
                "PSN profile or game data is not accessible with the current credentials."),
            HttpStatusCode.TooManyRequests => new PsnApiException(
                PsnApiErrorCode.RateLimited,
                "PSN API rate limit exceeded. Try again later."),
            >= HttpStatusCode.InternalServerError => new PsnApiException(
                PsnApiErrorCode.ServerError,
                "PSN API returned a server error."),
            _ => new PsnApiException(
                PsnApiErrorCode.ServerError,
                oauthMessage ?? $"PSN API request failed with status {(int)response.StatusCode}.")
        };
    }

    private static PsnOptions ConfigureOptions(PsnOptions value)
    {
        PsnOptions.ApplyDefaults(value);
        return value;
    }

    private static string? TryReadOnlineId(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("onlineId", out var onlineId))
        {
            return onlineId.GetString();
        }

        if (root.TryGetProperty("profiles", out var profiles) &&
            profiles.ValueKind == JsonValueKind.Array &&
            profiles.GetArrayLength() > 0 &&
            profiles[0].TryGetProperty("onlineId", out var nestedOnlineId))
        {
            return nestedOnlineId.GetString();
        }

        return null;
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static DateTimeOffset? ParseDateTime(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode) =>
        (int)statusCode is >= 300 and <= 399;

    private static string? ParseQueryParameter(string queryString, string key)
    {
        var query = queryString.StartsWith('?') ? queryString[1..] : queryString;
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            if (parts.Length == 2 &&
                string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token_expires_in")]
        public int RefreshTokenExpiresIn { get; set; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
    }

    private sealed class PlayedGamesResponse
    {
        public List<PlayedGamePayload>? Titles { get; set; }
    }

    private sealed class PlayedGamePayload
    {
        [JsonPropertyName("titleId")]
        public string? TitleId { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("playDuration")]
        public string? PlayDuration { get; set; }

        [JsonPropertyName("lastPlayedDateTime")]
        public string? LastPlayedDateTime { get; set; }

        [JsonPropertyName("firstPlayedDateTime")]
        public string? FirstPlayedDateTime { get; set; }

        [JsonPropertyName("playCount")]
        public int PlayCount { get; set; }

        public string? Category { get; set; }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

    private readonly PsnOptions _options = options.Value;

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
        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var payload = await DeserializeAsync<ProfileResponse>(response, cancellationToken);
        if (payload?.Profiles is null || payload.Profiles.Count == 0)
        {
            throw new PsnApiException(PsnApiErrorCode.Unauthorized, "PSN profile was not found.");
        }

        var profile = payload.Profiles[0];
        var onlineId = profile.OnlineId;
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
            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var payload = await DeserializeAsync<PlayedGamesResponse>(response, cancellationToken);
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

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.Found or HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect))
        {
            logger.LogWarning("PSN NPSSO exchange returned unexpected status {StatusCode}", response.StatusCode);
            throw new PsnApiException(
                PsnApiErrorCode.InvalidNpsso,
                "Could not exchange NPSSO token. Verify the token from ca.account.sony.com/api/v1/ssocookie is current.");
        }

        var location = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(location) || !location.Contains("code=", StringComparison.Ordinal))
        {
            throw new PsnApiException(
                PsnApiErrorCode.InvalidNpsso,
                "Could not exchange NPSSO token. Verify the token from ca.account.sony.com/api/v1/ssocookie is current.");
        }

        var redirectPart = location.Contains("redirect/", StringComparison.Ordinal)
            ? location[(location.IndexOf("redirect/", StringComparison.Ordinal) + "redirect/".Length)..]
            : location;

        var queryIndex = redirectPart.IndexOf('?');
        if (queryIndex >= 0)
        {
            redirectPart = redirectPart[(queryIndex + 1)..];
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
        request.Content = new FormUrlEncodedContent(formFields);
        return request;
    }

    private string BuildBasicAuthCredentials()
    {
        var raw = $"{_options.ClientId}:{_options.ClientSecret}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    private async Task<PsnTokens> ParseTokenResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var payload = await DeserializeAsync<TokenResponse>(response, cancellationToken);
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
        return request;
    }

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("PSN API request failed with {StatusCode}: {Body}", response.StatusCode, body);

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new PsnApiException(
                PsnApiErrorCode.Unauthorized,
                "PSN authorization failed. Reconnect your PlayStation account."),
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
                $"PSN API request failed with status {(int)response.StatusCode}.")
        };
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static DateTimeOffset? ParseDateTime(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

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

    private sealed class ProfileResponse
    {
        public List<ProfilePayload>? Profiles { get; set; }
    }

    private sealed class ProfilePayload
    {
        [JsonPropertyName("onlineId")]
        public string? OnlineId { get; set; }
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

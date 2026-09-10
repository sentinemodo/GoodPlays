using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoodPlays.Infrastructure.Metadata;

public sealed class IgdbClient : IIgdbClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _httpClient;
    private readonly IgdbOptions _options;
    private readonly ILogger<IgdbClient> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public IgdbClient(HttpClient httpClient, IOptions<IgdbOptions> options, ILogger<IgdbClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<IReadOnlyList<IgdbSearchResult>> SearchGamesAsync(string query, CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var escaped = query.Trim().Replace("\"", "\\\"", StringComparison.Ordinal);
        var body =
            $"search \"{escaped}\"; fields id,name,slug,summary,cover.url,first_release_date; where version_parent = null; limit 25;";

        var games = await QueryGamesAsync(body, cancellationToken);
        return games.Select(MapGame).Where(g => g is not null).Select(g => g!).ToList();
    }

    public async Task<IgdbSearchResult?> GetGameAsync(long igdbId, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var body = $"fields id,name,slug,summary,cover.url,first_release_date; where id = {igdbId};";
        var games = await QueryGamesAsync(body, cancellationToken);
        return games.Select(MapGame).FirstOrDefault(g => g is not null);
    }

    private async Task<IReadOnlyList<IgdbGamePayload>> QueryGamesAsync(string body, CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games")
            {
                Content = new StringContent(body, Encoding.UTF8, "text/plain")
            };
            request.Headers.Add("Client-ID", _options.ClientId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("IGDB query failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<List<IgdbGamePayload>>(stream, JsonOptions, cancellationToken) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "IGDB query failed");
            return [];
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _accessToken;
            }

            var url =
                $"https://id.twitch.tv/oauth2/token?client_id={Uri.EscapeDataString(_options.ClientId)}&client_secret={Uri.EscapeDataString(_options.ClientSecret)}&grant_type=client_credentials";

            using var response = await _httpClient.PostAsync(url, null, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<TwitchTokenPayload>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Empty Twitch token response.");

            _accessToken = payload.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(payload.ExpiresIn - 60, 60));
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static IgdbSearchResult? MapGame(IgdbGamePayload game)
    {
        if (game.Id is null || string.IsNullOrWhiteSpace(game.Name))
        {
            return null;
        }

        return new IgdbSearchResult(
            game.Id.Value,
            game.Name,
            game.Slug,
            NormalizeCoverUrl(game.Cover?.Url),
            game.Summary,
            game.FirstReleaseDate is null
                ? null
                : DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(game.FirstReleaseDate.Value).UtcDateTime));
    }

    private static string? NormalizeCoverUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return url.StartsWith("//", StringComparison.Ordinal) ? $"https:{url}" : url;
    }

    private sealed class TwitchTokenPayload
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class IgdbGamePayload
    {
        public long? Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? Summary { get; set; }

        [JsonPropertyName("first_release_date")]
        public long? FirstReleaseDate { get; set; }

        public IgdbCoverPayload? Cover { get; set; }
    }

    private sealed class IgdbCoverPayload
    {
        public string? Url { get; set; }
    }
}

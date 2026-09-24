using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoodPlays.Infrastructure.OpenCritic;

public sealed record OpenCriticGameResult(decimal? TopCriticScore, int? ReviewCount, string? Url);

public interface IOpenCriticClient
{
    bool IsConfigured { get; }

    Task<OpenCriticGameResult?> SearchByNameAsync(string title, CancellationToken cancellationToken);
}

public sealed class OpenCriticClient : IOpenCriticClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly OpenCriticOptions _options;
    private readonly ILogger<OpenCriticClient> _logger;

    public OpenCriticClient(HttpClient httpClient, IOptions<OpenCriticOptions> options, ILogger<OpenCriticClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress ??= new Uri("https://api.opencritic.com/api/");
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<OpenCriticGameResult?> SearchByNameAsync(string title, CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"meta/search?criteria={Uri.EscapeDataString(title)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenCritic search failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var results = await JsonSerializer.DeserializeAsync<List<OpenCriticSearchPayload>>(stream, JsonOptions, cancellationToken);
            var match = results?.FirstOrDefault();
            if (match is null)
            {
                return null;
            }

            return new OpenCriticGameResult(match.TopCriticScore, match.ReviewCount, match.Url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "OpenCritic search failed");
            return null;
        }
    }

    private sealed class OpenCriticSearchPayload
    {
        [JsonPropertyName("topCriticScore")]
        public decimal? TopCriticScore { get; set; }

        [JsonPropertyName("tier")]
        public string? Tier { get; set; }

        public int? ReviewCount { get; set; }
        public string? Url { get; set; }
    }
}

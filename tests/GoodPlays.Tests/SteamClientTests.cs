using System.Net;
using System.Text;
using GoodPlays.Infrastructure.Steam;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoodPlays.Tests;

public class SteamClientTests
{
    [Fact]
    public async Task ResolveVanityUrlAsync_ReturnsExistingSteamIdWithoutApiCall()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var steamId = await client.ResolveVanityUrlAsync("76561198000000000", "test-key", CancellationToken.None);

        Assert.Equal("76561198000000000", steamId);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ResolveVanityUrlAsync_CallsSteamForVanityName()
    {
        const string json = """
            {
              "response": {
                "success": 1,
                "steamid": "76561198000000000"
              }
            }
            """;

        var handler = new RecordingHandler(_ => JsonResponse(json));
        var client = CreateClient(handler);

        var steamId = await client.ResolveVanityUrlAsync("gaben", "test-key", CancellationToken.None);

        Assert.Equal("76561198000000000", steamId);
        Assert.Single(handler.Requests);
        Assert.Contains("ResolveVanityURL", handler.Requests[0].RequestUri?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOwnedGamesAsync_ConvertsPlaytimeToHours()
    {
        const string json = """
            {
              "response": {
                "game_count": 1,
                "games": [
                  {
                    "appid": 570,
                    "name": "Dota 2",
                    "playtime_forever": 120,
                    "playtime_2weeks": 30,
                    "rtime_last_played": 1700000000
                  }
                ]
              }
            }
            """;

        var handler = new RecordingHandler(_ => JsonResponse(json));
        var client = CreateClient(handler);

        var games = await client.GetOwnedGamesAsync("76561198000000000", "test-key", CancellationToken.None);

        var game = Assert.Single(games);
        Assert.Equal(570u, game.AppId);
        Assert.Equal("Dota 2", game.Name);
        Assert.Equal(120, game.PlaytimeForeverMinutes);
        Assert.Equal(2.0m, game.PlaytimeHours);
    }

    [Fact]
    public async Task GetPlayerSummaryAsync_ThrowsForUnauthorized()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SteamApiException>(() =>
            client.GetPlayerSummaryAsync("76561198000000000", "bad-key", CancellationToken.None));

        Assert.Equal(SteamApiErrorCode.InvalidApiKey, ex.ErrorCode);
    }

    private static SteamClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.steampowered.com/")
        };
        return new SteamClient(httpClient, NullLogger<SteamClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}

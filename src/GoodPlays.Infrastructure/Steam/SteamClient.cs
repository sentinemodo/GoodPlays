using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Infrastructure.Steam;

public sealed class SteamClient(HttpClient httpClient, ILogger<SteamClient> logger) : ISteamClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string?> ResolveVanityUrlAsync(string vanityOrUrl, string apiKey, CancellationToken cancellationToken)
    {
        var steamId = SteamIdParser.TryExtractSteamId64(vanityOrUrl);
        if (steamId is not null)
        {
            return steamId;
        }

        var vanity = SteamIdParser.TryExtractVanityName(vanityOrUrl);
        if (vanity is null)
        {
            return null;
        }

        var url =
            $"ISteamUser/ResolveVanityURL/v1/?key={Uri.EscapeDataString(apiKey)}&vanityurl={Uri.EscapeDataString(vanity)}";
        using var response = await SendAsync(url, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var payload = await DeserializeAsync<VanityResponse>(response, cancellationToken);
        if (payload?.Response?.Success != 1 || string.IsNullOrWhiteSpace(payload.Response.SteamId))
        {
            throw new SteamApiException(SteamApiErrorCode.VanityNotFound, "Steam vanity URL could not be resolved.");
        }

        return payload.Response.SteamId;
    }

    public async Task<SteamPlayerSummary?> GetPlayerSummaryAsync(
        string steamId64,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url =
            $"ISteamUser/GetPlayerSummaries/v2/?key={Uri.EscapeDataString(apiKey)}&steamids={Uri.EscapeDataString(steamId64)}";
        using var response = await SendAsync(url, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var payload = await DeserializeAsync<PlayerSummariesResponse>(response, cancellationToken);
        var player = payload?.Response?.Players?.FirstOrDefault();
        if (player is null || string.IsNullOrWhiteSpace(player.SteamId))
        {
            return null;
        }

        return new SteamPlayerSummary(
            player.SteamId,
            player.PersonaName ?? player.SteamId,
            player.AvatarFull,
            player.ProfileUrl,
            player.CommunityVisibilityState ?? 0);
    }

    public async Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(
        string steamId64,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url =
            $"IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}&include_appinfo=1&include_played_free_games=1&format=json";
        using var response = await SendAsync(url, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var payload = await DeserializeAsync<OwnedGamesResponse>(response, cancellationToken);
        if (payload?.Response?.Games is null)
        {
            return [];
        }

        return payload.Response.Games
            .Where(g => g.AppId > 0 && !string.IsNullOrWhiteSpace(g.Name))
            .Select(g => new SteamOwnedGame(
                g.AppId,
                g.Name!,
                g.PlaytimeForever,
                g.Playtime2Weeks,
                g.RtimeLastPlayed,
                SteamOwnedGame.MinutesToHours(g.PlaytimeForever)))
            .ToList();
    }

    public async Task<IReadOnlyList<SteamRecentlyPlayedGame>> GetRecentlyPlayedGamesAsync(
        string steamId64,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url =
            $"IPlayerService/GetRecentlyPlayedGames/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}&count=0&format=json";
        using var response = await SendAsync(url, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var payload = await DeserializeAsync<OwnedGamesResponse>(response, cancellationToken);
        if (payload?.Response?.Games is null)
        {
            return [];
        }

        return payload.Response.Games
            .Where(g => g.AppId > 0 && !string.IsNullOrWhiteSpace(g.Name))
            .Select(g => new SteamRecentlyPlayedGame(
                g.AppId,
                g.Name!,
                g.PlaytimeForever,
                g.Playtime2Weeks,
                SteamOwnedGame.MinutesToHours(g.PlaytimeForever)))
            .ToList();
    }

    public async Task<SteamPlayerAchievementsResult?> GetPlayerAchievementsAsync(
        string steamId64,
        uint appId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url =
            $"ISteamUserStats/GetPlayerAchievements/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}&appid={appId}&format=json";
        using var response = await SendAsync(url, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var payload = await DeserializeAsync<PlayerAchievementsResponse>(response, cancellationToken);
        if (payload?.PlayerStats is null)
        {
            return null;
        }

        var achievements = payload.PlayerStats.Achievements?
            .Select(a => new SteamPlayerAchievement(
                a.ApiName ?? string.Empty,
                a.Achieved == 1,
                a.UnlockTime))
            .ToList() ?? [];

        return new SteamPlayerAchievementsResult(appId, payload.PlayerStats.Success == 1, achievements);
    }

    private Task<HttpResponseMessage> SendAsync(string relativeUrl, CancellationToken cancellationToken) =>
        httpClient.GetAsync(relativeUrl, cancellationToken);

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Steam API request failed with {StatusCode}: {Body}", response.StatusCode, body);

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new SteamApiException(
                SteamApiErrorCode.InvalidApiKey,
                "Steam Web API key is invalid or unauthorized."),
            HttpStatusCode.Forbidden => new SteamApiException(
                SteamApiErrorCode.PrivateProfile,
                "Steam profile or game details are not accessible with the provided credentials."),
            HttpStatusCode.TooManyRequests => new SteamApiException(
                SteamApiErrorCode.RateLimited,
                "Steam Web API rate limit exceeded. Try again later."),
            >= HttpStatusCode.InternalServerError => new SteamApiException(
                SteamApiErrorCode.ServerError,
                "Steam Web API returned a server error."),
            _ => new SteamApiException(
                SteamApiErrorCode.ServerError,
                $"Steam Web API request failed with status {(int)response.StatusCode}.")
        };
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private sealed class VanityResponse
    {
        public VanityResponseBody? Response { get; set; }
    }

    private sealed class VanityResponseBody
    {
        public int Success { get; set; }

        [JsonPropertyName("steamid")]
        public string? SteamId { get; set; }
    }

    private sealed class PlayerSummariesResponse
    {
        public PlayerSummariesBody? Response { get; set; }
    }

    private sealed class PlayerSummariesBody
    {
        public List<PlayerSummaryPayload>? Players { get; set; }
    }

    private sealed class PlayerSummaryPayload
    {
        [JsonPropertyName("steamid")]
        public string? SteamId { get; set; }

        [JsonPropertyName("personaname")]
        public string? PersonaName { get; set; }

        [JsonPropertyName("avatarfull")]
        public string? AvatarFull { get; set; }

        [JsonPropertyName("profileurl")]
        public string? ProfileUrl { get; set; }

        [JsonPropertyName("communityvisibilitystate")]
        public int? CommunityVisibilityState { get; set; }
    }

    private sealed class OwnedGamesResponse
    {
        public OwnedGamesBody? Response { get; set; }
    }

    private sealed class OwnedGamesBody
    {
        [JsonPropertyName("game_count")]
        public int GameCount { get; set; }

        public List<OwnedGamePayload>? Games { get; set; }
    }

    private sealed class OwnedGamePayload
    {
        public uint AppId { get; set; }
        public string? Name { get; set; }

        [JsonPropertyName("playtime_forever")]
        public int PlaytimeForever { get; set; }

        [JsonPropertyName("playtime_2weeks")]
        public int Playtime2Weeks { get; set; }

        [JsonPropertyName("rtime_last_played")]
        public long? RtimeLastPlayed { get; set; }
    }

    private sealed class PlayerAchievementsResponse
    {
        [JsonPropertyName("playerstats")]
        public PlayerAchievementsBody? PlayerStats { get; set; }
    }

    private sealed class PlayerAchievementsBody
    {
        public int Success { get; set; }
        public List<AchievementPayload>? Achievements { get; set; }
    }

    private sealed class AchievementPayload
    {
        [JsonPropertyName("apiname")]
        public string? ApiName { get; set; }

        public int Achieved { get; set; }

        [JsonPropertyName("unlocktime")]
        public long? UnlockTime { get; set; }
    }
}

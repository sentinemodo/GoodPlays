namespace GoodPlays.Infrastructure.Steam;

public sealed record SteamPlayerSummary(
    string SteamId64,
    string PersonaName,
    string? AvatarFull,
    string? ProfileUrl,
    int CommunityVisibilityState);

public sealed record SteamOwnedGame(
    uint AppId,
    string Name,
    int PlaytimeForeverMinutes,
    int PlaytimeTwoWeeksMinutes,
    long? LastPlayedUnix,
    decimal PlaytimeHours)
{
    public static decimal MinutesToHours(int minutes) => Math.Round(minutes / 60.0m, 2);
}

public sealed record SteamRecentlyPlayedGame(
    uint AppId,
    string Name,
    int PlaytimeForeverMinutes,
    int PlaytimeTwoWeeksMinutes,
    decimal PlaytimeHours);

public sealed record SteamPlayerAchievementsResult(
    uint AppId,
    bool Success,
    IReadOnlyList<SteamPlayerAchievement> Achievements);

public sealed record SteamPlayerAchievement(
    string ApiName,
    bool Achieved,
    long? UnlockTimeUnix);

public enum SteamApiErrorCode
{
    None = 0,
    InvalidApiKey = 1,
    PrivateProfile = 2,
    VanityNotFound = 3,
    RateLimited = 4,
    ServerError = 5
}

public sealed record SteamAchievementDefinition(
    string? Name,
    string? DisplayName,
    string? Description,
    string? Icon);

public sealed record SteamGameSchema(
    uint AppId,
    IReadOnlyList<SteamAchievementDefinition> Achievements);

public sealed record SteamNewsItem(
    string Title,
    string Url,
    DateTimeOffset? PublishedAt);

public sealed class SteamApiException : Exception
{
    public SteamApiException(SteamApiErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public SteamApiErrorCode ErrorCode { get; }
}

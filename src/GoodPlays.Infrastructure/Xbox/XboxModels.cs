namespace GoodPlays.Infrastructure.Xbox;

public sealed record XboxLoginRequest(string Url);

public sealed record XboxTokens(string AccessToken, string RefreshToken, int ExpiresInSeconds);

public sealed record XboxAccount(string Xuid, string? Gamertag, string RefreshToken);

public sealed record XboxPlayedTitle(string TitleId, string Name, DateTimeOffset? LastPlayedAt);

public enum XboxApiErrorCode
{
    InvalidCode,
    Unauthorized,
    RateLimited,
    Upstream
}

public sealed class XboxApiException(XboxApiErrorCode errorCode, string message) : Exception(message)
{
    public XboxApiErrorCode ErrorCode { get; } = errorCode;
}

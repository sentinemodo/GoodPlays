namespace GoodPlays.Infrastructure.Psn;

public sealed record PsnTokens(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    int RefreshTokenExpiresInSeconds,
    string? IdToken);

public sealed record PsnUserProfile(string AccountId, string OnlineId);

public sealed record PsnTitleStat(
    string TitleId,
    string Name,
    decimal PlaytimeHours,
    DateTimeOffset? LastPlayedAt,
    DateTimeOffset? FirstPlayedAt,
    int PlayCount,
    string? Category);

public enum PsnApiErrorCode
{
    None = 0,
    InvalidNpsso = 1,
    TokenExpired = 2,
    Unauthorized = 3,
    RateLimited = 4,
    ServerError = 5
}

public sealed class PsnApiException : Exception
{
    public PsnApiException(PsnApiErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public PsnApiErrorCode ErrorCode { get; }
}

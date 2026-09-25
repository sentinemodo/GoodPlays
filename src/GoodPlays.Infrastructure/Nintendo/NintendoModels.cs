namespace GoodPlays.Infrastructure.Nintendo;

public sealed record NintendoLoginRequest(string Url, string CodeVerifier);

public sealed record NintendoAccount(string AccountId, string? DisplayName);

public sealed record NintendoPlayedTitle(
    string TitleId,
    string Name,
    decimal PlaytimeHours,
    DateTimeOffset? FirstPlayedAt,
    DateTimeOffset? LastPlayedAt,
    string? System);

public enum NintendoApiErrorCode
{
    InvalidSession,
    Unauthorized,
    RateLimited,
    Upstream
}

public sealed class NintendoApiException(NintendoApiErrorCode errorCode, string message) : Exception(message)
{
    public NintendoApiErrorCode ErrorCode { get; } = errorCode;
}

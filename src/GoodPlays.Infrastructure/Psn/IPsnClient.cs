namespace GoodPlays.Infrastructure.Psn;

public interface IPsnClient
{
    Task<PsnTokens> ExchangeNpssoAsync(string npsso, CancellationToken cancellationToken);

    Task<PsnTokens> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);

    Task<PsnUserProfile> GetProfileAsync(string accessToken, string accountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PsnTitleStat>> GetPlayedTitlesAsync(
        string accessToken,
        string accountId,
        CancellationToken cancellationToken);
}

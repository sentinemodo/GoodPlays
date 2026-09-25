namespace GoodPlays.Infrastructure.Nintendo;

public interface INintendoClient
{
    Task<string> ExchangeSessionTokenAsync(string sessionTokenCode, string codeVerifier, CancellationToken cancellationToken);

    Task<NintendoAccount> GetAccountAsync(string sessionToken, CancellationToken cancellationToken);

    Task<IReadOnlyList<NintendoPlayedTitle>> GetPlayHistoryAsync(string sessionToken, CancellationToken cancellationToken);
}

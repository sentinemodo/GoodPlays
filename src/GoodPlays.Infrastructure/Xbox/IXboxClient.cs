namespace GoodPlays.Infrastructure.Xbox;

public interface IXboxClient
{
    XboxLoginRequest CreateLogin();

    Task<XboxAccount> ConnectAsync(string callbackUrl, CancellationToken cancellationToken);

    Task<XboxLibrary> GetLibraryAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed record XboxLibrary(string RefreshToken, string Xuid, string? Gamertag, IReadOnlyList<XboxPlayedTitle> Titles);

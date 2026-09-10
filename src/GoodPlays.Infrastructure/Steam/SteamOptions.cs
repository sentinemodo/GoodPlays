namespace GoodPlays.Infrastructure.Steam;

public sealed class SteamOptions
{
    public const string SectionName = "Steam";

    /// <summary>
    /// Optional platform-level Steam Web API key for vanity resolution and public profile reads.
    /// </summary>
    public string? WebApiKey { get; set; }
}

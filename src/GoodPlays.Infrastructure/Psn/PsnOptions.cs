namespace GoodPlays.Infrastructure.Psn;

public sealed class PsnOptions
{
    public const string SectionName = "Psn";

    /// <summary>
    /// OAuth client ID for the PlayStation mobile app flow.
    /// </summary>
    public string ClientId { get; set; } = PsnOAuthDefaults.ClientId;

    /// <summary>
    /// OAuth client secret paired with <see cref="ClientId"/>.
    /// </summary>
    public string ClientSecret { get; set; } = PsnOAuthDefaults.ClientSecret;

    public string RedirectUri { get; set; } = PsnOAuthDefaults.RedirectUri;

    public string Scope { get; set; } = PsnOAuthDefaults.Scope;

    public static void ApplyDefaults(PsnOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            options.ClientId = PsnOAuthDefaults.ClientId;
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            options.ClientSecret = PsnOAuthDefaults.ClientSecret;
        }

        if (string.IsNullOrWhiteSpace(options.RedirectUri))
        {
            options.RedirectUri = PsnOAuthDefaults.RedirectUri;
        }

        if (string.IsNullOrWhiteSpace(options.Scope))
        {
            options.Scope = PsnOAuthDefaults.Scope;
        }
    }
}

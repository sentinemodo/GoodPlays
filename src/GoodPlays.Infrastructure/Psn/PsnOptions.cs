namespace GoodPlays.Infrastructure.Psn;

public sealed class PsnOptions
{
    public const string SectionName = "Psn";

    /// <summary>
    /// OAuth client ID for the PlayStation mobile app flow.
    /// </summary>
    public string ClientId { get; set; } = "09515159-7237-4370-9b40-3806e67c0891";

    /// <summary>
    /// OAuth client secret paired with <see cref="ClientId"/>.
    /// </summary>
    public string ClientSecret { get; set; } = "ucPjka5tnB2KqsP";

    public string RedirectUri { get; set; } = "com.scee.psxandroid.scecompcall://redirect";

    public string Scope { get; set; } = "psn:mobile.v2.core psn:clientapp";
}

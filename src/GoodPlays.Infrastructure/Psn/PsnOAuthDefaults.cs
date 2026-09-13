namespace GoodPlays.Infrastructure.Psn;

/// <summary>
/// Public PlayStation mobile-app OAuth credentials used by community PSN libraries.
/// </summary>
public static class PsnOAuthDefaults
{
    public const string ClientId = "09515159-7237-4370-9b40-3806e67c0891";

    public const string ClientSecret = "ucPjka5tntB2KqsP";

    public const string RedirectUri = "com.scee.psxandroid.scecompcall://redirect";

    public const string Scope = "psn:mobile.v2.core psn:clientapp";

    public const string BasicAuthParameter =
        "MDk1MTUxNTktNzIzNy00MzcwLTliNDAtMzgwNmU2N2MwODkxOnVjUGprYTV0bnRCMktxc1A=";
}

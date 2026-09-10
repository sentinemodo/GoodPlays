namespace GoodPlays.Infrastructure.Metadata;

public class IgdbOptions
{
    public const string SectionName = "IGDB";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

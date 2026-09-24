namespace GoodPlays.Infrastructure.OpenCritic;

public sealed class OpenCriticOptions
{
    public const string SectionName = "OpenCritic";

    public string ApiKey { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

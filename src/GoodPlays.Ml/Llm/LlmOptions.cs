namespace GoodPlays.Ml.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "LLM";

    public string Provider { get; set; } = "openai";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

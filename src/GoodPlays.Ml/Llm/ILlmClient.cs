namespace GoodPlays.Ml.Llm;

public interface ILlmClient
{
    bool IsConfigured { get; }

    Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}

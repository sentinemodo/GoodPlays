using System.Text.Json;
using System.Text.Json.Serialization;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using GoodPlays.Ml.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Ml;

public sealed class ResearchRecommendationEngine(
    GoodPlaysDbContext dbContext,
    IGameCatalogService gameCatalogService,
    ILlmClient llmClient,
    ILogger<ResearchRecommendationEngine> logger) : IRecommendationEngine
{
    private const string SystemPrompt =
        "You are a video game recommendation assistant. Respond with ONLY valid JSON.";

    public async Task<IReadOnlyList<RecommendationResult>> GetRecommendationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var libraryTitles = await dbContext.LibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => e.Game.Title)
            .Take(40)
            .ToListAsync(cancellationToken);

        var suggestions = llmClient.IsConfigured
            ? await GetLlmSuggestionsAsync(libraryTitles, cancellationToken)
            : [];

        if (suggestions.Count == 0)
        {
            suggestions = await GetCatalogFallbackSuggestionsAsync(userId, libraryTitles, cancellationToken);
        }

        var results = new List<RecommendationResult>();
        foreach (var suggestion in suggestions.Take(5))
        {
            var resolved = await ResolveSuggestionAsync(suggestion, cancellationToken);
            if (resolved is not null)
            {
                results.Add(resolved);
            }
        }

        return results;
    }

    public Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var message = llmClient.IsConfigured
            ? "Research recommendation engine ready (LLM + catalog lookup)"
            : "Research recommendation engine ready (catalog fallback — set LLM__ApiKey for AI suggestions)";

        return Task.FromResult(new HealthStatus(true, message));
    }

    private async Task<IReadOnlyList<LlmSuggestion>> GetLlmSuggestionsAsync(
        IReadOnlyList<string> libraryTitles,
        CancellationToken cancellationToken)
    {
        var libraryList = libraryTitles.Count == 0
            ? "The user has an empty library."
            : string.Join("\n", libraryTitles.Select(t => $"- {t}"));

        var userPrompt =
            "The user has these games in their library:\n" +
            libraryList +
            "\n\nRecommend exactly 5 video games they would enjoy. Do not recommend any title already in their library.\n" +
            "Return ONLY a JSON array: [{\"title\":\"Game Name\",\"reason\":\"One sentence why\"}]";

        var content = await llmClient.CompleteAsync(SystemPrompt, userPrompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        try
        {
            var json = ExtractJsonArray(content);
            var parsed = JsonSerializer.Deserialize<List<LlmSuggestion>>(json, JsonOptions);
            return parsed?.Where(s => !string.IsNullOrWhiteSpace(s.Title)).ToList() ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse LLM recommendation JSON");
            return [];
        }
    }

    private async Task<IReadOnlyList<LlmSuggestion>> GetCatalogFallbackSuggestionsAsync(
        Guid userId,
        IReadOnlyList<string> libraryTitles,
        CancellationToken cancellationToken)
    {
        var ownedGameIds = await dbContext.LibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => e.GameId)
            .ToListAsync(cancellationToken);

        var candidates = await dbContext.Games
            .AsNoTracking()
            .Where(g => !ownedGameIds.Contains(g.Id))
            .Where(g => g.MetadataStatus == MetadataStatus.Complete)
            .OrderByDescending(g => g.UpdatedAt)
            .Take(5)
            .Select(g => g.Title)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            candidates = await dbContext.Games
                .AsNoTracking()
                .Where(g => !ownedGameIds.Contains(g.Id))
                .OrderBy(g => g.SortTitle)
                .Take(5)
                .Select(g => g.Title)
                .ToListAsync(cancellationToken);
        }

        return candidates
            .Select(title => new LlmSuggestion
            {
                Title = title,
                Reason = libraryTitles.Count == 0
                    ? "Popular pick from the GoodPlays catalog."
                    : "Suggested from catalog titles you have not added yet."
            })
            .ToList();
    }

    private async Task<RecommendationResult?> ResolveSuggestionAsync(
        LlmSuggestion suggestion,
        CancellationToken cancellationToken)
    {
        var matches = await gameCatalogService.SearchAsync(suggestion.Title, cancellationToken);
        var match = matches.FirstOrDefault(m =>
                         string.Equals(m.Title, suggestion.Title, StringComparison.OrdinalIgnoreCase))
                     ?? matches.FirstOrDefault();

        if (match is null)
        {
            return null;
        }

        var gameId = match.Id;
        if (gameId == Guid.Empty && match.IgdbId is > 0)
        {
            var imported = await gameCatalogService.ImportFromIgdbAsync(match.IgdbId.Value, cancellationToken);
            gameId = imported?.Id ?? Guid.Empty;
        }

        if (gameId == Guid.Empty)
        {
            return null;
        }

        return new RecommendationResult(gameId, match.Title, match.CoverUrl, 1.0, suggestion.Reason);
    }

    private static string ExtractJsonArray(string content)
    {
        var start = content.IndexOf('[');
        var end = content.LastIndexOf(']');
        if (start >= 0 && end > start)
        {
            return content[start..(end + 1)];
        }

        return content.Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class LlmSuggestion
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}

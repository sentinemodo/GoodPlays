using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoodPlays.Infrastructure.Services;

public sealed class ImportStatsDocument
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string? InputText { get; set; }
    public List<ImportLineResult> Lines { get; set; } = [];
    public int AddedCount { get; set; }
    public int SkippedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public int AmbiguousCount { get; set; }

    public static ImportStatsDocument Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ImportStatsDocument();
        }

        return JsonSerializer.Deserialize<ImportStatsDocument>(json, JsonOptions) ?? new ImportStatsDocument();
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed class ImportLineResult
{
    public string RawLine { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? MatchedTitle { get; set; }
    public Guid? GameId { get; set; }
    public long? IgdbId { get; set; }
    public List<ImportMatchCandidate>? Candidates { get; set; }
}

public sealed class ImportMatchCandidate
{
    public string Title { get; set; } = string.Empty;
    public Guid? GameId { get; set; }
    public long? IgdbId { get; set; }
}

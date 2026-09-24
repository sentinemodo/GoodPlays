using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class GameEnrichmentRun
{
    public Guid GameId { get; set; }
    public EnrichmentKind Kind { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public string? Status { get; set; }

    public Game Game { get; set; } = null!;
}

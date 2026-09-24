using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class GameRatingCache
{
    public Guid GameId { get; set; }
    public RatingSource Source { get; set; }
    public decimal? Score { get; set; }
    public int? ReviewCount { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public Game Game { get; set; } = null!;
}

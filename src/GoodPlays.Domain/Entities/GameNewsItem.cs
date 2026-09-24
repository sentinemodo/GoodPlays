namespace GoodPlays.Domain.Entities;

public class GameNewsItem
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public required string Source { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public Game Game { get; set; } = null!;
}

namespace GoodPlays.Domain.Entities;

public class ShelfEntry
{
    public Guid ShelfId { get; set; }
    public Guid GameId { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    public Shelf Shelf { get; set; } = null!;
    public Game Game { get; set; } = null!;
}

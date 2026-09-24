namespace GoodPlays.Domain.Entities;

public class Shelf
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<ShelfEntry> Entries { get; set; } = [];
}

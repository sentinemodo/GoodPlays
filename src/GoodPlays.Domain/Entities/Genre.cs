namespace GoodPlays.Domain.Entities;

public class Genre
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }

    public ICollection<GameGenre> GameGenres { get; set; } = [];
}

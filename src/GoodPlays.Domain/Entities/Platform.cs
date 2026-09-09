namespace GoodPlays.Domain.Entities;

public class Platform
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }

    public ICollection<GamePlatform> GamePlatforms { get; set; } = [];
}

namespace GoodPlays.Domain.Entities;

public class Achievement
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public decimal? RarityPercent { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public Game Game { get; set; } = null!;
    public ICollection<UserAchievement> UserAchievements { get; set; } = [];
}

namespace GoodPlays.Domain.Entities;

public class UserAchievement
{
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public DateTimeOffset UnlockedAt { get; set; }
    public bool IsFeatured { get; set; }

    public User User { get; set; } = null!;
    public Achievement Achievement { get; set; } = null!;
}

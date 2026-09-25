namespace GoodPlays.Domain.Entities;

public class ActivityLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}

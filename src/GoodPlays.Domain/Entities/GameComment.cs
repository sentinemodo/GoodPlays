using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class GameComment
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid UserId { get; set; }
    public required string Body { get; set; }
    public short? Rating { get; set; }
    public Visibility Visibility { get; set; } = Visibility.Public;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Game Game { get; set; } = null!;
    public User User { get; set; } = null!;
}

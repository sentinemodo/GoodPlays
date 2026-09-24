namespace GoodPlays.Domain.Entities;

public class GameTag
{
    public Guid GameId { get; set; }
    public Guid TagId { get; set; }

    public Game Game { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class GameExternalId
{
    public Guid GameId { get; set; }
    public ExternalIdSource Source { get; set; }
    public required string ExternalId { get; set; }

    public Game Game { get; set; } = null!;
}

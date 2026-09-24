using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public TagScope Scope { get; set; } = TagScope.User;

    public User? User { get; set; }
    public ICollection<GameTag> GameTags { get; set; } = [];
}

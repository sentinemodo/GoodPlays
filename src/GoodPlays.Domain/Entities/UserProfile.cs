using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class UserProfile
{
    public Guid UserId { get; set; }
    public required string Username { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsProfilePublic { get; set; } = true;
    public LibraryVisibility LibraryVisibility { get; set; } = LibraryVisibility.Public;
    public string StatsJson { get; set; } = "{}";

    public User User { get; set; } = null!;
}

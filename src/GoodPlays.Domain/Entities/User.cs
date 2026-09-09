namespace GoodPlays.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string ClerkId { get; set; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public UserProfile? Profile { get; set; }
    public ICollection<LibraryEntry> LibraryEntries { get; set; } = [];
    public ICollection<ImportJob> ImportJobs { get; set; } = [];
    public ICollection<PlatformConnection> PlatformConnections { get; set; } = [];
}

using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class LibraryEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public LibraryStatus Status { get; set; } = LibraryStatus.Owned;
    public short? Rating { get; set; }
    public decimal? HoursPlayed { get; set; }
    public HoursPlayedSource HoursPlayedSource { get; set; } = HoursPlayedSource.Manual;
    public bool HoursPlayedLocked { get; set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? CompletedAt { get; set; }
    public LibraryEntrySource Source { get; set; } = LibraryEntrySource.Manual;
    public Visibility Visibility { get; set; } = Visibility.Public;
    public string? PlatformExternalId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Game Game { get; set; } = null!;
}

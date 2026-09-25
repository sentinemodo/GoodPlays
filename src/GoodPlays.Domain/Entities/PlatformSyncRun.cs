using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class PlatformSyncRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public PlatformSyncRunStatus Status { get; set; } = PlatformSyncRunStatus.Pending;
    public string Phase { get; set; } = "Queued";
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public string? Warning { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}

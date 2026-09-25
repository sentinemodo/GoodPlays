namespace GoodPlays.Infrastructure.Services;

public sealed record XboxSyncResultDto(
    int AddedCount,
    int UpdatedCount,
    int SkippedCount,
    int UnmatchedCount,
    DateTimeOffset SyncedAt,
    string? Warning);

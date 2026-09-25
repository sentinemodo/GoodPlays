namespace GoodPlays.Infrastructure.Services;

public sealed record SwitchSyncResultDto(
    int AddedCount,
    int UpdatedCount,
    int SkippedCount,
    int UnmatchedCount,
    DateTimeOffset SyncedAt,
    string? Warning);

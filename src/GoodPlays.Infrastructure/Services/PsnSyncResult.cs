namespace GoodPlays.Infrastructure.Services;

public sealed record PsnSyncResultDto(
    int AddedCount,
    int UpdatedCount,
    int SkippedCount,
    int UnmatchedCount,
    DateTimeOffset SyncedAt,
    string? Warning);

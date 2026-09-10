namespace GoodPlays.Infrastructure.Services;

public sealed record SteamSyncResultDto(
    int AddedCount,
    int UpdatedCount,
    int SkippedCount,
    int UnmatchedCount,
    DateTimeOffset SyncedAt,
    string? Warning);

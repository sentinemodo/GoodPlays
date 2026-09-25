namespace GoodPlays.Infrastructure.Services;

public sealed record SyncProgressUpdate(
    string Phase,
    int ProcessedCount,
    int TotalCount,
    int AddedCount,
    int UpdatedCount,
    int SkippedCount,
    int UnmatchedCount);

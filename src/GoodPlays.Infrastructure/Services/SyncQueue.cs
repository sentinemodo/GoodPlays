namespace GoodPlays.Infrastructure.Services;

public static class SyncQueue
{
    public static readonly TimeSpan MinimumAutomaticSyncAge = TimeSpan.FromDays(7);

    public static IReadOnlyList<T> Order<T>(
        IEnumerable<T> items,
        Func<T, string> externalId,
        IReadOnlyDictionary<string, DateTimeOffset> libraryUpdatedAt)
    {
        return items
            .OrderBy(item => libraryUpdatedAt.ContainsKey(externalId(item)) ? 1 : 0)
            .ThenBy(item => libraryUpdatedAt.TryGetValue(externalId(item), out var updated) ? updated : DateTimeOffset.MinValue)
            .ThenBy(item => externalId(item), StringComparer.Ordinal)
            .ToList();
    }

    public static bool ShouldAutomaticallySync(DateTimeOffset? lastCompletedAt, DateTimeOffset now) =>
        lastCompletedAt is not null && now - lastCompletedAt.Value >= MinimumAutomaticSyncAge;
}

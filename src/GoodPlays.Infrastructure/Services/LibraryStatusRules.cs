using GoodPlays.Domain.Enums;

namespace GoodPlays.Infrastructure.Services;

public static class LibraryStatusRules
{
    public const decimal PlayingHoursThreshold = 1m;

    /// <summary>
    /// Derives Owned vs Playing from playtime and last activity.
    /// Completed and Dropped are never changed by sync.
    /// </summary>
    public static LibraryStatus InferFromPlayActivity(
        decimal? hoursPlayed,
        DateOnly? lastPlayedAt,
        LibraryStatus currentStatus,
        DateOnly? today = null)
    {
        if (currentStatus is LibraryStatus.Completed or LibraryStatus.Dropped)
        {
            return currentStatus;
        }

        var hours = hoursPlayed ?? 0m;
        if (hours < PlayingHoursThreshold)
        {
            return LibraryStatus.Owned;
        }

        if (lastPlayedAt is null)
        {
            return LibraryStatus.Owned;
        }

        var reference = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = reference.AddMonths(-1);
        return lastPlayedAt >= cutoff ? LibraryStatus.Playing : LibraryStatus.Owned;
    }

    public static DateOnly? PickLatestLastPlayed(DateOnly? existing, DateOnly? incoming) =>
        incoming is null ? existing :
        existing is null ? incoming :
        incoming > existing ? incoming : existing;
}

public static class LibrarySourceLabels
{
    public static bool IsPlatformSync(LibraryEntrySource source) =>
        source is LibraryEntrySource.SteamSync
            or LibraryEntrySource.PsnSync
            or LibraryEntrySource.XboxSync
            or LibraryEntrySource.SwitchSync;

    public static string Format(LibraryEntrySource? source) => source switch
    {
        LibraryEntrySource.SteamSync => "Steam",
        LibraryEntrySource.PsnSync => "PlayStation",
        LibraryEntrySource.XboxSync => "Xbox",
        LibraryEntrySource.SwitchSync => "Nintendo Switch",
        LibraryEntrySource.Manual => "Manual",
        LibraryEntrySource.ImportText or LibraryEntrySource.ImportImage or LibraryEntrySource.ImportCsv => "Import",
        LibraryEntrySource.ResearchReco => "Recommendation",
        null => "Unknown",
        _ => source.Value.ToString()
    };
}

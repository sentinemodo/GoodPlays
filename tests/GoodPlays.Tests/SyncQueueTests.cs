using GoodPlays.Infrastructure.Services;

namespace GoodPlays.Tests;

public class SyncQueueTests
{
    [Fact]
    public void Order_PutsUnlistedGamesBeforeOlderLibraryData()
    {
        var now = DateTimeOffset.Parse("2026-09-25T00:00:00Z");
        var items = new[] { "old", "new", "missing" };
        var library = new Dictionary<string, DateTimeOffset>
        {
            ["old"] = now.AddDays(-30),
            ["new"] = now.AddDays(-1)
        };

        var ordered = SyncQueue.Order(items, id => id, library);

        Assert.Equal(["missing", "old", "new"], ordered);
    }

    [Fact]
    public void ShouldAutomaticallySync_RequiresAWeekSinceCompletion()
    {
        var now = DateTimeOffset.Parse("2026-09-25T00:00:00Z");
        Assert.False(SyncQueue.ShouldAutomaticallySync(now.AddDays(-6), now));
        Assert.False(SyncQueue.ShouldAutomaticallySync(null, now));
        Assert.True(SyncQueue.ShouldAutomaticallySync(now.AddDays(-7), now));
    }
}

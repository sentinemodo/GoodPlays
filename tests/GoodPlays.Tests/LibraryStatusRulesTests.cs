using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Services;

namespace GoodPlays.Tests;

public class LibraryStatusRulesTests
{
    private static readonly DateOnly Today = new(2026, 9, 13);

    [Theory]
    [InlineData(0, null, LibraryStatus.Owned)]
    [InlineData(0.5, null, LibraryStatus.Owned)]
    [InlineData(10, null, LibraryStatus.Owned)]
    [InlineData(10, "2026-09-01", LibraryStatus.Playing)]
    [InlineData(10, "2026-07-01", LibraryStatus.Owned)]
    public void InferFromPlayActivity_AppliesHoursAndRecencyRules(
        decimal hours,
        string? lastPlayed,
        LibraryStatus expected)
    {
        DateOnly? lastPlayedAt = lastPlayed is null ? null : DateOnly.Parse(lastPlayed);

        var status = LibraryStatusRules.InferFromPlayActivity(
            hours,
            lastPlayedAt,
            LibraryStatus.Owned,
            Today);

        Assert.Equal(expected, status);
    }

    [Fact]
    public void InferFromPlayActivity_PreservesCompletedAndDropped()
    {
        Assert.Equal(
            LibraryStatus.Completed,
            LibraryStatusRules.InferFromPlayActivity(100m, Today, LibraryStatus.Completed, Today));

        Assert.Equal(
            LibraryStatus.Dropped,
            LibraryStatusRules.InferFromPlayActivity(100m, Today, LibraryStatus.Dropped, Today));
    }
}

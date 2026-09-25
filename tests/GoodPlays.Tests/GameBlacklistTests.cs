using GoodPlays.Infrastructure.Services;

namespace GoodPlays.Tests;

public class GameBlacklistTests
{
    [Theory]
    [InlineData("Netflix", true)]
    [InlineData("Amazon Prime Video", true)]
    [InlineData("Prime Video", true)]
    [InlineData("Disney Plus", true)]
    [InlineData("Crunchyroll", true)]
    [InlineData("Tubi", true)]
    [InlineData("Helldivers 2", false)]
    [InlineData("God of War", false)]
    public void IsNonGameApplication_DetectsStreamingApps(string title, bool expected)
    {
        Assert.Equal(expected, GameBlacklist.IsNonGameApplication(title));
    }
}

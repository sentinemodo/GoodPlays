using GoodPlays.Infrastructure.Steam;

namespace GoodPlays.Tests;

public class SteamIdParserTests
{
    [Theory]
    [InlineData("76561198000000000")]
    [InlineData(" 76561198000000000 ")]
    public void IsSteamId64_ReturnsTrueForNumericId(string input)
    {
        Assert.True(SteamIdParser.IsSteamId64(input));
    }

    [Fact]
    public void TryExtractSteamId64_ParsesProfileUrl()
    {
        var id = SteamIdParser.TryExtractSteamId64("https://steamcommunity.com/profiles/76561198000000000");
        Assert.Equal("76561198000000000", id);
    }

    [Fact]
    public void TryExtractVanityName_ParsesVanityUrl()
    {
        var vanity = SteamIdParser.TryExtractVanityName("https://steamcommunity.com/id/gaben");
        Assert.Equal("gaben", vanity);
    }

    [Fact]
    public void TryExtractVanityName_ReturnsPlainVanityName()
    {
        var vanity = SteamIdParser.TryExtractVanityName("gaben");
        Assert.Equal("gaben", vanity);
    }
}

using GoodPlays.Infrastructure.Psn;

namespace GoodPlays.Tests;

public class PsnDurationParserTests
{
    [Theory]
    [InlineData("PT14H23M", 14.38)]
    [InlineData("PT228H56M33S", 228.94)]
    [InlineData("PT45M", 0.75)]
    [InlineData("", 0)]
    public void ParseToHours_ConvertsIso8601Duration(string input, decimal expectedHours)
    {
        var hours = PsnDurationParser.ParseToHours(input);
        Assert.Equal(expectedHours, hours, precision: 2);
    }
}

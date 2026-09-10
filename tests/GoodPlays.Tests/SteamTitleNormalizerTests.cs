using GoodPlays.Infrastructure.Steam;

namespace GoodPlays.Tests;

public class SteamTitleNormalizerTests
{
    [Theory]
    [InlineData("Hades™", "Hades")]
    [InlineData("The Witcher 3: Wild Hunt (Soundtrack)", "The Witcher 3: Wild Hunt")]
    [InlineData("Portal 2 (Demo)", "Portal 2")]
    public void Normalize_StripsTrademarksAndSuffixes(string input, string expected)
    {
        Assert.Equal(expected, SteamTitleNormalizer.Normalize(input));
    }
}

using GoodPlays.Infrastructure.Psn;

namespace GoodPlays.Tests;

public class PsnTitleNormalizerTests
{
    [Theory]
    [InlineData("God of War (PS4)", "God of War")]
    [InlineData("HELLDIVERS™ 2", "HELLDIVERS 2")]
    [InlineData("Horizon Forbidden West (PS5)", "Horizon Forbidden West")]
    [InlineData("Game Name (Complete Edition)", "Game Name")]
    public void Normalize_StripsPlatformAndEditionSuffixes(string input, string expected)
    {
        Assert.Equal(expected, PsnTitleNormalizer.Normalize(input));
    }
}

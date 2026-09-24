using GoodPlays.Infrastructure.Services;

namespace GoodPlays.Tests;

public class StarRatingTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(10, 5)]
    [InlineData(7, 3.5)]
    public void ToStars_ConvertsHalfStarScale(short halfStars, decimal expectedStars)
    {
        Assert.Equal(expectedStars, StarRating.ToStars(halfStars));
    }

    [Theory]
    [InlineData(4.5, 9)]
    [InlineData(0.25, 1)]
    [InlineData(5, 10)]
    public void FromStars_ConvertsToHalfStarScale(decimal stars, short expectedHalfStars)
    {
        Assert.Equal(expectedHalfStars, StarRating.FromStars(stars));
    }
}

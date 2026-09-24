namespace GoodPlays.Infrastructure.Services;

/// <summary>
/// Half-star rating scale stored as short 1–10 (2 = 1★, 10 = 5★).
/// </summary>
public static class StarRating
{
    public const short MinHalfStars = 1;
    public const short MaxHalfStars = 10;

    public static short? Normalize(short? halfStars)
    {
        if (halfStars is null)
        {
            return null;
        }

        return (short)Math.Clamp(halfStars.Value, MinHalfStars, MaxHalfStars);
    }

    public static decimal ToStars(short halfStars) => halfStars / 2m;

    public static short FromStars(decimal stars)
    {
        var clamped = Math.Clamp(stars, 0.5m, 5m);
        return (short)Math.Round(clamped * 2m, MidpointRounding.AwayFromZero);
    }
}

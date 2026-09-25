using GoodPlays.Domain.Enums;

namespace GoodPlays.Infrastructure.Services;

public static class TrophyClassRules
{
    public static TrophyClass FromRarity(decimal? rarityPercent) => rarityPercent switch
    {
        null => TrophyClass.Bronze,
        <= 5m => TrophyClass.Platinum,
        <= 20m => TrophyClass.Gold,
        <= 50m => TrophyClass.Silver,
        _ => TrophyClass.Bronze
    };
}

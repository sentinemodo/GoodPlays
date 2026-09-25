using GoodPlays.Domain.Enums;

namespace GoodPlays.Infrastructure.Services;

public sealed record ProfileStatDto(string Key, string Label, int GameCount, decimal Hours);

public sealed record ProfileGameDto(
    Guid GameId,
    string Title,
    string Slug,
    string? CoverUrl,
    decimal? HoursPlayed,
    DateOnly? LastPlayed,
    bool IsLoved);

public sealed record ProfileTrophyDto(
    Guid AchievementId,
    Guid GameId,
    string GameTitle,
    string GameSlug,
    string Name,
    string? Description,
    string? IconUrl,
    decimal? RarityPercent,
    TrophyClass TrophyClass,
    DateTimeOffset UnlockedAt,
    bool IsFeatured);

public sealed record ProfileTrophyGameDto(Guid GameId, string Title);

public sealed record ProfileDto(
    string Username,
    string? DisplayName,
    string? Bio,
    string? AvatarUrl,
    ProfileGameDto? MostLovedGame,
    bool MostLovedIsStarred,
    IReadOnlyList<ProfileStatDto> Platforms,
    IReadOnlyList<ProfileStatDto> Categories,
    IReadOnlyList<ProfileGameDto> TopGamesByTime,
    IReadOnlyList<ProfileGameDto> LastPlayedGames,
    ProfileTrophyDto? MostImportantTrophy,
    ProfileTrophyDto? MostRecentTrophy,
    IReadOnlyList<ProfileTrophyDto> Trophies,
    IReadOnlyList<ProfileTrophyGameDto> TrophyGames);

public sealed record UpdateProfileRequest(string? Bio, string? AvatarUrl);

public interface IProfileService
{
    Task<ProfileDto> GetAsync(
        Guid userId,
        Guid? trophyGameId,
        TrophyClass? trophyClass,
        decimal? maxRarity,
        CancellationToken cancellationToken);

    Task<ProfileDto?> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);

    Task<bool> SetFeaturedTrophyAsync(Guid userId, Guid achievementId, bool featured, CancellationToken cancellationToken);
}

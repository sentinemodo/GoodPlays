using GoodPlays.Domain.Enums;

namespace GoodPlays.Infrastructure.Services;

public sealed record GameRatingDto(
    RatingSource Source,
    decimal? Score,
    int? ReviewCount,
    string? Url,
    DateTimeOffset FetchedAt);

public sealed record GameNewsDto(
    Guid Id,
    string Source,
    string Title,
    string Url,
    DateTimeOffset? PublishedAt);

public sealed record GameHeroDto(
    string Username,
    string? DisplayName,
    decimal Value,
    string Metric);

public sealed record AchievementOwnerDto(string Username, string? DisplayName, DateTimeOffset UnlockedAt);

public sealed record AchievementDto(
    Guid Id,
    string Name,
    string? Description,
    string? IconUrl,
    decimal? RarityPercent,
    bool UnlockedByCurrentUser,
    IReadOnlyList<AchievementOwnerDto> Owners);

public sealed record GameCommentDto(
    Guid Id,
    string Username,
    string? DisplayName,
    string Body,
    short? Rating,
    DateTimeOffset CreatedAt);

public sealed record GameDetailDto(
    Guid Id,
    string Title,
    string Slug,
    string? Summary,
    string? CoverUrl,
    DateOnly? ReleaseDate,
    string? Developer,
    string? Publisher,
    GameType GameType,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<GameSummaryDto> Dlc,
    GameDetailStatsDto Stats,
    IReadOnlyList<GameRatingDto> Ratings,
    IReadOnlyList<GameHeroDto> Heroes,
    IReadOnlyList<AchievementDto> Achievements,
    IReadOnlyList<GameNewsDto> News,
    IReadOnlyList<GameCommentDto> Comments,
    LibraryEntryDto? UserLibraryEntry,
    IReadOnlyList<string> UserTags);

public sealed record GameDetailStatsDto(
    int TotalPlayers,
    int ActivePlayersLast30Days,
    decimal TotalHours,
    decimal? AvgRating,
    decimal? MedianRating,
    decimal CompletionRate,
    int BacklogCount);

public interface IGameDetailService
{
    Task<GameDetailDto?> GetBySlugAsync(string slug, Guid? userId, CancellationToken cancellationToken);
}

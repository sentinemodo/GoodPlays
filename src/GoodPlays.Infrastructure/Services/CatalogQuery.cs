using GoodPlays.Domain.Enums;

namespace GoodPlays.Infrastructure.Services;

public enum CatalogSortField
{
    Title,
    ReleaseDate,
    UpdatedAt,
    Rating,
    Hours,
    TotalPlayers,
    LastPlayed
}

public enum CatalogGroupBy
{
    None,
    Genre,
    Platform,
    Status,
    Tag,
    ReleaseDate
}

public sealed record CatalogQuery(
    string? Search = null,
    string? GenreSlug = null,
    string? PlatformSlug = null,
    string? TagSlug = null,
    string? ShelfSlug = null,
    int? ReleaseDecade = null,
    bool InLibrary = false,
    LibraryStatus? LibraryStatus = null,
    LibraryEntrySource? LibrarySource = null,
    CatalogSortField Sort = CatalogSortField.Title,
    bool Descending = false,
    int Page = 1,
    int PageSize = 20,
    CatalogGroupBy GroupBy = CatalogGroupBy.None,
    IReadOnlyList<string>? GroupKeys = null);

public sealed record CatalogGameDto(
    Guid Id,
    string Title,
    string Slug,
    string? CoverUrl,
    DateOnly? ReleaseDate,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Platforms,
    int TotalPlayers,
    decimal? AvgRating,
    Guid? LibraryEntryId,
    LibraryStatus? LibraryStatus,
    short? UserRating,
    decimal? UserHours,
    LibraryEntrySource? LibrarySource,
    DateOnly? UserLastPlayed,
    IReadOnlyList<string> UserTags,
    IReadOnlyList<CatalogGameDto> Dlc,
    bool IsLoved = false);

public sealed record CatalogGroupDto(
    string Key,
    string Label,
    IReadOnlyList<CatalogGameDto> Items);

public sealed record PaginatedCatalogResult(
    IReadOnlyList<CatalogGameDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<CatalogGroupDto>? Groups);

public sealed record CatalogFacetCount(string Slug, string Name, int Count);

public sealed record CatalogFacetsDto(
    int TotalGames,
    int? MyLibraryCount,
    decimal? MyLibraryHours,
    IReadOnlyList<CatalogFacetCount> Categories,
    IReadOnlyList<CatalogFacetCount> Platforms,
    IReadOnlyList<CatalogFacetCount> Tags,
    IReadOnlyList<CatalogFacetCount> Shelves,
    IReadOnlyList<CatalogFacetCount> LibrarySources,
    IReadOnlyList<CatalogFacetCount> LibraryStatuses);

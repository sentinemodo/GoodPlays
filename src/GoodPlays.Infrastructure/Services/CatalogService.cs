using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed class CatalogService(GoodPlaysDbContext dbContext) : ICatalogService
{
    private const int MaxPageSize = 100;

    public async Task<PaginatedCatalogResult> BrowseAsync(
        Guid? userId,
        CatalogQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var gamesQuery = dbContext.Games
            .AsNoTracking()
            .Where(g => !g.IsHiddenFromCatalog)
            .Where(g => g.GameType == GameType.Base || g.GameType == GameType.StandaloneExpansion);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            gamesQuery = gamesQuery.Where(g =>
                g.SortTitle.Contains(term) || g.Title.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.GenreSlug))
        {
            gamesQuery = gamesQuery.Where(g =>
                g.GameGenres.Any(gg => gg.Genre.Slug == query.GenreSlug));
        }

        if (!string.IsNullOrWhiteSpace(query.PlatformSlug))
        {
            gamesQuery = gamesQuery.Where(g =>
                g.GamePlatforms.Any(gp => gp.Platform.Slug == query.PlatformSlug));
        }

        if (!string.IsNullOrWhiteSpace(query.TagSlug))
        {
            gamesQuery = gamesQuery.Where(g =>
                g.GameTags.Any(gt =>
                    gt.Tag.Slug == query.TagSlug &&
                    (userId == null || gt.Tag.UserId == null || gt.Tag.UserId == userId)));
        }

        if (!string.IsNullOrWhiteSpace(query.ShelfSlug) && userId is not null)
        {
            gamesQuery = gamesQuery.Where(g =>
                g.ShelfEntries.Any(se =>
                    se.Shelf.UserId == userId && se.Shelf.Slug == query.ShelfSlug));
        }

        if (query.ReleaseDecade is not null)
        {
            var decadeStart = query.ReleaseDecade.Value;
            var decadeEnd = decadeStart + 9;
            gamesQuery = gamesQuery.Where(g =>
                g.ReleaseDate != null &&
                g.ReleaseDate.Value.Year >= decadeStart &&
                g.ReleaseDate.Value.Year <= decadeEnd);
        }

        if (query.InLibrary && userId is not null)
        {
            gamesQuery = gamesQuery.Where(g =>
                g.LibraryEntries.Any(e => e.UserId == userId));

            if (query.LibraryStatus is not null)
            {
                gamesQuery = gamesQuery.Where(g =>
                    g.LibraryEntries.Any(e => e.UserId == userId && e.Status == query.LibraryStatus));
            }

            if (query.LibrarySource is not null)
            {
                gamesQuery = gamesQuery.Where(g =>
                    g.LibraryEntries.Any(e => e.UserId == userId && e.Source == query.LibrarySource));
            }
        }

        var projected = gamesQuery.Select(g => new CatalogProjection
        {
            Id = g.Id,
            Title = g.Title,
            Slug = g.Slug,
            CoverUrl = g.CoverUrl,
            ReleaseDate = g.ReleaseDate,
            UpdatedAt = g.UpdatedAt,
            Categories = g.GameGenres.Select(gg => gg.Genre.Name).ToList(),
            CategorySlugs = g.GameGenres.Select(gg => gg.Genre.Slug).ToList(),
            Platforms = g.GamePlatforms.Select(gp => gp.Platform.Name).ToList(),
            PlatformSlugs = g.GamePlatforms.Select(gp => gp.Platform.Slug).ToList(),
            TotalPlayers = g.LibraryEntries.Count,
            AvgRating = g.LibraryEntries.Where(e => e.Rating != null).Average(e => (double?)e.Rating),
            LibraryEntry = userId == null
                ? null
                : g.LibraryEntries
                    .Where(e => e.UserId == userId)
                    .OrderByDescending(e => e.UpdatedAt)
                    .Select(e => new LibraryProjection
                    {
                        EntryId = e.Id,
                        Status = e.Status,
                        Rating = e.Rating,
                        HoursPlayed = e.HoursPlayed,
                        Source = e.Source,
                        LastPlayed = e.StartedAt,
                        IsLoved = e.IsLoved
                    })
                    .FirstOrDefault()
        });

        projected = ApplySort(projected, query, userId);

        var totalCount = await projected.CountAsync(cancellationToken);

        if (query.GroupBy != CatalogGroupBy.None)
        {
            var allItems = await projected.ToListAsync(cancellationToken);
            await EnrichUserTagsAsync(allItems, userId, cancellationToken);
            var groups = BuildGroups(allItems, query.GroupBy, query.InLibrary);

            if (query.GroupKeys is { Count: > 0 })
            {
                var allowed = query.GroupKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                groups = groups.Where(g => allowed.Contains(g.Key)).ToList();
            }

            var uniqueGames = groups
                .SelectMany(g => g.Items)
                .DistinctBy(i => i.Id)
                .ToList();
            var groupedTotalCount = uniqueGames.Count;

            var pagedGames = uniqueGames
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            var pagedIds = pagedGames.Select(g => g.Id).ToHashSet();

            var dlcByParent = await LoadDlcByParentAsync(
                pagedGames.Select(i => i.Id).ToList(),
                userId,
                query.InLibrary,
                cancellationToken);

            var pagedGroups = groups
                .Select(g => new GroupBucket(
                    g.Key,
                    g.Label,
                    g.Items.Where(i => pagedIds.Contains(i.Id)).DistinctBy(i => i.Id).ToList()))
                .Where(g => g.Items.Count > 0)
                .Select(g => new CatalogGroupDto(
                    g.Key,
                    g.Label,
                    g.Items.Select(i => MapProjection(i, dlcByParent.GetValueOrDefault(i.Id, []))).ToList()))
                .ToList();

            return new PaginatedCatalogResult(
                [],
                groupedTotalCount,
                page,
                pageSize,
                pagedGroups);
        }

        var pageItems = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        await EnrichUserTagsAsync(pageItems, userId, cancellationToken);

        var pageDlc = await LoadDlcByParentAsync(
            pageItems.Select(i => i.Id).ToList(),
            userId,
            query.InLibrary,
            cancellationToken);

        return new PaginatedCatalogResult(
            pageItems.Select(i => MapProjection(i, pageDlc.GetValueOrDefault(i.Id, []))).ToList(),
            totalCount,
            page,
            pageSize,
            null);
    }

    public async Task<CatalogFacetsDto> GetFacetsAsync(Guid? userId, CancellationToken cancellationToken)
    {
        var visibleBaseGames = dbContext.Games
            .AsNoTracking()
            .Where(g => !g.IsHiddenFromCatalog)
            .Where(g => g.GameType == GameType.Base || g.GameType == GameType.StandaloneExpansion);

        var totalGames = await visibleBaseGames.CountAsync(cancellationToken);

        int? myCount = null;
        decimal? myHours = null;
        IReadOnlyList<CatalogFacetCount> librarySources = [];
        if (userId is not null)
        {
            var userLibrary = dbContext.LibraryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId && !e.Game.IsHiddenFromCatalog);

            myCount = await userLibrary
                .Select(e => e.GameId)
                .Distinct()
                .CountAsync(cancellationToken);

            myHours = await userLibrary
                .Where(e => e.HoursPlayed != null)
                .SumAsync(e => e.HoursPlayed, cancellationToken);

            var sourceRows = await userLibrary
                .GroupBy(e => e.Source)
                .Select(g => new { g.Key, Count = g.Select(e => e.GameId).Distinct().Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync(cancellationToken);

            librarySources = sourceRows
                .Select(g => new CatalogFacetCount(
                    g.Key.ToString(),
                    FormatLibrarySource(g.Key),
                    g.Count))
                .ToList();
        }

        var categoryRows = await dbContext.GameGenres
            .AsNoTracking()
            .Where(gg => !gg.Game.IsHiddenFromCatalog)
            .Where(gg => gg.Game.GameType == GameType.Base || gg.Game.GameType == GameType.StandaloneExpansion)
            .GroupBy(gg => new { gg.Genre.Slug, gg.Genre.Name })
            .Select(g => new { g.Key.Slug, g.Key.Name, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(30)
            .ToListAsync(cancellationToken);

        var categories = categoryRows
            .Select(g => new CatalogFacetCount(g.Slug, g.Name, g.Count))
            .ToList();

        var platformRows = await dbContext.GamePlatforms
            .AsNoTracking()
            .Where(gp => !gp.Game.IsHiddenFromCatalog)
            .Where(gp => gp.Game.GameType == GameType.Base || gp.Game.GameType == GameType.StandaloneExpansion)
            .GroupBy(gp => new { gp.Platform.Slug, gp.Platform.Name })
            .Select(g => new { g.Key.Slug, g.Key.Name, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(20)
            .ToListAsync(cancellationToken);

        var platforms = platformRows
            .Select(g => new CatalogFacetCount(g.Slug, g.Name, g.Count))
            .ToList();

        var tagRows = await dbContext.GameTags
            .AsNoTracking()
            .Where(gt => !gt.Game.IsHiddenFromCatalog)
            .Where(gt => userId == null || gt.Tag.UserId == null || gt.Tag.UserId == userId)
            .GroupBy(gt => new { gt.Tag.Slug, gt.Tag.Name })
            .Select(g => new { g.Key.Slug, g.Key.Name, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(30)
            .ToListAsync(cancellationToken);

        var tags = tagRows
            .Select(g => new CatalogFacetCount(g.Slug, g.Name, g.Count))
            .ToList();

        var shelves = userId is null
            ? []
            : (await dbContext.Shelves
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => new { s.Slug, s.Name, Count = s.Entries.Count })
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken))
                .Select(s => new CatalogFacetCount(s.Slug, s.Name, s.Count))
                .ToList();

        IReadOnlyList<CatalogFacetCount> libraryStatuses = [];
        if (userId is not null)
        {
            var statusRows = await dbContext.LibraryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId && !e.Game.IsHiddenFromCatalog)
                .GroupBy(e => e.Status)
                .Select(g => new { g.Key, Count = g.Select(e => e.GameId).Distinct().Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync(cancellationToken);

            libraryStatuses = statusRows
                .Select(g => new CatalogFacetCount(
                    g.Key.ToString(),
                    g.Key.ToString(),
                    g.Count))
                .ToList();
        }

        return new CatalogFacetsDto(
            totalGames,
            myCount,
            myHours,
            categories,
            platforms,
            tags,
            shelves,
            librarySources,
            libraryStatuses);
    }

    private async Task EnrichUserTagsAsync(
        IReadOnlyList<CatalogProjection> items,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (userId is null || items.Count == 0)
        {
            return;
        }

        var gameIds = items.Select(i => i.Id).ToList();
        var tagRows = await dbContext.GameTags
            .AsNoTracking()
            .Where(gt => gameIds.Contains(gt.GameId))
            .Where(gt => gt.Tag.UserId == null || gt.Tag.UserId == userId)
            .Select(gt => new { gt.GameId, gt.Tag.Name, gt.Tag.Slug })
            .ToListAsync(cancellationToken);

        var tagsByGame = tagRows
            .GroupBy(r => r.GameId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var item in items)
        {
            if (!tagsByGame.TryGetValue(item.Id, out var tags))
            {
                continue;
            }

            item.UserTags = tags.Select(t => t.Name).ToList();
            item.UserTagSlugs = tags.Select(t => t.Slug).ToList();
        }
    }

    private async Task<Dictionary<Guid, List<CatalogGameDto>>> LoadDlcByParentAsync(
        IReadOnlyList<Guid> parentIds,
        Guid? userId,
        bool inLibrary,
        CancellationToken cancellationToken)
    {
        if (parentIds.Count == 0)
        {
            return [];
        }

        var dlcQuery = dbContext.Games
            .AsNoTracking()
            .Where(g => !g.IsHiddenFromCatalog)
            .Where(g => g.ParentGameId != null && parentIds.Contains(g.ParentGameId.Value))
            .Where(g => g.GameType == GameType.Dlc || g.GameType == GameType.Expansion);

        if (inLibrary && userId is not null)
        {
            dlcQuery = dlcQuery.Where(g => g.LibraryEntries.Any(e => e.UserId == userId));
        }

        var dlcItems = await dlcQuery
            .Select(g => new CatalogProjection
            {
                Id = g.Id,
                ParentGameId = g.ParentGameId,
                Title = g.Title,
                Slug = g.Slug,
                CoverUrl = g.CoverUrl,
                ReleaseDate = g.ReleaseDate,
                UpdatedAt = g.UpdatedAt,
                Categories = g.GameGenres.Select(gg => gg.Genre.Name).ToList(),
                CategorySlugs = g.GameGenres.Select(gg => gg.Genre.Slug).ToList(),
                Platforms = g.GamePlatforms.Select(gp => gp.Platform.Name).ToList(),
                PlatformSlugs = g.GamePlatforms.Select(gp => gp.Platform.Slug).ToList(),
                TotalPlayers = g.LibraryEntries.Count,
                AvgRating = g.LibraryEntries.Where(e => e.Rating != null).Average(e => (double?)e.Rating),
                LibraryEntry = userId == null
                    ? null
                    : g.LibraryEntries
                        .Where(e => e.UserId == userId)
                        .OrderByDescending(e => e.UpdatedAt)
                        .Select(e => new LibraryProjection
                        {
                            EntryId = e.Id,
                            Status = e.Status,
                            Rating = e.Rating,
                            HoursPlayed = e.HoursPlayed,
                            Source = e.Source,
                            LastPlayed = e.StartedAt,
                            IsLoved = e.IsLoved
                        })
                        .FirstOrDefault()
            })
            .OrderBy(g => g.Title)
            .ToListAsync(cancellationToken);

        await EnrichUserTagsAsync(dlcItems, userId, cancellationToken);

        return dlcItems
            .Where(d => d.ParentGameId is not null)
            .GroupBy(d => d.ParentGameId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => MapProjection(d, [])).ToList());
    }

    private static IQueryable<CatalogProjection> ApplySort(
        IQueryable<CatalogProjection> query,
        CatalogQuery catalogQuery,
        Guid? userId)
    {
        return catalogQuery.Sort switch
        {
            CatalogSortField.ReleaseDate => catalogQuery.Descending
                ? query.OrderByDescending(g => g.ReleaseDate).ThenBy(g => g.Title)
                : query.OrderBy(g => g.ReleaseDate).ThenBy(g => g.Title),
            CatalogSortField.LastPlayed when userId is not null => query
                .OrderBy(g => g.LibraryEntry!.LastPlayed == null)
                .ThenByDescending(g => g.LibraryEntry!.LastPlayed)
                .ThenBy(g => g.Title),
            CatalogSortField.UpdatedAt => query
                .OrderByDescending(g => g.UpdatedAt)
                .ThenBy(g => g.Title),
            CatalogSortField.Rating when userId is not null => query
                .OrderByDescending(g => g.LibraryEntry!.Rating ?? 0)
                .ThenBy(g => g.Title),
            CatalogSortField.Hours when userId is not null => query
                .OrderByDescending(g => g.LibraryEntry!.HoursPlayed ?? 0m)
                .ThenBy(g => g.Title),
            CatalogSortField.TotalPlayers => query
                .OrderByDescending(g => g.TotalPlayers)
                .ThenBy(g => g.Title),
            _ => catalogQuery.Descending
                ? query.OrderByDescending(g => g.Title)
                : query.OrderBy(g => g.Title)
        };
    }

    private static List<GroupBucket> BuildGroups(
        IReadOnlyList<CatalogProjection> items,
        CatalogGroupBy groupBy,
        bool inLibrary)
    {
        IEnumerable<(string Key, string Label, CatalogProjection Item)> buckets = groupBy switch
        {
            CatalogGroupBy.Genre => items.SelectMany(i =>
                i.Categories.Count == 0
                    ? [(Key: "unknown", Label: "Unknown", Item: i)]
                    : i.Categories.Zip(i.CategorySlugs, (name, slug) => (Key: slug, Label: name, Item: i))),
            CatalogGroupBy.Platform when inLibrary => items.Select(i => (
                Key: i.LibraryEntry?.Source.ToString() ?? "none",
                Label: i.LibraryEntry is null
                    ? "Not in library"
                    : LibrarySourceLabels.Format(i.LibraryEntry.Source),
                Item: i)),
            CatalogGroupBy.Platform => items.SelectMany(i =>
                i.Platforms.Count == 0
                    ? [(Key: "unknown", Label: "Unknown", Item: i)]
                    : i.Platforms.Zip(i.PlatformSlugs, (name, slug) => (Key: slug, Label: name, Item: i))),
            CatalogGroupBy.Status => items.Select(i => (
                Key: i.LibraryEntry?.Status.ToString() ?? "none",
                Label: i.LibraryEntry?.Status.ToString() ?? "Not in library",
                Item: i)),
            CatalogGroupBy.Tag => items.SelectMany(i =>
                i.UserTags.Count == 0
                    ? [(Key: "untagged", Label: "Untagged", Item: i)]
                    : i.UserTags.Zip(i.UserTagSlugs, (name, slug) => (Key: slug, Label: name, Item: i))),
            CatalogGroupBy.ReleaseDate => items.Select(i =>
            {
                var (key, label) = GetReleaseDateBucket(i.ReleaseDate);
                return (Key: key, Label: label, Item: i);
            }),
            _ => items.Select(i => (Key: "all", Label: "All", Item: i))
        };

        return buckets
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => GetReleaseDateSortOrder(g.Key))
            .ThenBy(g => g.Key)
            .Select(g =>
            {
                var label = g.First().Label;
                return new GroupBucket(g.Key, label, g.Select(x => x.Item).DistinctBy(i => i.Id).ToList());
            })
            .ToList();
    }

    private static (string Key, string Label) GetReleaseDateBucket(DateOnly? releaseDate)
    {
        if (releaseDate is null)
        {
            return ("unknown", "Unknown");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysAgo = today.DayNumber - releaseDate.Value.DayNumber;

        if (daysAgo <= 30)
        {
            return ("last-month", "Last month");
        }

        if (daysAgo <= 365)
        {
            return ("last-year", "Last year");
        }

        var decade = releaseDate.Value.Year / 10 * 10;
        return ($"decade-{decade}", $"{decade}s");
    }

    private static int GetReleaseDateSortOrder(string key) => key switch
    {
        "last-month" => 0,
        "last-year" => 1,
        var k when k.StartsWith("decade-", StringComparison.Ordinal) =>
            -int.Parse(k["decade-".Length..], System.Globalization.CultureInfo.InvariantCulture),
        "unknown" => 999,
        _ => 500
    };

    private static CatalogGameDto MapProjection(CatalogProjection p, IReadOnlyList<CatalogGameDto> dlc) =>
        new(
            p.Id,
            p.Title,
            p.Slug,
            p.CoverUrl,
            p.ReleaseDate,
            p.Categories,
            p.Platforms,
            p.TotalPlayers,
            p.AvgRating is null ? null : (decimal)p.AvgRating,
            p.LibraryEntry?.EntryId,
            p.LibraryEntry?.Status,
            p.LibraryEntry?.Rating,
            p.LibraryEntry?.HoursPlayed,
            p.LibraryEntry?.Source,
            p.LibraryEntry?.LastPlayed,
            p.UserTags,
            dlc,
            p.LibraryEntry?.IsLoved ?? false);

    private static string FormatLibrarySource(LibraryEntrySource source) =>
        LibrarySourceLabels.Format(source);

    private sealed class CatalogProjection
    {
        public Guid Id { get; set; }
        public Guid? ParentGameId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public DateOnly? ReleaseDate { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public List<string> Categories { get; set; } = [];
        public List<string> CategorySlugs { get; set; } = [];
        public List<string> Platforms { get; set; } = [];
        public List<string> PlatformSlugs { get; set; } = [];
        public List<string> UserTags { get; set; } = [];
        public List<string> UserTagSlugs { get; set; } = [];
        public int TotalPlayers { get; set; }
        public double? AvgRating { get; set; }
        public LibraryProjection? LibraryEntry { get; set; }
    }

    private sealed class LibraryProjection
    {
        public Guid EntryId { get; set; }
        public LibraryStatus Status { get; set; }
        public short? Rating { get; set; }
        public decimal? HoursPlayed { get; set; }
        public LibraryEntrySource Source { get; set; }
        public DateOnly? LastPlayed { get; set; }
        public bool IsLoved { get; set; }
    }

    private sealed record GroupBucket(string Key, string Label, IReadOnlyList<CatalogProjection> Items);
}

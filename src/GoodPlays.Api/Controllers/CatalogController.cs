using GoodPlays.Api.Services;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/catalog")]
public class CatalogController(
    ICatalogService catalogService,
    ICurrentUserAccessor currentUserAccessor,
    ICoverRefreshScheduler coverRefreshScheduler) : ControllerBase
{
    [HttpGet("games")]
    public async Task<IActionResult> BrowseGames([FromQuery] CatalogBrowseRequest request, CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        var query = request.ToQuery();
        var result = await catalogService.BrowseAsync(user?.Id, query, cancellationToken);
        coverRefreshScheduler.Schedule(MissingCoverIds.FromCatalog(result));
        return Ok(result);
    }

    [HttpGet("facets")]
    public async Task<IActionResult> GetFacets(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        var facets = await catalogService.GetFacetsAsync(user?.Id, cancellationToken);
        return Ok(facets);
    }

    public sealed class CatalogBrowseRequest
    {
        public string? Search { get; set; }
        public string? Genre { get; set; }
        public string? Platform { get; set; }
        public string? Tag { get; set; }
        public string? Shelf { get; set; }
        public int? ReleaseDecade { get; set; }
        public bool InLibrary { get; set; }
        public LibraryStatus? Status { get; set; }
        public LibraryEntrySource? Source { get; set; }
        public CatalogSortField Sort { get; set; } = CatalogSortField.Title;
        public bool Desc { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public CatalogGroupBy GroupBy { get; set; } = CatalogGroupBy.None;
        public string? GroupKeys { get; set; }

        public CatalogQuery ToQuery()
        {
            IReadOnlyList<string>? groupKeys = string.IsNullOrWhiteSpace(GroupKeys)
                ? null
                : GroupKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new(
                Search,
                Genre,
                Platform,
                Tag,
                Shelf,
                ReleaseDecade,
                InLibrary,
                Status,
                Source,
                Sort,
                Desc,
                Page,
                PageSize,
                GroupBy,
                groupKeys);
        }
    }
}

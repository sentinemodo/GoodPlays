namespace GoodPlays.Infrastructure.Services;

public interface ICatalogService
{
    Task<PaginatedCatalogResult> BrowseAsync(Guid? userId, CatalogQuery query, CancellationToken cancellationToken);

    Task<CatalogFacetsDto> GetFacetsAsync(Guid? userId, CancellationToken cancellationToken);
}

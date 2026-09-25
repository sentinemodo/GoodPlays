using GoodPlays.Infrastructure.Services;

namespace GoodPlays.Api.Jobs;

public sealed class CoverRefreshJob(ICoverRefreshService coverRefreshService)
{
    public Task RunAsync(Guid[] gameIds, CancellationToken cancellationToken) =>
        coverRefreshService.RefreshMissingAsync(gameIds, cancellationToken);
}

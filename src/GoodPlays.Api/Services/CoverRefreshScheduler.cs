using GoodPlays.Api.Jobs;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace GoodPlays.Api.Services;

public interface ICoverRefreshScheduler
{
    void Schedule(IReadOnlyCollection<Guid> gameIds);
}

public sealed class HangfireCoverRefreshScheduler(IBackgroundJobClient backgroundJobClient) : ICoverRefreshScheduler
{
    public void Schedule(IReadOnlyCollection<Guid> gameIds)
    {
        if (gameIds.Count == 0)
        {
            return;
        }

        var ids = gameIds.Distinct().ToArray();
        backgroundJobClient.Enqueue<CoverRefreshJob>(job => job.RunAsync(ids, CancellationToken.None));
    }
}

public sealed class InlineCoverRefreshScheduler(IServiceScopeFactory scopeFactory) : ICoverRefreshScheduler
{
    public void Schedule(IReadOnlyCollection<Guid> gameIds)
    {
        if (gameIds.Count == 0)
        {
            return;
        }

        var ids = gameIds.Distinct().ToArray();
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<GoodPlays.Infrastructure.Services.ICoverRefreshService>();
            await service.RefreshMissingAsync(ids, CancellationToken.None);
        });
    }
}

using GoodPlays.Api.Jobs;
using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Services;

public interface IImportJobScheduler
{
    void ScheduleParseText(Guid jobId);
}

public sealed class HangfireImportJobScheduler(IBackgroundJobClient backgroundJobClient) : IImportJobScheduler
{
    public void ScheduleParseText(Guid jobId) =>
        backgroundJobClient.Enqueue<ImportParseTextJob>(job => job.RunAsync(jobId, CancellationToken.None));
}

public sealed class InlineImportJobScheduler(IServiceScopeFactory scopeFactory) : IImportJobScheduler
{
    public void ScheduleParseText(Guid jobId)
    {
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var importService = scope.ServiceProvider.GetRequiredService<IImportService>();
            await importService.ProcessTextImportAsync(jobId, CancellationToken.None);
        });
    }
}

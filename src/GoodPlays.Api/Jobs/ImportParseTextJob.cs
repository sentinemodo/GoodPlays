using GoodPlays.Infrastructure.Services;
using Hangfire;

namespace GoodPlays.Api.Jobs;

public sealed class ImportParseTextJob(IImportService importService)
{
    [Queue("default")]
    public Task RunAsync(Guid jobId, CancellationToken cancellationToken) =>
        importService.ProcessTextImportAsync(jobId, cancellationToken);
}

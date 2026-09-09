using Hangfire;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Api.Jobs;

public sealed class StubRecurringJobs(ILogger<StubRecurringJobs> logger)
{
    public static void Register()
    {
        // TODO(architecture): technology.md — MlTrainNightly cron at 03:00 UTC (Phase 3)
        RecurringJob.AddOrUpdate<StubRecurringJobs>(
            "ml-train-nightly-stub",
            job => job.NightlyTrainStub(),
            Cron.Daily(3),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }

    [Queue("default")]
    public Task NightlyTrainStub()
    {
        logger.LogInformation("Stub nightly ML train job executed (Phase 0)");
        return Task.CompletedTask;
    }
}

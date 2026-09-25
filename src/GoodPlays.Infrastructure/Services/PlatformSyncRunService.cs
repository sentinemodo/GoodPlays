using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed record PlatformSyncRunDto(
    Guid Id,
    PlatformSyncRunStatus Status,
    string Phase,
    int ProcessedCount,
    int TotalCount,
    int AddedCount,
    int UpdatedCount,
    int SkippedCount,
    int UnmatchedCount,
    string? Warning,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PlatformSyncRunStart(PlatformSyncRunDto Run, bool Created);

public sealed record CurrentPlatformSync(PlatformSyncRunDto? Run, bool ScheduleRestart);

public interface IPlatformSyncRunService
{
    Task<PlatformSyncRunStart> StartAsync(Guid userId, CancellationToken cancellationToken);

    Task<CurrentPlatformSync> GetCurrentAsync(Guid userId, CancellationToken cancellationToken);

    Task<PlatformSyncRunDto?> StopAsync(Guid userId, CancellationToken cancellationToken);

    Task<PlatformSyncRunDto> ExecuteAsync(Guid runId, CancellationToken cancellationToken);
}

public sealed class PlatformSyncRunService(
    GoodPlaysDbContext dbContext,
    ISteamSyncService steamSyncService,
    IPsnSyncService psnSyncService,
    IXboxSyncService xboxSyncService,
    ISwitchSyncService switchSyncService,
    IAchievementSyncService achievementSyncService,
    IActivityLogService activityLogService,
    IClock clock) : IPlatformSyncRunService
{
    public static readonly TimeSpan StallTimeout = TimeSpan.FromMinutes(2);
    public async Task<PlatformSyncRunStart> StartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connected = await dbContext.PlatformConnections
            .Where(c => c.UserId == userId && c.SyncEnabled)
            .Select(c => c.Platform)
            .ToListAsync(cancellationToken);

        var hasSteam = connected.Contains(PlatformConnectionPlatform.Steam);
        var hasPsn = connected.Contains(PlatformConnectionPlatform.Psn);
        var hasXbox = connected.Contains(PlatformConnectionPlatform.Xbox);
        var hasSwitch = connected.Contains(PlatformConnectionPlatform.Switch);
        if (!hasSteam && !hasPsn && !hasXbox && !hasSwitch)
        {
            throw new InvalidOperationException("Connect Steam, PlayStation, Xbox, or Nintendo Switch before syncing.");
        }

        var active = await dbContext.PlatformSyncRuns
            .Where(r => r.UserId == userId &&
                        (r.Status == PlatformSyncRunStatus.Pending || r.Status == PlatformSyncRunStatus.Processing))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (active is not null)
        {
            return new PlatformSyncRunStart(ToDto(active), false);
        }

        var now = clock.UtcNow;
        var run = new PlatformSyncRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = PlatformSyncRunStatus.Pending,
            Phase = "Queued",
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.PlatformSyncRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLogService.RecordAsync(userId, "Sync", "Sync queued.", cancellationToken);
        return new PlatformSyncRunStart(ToDto(run), true);
    }

    public async Task<CurrentPlatformSync> GetCurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var run = await dbContext.PlatformSyncRuns
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            return new CurrentPlatformSync(null, false);
        }

        var stalled = run.Status is PlatformSyncRunStatus.Pending or PlatformSyncRunStatus.Processing &&
                      clock.UtcNow - run.UpdatedAt >= StallTimeout;
        if (!stalled)
        {
            return new CurrentPlatformSync(ToDto(run), false);
        }

        var position = $"{run.ProcessedCount}/{run.TotalCount}";
        var stalledPhase = run.Phase;
        run.Status = PlatformSyncRunStatus.Failed;
        run.Phase = "Sync stalled";
        run.ErrorMessage = $"Sync stalled at {position}.";
        run.UpdatedAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLogService.RecordAsync(
            userId,
            "Sync",
            $"Sync stalled at {position} during {stalledPhase}.",
            cancellationToken);
        return new CurrentPlatformSync(ToDto(run), false);
    }

    public async Task<PlatformSyncRunDto?> StopAsync(Guid userId, CancellationToken cancellationToken)
    {
        var run = await dbContext.PlatformSyncRuns
            .Where(r => r.UserId == userId &&
                        (r.Status == PlatformSyncRunStatus.Pending || r.Status == PlatformSyncRunStatus.Processing))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            return null;
        }

        run.Status = PlatformSyncRunStatus.Stopped;
        run.Phase = "Sync stopped";
        run.ErrorMessage = "Stopped by user.";
        run.UpdatedAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLogService.RecordAsync(userId, "Sync", $"Sync stopped at {run.ProcessedCount}/{run.TotalCount}.", cancellationToken);
        return ToDto(run);
    }

    public async Task<PlatformSyncRunDto> ExecuteAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await dbContext.PlatformSyncRuns.FirstAsync(r => r.Id == runId, cancellationToken);
        run.Status = PlatformSyncRunStatus.Processing;
        run.Phase = "Starting sync";
        run.UpdatedAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLogService.RecordAsync(run.UserId, "Sync", "Sync started.", cancellationToken);

        try
        {
            var connected = await dbContext.PlatformConnections
                .Where(c => c.UserId == run.UserId && c.SyncEnabled)
                .Select(c => c.Platform)
                .ToListAsync(cancellationToken);

            if (connected.Contains(PlatformConnectionPlatform.Steam))
            {
                await SyncPlatformAsync(
                    run,
                    (progress, token) => steamSyncService.SyncAsync(run.UserId, token, progress),
                    result => (result.AddedCount, result.UpdatedCount, result.SkippedCount, result.UnmatchedCount, result.Warning),
                    cancellationToken);
                run.Phase = "Syncing Steam achievements";
                run.UpdatedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                await achievementSyncService.SyncUserSteamAchievementsAsync(run.UserId, cancellationToken);
            }

            if (connected.Contains(PlatformConnectionPlatform.Psn))
            {
                await SyncPlatformAsync(
                    run,
                    (progress, token) => psnSyncService.SyncAsync(run.UserId, token, progress),
                    result => (result.AddedCount, result.UpdatedCount, result.SkippedCount, result.UnmatchedCount, result.Warning),
                    cancellationToken);
            }

            if (connected.Contains(PlatformConnectionPlatform.Xbox))
            {
                await SyncPlatformAsync(
                    run,
                    (progress, token) => xboxSyncService.SyncAsync(run.UserId, token, progress),
                    result => (result.AddedCount, result.UpdatedCount, result.SkippedCount, result.UnmatchedCount, result.Warning),
                    cancellationToken);
            }

            if (connected.Contains(PlatformConnectionPlatform.Switch))
            {
                await SyncPlatformAsync(
                    run,
                    (progress, token) => switchSyncService.SyncAsync(run.UserId, token, progress),
                    result => (result.AddedCount, result.UpdatedCount, result.SkippedCount, result.UnmatchedCount, result.Warning),
                    cancellationToken);
            }

            if (await IsSupersededAsync(run, cancellationToken))
            {
                return ToDto(run);
            }

            run.Status = PlatformSyncRunStatus.Completed;
            run.Phase = "Sync complete";
            run.ErrorMessage = null;
            run.UpdatedAt = clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await activityLogService.RecordRankingMilestonesAsync(run.UserId, cancellationToken);
            await activityLogService.RecordAsync(
                run.UserId,
                "Sync",
                $"Sync complete. Added {run.AddedCount}, updated {run.UpdatedCount}, skipped {run.SkippedCount}, unmatched {run.UnmatchedCount}.",
                cancellationToken);
        }
        catch (SyncSupersededException)
        {
            return ToDto(run);
        }
        catch (Exception ex)
        {
            await activityLogService.RecordAsync(run.UserId, "Sync", $"Sync failed: {ex.Message}", cancellationToken);
            if (await IsSupersededAsync(run, cancellationToken))
            {
                return ToDto(run);
            }

            run.Status = PlatformSyncRunStatus.Failed;
            run.Phase = "Sync failed";
            run.ErrorMessage = ex.Message;
            run.UpdatedAt = clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToDto(run);
    }

    private async Task SyncPlatformAsync<T>(
        PlatformSyncRun run,
        Func<Func<SyncProgressUpdate, CancellationToken, Task>, CancellationToken, Task<T>> sync,
        Func<T, (int Added, int Updated, int Skipped, int Unmatched, string? Warning)> readResult,
        CancellationToken cancellationToken)
    {
        var baselineProcessed = run.ProcessedCount;
        var baselineTotal = run.TotalCount;
        var baselineAdded = run.AddedCount;
        var baselineUpdated = run.UpdatedCount;
        var baselineSkipped = run.SkippedCount;
        var baselineUnmatched = run.UnmatchedCount;

        var result = await sync(
            async (update, token) =>
            {
                if (await IsSupersededAsync(run, token))
                {
                    throw new SyncSupersededException();
                }

                run.Phase = update.Phase;
                if (update.TotalCount == 0)
                {
                    run.ProcessedCount = 0;
                    run.TotalCount = 0;
                }
                else
                {
                    run.ProcessedCount = baselineProcessed + update.ProcessedCount;
                    run.TotalCount = baselineTotal + update.TotalCount;
                }
                run.AddedCount = baselineAdded + update.AddedCount;
                run.UpdatedCount = baselineUpdated + update.UpdatedCount;
                run.SkippedCount = baselineSkipped + update.SkippedCount;
                run.UnmatchedCount = baselineUnmatched + update.UnmatchedCount;
                run.UpdatedAt = clock.UtcNow;
                await dbContext.SaveChangesAsync(token);

                var shouldLog = update.Phase != lastLoggedPhase ||
                                update.ProcessedCount == update.TotalCount ||
                                (update.ProcessedCount > 0 && update.ProcessedCount % 10 == 0);
                if (shouldLog)
                {
                    lastLoggedPhase = update.Phase;
                    var phaseProgress = update.TotalCount > 0
                        ? $" {update.ProcessedCount}/{update.TotalCount}"
                        : "";
                    await activityLogService.RecordAsync(
                        run.UserId,
                        "Sync",
                        $"Sync progress: {update.Phase}{phaseProgress}.",
                        token);
                }
            },
            cancellationToken);

        var counts = readResult(result);
        run.ProcessedCount = baselineProcessed + counts.Added + counts.Updated + counts.Skipped + counts.Unmatched;
        run.TotalCount = Math.Max(run.TotalCount, run.ProcessedCount);
        run.AddedCount = baselineAdded + counts.Added;
        run.UpdatedCount = baselineUpdated + counts.Updated;
        run.SkippedCount = baselineSkipped + counts.Skipped;
        run.UnmatchedCount = baselineUnmatched + counts.Unmatched;
        if (!string.IsNullOrWhiteSpace(counts.Warning))
        {
            run.Warning = string.IsNullOrWhiteSpace(run.Warning)
                ? counts.Warning
                : $"{run.Warning} {counts.Warning}";
        }

        run.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string? lastLoggedPhase;

    private async Task<bool> IsSupersededAsync(PlatformSyncRun run, CancellationToken cancellationToken)
    {
        var stored = await dbContext.Entry(run).GetDatabaseValuesAsync(cancellationToken);
        if (stored is null)
        {
            return false;
        }

        var status = stored.GetValue<PlatformSyncRunStatus>(nameof(PlatformSyncRun.Status));
        return status is PlatformSyncRunStatus.Failed or PlatformSyncRunStatus.Stopped &&
               run.Status != status;
    }

    private static PlatformSyncRunDto ToDto(PlatformSyncRun run) =>
        new(
            run.Id,
            run.Status,
            run.Phase,
            run.ProcessedCount,
            run.TotalCount,
            run.AddedCount,
            run.UpdatedCount,
            run.SkippedCount,
            run.UnmatchedCount,
            run.Warning,
            run.ErrorMessage,
            run.CreatedAt,
            run.UpdatedAt);
}

public sealed class SyncSupersededException : Exception;

using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Psn;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Tests;

public class PlatformSyncRunServiceTests
{
    [Fact]
    public async Task StartAsync_WithoutConnections_Throws()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context);
        var service = CreateService(context, new RecordingSteamSync(), new RecordingPsnSync(), new RecordingAchievements());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartAsync(userId, CancellationToken.None));

        Assert.Equal("Connect Steam, PlayStation, Xbox, or Nintendo Switch before syncing.", ex.Message);
    }

    [Fact]
    public async Task StartAsync_CreatesPendingRunForConnectedPlatforms()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true, psn: true);
        var service = CreateService(context, new RecordingSteamSync(), new RecordingPsnSync(), new RecordingAchievements());

        var start = await service.StartAsync(userId, CancellationToken.None);

        Assert.True(start.Created);
        Assert.Equal(PlatformSyncRunStatus.Pending, start.Run.Status);
        Assert.Equal("Queued", start.Run.Phase);
        Assert.Equal(0, start.Run.ProcessedCount);
    }

    [Fact]
    public async Task StartAsync_ReturnsExistingRunWhenOneIsActive()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true);
        var service = CreateService(context, new RecordingSteamSync(), new RecordingPsnSync(), new RecordingAchievements());

        var first = await service.StartAsync(userId, CancellationToken.None);
        var second = await service.StartAsync(userId, CancellationToken.None);

        Assert.False(second.Created);
        Assert.Equal(first.Run.Id, second.Run.Id);
        Assert.Equal(1, await context.PlatformSyncRuns.CountAsync());
    }

    [Fact]
    public async Task ExecuteAsync_SyncsConnectedPlatformsAndRecordsProgress()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true, psn: true);
        var steam = new RecordingSteamSync();
        var psn = new RecordingPsnSync();
        var achievements = new RecordingAchievements();
        var service = CreateService(context, steam, psn, achievements);

        var start = await service.StartAsync(userId, CancellationToken.None);
        var result = await service.ExecuteAsync(start.Run.Id, CancellationToken.None);

        Assert.Equal(PlatformSyncRunStatus.Completed, result.Status);
        Assert.Equal("Sync complete", result.Phase);
        Assert.Equal(2, result.ProcessedCount);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.AddedCount);
        Assert.Equal(1, steam.Calls);
        Assert.Equal(1, psn.Calls);
        Assert.Equal(1, achievements.Calls);
        Assert.Contains("Fetching from Steam", steam.Phases);
        Assert.Contains("Fetching from PlayStation", psn.Phases);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsPlatformsThatAreNotConnected()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true);
        var steam = new RecordingSteamSync();
        var psn = new RecordingPsnSync();
        var service = CreateService(context, steam, psn, new RecordingAchievements());

        var start = await service.StartAsync(userId, CancellationToken.None);
        var result = await service.ExecuteAsync(start.Run.Id, CancellationToken.None);

        Assert.Equal(PlatformSyncRunStatus.Completed, result.Status);
        Assert.Equal(1, steam.Calls);
        Assert.Equal(0, psn.Calls);
        Assert.Equal(1, result.AddedCount);
    }

    [Fact]
    public async Task ExecuteAsync_MarksRunFailedWhenSyncThrows()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true);
        var steam = new RecordingSteamSync { ThrowOnSync = true };
        var service = CreateService(context, steam, new RecordingPsnSync(), new RecordingAchievements());

        var start = await service.StartAsync(userId, CancellationToken.None);
        var result = await service.ExecuteAsync(start.Run.Id, CancellationToken.None);

        Assert.Equal(PlatformSyncRunStatus.Failed, result.Status);
        Assert.Equal("Steam sync failed.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetCurrentAsync_WhenProgressIsStale_FailsTheRunWithoutStartingAnother()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-25T10:00:00Z"));
        var service = CreateService(context, new RecordingSteamSync(), new RecordingPsnSync(), new RecordingAchievements(), clock);

        var start = await service.StartAsync(userId, CancellationToken.None);
        var run = await context.PlatformSyncRuns.SingleAsync();
        run.Status = PlatformSyncRunStatus.Processing;
        run.Phase = "Updating PlayStation library";
        run.ProcessedCount = 52;
        run.TotalCount = 347;
        run.UpdatedAt = clock.UtcNow;
        await context.SaveChangesAsync();

        clock.UtcNow = clock.UtcNow.AddMinutes(3);
        var current = await service.GetCurrentAsync(userId, CancellationToken.None);

        Assert.False(current.ScheduleRestart);
        Assert.Equal(start.Run.Id, current.Run!.Id);
        Assert.Equal(PlatformSyncRunStatus.Failed, current.Run.Status);
        Assert.Contains("52/347", current.Run.ErrorMessage);
        Assert.DoesNotContain("Restarting", current.Run.ErrorMessage);
        Assert.Equal(1, await context.PlatformSyncRuns.CountAsync());
        Assert.Contains(
            context.ActivityLogs,
            entry => entry.Category == "Sync" && entry.Message.Contains("52/347") && !entry.Message.Contains("Restarting"));
    }

    [Fact]
    public async Task GetCurrentAsync_WhenProgressIsRecent_DoesNotRestart()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, psn: true);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-25T10:00:00Z"));
        var service = CreateService(context, new RecordingSteamSync(), new RecordingPsnSync(), new RecordingAchievements(), clock);
        var start = await service.StartAsync(userId, CancellationToken.None);
        var run = await context.PlatformSyncRuns.SingleAsync();
        run.Status = PlatformSyncRunStatus.Processing;
        run.ProcessedCount = 52;
        run.TotalCount = 347;
        run.UpdatedAt = clock.UtcNow;
        await context.SaveChangesAsync();

        clock.UtcNow = clock.UtcNow.AddSeconds(30);
        var current = await service.GetCurrentAsync(userId, CancellationToken.None);

        Assert.False(current.ScheduleRestart);
        Assert.Equal(start.Run.Id, current.Run!.Id);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsSyncAttemptAndProgress()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true);
        var service = CreateService(context, new RecordingSteamSync(), new RecordingPsnSync(), new RecordingAchievements());

        var start = await service.StartAsync(userId, CancellationToken.None);
        await service.ExecuteAsync(start.Run.Id, CancellationToken.None);

        var messages = await context.ActivityLogs.Select(entry => entry.Message).ToListAsync();
        Assert.Contains(messages, message => message.Contains("Sync started"));
        Assert.Contains(messages, message => message.Contains("Fetching from Steam"));
        Assert.Contains(messages, message => message.Contains("Sync complete"));
    }

    [Fact]
    public async Task ExecuteAsync_PlayStationFetchDoesNotReusePreviousPlatformCounts()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true, psn: true);
        var service = CreateService(
            context,
            new RecordingSteamSync { LibrarySize = 40 },
            new RecordingPsnSync { FetchOnly = true },
            new RecordingAchievements());

        var start = await service.StartAsync(userId, CancellationToken.None);
        await service.ExecuteAsync(start.Run.Id, CancellationToken.None);

        var messages = await context.ActivityLogs.Select(entry => entry.Message).ToListAsync();
        Assert.Contains("Sync progress: Fetching from PlayStation.", messages);
        Assert.DoesNotContain(messages, message => message.Contains("Fetching from PlayStation") && message.Contains("40/40"));
    }

    [Fact]
    public async Task ExecuteAsync_LogsPlayStationFailureEvenAfterTheRunWasMarkedStalled()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, psn: true);
        var service = CreateService(
            context,
            new RecordingSteamSync(),
            new RecordingPsnSync { FailAfterMarkingStalled = true, Context = context },
            new RecordingAchievements());

        var start = await service.StartAsync(userId, CancellationToken.None);
        await service.ExecuteAsync(start.Run.Id, CancellationToken.None);

        var messages = await context.ActivityLogs.Select(entry => entry.Message).ToListAsync();
        Assert.Contains(messages, message => message.Contains("PSN API returned a server error."));
    }

    [Fact]
    public async Task StopAsync_MarksTheActiveRunStoppedAndDoesNotRestartIt()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true);
        var service = CreateService(context, new RecordingSteamSync(), new RecordingPsnSync(), new RecordingAchievements());

        var start = await service.StartAsync(userId, CancellationToken.None);
        var stopped = await service.StopAsync(userId, CancellationToken.None);
        var current = await service.GetCurrentAsync(userId, CancellationToken.None);

        Assert.Equal(PlatformSyncRunStatus.Stopped, stopped!.Status);
        Assert.Equal("Sync stopped", stopped.Phase);
        Assert.False(current.ScheduleRestart);
        Assert.Equal(start.Run.Id, current.Run!.Id);
        Assert.Contains(
            context.ActivityLogs,
            entry => entry.Category == "Sync" && entry.Message.Contains("Sync stopped"));
    }

    [Fact]
    public async Task GetCurrentAsync_DoesNotStartASyncWhenTheLastCompletionIsAWeekOld()
    {
        await using var context = CreateContext();
        var userId = await SeedUserAsync(context, steam: true);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-25T10:00:00Z"));
        var service = CreateService(context, new RecordingSteamSync(), new RecordingPsnSync(), new RecordingAchievements(), clock);
        var start = await service.StartAsync(userId, CancellationToken.None);
        var run = await context.PlatformSyncRuns.SingleAsync();
        run.Status = PlatformSyncRunStatus.Completed;
        run.UpdatedAt = clock.UtcNow;
        await context.SaveChangesAsync();

        clock.UtcNow = clock.UtcNow.AddDays(6);
        var recent = await service.GetCurrentAsync(userId, CancellationToken.None);
        Assert.False(recent.ScheduleRestart);
        Assert.Equal(start.Run.Id, recent.Run!.Id);

        clock.UtcNow = clock.UtcNow.AddDays(1);
        var due = await service.GetCurrentAsync(userId, CancellationToken.None);
        Assert.False(due.ScheduleRestart);
        Assert.Equal(start.Run.Id, due.Run!.Id);
        Assert.Equal(PlatformSyncRunStatus.Completed, due.Run.Status);
    }

    private static PlatformSyncRunService CreateService(
        GoodPlaysDbContext context,
        ISteamSyncService steam,
        IPsnSyncService psn,
        IAchievementSyncService achievements,
        FakeClock? clock = null) =>
        new(
            context,
            steam,
            psn,
            new RecordingXboxSync(),
            new RecordingSwitchSync(),
            achievements,
            new ActivityLogService(context),
            clock ?? new FakeClock(DateTimeOffset.UtcNow));

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private static GoodPlaysDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GoodPlaysDbContext(options);
    }

    private static async Task<Guid> SeedUserAsync(GoodPlaysDbContext context, bool steam = false, bool psn = false)
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = $"clerk_{userId:N}",
            Email = "sync-all@test.local",
            CreatedAt = now
        });
        if (steam)
        {
            context.PlatformConnections.Add(Connection(userId, PlatformConnectionPlatform.Steam, now));
        }

        if (psn)
        {
            context.PlatformConnections.Add(Connection(userId, PlatformConnectionPlatform.Psn, now));
        }

        await context.SaveChangesAsync();
        return userId;
    }

    private static PlatformConnection Connection(Guid userId, PlatformConnectionPlatform platform, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = platform,
            ExternalAccountId = platform.ToString(),
            SyncEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

    private sealed class RecordingSteamSync : ISteamSyncService
    {
        public int Calls { get; private set; }
        public bool ThrowOnSync { get; init; }
        public int LibrarySize { get; init; } = 1;
        public List<string> Phases { get; } = [];

        public async Task<SteamSyncResultDto> SyncAsync(
            Guid userId,
            CancellationToken cancellationToken,
            Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
            string? onlyExternalId = null)
        {
            Calls++;
            if (ThrowOnSync)
            {
                throw new InvalidOperationException("Steam sync failed.");
            }

            if (reportProgress is not null)
            {
                var fetching = new SyncProgressUpdate("Fetching from Steam", 0, LibrarySize, 0, 0, 0, 0);
                Phases.Add(fetching.Phase);
                await reportProgress(fetching, cancellationToken);
                var done = new SyncProgressUpdate("Updating Steam library", LibrarySize, LibrarySize, LibrarySize, 0, 0, 0);
                Phases.Add(done.Phase);
                await reportProgress(done, cancellationToken);
            }

            return new SteamSyncResultDto(LibrarySize, 0, 0, 0, DateTimeOffset.UtcNow, null);
        }
    }

    private sealed class RecordingPsnSync : IPsnSyncService
    {
        public int Calls { get; private set; }
        public bool FetchOnly { get; init; }
        public bool FailAfterMarkingStalled { get; init; }
        public GoodPlaysDbContext? Context { get; init; }
        public List<string> Phases { get; } = [];

        public async Task<PsnSyncResultDto> SyncAsync(
            Guid userId,
            CancellationToken cancellationToken,
            Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
            string? onlyExternalId = null)
        {
            Calls++;
            if (FailAfterMarkingStalled)
            {
                var run = await Context!.PlatformSyncRuns.SingleAsync(cancellationToken);
                run.Status = PlatformSyncRunStatus.Failed;
                run.ErrorMessage = "Sync stalled at 40/40. Restarting.";
                await Context.SaveChangesAsync(cancellationToken);
                throw new PsnApiException(PsnApiErrorCode.ServerError, "PSN API returned a server error.");
            }

            if (reportProgress is not null)
            {
                var fetching = new SyncProgressUpdate(
                    "Fetching from PlayStation",
                    0,
                    FetchOnly ? 0 : 1,
                    0,
                    0,
                    0,
                    0);
                Phases.Add(fetching.Phase);
                await reportProgress(fetching, cancellationToken);
                if (!FetchOnly)
                {
                    var done = new SyncProgressUpdate("Updating PlayStation library", 1, 1, 1, 0, 0, 0);
                    Phases.Add(done.Phase);
                    await reportProgress(done, cancellationToken);
                }
            }

            return new PsnSyncResultDto(FetchOnly ? 0 : 1, 0, 0, 0, DateTimeOffset.UtcNow, null);
        }
    }

    private sealed class RecordingXboxSync : IXboxSyncService
    {
        public Task<XboxSyncResultDto> SyncAsync(
            Guid userId,
            CancellationToken cancellationToken,
            Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
            string? onlyExternalId = null) =>
            Task.FromResult(new XboxSyncResultDto(0, 0, 0, 0, DateTimeOffset.UtcNow, null));
    }

    private sealed class RecordingSwitchSync : ISwitchSyncService
    {
        public Task<SwitchSyncResultDto> SyncAsync(
            Guid userId,
            CancellationToken cancellationToken,
            Func<SyncProgressUpdate, CancellationToken, Task>? reportProgress = null,
            string? onlyExternalId = null) =>
            Task.FromResult(new SwitchSyncResultDto(0, 0, 0, 0, DateTimeOffset.UtcNow, null));
    }

    private sealed class RecordingAchievements : IAchievementSyncService
    {
        public int Calls { get; private set; }

        public Task SyncUserSteamAchievementsAsync(Guid userId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}

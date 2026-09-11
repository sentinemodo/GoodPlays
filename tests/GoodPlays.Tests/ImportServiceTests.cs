using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoodPlays.Tests;

public class ImportServiceTests
{
    [Fact]
    public void ParseLines_SplitsAndDedupes()
    {
        var lines = ImportService.ParseLines("Hades\n\rCeleste\nHades\n\n  ");
        Assert.Equal(2, lines.Count);
        Assert.Contains("Hades", lines);
        Assert.Contains("Celeste", lines);
    }

    [Fact]
    public async Task ProcessTextImportAsync_AddsExactCatalogMatch()
    {
        var (context, userId, gameId) = await SeedUserAndGameAsync("Hades");
        await using (context)
        {
            var catalog = new FakeGameCatalogService([
                new GameSummaryDto(gameId, "Hades", "hades", null, null, "local")
            ]);
            var service = new ImportService(
                context,
                catalog,
                new LibraryService(context),
                NullLogger<ImportService>.Instance);

            var job = await service.CreateTextImportAsync(userId, "Hades", CancellationToken.None);
            await service.ProcessTextImportAsync(job.Id, CancellationToken.None);

            var updated = await service.GetJobAsync(userId, job.Id, CancellationToken.None);
            Assert.NotNull(updated);
            Assert.Equal(ImportJobStatus.Completed, updated.Status);
            Assert.Equal(1, updated.Stats.AddedCount);

            var entry = await context.LibraryEntries.SingleAsync();
            Assert.Equal(LibraryEntrySource.ImportText, entry.Source);
        }
    }

    [Fact]
    public async Task ProcessTextImportAsync_MarksAmbiguousWhenMultipleMatches()
    {
        var (context, userId, _) = await SeedUserAndGameAsync("Hades");
        await using (context)
        {
            var catalog = new FakeGameCatalogService([
                new GameSummaryDto(Guid.NewGuid(), "Final Fantasy VII", "ff7", null, 1, "local"),
                new GameSummaryDto(Guid.NewGuid(), "Final Fantasy VII Remake", "ff7r", null, 2, "local")
            ]);
            var service = new ImportService(
                context,
                catalog,
                new LibraryService(context),
                NullLogger<ImportService>.Instance);

            var job = await service.CreateTextImportAsync(userId, "Final Fantasy", CancellationToken.None);
            await service.ProcessTextImportAsync(job.Id, CancellationToken.None);

            var updated = await service.GetJobAsync(userId, job.Id, CancellationToken.None);
            Assert.NotNull(updated);
            Assert.Equal(ImportJobStatus.AwaitingReview, updated.Status);
            Assert.Equal(1, updated.Stats.AmbiguousCount);
        }
    }

    private static async Task<(GoodPlaysDbContext Context, Guid UserId, Guid GameId)> SeedUserAndGameAsync(string title)
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = "user_test",
            Email = "test@example.com",
            CreatedAt = now
        });

        context.Games.Add(new Game
        {
            Id = gameId,
            Slug = title.ToLowerInvariant(),
            Title = title,
            SortTitle = title.ToLowerInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return (context, userId, gameId);
    }

    private sealed class FakeGameCatalogService(IReadOnlyList<GameSummaryDto> results) : IGameCatalogService
    {
        public Task<IReadOnlyList<GameSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult(results);

        public Task<Game?> ImportFromIgdbAsync(long igdbId, CancellationToken cancellationToken) =>
            Task.FromResult<Game?>(null);

        public Task<Game?> FindBySteamAppIdAsync(uint appId, CancellationToken cancellationToken) =>
            Task.FromResult<Game?>(null);

        public Task<Game> ImportFromSteamAppAsync(uint appId, string title, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Game> ResolveForSteamSyncAsync(uint appId, string steamTitle, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Game?> EnrichFromIgdbAsync(Game game, uint steamAppId, CancellationToken cancellationToken) =>
            Task.FromResult<Game?>(game);

        public Task<Game> ResolveForPsnSyncAsync(string titleId, string psnTitle, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Game?> EnrichFromIgdbForPsnAsync(
            Game game,
            string titleId,
            string psnTitle,
            CancellationToken cancellationToken) =>
            Task.FromResult<Game?>(game);
    }
}

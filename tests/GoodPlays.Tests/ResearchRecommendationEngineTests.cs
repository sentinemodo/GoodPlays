using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
using GoodPlays.Ml;
using GoodPlays.Ml.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoodPlays.Tests;

public class ResearchRecommendationEngineTests
{
    [Fact]
    public async Task GetRecommendationsAsync_UsesCatalogFallbackWhenLlmDisabled()
    {
        var (context, userId, ownedGameId, candidateGameId) = await SeedLibraryAsync();
        await using (context)
        {
            var engine = new ResearchRecommendationEngine(
                context,
                new FakeGameCatalogService([
                    new GameSummaryDto(candidateGameId, "Celeste", "celeste", null, null, "local")
                ]),
                new FakeLlmClient(isConfigured: false),
                NullLogger<ResearchRecommendationEngine>.Instance);

            var results = await engine.GetRecommendationsAsync(userId, CancellationToken.None);

            Assert.Single(results);
            Assert.Equal(candidateGameId, results[0].GameId);
            Assert.Equal("Celeste", results[0].Title);
            Assert.DoesNotContain(results, r => r.GameId == ownedGameId);
        }
    }

    private static async Task<(GoodPlaysDbContext Context, Guid UserId, Guid OwnedGameId, Guid CandidateGameId)> SeedLibraryAsync()
    {
        var options = new DbContextOptionsBuilder<GoodPlaysDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new GoodPlaysDbContext(options);
        var userId = Guid.NewGuid();
        var ownedGameId = Guid.NewGuid();
        var candidateGameId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Users.Add(new User
        {
            Id = userId,
            ClerkId = "user_test",
            Email = "test@example.com",
            CreatedAt = now
        });

        context.Games.AddRange(
            new Game
            {
                Id = ownedGameId,
                Slug = "hades",
                Title = "Hades",
                SortTitle = "hades",
                MetadataStatus = MetadataStatus.Complete,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Game
            {
                Id = candidateGameId,
                Slug = "celeste",
                Title = "Celeste",
                SortTitle = "celeste",
                MetadataStatus = MetadataStatus.Complete,
                CreatedAt = now,
                UpdatedAt = now
            });

        context.LibraryEntries.Add(new LibraryEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = ownedGameId,
            Status = LibraryStatus.Completed,
            Source = LibraryEntrySource.Manual,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return (context, userId, ownedGameId, candidateGameId);
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

    private sealed class FakeLlmClient(bool isConfigured) : ILlmClient
    {
        public bool IsConfigured => isConfigured;

        public Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}

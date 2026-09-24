using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.OpenCritic;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Steam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoodPlays.Infrastructure.Services;

public sealed class GameEnrichmentService(
    GoodPlaysDbContext dbContext,
    IOpenCriticClient openCriticClient,
    ISteamClient steamClient,
    IOptions<SteamOptions> steamOptions,
    ILogger<GameEnrichmentService> logger) : IGameEnrichmentService
{
    private static readonly TimeSpan RatingsTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan NewsTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan AchievementsTtl = TimeSpan.FromDays(30);

    public async Task EnrichIfStaleAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var runs = await dbContext.GameEnrichmentRuns
            .Where(r => r.GameId == gameId)
            .ToListAsync(cancellationToken);

        if (IsStale(runs, EnrichmentKind.Ratings, now))
        {
            await EnrichRatingsAsync(gameId, cancellationToken);
        }

        if (IsStale(runs, EnrichmentKind.News, now))
        {
            await EnrichNewsAsync(gameId, cancellationToken);
        }

        if (IsStale(runs, EnrichmentKind.Achievements, now))
        {
            await EnrichAchievementsAsync(gameId, cancellationToken);
        }
    }

    public async Task EnrichRatingsAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await dbContext.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
        if (game is null)
        {
            return;
        }

        if (openCriticClient.IsConfigured)
        {
            var oc = await openCriticClient.SearchByNameAsync(game.Title, cancellationToken);
            if (oc is not null)
            {
                await UpsertRatingAsync(gameId, RatingSource.OpenCritic, oc.TopCriticScore, oc.ReviewCount, oc.Url, cancellationToken);
            }
        }

        var steamAppId = await dbContext.GameExternalIds
            .AsNoTracking()
            .Where(x => x.GameId == gameId && x.Source == ExternalIdSource.Steam)
            .Select(x => x.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);

        if (uint.TryParse(steamAppId, out var appId))
        {
            var schema = await steamClient.GetGameSchemaAsync(appId, steamOptions.Value.WebApiKey, cancellationToken);
            if (schema?.Achievements.Count > 0)
            {
                await UpsertRatingAsync(
                    gameId,
                    RatingSource.Steam,
                    null,
                    schema.Achievements.Count,
                    $"https://store.steampowered.com/app/{appId}",
                    cancellationToken);
            }
        }

        await MarkRunAsync(gameId, EnrichmentKind.Ratings, RatingsTtl, cancellationToken);
    }

    public async Task EnrichNewsAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var steamAppId = await dbContext.GameExternalIds
            .AsNoTracking()
            .Where(x => x.GameId == gameId && x.Source == ExternalIdSource.Steam)
            .Select(x => x.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);

        if (uint.TryParse(steamAppId, out var appId))
        {
            var news = await steamClient.GetNewsForAppAsync(appId, cancellationToken);
            foreach (var item in news)
            {
                var exists = await dbContext.GameNewsItems.AnyAsync(
                    n => n.GameId == gameId && n.Url == item.Url,
                    cancellationToken);
                if (exists)
                {
                    continue;
                }

                dbContext.GameNewsItems.Add(new GameNewsItem
                {
                    Id = Guid.NewGuid(),
                    GameId = gameId,
                    Source = "Steam",
                    Title = item.Title,
                    Url = item.Url,
                    PublishedAt = item.PublishedAt,
                    FetchedAt = DateTimeOffset.UtcNow
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await MarkRunAsync(gameId, EnrichmentKind.News, NewsTtl, cancellationToken);
    }

    public async Task EnrichAchievementsAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var steamAppId = await dbContext.GameExternalIds
            .AsNoTracking()
            .Where(x => x.GameId == gameId && x.Source == ExternalIdSource.Steam)
            .Select(x => x.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!uint.TryParse(steamAppId, out var appId))
        {
            await MarkRunAsync(gameId, EnrichmentKind.Achievements, AchievementsTtl, cancellationToken);
            return;
        }

        var schema = await steamClient.GetGameSchemaAsync(appId, steamOptions.Value.WebApiKey, cancellationToken);
        var defs = schema?.Achievements ?? [];
        var now = DateTimeOffset.UtcNow;

        foreach (var def in defs)
        {
            var externalId = def.Name ?? def.DisplayName ?? Guid.NewGuid().ToString("N");
            var existing = await dbContext.Achievements
                .FirstOrDefaultAsync(a => a.GameId == gameId && a.ExternalId == externalId, cancellationToken);

            if (existing is null)
            {
                dbContext.Achievements.Add(new Achievement
                {
                    Id = Guid.NewGuid(),
                    GameId = gameId,
                    ExternalId = externalId,
                    Name = def.DisplayName ?? externalId,
                    Description = def.Description,
                    IconUrl = def.Icon,
                    FetchedAt = now
                });
            }
            else
            {
                existing.Name = def.DisplayName ?? existing.Name;
                existing.Description = def.Description;
                existing.IconUrl = def.Icon;
                existing.FetchedAt = now;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await MarkRunAsync(gameId, EnrichmentKind.Achievements, AchievementsTtl, cancellationToken);
    }

    public async Task RunWeeklyStaleEnrichmentAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var staleGameIds = await dbContext.GameEnrichmentRuns
            .Where(r => r.NextRunAt == null || r.NextRunAt <= now)
            .Select(r => r.GameId)
            .Distinct()
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var gameId in staleGameIds)
        {
            try
            {
                await EnrichIfStaleAsync(gameId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Weekly enrichment failed for game {GameId}", gameId);
            }
        }
    }

    private async Task UpsertRatingAsync(
        Guid gameId,
        RatingSource source,
        decimal? score,
        int? reviewCount,
        string? url,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.GameRatingCaches
            .FirstOrDefaultAsync(r => r.GameId == gameId && r.Source == source, cancellationToken);

        if (existing is null)
        {
            dbContext.GameRatingCaches.Add(new GameRatingCache
            {
                GameId = gameId,
                Source = source,
                Score = score,
                ReviewCount = reviewCount,
                Url = url,
                FetchedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Score = score ?? existing.Score;
            existing.ReviewCount = reviewCount ?? existing.ReviewCount;
            existing.Url = url ?? existing.Url;
            existing.FetchedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkRunAsync(
        Guid gameId,
        EnrichmentKind kind,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var run = await dbContext.GameEnrichmentRuns
            .FirstOrDefaultAsync(r => r.GameId == gameId && r.Kind == kind, cancellationToken);

        if (run is null)
        {
            dbContext.GameEnrichmentRuns.Add(new GameEnrichmentRun
            {
                GameId = gameId,
                Kind = kind,
                LastRunAt = now,
                NextRunAt = now.Add(ttl),
                Status = "ok"
            });
        }
        else
        {
            run.LastRunAt = now;
            run.NextRunAt = now.Add(ttl);
            run.Status = "ok";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsStale(IReadOnlyList<GameEnrichmentRun> runs, EnrichmentKind kind, DateTimeOffset now)
    {
        var run = runs.FirstOrDefault(r => r.Kind == kind);
        return run is null || run.NextRunAt is null || run.NextRunAt <= now;
    }
}

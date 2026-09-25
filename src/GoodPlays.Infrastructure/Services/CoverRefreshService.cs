using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Steam;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public interface ICoverRefreshService
{
    Task<int> RefreshMissingAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken);
}

public sealed class CoverRefreshService(GoodPlaysDbContext dbContext, IIgdbClient igdbClient) : ICoverRefreshService
{
    private static readonly TimeSpan FoundTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan MissingTtl = TimeSpan.FromDays(7);

    public async Task<int> RefreshMissingAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken)
    {
        if (gameIds.Count == 0 || !igdbClient.IsConfigured)
        {
            return 0;
        }

        var ids = gameIds.Distinct().ToList();
        var games = await dbContext.Games
            .Include(g => g.ExternalIds)
            .Where(g => ids.Contains(g.Id))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var runs = await dbContext.GameEnrichmentRuns
            .Where(r => ids.Contains(r.GameId) && r.Kind == EnrichmentKind.Cover)
            .ToListAsync(cancellationToken);
        var runByGame = runs.ToDictionary(r => r.GameId);

        var updated = 0;
        foreach (var game in games)
        {
            if (!string.IsNullOrWhiteSpace(game.CoverUrl))
            {
                continue;
            }

            if (runByGame.TryGetValue(game.Id, out var existing) && existing.NextRunAt > now)
            {
                continue;
            }

            var igdbId = await ResolveIgdbIdAsync(game, cancellationToken);
            var details = igdbId is > 0
                ? await igdbClient.GetGameDetailsAsync(igdbId.Value, cancellationToken)
                : null;
            var found = details is not null && !string.IsNullOrWhiteSpace(details.CoverUrl);

            if (found)
            {
                await IgdbMetadataApplier.ApplyDetailsAsync(dbContext, game, details!, cancellationToken);
                EnsureIgdbExternalId(game, igdbId!.Value);
                updated++;
            }

            MarkRun(game.Id, runByGame, found ? FoundTtl : MissingTtl, found ? "ok" : "missing", now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return updated;
    }

    private async Task<long?> ResolveIgdbIdAsync(Game game, CancellationToken cancellationToken)
    {
        var stored = game.ExternalIds.FirstOrDefault(x => x.Source == ExternalIdSource.Igdb);
        if (stored is not null && long.TryParse(stored.ExternalId, out var igdbId) && igdbId > 0)
        {
            return igdbId;
        }

        var steam = game.ExternalIds.FirstOrDefault(x => x.Source == ExternalIdSource.Steam);
        if (steam is not null && uint.TryParse(steam.ExternalId, out var appId))
        {
            var mapped = await igdbClient.FindIgdbIdBySteamAppIdAsync(appId, cancellationToken);
            if (mapped is > 0)
            {
                return mapped;
            }
        }

        var normalized = SteamTitleNormalizer.Normalize(game.Title);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var results = await igdbClient.SearchGamesAsync(normalized, cancellationToken);
        var match = results.FirstOrDefault(r =>
            string.Equals(SteamTitleNormalizer.Normalize(r.Title), normalized, StringComparison.OrdinalIgnoreCase));
        return match?.IgdbId is > 0 ? match.IgdbId : null;
    }

    private static void EnsureIgdbExternalId(Game game, long igdbId)
    {
        if (game.ExternalIds.Any(x => x.Source == ExternalIdSource.Igdb))
        {
            return;
        }

        var externalId = new GameExternalId
        {
            GameId = game.Id,
            Source = ExternalIdSource.Igdb,
            ExternalId = igdbId.ToString()
        };
        game.ExternalIds.Add(externalId);
    }

    private void MarkRun(
        Guid gameId,
        Dictionary<Guid, GameEnrichmentRun> runByGame,
        TimeSpan ttl,
        string status,
        DateTimeOffset now)
    {
        if (!runByGame.TryGetValue(gameId, out var run))
        {
            run = new GameEnrichmentRun
            {
                GameId = gameId,
                Kind = EnrichmentKind.Cover
            };
            dbContext.GameEnrichmentRuns.Add(run);
            runByGame[gameId] = run;
        }

        run.LastRunAt = now;
        run.NextRunAt = now.Add(ttl);
        run.Status = status;
    }
}

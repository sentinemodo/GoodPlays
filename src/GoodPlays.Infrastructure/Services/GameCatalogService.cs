using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed class GameCatalogService(
    GoodPlaysDbContext dbContext,
    IIgdbClient igdbClient) : IGameCatalogService
{
    public async Task<IReadOnlyList<GameSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalized = query.Trim();
        var results = new Dictionary<string, GameSummaryDto>(StringComparer.OrdinalIgnoreCase);

        if (igdbClient.IsConfigured)
        {
            var igdbResults = await igdbClient.SearchGamesAsync(normalized, cancellationToken);
            foreach (var result in igdbResults)
            {
                var existing = await dbContext.GameExternalIds
                    .AsNoTracking()
                    .Where(x => x.Source == ExternalIdSource.Igdb && x.ExternalId == result.IgdbId.ToString())
                    .Select(x => new { x.GameId, x.Game.Title, x.Game.Slug, x.Game.CoverUrl })
                    .FirstOrDefaultAsync(cancellationToken);

                if (existing is not null)
                {
                    results[existing.GameId.ToString()] = new GameSummaryDto(
                        existing.GameId,
                        existing.Title,
                        existing.Slug,
                        existing.CoverUrl,
                        result.IgdbId,
                        "local");
                    continue;
                }

                var key = $"igdb:{result.IgdbId}";
                results[key] = new GameSummaryDto(
                    Guid.Empty,
                    result.Title,
                    result.Slug ?? SlugHelper.CreateSlug(result.Title, result.IgdbId),
                    result.CoverUrl,
                    result.IgdbId,
                    "igdb");
            }
        }

        var lowerQuery = normalized.ToLowerInvariant();
        var localMatches = await dbContext.Games
            .AsNoTracking()
            .Where(g => g.SortTitle.Contains(lowerQuery) || g.Title.ToLower().Contains(lowerQuery))
            .OrderBy(g => g.SortTitle)
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.Slug,
                g.CoverUrl,
                IgdbId = g.ExternalIds
                    .Where(x => x.Source == ExternalIdSource.Igdb)
                    .Select(x => x.ExternalId)
                    .FirstOrDefault()
            })
            .Take(25)
            .ToListAsync(cancellationToken);

        foreach (var match in localMatches)
        {
            long? igdbId = long.TryParse(match.IgdbId, out var parsed) ? parsed : null;
            results[match.Id.ToString()] = new GameSummaryDto(
                match.Id,
                match.Title,
                match.Slug,
                match.CoverUrl,
                igdbId,
                "local");
        }

        return results.Values
            .OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .ToList();
    }

    public async Task<Game?> ImportFromIgdbAsync(long igdbId, CancellationToken cancellationToken)
    {
        var existingExternal = await dbContext.GameExternalIds
            .Include(x => x.Game)
            .FirstOrDefaultAsync(
                x => x.Source == ExternalIdSource.Igdb && x.ExternalId == igdbId.ToString(),
                cancellationToken);

        if (existingExternal?.Game is not null)
        {
            return existingExternal.Game;
        }

        if (!igdbClient.IsConfigured)
        {
            return null;
        }

        var igdbGame = await igdbClient.GetGameAsync(igdbId, cancellationToken);
        if (igdbGame is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var slug = igdbGame.Slug ?? SlugHelper.CreateSlug(igdbGame.Title, igdbGame.IgdbId);
        var slugTaken = await dbContext.Games.AnyAsync(g => g.Slug == slug, cancellationToken);
        if (slugTaken)
        {
            slug = SlugHelper.CreateSlug(igdbGame.Title, igdbGame.IgdbId);
        }

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = igdbGame.Title,
            SortTitle = igdbGame.Title.ToLowerInvariant(),
            Slug = slug,
            Summary = igdbGame.Summary,
            CoverUrl = igdbGame.CoverUrl,
            ReleaseDate = igdbGame.ReleaseDate,
            MetadataStatus = MetadataStatus.Complete,
            CreatedAt = now,
            UpdatedAt = now
        };

        game.ExternalIds.Add(new GameExternalId
        {
            GameId = game.Id,
            Source = ExternalIdSource.Igdb,
            ExternalId = igdbId.ToString()
        });

        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync(cancellationToken);
        return game;
    }
}

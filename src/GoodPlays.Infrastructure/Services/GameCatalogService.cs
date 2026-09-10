using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Steam;
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

    public Task<Game?> ImportFromIgdbAsync(long igdbId, CancellationToken cancellationToken) =>
        ImportFromIgdbAsync(igdbId, cancellationToken, null);

    private async Task<Game?> ImportFromIgdbAsync(long igdbId, CancellationToken cancellationToken, uint? steamAppId)
    {
        var existingExternal = await dbContext.GameExternalIds
            .Include(x => x.Game)
            .FirstOrDefaultAsync(
                x => x.Source == ExternalIdSource.Igdb && x.ExternalId == igdbId.ToString(),
                cancellationToken);

        if (existingExternal?.Game is not null)
        {
            if (steamAppId is not null)
            {
                await AttachSteamExternalIdAsync(existingExternal.Game.Id, steamAppId.Value, cancellationToken);
            }

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

        if (steamAppId is not null)
        {
            await AttachSteamExternalIdAsync(game.Id, steamAppId.Value, cancellationToken);
        }

        return game;
    }

    public async Task<Game?> FindBySteamAppIdAsync(uint appId, CancellationToken cancellationToken)
    {
        var externalId = appId.ToString();
        return await dbContext.GameExternalIds
            .AsNoTracking()
            .Where(x => x.Source == ExternalIdSource.Steam && x.ExternalId == externalId)
            .Select(x => x.Game)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Game> ImportFromSteamAppAsync(uint appId, string title, CancellationToken cancellationToken)
    {
        var existing = await FindBySteamAppIdAsync(appId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var slug = SlugHelper.CreateSlug(title, appId);
        var slugTaken = await dbContext.Games.AnyAsync(g => g.Slug == slug, cancellationToken);
        if (slugTaken)
        {
            slug = SlugHelper.CreateSlug($"{title}-steam-{appId}", appId);
        }

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = title,
            SortTitle = title.ToLowerInvariant(),
            Slug = slug,
            MetadataStatus = MetadataStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        game.ExternalIds.Add(new GameExternalId
        {
            GameId = game.Id,
            Source = ExternalIdSource.Steam,
            ExternalId = appId.ToString()
        });

        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync(cancellationToken);
        return game;
    }

    public async Task<Game> ResolveForSteamSyncAsync(uint appId, string steamTitle, CancellationToken cancellationToken)
    {
        var existingSteam = await dbContext.GameExternalIds
            .Include(x => x.Game)
            .Where(x => x.Source == ExternalIdSource.Steam && x.ExternalId == appId.ToString())
            .Select(x => x.Game)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingSteam?.MetadataStatus == MetadataStatus.Complete)
        {
            return existingSteam;
        }

        if (igdbClient.IsConfigured)
        {
            var igdbId = await igdbClient.FindIgdbIdBySteamAppIdAsync(appId, cancellationToken);
            if (igdbId is not null)
            {
                var igdbGame = await ImportFromIgdbAsync(igdbId.Value, cancellationToken, appId);
                if (igdbGame is not null)
                {
                    return igdbGame;
                }
            }
        }

        var titleResolved = await ResolveByTitleAsync(appId, steamTitle, cancellationToken);
        if (titleResolved is not null)
        {
            return titleResolved;
        }

        if (existingSteam is not null)
        {
            return await EnrichFromIgdbAsync(existingSteam, appId, cancellationToken) ?? existingSteam;
        }

        var stub = await ImportFromSteamAppAsync(appId, steamTitle, cancellationToken);
        return await EnrichFromIgdbAsync(stub, appId, cancellationToken) ?? stub;
    }

    public async Task<Game?> EnrichFromIgdbAsync(Game game, uint steamAppId, CancellationToken cancellationToken)
    {
        if (game.MetadataStatus == MetadataStatus.Complete)
        {
            await AttachSteamExternalIdAsync(game.Id, steamAppId, cancellationToken);
            return game;
        }

        if (!igdbClient.IsConfigured)
        {
            return null;
        }

        long? igdbId = await igdbClient.FindIgdbIdBySteamAppIdAsync(steamAppId, cancellationToken);
        if (igdbId is null)
        {
            var normalizedTitle = SteamTitleNormalizer.Normalize(game.Title);
            var results = await SearchAsync(normalizedTitle, cancellationToken);
            var match = results.FirstOrDefault(r =>
                             string.Equals(r.Title, game.Title, StringComparison.OrdinalIgnoreCase))
                         ?? results.FirstOrDefault();
            igdbId = match?.IgdbId;
        }

        if (igdbId is null or <= 0)
        {
            return null;
        }

        var igdbGame = await igdbClient.GetGameAsync(igdbId.Value, cancellationToken);
        if (igdbGame is null)
        {
            return null;
        }

        game.Title = igdbGame.Title;
        game.SortTitle = igdbGame.Title.ToLowerInvariant();
        game.Summary = igdbGame.Summary;
        game.CoverUrl = igdbGame.CoverUrl;
        game.ReleaseDate = igdbGame.ReleaseDate;
        game.MetadataStatus = MetadataStatus.Complete;
        game.UpdatedAt = DateTimeOffset.UtcNow;

        var hasIgdbId = await dbContext.GameExternalIds.AnyAsync(
            x => x.GameId == game.Id && x.Source == ExternalIdSource.Igdb,
            cancellationToken);
        if (!hasIgdbId)
        {
            dbContext.GameExternalIds.Add(new GameExternalId
            {
                GameId = game.Id,
                Source = ExternalIdSource.Igdb,
                ExternalId = igdbId.Value.ToString()
            });
        }

        await AttachSteamExternalIdAsync(game.Id, steamAppId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return game;
    }

    private async Task<Game?> ResolveByTitleAsync(uint appId, string steamTitle, CancellationToken cancellationToken)
    {
        var normalizedTitle = SteamTitleNormalizer.Normalize(steamTitle);
        var results = await SearchAsync(normalizedTitle, cancellationToken);
        if (results.Count == 0)
        {
            return null;
        }

        var exactMatches = results
            .Where(r =>
                string.Equals(r.Title, steamTitle, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(SteamTitleNormalizer.Normalize(r.Title), normalizedTitle, StringComparison.OrdinalIgnoreCase))
            .ToList();

        GameSummaryDto? chosen = exactMatches.Count switch
        {
            1 => exactMatches[0],
            > 1 => null,
            _ => results.Count == 1 ? results[0] : null
        };

        if (chosen is null)
        {
            return null;
        }

        if (chosen.Id != Guid.Empty)
        {
            var local = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == chosen.Id, cancellationToken);
            if (local is not null)
            {
                await AttachSteamExternalIdAsync(local.Id, appId, cancellationToken);
            }

            return local;
        }

        if (chosen.IgdbId is > 0)
        {
            return await ImportFromIgdbAsync(chosen.IgdbId.Value, cancellationToken, appId);
        }

        return null;
    }

    private async Task AttachSteamExternalIdAsync(Guid gameId, uint appId, CancellationToken cancellationToken)
    {
        var externalId = appId.ToString();
        var exists = await dbContext.GameExternalIds.AnyAsync(
            x => x.GameId == gameId && x.Source == ExternalIdSource.Steam && x.ExternalId == externalId,
            cancellationToken);

        if (!exists)
        {
            dbContext.GameExternalIds.Add(new GameExternalId
            {
                GameId = gameId,
                Source = ExternalIdSource.Steam,
                ExternalId = externalId
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

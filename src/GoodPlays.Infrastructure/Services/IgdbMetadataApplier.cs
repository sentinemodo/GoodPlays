using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

internal static class IgdbMetadataApplier
{
    public static async Task ApplyDetailsAsync(
        GoodPlaysDbContext dbContext,
        Game game,
        IgdbGameDetails details,
        CancellationToken cancellationToken)
    {
        game.Title = details.Title;
        game.SortTitle = details.Title.ToLowerInvariant();
        game.Summary = details.Summary;
        game.CoverUrl = details.CoverUrl;
        game.ReleaseDate = details.ReleaseDate;
        game.Developer = details.Developer;
        game.Publisher = details.Publisher;
        game.GameType = details.GameType;
        game.MetadataStatus = MetadataStatus.Complete;
        game.UpdatedAt = DateTimeOffset.UtcNow;

        if (details.ParentIgdbId is not null)
        {
            var parentId = await dbContext.GameExternalIds
                .AsNoTracking()
                .Where(x => x.Source == ExternalIdSource.Igdb && x.ExternalId == details.ParentIgdbId.Value.ToString())
                .Select(x => x.GameId)
                .FirstOrDefaultAsync(cancellationToken);

            game.ParentGameId = parentId == Guid.Empty ? null : parentId;
        }

        foreach (var category in details.Genres.Concat(details.PlayerPerspectives))
        {
            var slug = SlugHelper.CreateSlug(category.Name, category.Id);
            var entity = await dbContext.Genres.FirstOrDefaultAsync(g => g.Slug == slug, cancellationToken);
            if (entity is null)
            {
                entity = new Genre
                {
                    Id = Guid.NewGuid(),
                    Name = category.Name,
                    Slug = slug
                };
                dbContext.Genres.Add(entity);
            }

            if (!await dbContext.GameGenres.AnyAsync(
                    gg => gg.GameId == game.Id && gg.GenreId == entity.Id,
                    cancellationToken))
            {
                dbContext.GameGenres.Add(new GameGenre { GameId = game.Id, GenreId = entity.Id });
            }
        }

        foreach (var platform in details.Platforms)
        {
            var slug = SlugHelper.CreateSlug(platform.Name, platform.Id);
            var entity = await dbContext.Platforms.FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);
            if (entity is null)
            {
                entity = new Platform
                {
                    Id = Guid.NewGuid(),
                    Name = platform.Name,
                    Slug = slug
                };
                dbContext.Platforms.Add(entity);
            }

            if (!await dbContext.GamePlatforms.AnyAsync(
                    gp => gp.GameId == game.Id && gp.PlatformId == entity.Id,
                    cancellationToken))
            {
                dbContext.GamePlatforms.Add(new GamePlatform { GameId = game.Id, PlatformId = entity.Id });
            }
        }
    }
}

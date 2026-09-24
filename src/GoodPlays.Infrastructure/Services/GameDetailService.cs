using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed class GameDetailService(
    GoodPlaysDbContext dbContext,
    IGameEnrichmentService enrichmentService) : IGameDetailService
{
    public async Task<GameDetailDto?> GetBySlugAsync(string slug, Guid? userId, CancellationToken cancellationToken)
    {
        var game = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
            .Include(g => g.GamePlatforms).ThenInclude(gp => gp.Platform)
            .FirstOrDefaultAsync(g => g.Slug == slug, cancellationToken);

        if (game is null)
        {
            return null;
        }

        await enrichmentService.EnrichIfStaleAsync(game.Id, cancellationToken);

        var entries = await dbContext.LibraryEntries
            .AsNoTracking()
            .Where(e => e.GameId == game.Id)
            .ToListAsync(cancellationToken);

        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
        var ratings = entries.Where(e => e.Rating is not null).Select(e => e.Rating!.Value).OrderBy(r => r).ToList();
        var median = ratings.Count == 0
            ? (decimal?)null
            : ratings.Count % 2 == 1
                ? ratings[ratings.Count / 2]
                : (ratings[ratings.Count / 2 - 1] + ratings[ratings.Count / 2]) / 2m;

        var stats = new GameDetailStatsDto(
            entries.Count,
            entries.Count(e => e.UpdatedAt >= thirtyDaysAgo),
            entries.Where(e => e.HoursPlayed is not null).Sum(e => e.HoursPlayed ?? 0),
            ratings.Count == 0 ? null : (decimal)ratings.Average(r => r),
            median,
            entries.Count == 0 ? 0 : (decimal)entries.Count(e => e.Status == LibraryStatus.Completed) / entries.Count * 100,
            entries.Count(e => e.Status == LibraryStatus.Backlog));

        var dlc = await dbContext.Games
            .AsNoTracking()
            .Where(g => g.ParentGameId == game.Id)
            .OrderBy(g => g.Title)
            .Select(g => new GameSummaryDto(g.Id, g.Title, g.Slug, g.CoverUrl, null, "local"))
            .ToListAsync(cancellationToken);

        var ratingCaches = await dbContext.GameRatingCaches
            .AsNoTracking()
            .Where(r => r.GameId == game.Id)
            .Select(r => new GameRatingDto(r.Source, r.Score, r.ReviewCount, r.Url, r.FetchedAt))
            .ToListAsync(cancellationToken);

        var news = await dbContext.GameNewsItems
            .AsNoTracking()
            .Where(n => n.GameId == game.Id)
            .OrderByDescending(n => n.PublishedAt)
            .Take(10)
            .Select(n => new GameNewsDto(n.Id, n.Source, n.Title, n.Url, n.PublishedAt))
            .ToListAsync(cancellationToken);

        var achievementRows = await dbContext.Achievements
            .AsNoTracking()
            .Where(a => a.GameId == game.Id)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        HashSet<Guid> userUnlocked = [];
        if (userId is not null)
        {
            userUnlocked = await dbContext.UserAchievements
                .AsNoTracking()
                .Where(ua => ua.UserId == userId && ua.Achievement.GameId == game.Id)
                .Select(ua => ua.AchievementId)
                .ToHashSetAsync(cancellationToken);
        }

        var achievementDtos = new List<AchievementDto>();
        foreach (var a in achievementRows)
        {
            var owners = await dbContext.UserAchievements
                .AsNoTracking()
                .Where(ua => ua.AchievementId == a.Id)
                .Join(dbContext.UserProfiles.AsNoTracking(), ua => ua.UserId, p => p.UserId, (ua, p) => new { ua, p })
                .Join(dbContext.Users.AsNoTracking(), x => x.ua.UserId, u => u.Id, (x, u) => new AchievementOwnerDto(
                    x.p.Username,
                    u.DisplayName,
                    x.ua.UnlockedAt))
                .Take(5)
                .ToListAsync(cancellationToken);

            achievementDtos.Add(new AchievementDto(
                a.Id,
                a.Name,
                a.Description,
                a.IconUrl,
                a.RarityPercent,
                userUnlocked.Contains(a.Id),
                owners));
        }

        var comments = await dbContext.GameComments
            .AsNoTracking()
            .Where(c => c.GameId == game.Id && c.Visibility == Visibility.Public)
            .OrderByDescending(c => c.CreatedAt)
            .Take(20)
            .Join(dbContext.UserProfiles, c => c.UserId, p => p.UserId, (c, p) => new GameCommentDto(
                c.Id,
                p.Username,
                c.User.DisplayName,
                c.Body,
                c.Rating,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        var heroes = await BuildHeroesAsync(game.Id, cancellationToken);

        var userTags = userId is null
            ? []
            : await dbContext.GameTags
                .AsNoTracking()
                .Where(gt => gt.GameId == game.Id && (gt.Tag.UserId == null || gt.Tag.UserId == userId))
                .Select(gt => gt.Tag.Name)
                .ToListAsync(cancellationToken);

        LibraryEntryDto? userEntry = null;
        if (userId is not null)
        {
            userEntry = await dbContext.LibraryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.GameId == game.Id)
                .OrderByDescending(e => e.UpdatedAt)
                .Select(e => new LibraryEntryDto(
                    e.Id,
                    e.GameId,
                    game.Title,
                    game.CoverUrl,
                    e.Status,
                    e.Rating,
                    e.HoursPlayed,
                    e.Source,
                    e.UpdatedAt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new GameDetailDto(
            game.Id,
            game.Title,
            game.Slug,
            game.Summary,
            game.CoverUrl,
            game.ReleaseDate,
            game.Developer,
            game.Publisher,
            game.GameType,
            game.GameGenres.Select(gg => gg.Genre.Name).ToList(),
            game.GamePlatforms.Select(gp => gp.Platform.Name).ToList(),
            dlc,
            stats,
            ratingCaches,
            heroes,
            achievementDtos,
            news,
            comments,
            userEntry,
            userTags);
    }

    private async Task<IReadOnlyList<GameHeroDto>> BuildHeroesAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var mostHours = await dbContext.LibraryEntries
            .AsNoTracking()
            .Where(e => e.GameId == gameId && e.HoursPlayed != null)
            .OrderByDescending(e => e.HoursPlayed)
            .Join(dbContext.UserProfiles, e => e.UserId, p => p.UserId, (e, p) => new
            {
                p.Username,
                e.User.DisplayName,
                e.HoursPlayed
            })
            .FirstOrDefaultAsync(cancellationToken);

        var heroes = new List<GameHeroDto>();
        if (mostHours?.HoursPlayed is not null)
        {
            heroes.Add(new GameHeroDto(
                mostHours.Username,
                mostHours.DisplayName,
                mostHours.HoursPlayed.Value,
                "Most hours"));
        }

        var totalAchievements = await dbContext.Achievements.CountAsync(a => a.GameId == gameId, cancellationToken);
        if (totalAchievements == 0)
        {
            return heroes;
        }

        var platinumCandidates = await dbContext.UserAchievements
            .AsNoTracking()
            .Where(ua => ua.Achievement.GameId == gameId)
            .GroupBy(ua => ua.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                First = g.Min(x => x.UnlockedAt),
                Last = g.Max(x => x.UnlockedAt)
            })
            .Where(x => x.Count >= totalAchievements)
            .OrderBy(x => x.Last - x.First)
            .Join(dbContext.UserProfiles, x => x.UserId, p => p.UserId, (x, p) => new
            {
                p.Username,
                DisplayName = dbContext.Users.Where(u => u.Id == x.UserId).Select(u => u.DisplayName).FirstOrDefault(),
                Days = (x.Last - x.First).TotalDays
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (platinumCandidates is not null)
        {
            heroes.Add(new GameHeroDto(
                platinumCandidates.Username,
                platinumCandidates.DisplayName,
                (decimal)platinumCandidates.Days,
                "Fastest platinum (days)"));
        }

        return heroes;
    }
}

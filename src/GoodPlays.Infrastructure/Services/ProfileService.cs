using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed class ProfileService(GoodPlaysDbContext dbContext) : IProfileService
{
    public async Task<ProfileDto> GetAsync(
        Guid userId,
        Guid? trophyGameId,
        TrophyClass? trophyClass,
        decimal? maxRarity,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.Profile)
            .FirstAsync(u => u.Id == userId, cancellationToken);
        var profile = await EnsureProfileAsync(user, cancellationToken);

        var entries = await dbContext.LibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .WithPlayableGame()
            .Select(e => new LibraryRow(
                e.GameId,
                e.Game.Title,
                e.Game.Slug,
                e.Game.CoverUrl,
                e.HoursPlayed,
                e.StartedAt,
                e.Source,
                e.IsLoved,
                e.HoursPlayedLocked,
                e.Game.GameGenres.Select(gg => new GenreRow(gg.Genre.Slug, gg.Genre.Name)).ToList()))
            .ToListAsync(cancellationToken);

        var loved = entries.FirstOrDefault(e => e.IsLoved);
        var ranked = entries.Where(e => !e.HoursPlayedLocked).ToList();
        var mostHours = ranked
            .OrderByDescending(e => e.HoursPlayed ?? 0m)
            .ThenBy(e => e.Title)
            .FirstOrDefault();
        var mostLoved = loved ?? mostHours;

        var platforms = entries
            .GroupBy(e => e.Source)
            .Select(g => new ProfileStatDto(
                g.Key.ToString(),
                LibrarySourceLabels.Format(g.Key),
                g.Count(),
                g.Where(e => !e.HoursPlayedLocked).Sum(e => e.HoursPlayed ?? 0m)))
            .OrderByDescending(s => s.Hours)
            .ThenBy(s => s.Label)
            .ToList();

        var categories = entries
            .SelectMany(e => e.Genres.Count == 0
                ? [new GenreRow("uncategorized", "Uncategorized")]
                : e.Genres.DistinctBy(g => g.Slug))
            .GroupBy(g => g.Slug)
            .Select(g =>
            {
                var matching = entries.Where(e =>
                    g.Key == "uncategorized"
                        ? e.Genres.Count == 0
                        : e.Genres.Any(genre => genre.Slug == g.Key)).ToList();
                return new ProfileStatDto(
                    g.Key,
                    g.First().Name,
                    matching.Count,
                    matching.Where(e => !e.HoursPlayedLocked).Sum(e => e.HoursPlayed ?? 0m));
            })
            .OrderByDescending(s => s.Hours)
            .ThenBy(s => s.Label)
            .ToList();

        var topGames = ranked
            .OrderByDescending(e => e.HoursPlayed ?? 0m)
            .ThenBy(e => e.Title)
            .Take(5)
            .Select(ToGame)
            .ToList();

        var lastPlayed = entries
            .Where(e => e.LastPlayed is not null)
            .OrderByDescending(e => e.LastPlayed)
            .ThenBy(e => e.Title)
            .Take(5)
            .Select(ToGame)
            .ToList();

        var trophyRows = await dbContext.UserAchievements
            .AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .Select(ua => new TrophyRow(
                ua.AchievementId,
                ua.Achievement.GameId,
                ua.Achievement.Game.Title,
                ua.Achievement.Game.Slug,
                ua.Achievement.Name,
                ua.Achievement.Description,
                ua.Achievement.IconUrl,
                ua.Achievement.RarityPercent,
                ua.UnlockedAt,
                ua.IsFeatured))
            .ToListAsync(cancellationToken);

        var trophies = trophyRows.Select(ToTrophy).ToList();
        var trophyGames = trophies
            .GroupBy(t => t.GameId)
            .Select(g => new ProfileTrophyGameDto(g.Key, g.First().GameTitle))
            .OrderBy(g => g.Title)
            .ToList();

        var filtered = trophies.Where(t =>
            (trophyGameId is null || t.GameId == trophyGameId) &&
            (trophyClass is null || t.TrophyClass == trophyClass) &&
            (maxRarity is null || (t.RarityPercent is not null && t.RarityPercent <= maxRarity)))
            .OrderByDescending(t => t.UnlockedAt)
            .ToList();

        return new ProfileDto(
            profile.Username,
            user.DisplayName,
            profile.Bio,
            profile.AvatarUrl,
            mostLoved is null ? null : ToGame(mostLoved),
            loved is not null,
            platforms,
            categories,
            topGames,
            lastPlayed,
            trophies.FirstOrDefault(t => t.IsFeatured),
            trophies.OrderByDescending(t => t.UnlockedAt).FirstOrDefault(),
            filtered,
            trophyGames);
    }

    public async Task<ProfileDto?> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var profile = await EnsureProfileAsync(user, cancellationToken);
        if (request.Bio is not null)
        {
            var bio = request.Bio.Trim();
            profile.Bio = bio.Length == 0 ? null : bio.Length > 500 ? bio[..500] : bio;
        }

        if (request.AvatarUrl is not null)
        {
            var avatar = request.AvatarUrl.Trim();
            profile.AvatarUrl = avatar.Length == 0 ? null : avatar;
        }

        if (request.Username is not null)
        {
            var nextUsername = request.Username.Trim().ToLowerInvariant();
            if (!string.Equals(profile.Username, nextUsername, StringComparison.Ordinal))
            {
                profile.Username = await NormalizeUsernameAsync(request.Username, userId, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, null, null, null, cancellationToken);
    }

    public async Task<bool> SetFeaturedTrophyAsync(
        Guid userId,
        Guid achievementId,
        bool featured,
        CancellationToken cancellationToken)
    {
        var owned = await dbContext.UserAchievements
            .Where(ua => ua.UserId == userId)
            .ToListAsync(cancellationToken);
        var target = owned.FirstOrDefault(ua => ua.AchievementId == achievementId);
        if (target is null)
        {
            return false;
        }

        if (featured)
        {
            foreach (var other in owned.Where(ua => ua.IsFeatured && ua.AchievementId != achievementId))
            {
                other.IsFeatured = false;
            }
        }

        target.IsFeatured = featured;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Guid?> FindUserIdByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return await dbContext.UserProfiles
            .Where(profile => profile.Username == normalized)
            .Select(profile => (Guid?)profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> NormalizeUsernameAsync(string username, Guid userId, CancellationToken cancellationToken)
    {
        var normalized = username.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 24 || !normalized.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
        {
            throw new ArgumentException("Nickname must be 3–24 characters and use only letters, numbers, hyphens, or underscores.");
        }

        var taken = await dbContext.UserProfiles.AnyAsync(
            profile => profile.Username == normalized && profile.UserId != userId,
            cancellationToken);
        if (taken)
        {
            throw new UsernameTakenException("That nickname is already taken.");
        }

        return normalized;
    }

    private async Task<UserProfile> EnsureProfileAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Profile is not null)
        {
            return user.Profile;
        }

        var username = await UniqueUsernameAsync(user, cancellationToken);
        var profile = new UserProfile
        {
            UserId = user.Id,
            Username = username
        };
        dbContext.UserProfiles.Add(profile);
        user.Profile = profile;
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private async Task<string> UniqueUsernameAsync(User user, CancellationToken cancellationToken)
    {
        var raw = user.DisplayName ?? user.Email.Split('@')[0];
        var slug = new string(raw.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "player";
        }

        var candidate = $"{slug}-{user.Id.ToString("N")[..6]}";
        if (candidate.Length > 40)
        {
            candidate = candidate[..40];
        }

        var taken = await dbContext.UserProfiles.AnyAsync(p => p.Username == candidate, cancellationToken);
        return taken ? $"{candidate[..Math.Min(33, candidate.Length)]}-{Guid.NewGuid().ToString("N")[..6]}" : candidate;
    }

    private static ProfileGameDto ToGame(LibraryRow entry) =>
        new(entry.GameId, entry.Title, entry.Slug, entry.CoverUrl, entry.HoursPlayed, entry.LastPlayed, entry.IsLoved);

    private static ProfileTrophyDto ToTrophy(TrophyRow row) =>
        new(
            row.AchievementId,
            row.GameId,
            row.GameTitle,
            row.GameSlug,
            row.Name,
            row.Description,
            row.IconUrl,
            row.RarityPercent,
            TrophyClassRules.FromRarity(row.RarityPercent),
            row.UnlockedAt,
            row.IsFeatured);

    private sealed record GenreRow(string Slug, string Name);

    private sealed record LibraryRow(
        Guid GameId,
        string Title,
        string Slug,
        string? CoverUrl,
        decimal? HoursPlayed,
        DateOnly? LastPlayed,
        LibraryEntrySource Source,
        bool IsLoved,
        bool HoursPlayedLocked,
        List<GenreRow> Genres);

    private sealed record TrophyRow(
        Guid AchievementId,
        Guid GameId,
        string GameTitle,
        string GameSlug,
        string Name,
        string? Description,
        string? IconUrl,
        decimal? RarityPercent,
        DateTimeOffset UnlockedAt,
        bool IsFeatured);
}

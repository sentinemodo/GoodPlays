using GoodPlays.Domain.Entities;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed record ActivityLogDto(Guid Id, string Category, string Message, DateTimeOffset CreatedAt);

public sealed record AdminActivityLogDto(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string? Username,
    string Category,
    string Message,
    DateTimeOffset CreatedAt);

public sealed record AdminActivityQuery(
    string? Category,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Search,
    int Limit,
    int Offset,
    string? Nickname = null);

public sealed record AdminActivityPage(IReadOnlyList<AdminActivityLogDto> Items, int Total);

public interface IActivityLogService
{
    Task RecordAsync(Guid? userId, string category, string message, CancellationToken cancellationToken);

    Task RecordLoginAsync(Guid userId, string email, bool succeeded, CancellationToken cancellationToken);

    Task RecordProfileEventsAsync(Guid userId, string category, IReadOnlyList<string> messages, CancellationToken cancellationToken);

    Task RecordRankingMilestonesAsync(Guid userId, CancellationToken cancellationToken);

    Task BackfillProfileMilestonesAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivityLogDto>> ListAsync(Guid userId, int limit, CancellationToken cancellationToken);

    Task<AdminActivityPage> SearchAdminAsync(AdminActivityQuery query, CancellationToken cancellationToken);
}

public sealed class ActivityLogService(GoodPlaysDbContext dbContext) : IActivityLogService
{
    public async Task RecordAsync(Guid? userId, string category, string message, CancellationToken cancellationToken)
    {
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Category = category,
            Message = message,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordLoginAsync(Guid userId, string email, bool succeeded, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-6);
        var alreadyLogged = await dbContext.ActivityLogs.AnyAsync(
            entry => entry.UserId == userId &&
                     entry.Category == "Login" &&
                     entry.CreatedAt >= since &&
                     entry.Message.StartsWith(succeeded ? "Signed in" : "Sign-in failed"),
            cancellationToken);
        if (alreadyLogged)
        {
            return;
        }

        var message = succeeded
            ? $"Signed in as {email}"
            : $"Sign-in failed for {email}";
        await RecordAsync(userId, "Login", message, cancellationToken);
    }

    public async Task RecordProfileEventsAsync(
        Guid userId,
        string category,
        IReadOnlyList<string> messages,
        CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var existing = await dbContext.ActivityLogs
            .Where(entry => entry.UserId == userId && entry.Category == category && messages.Contains(entry.Message))
            .Select(entry => entry.Message)
            .ToListAsync(cancellationToken);

        foreach (var message in messages.Where(message => !existing.Contains(message)))
        {
            dbContext.ActivityLogs.Add(new ActivityLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Category = category,
                Message = message,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordRankingMilestonesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.LibraryEntries
            .AsNoTracking()
            .Where(entry => entry.HoursPlayed > 0)
            .Select(entry => new
            {
                entry.UserId,
                Hours = entry.HoursPlayed ?? 0m,
                Genres = entry.Game.GameGenres.Select(link => link.Genre.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        var totals = new Dictionary<string, Dictionary<Guid, decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            foreach (var genre in row.Genres.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!totals.TryGetValue(genre, out var byUser))
                {
                    byUser = [];
                    totals[genre] = byUser;
                }

                byUser[row.UserId] = byUser.GetValueOrDefault(row.UserId) + row.Hours;
            }
        }

        var messages = new List<string>();
        foreach (var (genre, byUser) in totals)
        {
            if (byUser.Count < 2 || !byUser.TryGetValue(userId, out var hours) || hours <= 0)
            {
                continue;
            }

            var position = byUser.Values.Count(other => other > hours) + 1;
            var message = ProfileActivityRules.RankingMessage(position, genre);
            if (message is not null)
            {
                messages.Add(message);
            }
        }

        await RecordProfileEventsAsync(userId, ProfileActivityRules.Ranking, messages, cancellationToken);
    }

    public async Task BackfillProfileMilestonesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var seeded = await dbContext.ActivityLogs.AnyAsync(
            entry => entry.UserId == userId && entry.Category == ProfileActivityRules.FirstPlay,
            cancellationToken);
        if (seeded)
        {
            return;
        }

        var played = await dbContext.LibraryEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId && entry.HoursPlayed > 0)
            .Select(entry => new { entry.Game.Title, entry.HoursPlayed })
            .ToListAsync(cancellationToken);

        var firstPlays = new List<string>();
        var hundredHours = new List<string>();
        foreach (var entry in played)
        {
            foreach (var message in ProfileActivityRules.PlaytimeMessages(0, entry.HoursPlayed, entry.Title))
            {
                if (message.StartsWith("Played ", StringComparison.Ordinal))
                {
                    firstPlays.Add(message);
                }
                else
                {
                    hundredHours.Add(message);
                }
            }
        }

        await RecordProfileEventsAsync(userId, ProfileActivityRules.FirstPlay, firstPlays, cancellationToken);
        await RecordProfileEventsAsync(userId, ProfileActivityRules.Playtime, hundredHours, cancellationToken);

        var trophies = await dbContext.UserAchievements
            .AsNoTracking()
            .Where(unlock => unlock.UserId == userId)
            .Select(unlock => ProfileActivityRules.TrophyMessage(unlock.Achievement.Name, unlock.Achievement.Game.Title))
            .ToListAsync(cancellationToken);
        await RecordProfileEventsAsync(userId, ProfileActivityRules.Trophy, trophies, cancellationToken);
        await RecordRankingMilestonesAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityLogDto>> ListAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);
        return await dbContext.ActivityLogs
            .Where(entry => entry.UserId == userId && ProfileActivityRules.ProfileCategories.Contains(entry.Category))
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(limit)
            .Select(entry => new ActivityLogDto(entry.Id, entry.Category, entry.Message, entry.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminActivityPage> SearchAdminAsync(AdminActivityQuery query, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit, 1, 200);
        var offset = Math.Max(query.Offset, 0);
        var logs = dbContext.ActivityLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            logs = logs.Where(entry => entry.Category == query.Category);
        }

        if (query.From is not null)
        {
            logs = logs.Where(entry => entry.CreatedAt >= query.From);
        }

        if (query.To is not null)
        {
            logs = logs.Where(entry => entry.CreatedAt <= query.To);
        }

        if (!string.IsNullOrWhiteSpace(query.Nickname))
        {
            var nickname = query.Nickname.Trim().ToLowerInvariant();
            logs = logs.Where(entry =>
                entry.User != null &&
                entry.User.Profile != null &&
                entry.User.Profile.Username == nickname);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            logs = logs.Where(entry =>
                entry.Message.Contains(search) ||
                entry.Category.Contains(search) ||
                (entry.User != null && entry.User.Email.Contains(search)));
        }

        var total = await logs.CountAsync(cancellationToken);
        var items = await logs
            .OrderByDescending(entry => entry.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(entry => new AdminActivityLogDto(
                entry.Id,
                entry.UserId,
                entry.User != null ? entry.User.Email : null,
                entry.User != null && entry.User.Profile != null ? entry.User.Profile.Username : null,
                entry.Category,
                entry.Message,
                entry.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminActivityPage(items, total);
    }
}

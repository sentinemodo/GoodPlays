using GoodPlays.Domain.Entities;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public static class ProfileActivityRules
{
    public const string FirstPlay = "FirstPlay";
    public const string Trophy = "Trophy";
    public const string Playtime = "Playtime";
    public const string Ranking = "Ranking";

    public static readonly string[] ProfileCategories = [FirstPlay, Trophy, Playtime, Ranking];

    public static readonly int[] RankingPositions = [1, 10, 25, 50, 100];

    public static IReadOnlyList<string> PlaytimeMessages(decimal? previousHours, decimal? nextHours, string gameTitle)
    {
        var previous = previousHours ?? 0m;
        var next = nextHours ?? 0m;
        var messages = new List<string>();
        if (previous <= 0m && next > 0m)
        {
            messages.Add($"Played {gameTitle} for the first time.");
        }

        if (previous < 100m && next >= 100m)
        {
            messages.Add($"Spent 100 hours in {gameTitle}.");
        }

        return messages;
    }

    public static string TrophyMessage(string trophyName, string gameTitle) =>
        $"Acquired {trophyName} in {gameTitle}.";

    public static string? RankingMessage(int position, string genreName)
    {
        if (!RankingPositions.Contains(position))
        {
            return null;
        }

        return $"Reached {Ordinal(position)} on Best {genreName} players.";
    }

    public static async Task RecordPlaytimeAsync(
        GoodPlaysDbContext dbContext,
        Guid userId,
        string gameTitle,
        decimal? previousHours,
        decimal? nextHours,
        CancellationToken cancellationToken)
    {
        var messages = PlaytimeMessages(previousHours, nextHours, gameTitle);
        if (messages.Count == 0)
        {
            return;
        }

        var firstPlay = messages.Where(message => message.StartsWith("Played ", StringComparison.Ordinal)).ToArray();
        var playtime = messages.Where(message => message.StartsWith("Spent ", StringComparison.Ordinal)).ToArray();
        await AddAsync(dbContext, userId, FirstPlay, firstPlay, cancellationToken);
        await AddAsync(dbContext, userId, Playtime, playtime, cancellationToken);
    }

    public static async Task RecordTrophyAsync(
        GoodPlaysDbContext dbContext,
        Guid userId,
        string trophyName,
        string gameTitle,
        CancellationToken cancellationToken)
    {
        await AddAsync(dbContext, userId, Trophy, [TrophyMessage(trophyName, gameTitle)], cancellationToken);
    }

    private static async Task AddAsync(
        GoodPlaysDbContext dbContext,
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

        var added = false;
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
            added = true;
        }

        if (added)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public static string Ordinal(int position)
    {
        var teen = position % 100;
        if (teen is >= 11 and <= 13)
        {
            return $"{position}th";
        }

        return (position % 10) switch
        {
            1 => $"{position}st",
            2 => $"{position}nd",
            3 => $"{position}rd",
            _ => $"{position}th"
        };
    }
}

public static class AdminAccess
{
    public static IReadOnlyList<string> Parse(string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? []
            : configured.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool IsAllowed(string? email, IEnumerable<string> allowlist, bool allowLocalClerkFallback = false)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        if (allowlist.Any(entry => string.Equals(entry.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return allowLocalClerkFallback &&
               email.EndsWith("@users.clerk", StringComparison.OrdinalIgnoreCase);
    }
}

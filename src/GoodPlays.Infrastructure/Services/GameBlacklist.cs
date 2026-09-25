using System.Linq.Expressions;
using GoodPlays.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

/// <summary>
/// Detects non-game applications synced from platforms (streaming apps, etc.).
/// These may be imported but are hidden from catalog, library, profile, and recommendations.
/// </summary>
public static class GameBlacklist
{
    internal static readonly string[] BlockedTitleFragments =
    [
        "netflix",
        "amazon prime",
        "prime video",
        "disney+",
        "disney plus",
        "hulu",
        "spotify",
        "youtube",
        "twitch",
        "apple tv",
        "crunchyroll",
        "plex",
        "media player",
        "blu-ray player",
        "dvd player",
        "hbo",
        "max app",
        "peacock",
        "paramount+",
        "paramount plus",
        "apple music",
        "tidal",
        "discovery+",
        "discovery plus",
        "tubi",
        "pluto tv",
        "vudu",
        "rakuten tv",
        "mubi",
        "shudder",
        "funimation",
        "britbox",
        "crave",
        "starz",
        "showtime",
        "dazn",
        "espn+",
        "fubo",
        "sling tv",
        "google tv",
        "now tv",
        "sky go",
        "deezer",
        "soundcloud",
        "audible"
    ];

    private static readonly Expression<Func<Game, bool>> PlayableGame = BuildPlayable<Game>(
        game => game.Title,
        game => game.IsHiddenFromCatalog);

    private static readonly Expression<Func<LibraryEntry, bool>> PlayableEntry = BuildPlayable<LibraryEntry>(
        entry => entry.Game.Title,
        entry => entry.Game.IsHiddenFromCatalog);

    public static readonly Expression<Func<GameGenre, bool>> PlayableGenre = BuildPlayable<GameGenre>(
        row => row.Game.Title,
        row => row.Game.IsHiddenFromCatalog);

    public static readonly Expression<Func<GamePlatform, bool>> PlayablePlatform = BuildPlayable<GamePlatform>(
        row => row.Game.Title,
        row => row.Game.IsHiddenFromCatalog);

    public static readonly Expression<Func<GameTag, bool>> PlayableTag = BuildPlayable<GameTag>(
        row => row.Game.Title,
        row => row.Game.IsHiddenFromCatalog);

    public static IQueryable<Game> Playable(this IQueryable<Game> games) => games.Where(PlayableGame);

    public static IQueryable<LibraryEntry> WithPlayableGame(this IQueryable<LibraryEntry> entries) =>
        entries.Where(PlayableEntry);

    private static Expression<Func<T, bool>> BuildPlayable<T>(
        Expression<Func<T, string>> title,
        Expression<Func<T, bool>> hidden)
    {
        var parameter = title.Parameters[0];
        var titleBody = new ReplaceParameter(title.Parameters[0], parameter).Visit(title.Body)!;
        var hiddenBody = new ReplaceParameter(hidden.Parameters[0], parameter).Visit(hidden.Body)!;
        var lowered = Expression.Call(titleBody, nameof(string.ToLower), Type.EmptyTypes);
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression blocked = Expression.Constant(false);
        foreach (var fragment in BlockedTitleFragments)
        {
            blocked = Expression.OrElse(blocked, Expression.Call(lowered, contains, Expression.Constant(fragment)));
        }

        var body = Expression.AndAlso(Expression.Not(hiddenBody), Expression.Not(blocked));
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }

    public static bool IsNonGameApplication(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized = title.Trim().ToLowerInvariant();
        return BlockedTitleFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
    }
}

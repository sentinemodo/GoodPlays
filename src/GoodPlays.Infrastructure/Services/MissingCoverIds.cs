namespace GoodPlays.Infrastructure.Services;

public static class MissingCoverIds
{
    public static Guid[] FromCatalog(PaginatedCatalogResult result)
    {
        var games = result.Items.AsEnumerable();
        if (result.Groups is not null)
        {
            games = games.Concat(result.Groups.SelectMany(group => group.Items));
        }

        return games
            .SelectMany(game => new[] { game }.Concat(game.Dlc))
            .Where(game => string.IsNullOrWhiteSpace(game.CoverUrl))
            .Select(game => game.Id)
            .Distinct()
            .ToArray();
    }

    public static Guid[] FromDetail(GameDetailDto detail)
    {
        var ids = new List<Guid>();
        if (string.IsNullOrWhiteSpace(detail.CoverUrl))
        {
            ids.Add(detail.Id);
        }

        ids.AddRange(detail.Dlc.Where(dlc => string.IsNullOrWhiteSpace(dlc.CoverUrl)).Select(dlc => dlc.Id));
        return ids.Distinct().ToArray();
    }
}

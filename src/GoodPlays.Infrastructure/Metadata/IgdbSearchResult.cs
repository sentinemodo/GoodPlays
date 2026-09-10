namespace GoodPlays.Infrastructure.Metadata;

public sealed record IgdbSearchResult(
    long IgdbId,
    string Title,
    string? Slug,
    string? CoverUrl,
    string? Summary,
    DateOnly? ReleaseDate);

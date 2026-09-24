using GoodPlays.Domain.Enums;

namespace GoodPlays.Infrastructure.Metadata;

public sealed record IgdbNamedRef(long Id, string Name);

public sealed record IgdbGameDetails(
    long IgdbId,
    string Title,
    string? Slug,
    string? CoverUrl,
    string? Summary,
    DateOnly? ReleaseDate,
    string? Developer,
    string? Publisher,
    GameType GameType,
    long? ParentIgdbId,
    IReadOnlyList<IgdbNamedRef> Genres,
    IReadOnlyList<IgdbNamedRef> PlayerPerspectives,
    IReadOnlyList<IgdbNamedRef> Platforms);

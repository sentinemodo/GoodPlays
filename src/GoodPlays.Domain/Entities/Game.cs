using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class Game
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string Title { get; set; }
    public required string SortTitle { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? Summary { get; set; }
    public string? CoverUrl { get; set; }
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public GameType GameType { get; set; } = GameType.Base;
    public Guid? ParentGameId { get; set; }
    public MetadataStatus MetadataStatus { get; set; } = MetadataStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Game? ParentGame { get; set; }
    public ICollection<Game> ChildGames { get; set; } = [];
    public ICollection<GameExternalId> ExternalIds { get; set; } = [];
    public ICollection<GameGenre> GameGenres { get; set; } = [];
    public ICollection<GamePlatform> GamePlatforms { get; set; } = [];
    public ICollection<LibraryEntry> LibraryEntries { get; set; } = [];
}

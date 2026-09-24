namespace GoodPlays.Infrastructure.Services;

public sealed record TagDto(Guid Id, string Name, string Slug, string Scope);

public sealed record ShelfDto(Guid Id, string Name, string Slug, int GameCount);

public sealed record CreateTagRequest(string Name);

public sealed record CreateShelfRequest(string Name);

public interface ITagService
{
    Task<IReadOnlyList<TagDto>> GetTagsAsync(Guid? userId, CancellationToken cancellationToken);

    Task<TagDto?> CreateTagAsync(Guid userId, CreateTagRequest request, CancellationToken cancellationToken);

    Task<bool> AddTagToGameAsync(Guid userId, Guid gameId, string tagSlug, CancellationToken cancellationToken);

    Task<bool> RemoveTagFromGameAsync(Guid userId, Guid gameId, string tagSlug, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShelfDto>> GetShelvesAsync(Guid userId, CancellationToken cancellationToken);

    Task<ShelfDto?> CreateShelfAsync(Guid userId, CreateShelfRequest request, CancellationToken cancellationToken);

    Task<bool> AddGameToShelfAsync(Guid userId, string shelfSlug, Guid gameId, CancellationToken cancellationToken);
}

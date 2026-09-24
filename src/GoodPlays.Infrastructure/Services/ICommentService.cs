namespace GoodPlays.Infrastructure.Services;

public sealed record CreateCommentRequest(string Body, short? Rating);

public interface ICommentService
{
    Task<IReadOnlyList<GameCommentDto>> GetForGameAsync(Guid gameId, int page, int pageSize, CancellationToken cancellationToken);

    Task<GameCommentDto?> CreateAsync(Guid userId, Guid gameId, CreateCommentRequest request, CancellationToken cancellationToken);
}

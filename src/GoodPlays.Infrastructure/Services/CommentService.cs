using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed class CommentService(GoodPlaysDbContext dbContext) : ICommentService
{
    public async Task<IReadOnlyList<GameCommentDto>> GetForGameAsync(
        Guid gameId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        return await dbContext.GameComments
            .AsNoTracking()
            .Where(c => c.GameId == gameId && c.Visibility == Visibility.Public)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(
                dbContext.UserProfiles.AsNoTracking(),
                c => c.UserId,
                p => p.UserId,
                (c, p) => new { c, p })
            .Join(
                dbContext.Users.AsNoTracking(),
                x => x.c.UserId,
                u => u.Id,
                (x, u) => new GameCommentDto(
                    x.c.Id,
                    x.p.Username,
                    u.DisplayName,
                    x.c.Body,
                    x.c.Rating,
                    x.c.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<GameCommentDto?> CreateAsync(
        Guid userId,
        Guid gameId,
        CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return null;
        }

        var gameExists = await dbContext.Games.AnyAsync(g => g.Id == gameId, cancellationToken);
        if (!gameExists)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var comment = new GameComment
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UserId = userId,
            Body = request.Body.Trim(),
            Rating = StarRating.Normalize(request.Rating),
            Visibility = Visibility.Public,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.GameComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var profile = await dbContext.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var user = await dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == userId, cancellationToken);

        return new GameCommentDto(
            comment.Id,
            profile?.Username ?? user.Email,
            user.DisplayName,
            comment.Body,
            comment.Rating,
            comment.CreatedAt);
    }
}

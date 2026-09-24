using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed class TagService(GoodPlaysDbContext dbContext) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> GetTagsAsync(Guid? userId, CancellationToken cancellationToken) =>
        await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.Scope == TagScope.System || (userId != null && t.UserId == userId))
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.Slug, t.Scope.ToString()))
            .ToListAsync(cancellationToken);

    public async Task<TagDto?> CreateTagAsync(Guid userId, CreateTagRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return null;
        }

        var slug = SlugHelper.CreateSlug(request.Name);
        var exists = await dbContext.Tags.AnyAsync(t => t.UserId == userId && t.Slug == slug, cancellationToken);
        if (exists)
        {
            return null;
        }

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Slug = slug,
            Scope = TagScope.User
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TagDto(tag.Id, tag.Name, tag.Slug, tag.Scope.ToString());
    }

    public async Task<bool> AddTagToGameAsync(
        Guid userId,
        Guid gameId,
        string tagSlug,
        CancellationToken cancellationToken)
    {
        var tag = await dbContext.Tags
            .FirstOrDefaultAsync(t => t.Slug == tagSlug && (t.UserId == userId || t.Scope == TagScope.System), cancellationToken);
        if (tag is null)
        {
            return false;
        }

        if (await dbContext.GameTags.AnyAsync(gt => gt.GameId == gameId && gt.TagId == tag.Id, cancellationToken))
        {
            return true;
        }

        dbContext.GameTags.Add(new GameTag { GameId = gameId, TagId = tag.Id });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveTagFromGameAsync(
        Guid userId,
        Guid gameId,
        string tagSlug,
        CancellationToken cancellationToken)
    {
        var tag = await dbContext.Tags
            .FirstOrDefaultAsync(t => t.Slug == tagSlug && (t.UserId == userId || t.Scope == TagScope.System), cancellationToken);
        if (tag is null)
        {
            return false;
        }

        var link = await dbContext.GameTags
            .FirstOrDefaultAsync(gt => gt.GameId == gameId && gt.TagId == tag.Id, cancellationToken);
        if (link is null)
        {
            return true;
        }

        dbContext.GameTags.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ShelfDto>> GetShelvesAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Shelves
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Name)
            .Select(s => new ShelfDto(s.Id, s.Name, s.Slug, s.Entries.Count))
            .ToListAsync(cancellationToken);

    public async Task<ShelfDto?> CreateShelfAsync(
        Guid userId,
        CreateShelfRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return null;
        }

        var slug = SlugHelper.CreateSlug(request.Name);
        var exists = await dbContext.Shelves.AnyAsync(s => s.UserId == userId && s.Slug == slug, cancellationToken);
        if (exists)
        {
            return null;
        }

        var shelf = new Shelf
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Slug = slug,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Shelves.Add(shelf);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ShelfDto(shelf.Id, shelf.Name, shelf.Slug, 0);
    }

    public async Task<bool> AddGameToShelfAsync(
        Guid userId,
        string shelfSlug,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var shelf = await dbContext.Shelves
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Slug == shelfSlug, cancellationToken);
        if (shelf is null)
        {
            return false;
        }

        if (await dbContext.ShelfEntries.AnyAsync(se => se.ShelfId == shelf.Id && se.GameId == gameId, cancellationToken))
        {
            return true;
        }

        dbContext.ShelfEntries.Add(new ShelfEntry
        {
            ShelfId = shelf.Id,
            GameId = gameId,
            AddedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

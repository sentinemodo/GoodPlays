using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoodPlays.Infrastructure.Services;

public sealed class LibraryService(GoodPlaysDbContext dbContext) : ILibraryService
{
    public async Task<IReadOnlyList<LibraryEntryDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.LibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => new LibraryEntryDto(
                e.Id,
                e.GameId,
                e.Game.Title,
                e.Game.CoverUrl,
                e.Status,
                e.Rating,
                e.HoursPlayed,
                e.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<LibraryEntryDto?> CreateAsync(
        Guid userId,
        CreateLibraryEntryRequest request,
        CancellationToken cancellationToken)
    {
        var gameExists = await dbContext.Games.AnyAsync(g => g.Id == request.GameId, cancellationToken);
        if (!gameExists)
        {
            return null;
        }

        var duplicate = await dbContext.LibraryEntries
            .AnyAsync(e => e.UserId == userId && e.GameId == request.GameId, cancellationToken);
        if (duplicate)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var entry = new LibraryEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = request.GameId,
            Status = request.Status ?? LibraryStatus.Owned,
            Rating = request.Rating,
            HoursPlayed = request.HoursPlayed,
            Source = LibraryEntrySource.Manual,
            Visibility = Visibility.Public,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.LibraryEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        var game = await dbContext.Games
            .AsNoTracking()
            .Where(g => g.Id == request.GameId)
            .Select(g => new { g.Title, g.CoverUrl })
            .FirstAsync(cancellationToken);

        return new LibraryEntryDto(
            entry.Id,
            entry.GameId,
            game.Title,
            game.CoverUrl,
            entry.Status,
            entry.Rating,
            entry.HoursPlayed,
            entry.UpdatedAt);
    }

    public async Task<LibraryEntryDto?> UpdateAsync(
        Guid userId,
        Guid entryId,
        UpdateLibraryEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await dbContext.LibraryEntries
            .Include(e => e.Game)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        if (request.Status is not null)
        {
            entry.Status = request.Status.Value;
        }

        if (request.Rating is not null)
        {
            entry.Rating = request.Rating;
        }

        if (request.HoursPlayed is not null)
        {
            entry.HoursPlayed = request.HoursPlayed;
        }

        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LibraryEntryDto(
            entry.Id,
            entry.GameId,
            entry.Game.Title,
            entry.Game.CoverUrl,
            entry.Status,
            entry.Rating,
            entry.HoursPlayed,
            entry.UpdatedAt);
    }
}

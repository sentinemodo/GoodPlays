using GoodPlays.Domain.Enums;

namespace GoodPlays.Infrastructure.Services;

public sealed record LibraryEntryDto(
    Guid Id,
    Guid GameId,
    string GameTitle,
    string? CoverUrl,
    LibraryStatus Status,
    short? Rating,
    decimal? HoursPlayed,
    DateTimeOffset UpdatedAt);

public sealed record CreateLibraryEntryRequest(
    Guid GameId,
    LibraryStatus? Status,
    short? Rating,
    decimal? HoursPlayed,
    LibraryEntrySource? Source = null);

public sealed record UpdateLibraryEntryRequest(
    LibraryStatus? Status,
    short? Rating,
    decimal? HoursPlayed);

public interface ILibraryService
{
    Task<IReadOnlyList<LibraryEntryDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<LibraryEntryDto?> CreateAsync(
        Guid userId,
        CreateLibraryEntryRequest request,
        CancellationToken cancellationToken);

    Task<LibraryEntryDto?> UpdateAsync(
        Guid userId,
        Guid entryId,
        UpdateLibraryEntryRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken);
}

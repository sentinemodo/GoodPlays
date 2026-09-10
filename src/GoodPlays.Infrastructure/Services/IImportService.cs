using GoodPlays.Domain.Enums;

namespace GoodPlays.Infrastructure.Services;

public sealed record ImportJobDto(
    Guid Id,
    ImportModality Modality,
    ImportJobStatus Status,
    ImportStatsDocument Stats,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateTextImportRequest(string Text);

public interface IImportService
{
    Task<ImportJobDto> CreateTextImportAsync(Guid userId, string text, CancellationToken cancellationToken);

    Task ProcessTextImportAsync(Guid jobId, CancellationToken cancellationToken);

    Task<ImportJobDto?> GetJobAsync(Guid userId, Guid jobId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportJobDto>> ListJobsAsync(Guid userId, CancellationToken cancellationToken);
}

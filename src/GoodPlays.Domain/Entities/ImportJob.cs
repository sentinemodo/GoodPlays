using GoodPlays.Domain.Enums;

namespace GoodPlays.Domain.Entities;

public class ImportJob
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ImportModality Modality { get; set; }
    public ImportJobStatus Status { get; set; } = ImportJobStatus.Pending;
    public string? SourceObjectKey { get; set; }
    public string StatsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}

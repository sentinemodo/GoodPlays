using GoodPlays.Domain.Entities;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoodPlays.Infrastructure.Services;

public sealed class ImportService(
    GoodPlaysDbContext dbContext,
    IGameCatalogService gameCatalogService,
    ILibraryService libraryService,
    ILogger<ImportService> logger) : IImportService
{
    public async Task<ImportJobDto> CreateTextImportAsync(Guid userId, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Import text is required.", nameof(text));
        }

        var now = DateTimeOffset.UtcNow;
        var stats = new ImportStatsDocument { InputText = text.Trim() };
        var job = new ImportJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Modality = ImportModality.Text,
            Status = ImportJobStatus.Pending,
            StatsJson = stats.ToJson(),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ImportJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(job);
    }

    public async Task ProcessTextImportAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.ImportJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Import job {JobId} not found", jobId);
            return;
        }

        if (job.Status is ImportJobStatus.Completed or ImportJobStatus.Failed)
        {
            return;
        }

        job.Status = ImportJobStatus.Processing;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var stats = ImportStatsDocument.Parse(job.StatsJson);
        var lines = ParseLines(stats.InputText ?? string.Empty);

        foreach (var line in lines)
        {
            var lineResult = await ProcessLineAsync(job.UserId, line, cancellationToken);
            stats.Lines.Add(lineResult);

            switch (lineResult.Outcome)
            {
                case "added":
                    stats.AddedCount++;
                    break;
                case "skipped":
                    stats.SkippedCount++;
                    break;
                case "ambiguous":
                    stats.AmbiguousCount++;
                    break;
                default:
                    stats.UnmatchedCount++;
                    break;
            }
        }

        job.Status = stats.AmbiguousCount > 0 ? ImportJobStatus.AwaitingReview : ImportJobStatus.Completed;
        job.StatsJson = stats.ToJson();
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ImportJobDto?> GetJobAsync(Guid userId, Guid jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.ImportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        return job is null ? null : ToDto(job);
    }

    public async Task<IReadOnlyList<ImportJobDto>> ListJobsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var jobs = await dbContext.ImportJobs
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        return jobs.Select(ToDto).ToList();
    }

    public static IReadOnlyList<string> ParseLines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<ImportLineResult> ProcessLineAsync(Guid userId, string line, CancellationToken cancellationToken)
    {
        var results = await gameCatalogService.SearchAsync(line, cancellationToken);
        if (results.Count == 0)
        {
            return new ImportLineResult { RawLine = line, Outcome = "unmatched" };
        }

        var exactMatches = results
            .Where(r => string.Equals(r.Title, line, StringComparison.OrdinalIgnoreCase))
            .ToList();

        GameSummaryDto? chosen = exactMatches.Count switch
        {
            1 => exactMatches[0],
            > 1 => null,
            _ => results.Count == 1 ? results[0] : null
        };

        if (chosen is null)
        {
            return new ImportLineResult
            {
                RawLine = line,
                Outcome = "ambiguous",
                Candidates = results.Take(5).Select(r => new ImportMatchCandidate
                {
                    Title = r.Title,
                    GameId = r.Id == Guid.Empty ? null : r.Id,
                    IgdbId = r.IgdbId
                }).ToList()
            };
        }

        var gameId = await ResolveGameIdAsync(chosen, cancellationToken);
        if (gameId is null)
        {
            return new ImportLineResult { RawLine = line, Outcome = "unmatched", MatchedTitle = chosen.Title };
        }

        var alreadyInLibrary = await dbContext.LibraryEntries
            .AnyAsync(e => e.UserId == userId && e.GameId == gameId.Value, cancellationToken);

        if (alreadyInLibrary)
        {
            return new ImportLineResult
            {
                RawLine = line,
                Outcome = "skipped",
                MatchedTitle = chosen.Title,
                GameId = gameId,
                IgdbId = chosen.IgdbId
            };
        }

        var entry = await libraryService.CreateAsync(
            userId,
            new CreateLibraryEntryRequest(gameId.Value, LibraryStatus.Owned, null, null, LibraryEntrySource.ImportText),
            cancellationToken);

        if (entry is null)
        {
            return new ImportLineResult
            {
                RawLine = line,
                Outcome = "skipped",
                MatchedTitle = chosen.Title,
                GameId = gameId,
                IgdbId = chosen.IgdbId
            };
        }

        return new ImportLineResult
        {
            RawLine = line,
            Outcome = "added",
            MatchedTitle = entry.GameTitle,
            GameId = entry.GameId,
            IgdbId = chosen.IgdbId
        };
    }

    private async Task<Guid?> ResolveGameIdAsync(GameSummaryDto match, CancellationToken cancellationToken)
    {
        if (match.Id != Guid.Empty)
        {
            return match.Id;
        }

        if (match.IgdbId is null or <= 0)
        {
            return null;
        }

        var game = await gameCatalogService.ImportFromIgdbAsync(match.IgdbId.Value, cancellationToken);
        return game?.Id;
    }

    private static ImportJobDto ToDto(ImportJob job) =>
        new(
            job.Id,
            job.Modality,
            job.Status,
            ImportStatsDocument.Parse(job.StatsJson),
            job.CreatedAt,
            job.UpdatedAt);
}

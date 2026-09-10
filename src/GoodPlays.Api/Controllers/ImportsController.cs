using GoodPlays.Api.Services;
using GoodPlays.Domain.Enums;
using GoodPlays.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoodPlays.Api.Controllers;

[ApiController]
[Route("api/v1/imports")]
public class ImportsController(
    IImportService importService,
    IImportJobScheduler importJobScheduler,
    ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListImports(CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var jobs = await importService.ListJobsAsync(user.Id, cancellationToken);
        return Ok(jobs);
    }

    [HttpGet("{jobId:guid}")]
    public async Task<IActionResult> GetImport(Guid jobId, CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var job = await importService.GetJobAsync(user.Id, jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(job);
    }

    [HttpPost]
    public async Task<IActionResult> CreateImport(
        [FromBody] CreateImportRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (request.Modality != ImportModality.Text)
        {
            return BadRequest(new { message = "Only Text imports are supported in Phase 1." });
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { message = "text is required for Text imports." });
        }

        var job = await importService.CreateTextImportAsync(user.Id, request.Text, cancellationToken);
        importJobScheduler.ScheduleParseText(job.Id);

        return Accepted($"/api/v1/imports/{job.Id}", job);
    }

    public sealed record CreateImportRequest(ImportModality Modality, string? Text);
}

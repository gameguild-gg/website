using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.Assets.Commands;
using GameGuild.Assets.Queries;

namespace GameGuild.Assets.Controllers;

/// <summary>
/// Admin controller for asset moderation.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("assets/admin")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/admin/assets")]
[Authorize(Policy = "RequireAdminRole")]
public class AssetsAdminController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <summary>
    /// Get asset/document statistics for document center dashboards.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(AssetStatisticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(CancellationToken ct = default)
    {
        var result = await sender.Send(new GetAssetStatisticsQuery(), ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Export asset/document statistics as CSV or PDF.
    /// </summary>
    [HttpGet("statistics:export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportStatistics(
        [FromQuery] string format = "csv",
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ExportAssetStatisticsQuery(format), ct).ConfigureAwait(false);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Get moderation queue.
    /// </summary>
    [HttpGet("moderation-queue")]
    public async Task<IActionResult> GetModerationQueue(
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var query = new GetModerationQueueQuery(limit);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Get reports for an asset.
    /// </summary>
    [HttpGet("{id:guid}/reports")]
    public async Task<IActionResult> GetAssetReports(
        Guid id,
        CancellationToken ct = default)
    {
        var query = new GetAssetReportsQuery(id);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Review a moderation report.
    /// </summary>
    [HttpPost("reports/{reportId:guid}:review")]
    public async Task<IActionResult> ReviewReport(
        Guid reportId,
        [FromBody] ReviewReportRequest request,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        var command = new ReviewReportCommand(
            reportId,
            Actor.SubjectIdAsGuid.Value,
            request.Decision,
            request.Notes);

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (result == null)
        {
            return BadRequest(new ProblemDetails { Title = "Unable to review report" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Force delete an asset (admin override).
    /// </summary>
    [HttpPost("{id:guid}:force-delete")]
    public async Task<IActionResult> ForceDeleteAsset(
        Guid id,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        var command = new DeleteAssetCommand(id, Actor.SubjectIdAsGuid.Value, ForceDelete: true);

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails { Title = "Unable to delete asset" });
        }

        return NoContent();
    }

    /// <summary>
    /// List admin assets with optional status filter.
    /// Use status=pending-virus-scan or status=pending-moderation to filter.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAdminAssets(
        [FromServices] IAssetContentRepository contentRepository,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        if (status == "pending-virus-scan")
        {
            var virusScanItems = await contentRepository.GetPendingVirusScanAsync(limit, ct).ConfigureAwait(false);
            return Ok(virusScanItems.Select(x => new
            {
                x.Id,
                x.ContentHash,
                x.BucketName,
                x.ObjectKey,
                x.MimeType,
                x.SizeBytes,
                x.CreatedAt
            }));
        }

        if (status == "pending-moderation")
        {
            var moderationItems = await contentRepository.GetPendingModerationAsync(limit, ct).ConfigureAwait(false);
            return Ok(moderationItems.Select(x => new
            {
                x.Id,
                x.ContentHash,
                x.BucketName,
                x.ObjectKey,
                x.MimeType,
                x.SizeBytes,
                x.ModerationStatus,
                x.CreatedAt
            }));
        }

        // Default: return empty or all based on requirements
        return Ok(new { message = "Use status=pending-virus-scan or status=pending-moderation to filter" });
    }

    /// <summary>
    /// Get garbage collection candidates.
    /// </summary>
    [HttpGet("gc-candidates")]
    public async Task<IActionResult> GetGarbageCollectionCandidates(
        [FromServices] IAssetContentRepository contentRepository,
        [FromQuery] int gracePeriodHours = 24,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var items = await contentRepository.GetGarbageCollectionCandidatesAsync(
            TimeSpan.FromHours(gracePeriodHours),
            limit,
            ct).ConfigureAwait(false);

        return Ok(items.Select(x => new
        {
            x.Id,
            x.ContentHash,
            x.BucketName,
            x.ObjectKey,
            x.SizeBytes,
            x.MarkedForDeletionAt
        }));
    }

    /// <summary>
    /// Run virus scan on an asset.
    /// </summary>
    [HttpPost("{contentId:guid}:run-virus-scan")]
    public async Task<IActionResult> UpdateVirusScanStatus(
        Guid contentId,
        [FromBody] UpdateVirusScanRequest request,
        [FromServices] IAssetContentRepository contentRepository,
        CancellationToken ct = default)
    {
        var content = await sender.Send(new UpdateAssetVirusScanStatusCommand(
            contentId,
            request.Status,
            request.ScanResult), ct).ConfigureAwait(false);
        if (content == null) return NotFound();

        return Ok(new { content.Id, content.VirusScanStatus });
    }

    #region Garbage Collection & Maintenance

    /// <summary>
    /// Get the current retention candidate report.
    /// </summary>
    [HttpGet("retention")]
    [ProducesResponseType(typeof(AssetRetentionReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRetentionReport(
        [FromQuery] int gracePeriodHours = 24,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetAssetRetentionReportQuery(gracePeriodHours, limit),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Trigger manual garbage collection.
    /// </summary>
    /// <remarks>
    /// Runs the garbage collection process manually instead of waiting for the scheduled background job.
    /// Only deletes content that has been marked for deletion and past the grace period.
    /// </remarks>
    [HttpPost(":run-gc")]
    [HttpPost("retention:run")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerGarbageCollection(
        [FromQuery] int gracePeriodHours = 24,
        [FromQuery] int limit = 100,
        [FromQuery] bool dryRun = false,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new RunAssetRetentionCommand(gracePeriodHours, limit, dryRun),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Mark an asset content as non-deletable (legal hold).
    /// </summary>
    /// <remarks>
    /// Prevents the asset from being garbage collected, even if all references are deleted.
    /// Use for legal holds, compliance requirements, or audit preservation.
    /// </remarks>
    [HttpPost("{contentId:guid}:mark-undeletable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsNonDeletable(
        Guid contentId,
        [FromBody] MarkNonDeletableRequest? request,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new SetAssetLegalHoldCommand(contentId, Enabled: true, request?.Reason),
            ct).ConfigureAwait(false);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Remove the non-deletable flag from an asset.
    /// </summary>
    [HttpPost("{contentId:guid}:unmark-undeletable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveNonDeletable(
        Guid contentId,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new SetAssetLegalHoldCommand(contentId, Enabled: false),
            ct).ConfigureAwait(false);

        return result is null ? NotFound() : Ok(result);
    }

    #endregion

    #region Content Moderation

    /// <summary>
    /// Review and moderate content directly.
    /// </summary>
    /// <remarks>
    /// Unlike report review which handles user reports, this endpoint allows
    /// direct moderation of content by admins for proactive moderation workflows.
    /// </remarks>
    [HttpPost("{contentId:guid}:review-moderation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewContentModeration(
        Guid contentId,
        [FromBody] ContentModerationRequest request,
        [FromServices] IAssetContentRepository contentRepository,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        var content = await sender.Send(new ReviewAssetContentModerationCommand(
            contentId,
            request.Status,
            Actor.SubjectIdAsGuid.Value,
            request.Labels,
            request.Notes), ct).ConfigureAwait(false);
        if (content == null) return NotFound();

        return Ok(new
        {
            content.Id,
            content.ModerationStatus,
            ReviewedBy = Actor.SubjectIdAsGuid.Value,
            ReviewedAt = SystemClock.UtcNow
        });
    }

    #endregion
}

public sealed record ReviewReportRequest(
    ReviewDecision Decision,
    string? Notes = null);

public sealed record UpdateVirusScanRequest(
    VirusScanStatus Status,
    string? ScanResult = null);

public sealed record MarkNonDeletableRequest(
    string? Reason = null);

public sealed record ContentModerationRequest(
    ModerationStatus Status,
    string[]? Labels = null,
    string? Notes = null);

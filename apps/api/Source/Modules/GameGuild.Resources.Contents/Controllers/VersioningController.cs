using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;

namespace GameGuild.Resources.Contents;

/// <summary>
/// REST API controller for content versioning
/// </summary>
[Microsoft.AspNetCore.Http.Tags("resources/contents/versioning")]
[Route("api/contents/[controller]")]
[Authorize]
public class VersioningController : BaseApiController
{
    private readonly IContentVersioningService _versioningService;
    private readonly ISender _sender;

    public VersioningController(IContentVersioningService versioningService, ISender sender)
    {
        _versioningService = versioningService;
        _sender = sender;
    }

    /// <summary>
    /// Create a new draft version
    /// </summary>
    [HttpPost("drafts")]
    [ProducesResponseType(typeof(ContentVersionDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateDraft([FromBody] CreateDraftRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateContentDraftCommand(request), ct).ConfigureAwait(false);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetVersion), new { versionId = result.Value.Id }, ContentVersionDto.FromEntity(result.Value))
            : BadRequest(result.Error);
    }

    /// <summary>
    /// Update a draft version
    /// </summary>
    [HttpPut("drafts/{versionId:guid}")]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateDraft(Guid versionId, [FromBody] UpdateDraftRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new UpdateContentDraftCommand(versionId, request), ct).ConfigureAwait(false);

        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get a specific version
    /// </summary>
    [HttpGet("{versionId:guid}")]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _versioningService.GetVersionAsync(versionId, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : NotFound(result.Error);
    }

    /// <summary>
    /// Get version history for an entity
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId:guid}/history")]
    [ProducesResponseType(typeof(IEnumerable<ContentVersionDto>), 200)]
    public async Task<IActionResult> GetVersionHistory(string entityType, Guid entityId, CancellationToken ct)
    {
        var result = await _versioningService.GetVersionHistoryAsync(entityId, entityType, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var dtos = result.Value.Select(ContentVersionDto.FromEntity);
        return Ok(dtos);
    }

    /// <summary>
    /// Get the current published version for an entity
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId:guid}/current")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetCurrentVersion(string entityType, Guid entityId, CancellationToken ct)
    {
        var result = await _versioningService.GetCurrentVersionAsync(entityId, entityType, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : NotFound(result.Error);
    }

    /// <summary>
    /// Get a specific version by number
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId:guid}/version/{versionNumber:int}")]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetVersionByNumber(string entityType, Guid entityId, int versionNumber, CancellationToken ct)
    {
        var result = await _versioningService.GetVersionByNumberAsync(entityId, entityType, versionNumber, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : NotFound(result.Error);
    }

    /// <summary>
    /// Submit a draft for review
    /// </summary>
    [HttpPost("{versionId:guid}/submit-for-review")]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SubmitForReview(Guid versionId, CancellationToken ct)
    {
        var result = await _sender.Send(new SubmitContentForReviewCommand(versionId), ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get versions pending review
    /// </summary>
    [HttpGet("pending-review")]
    [ProducesResponseType(typeof(IEnumerable<ContentVersionDto>), 200)]
    public async Task<IActionResult> GetPendingReview(
        [FromQuery] string? entityType = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await _versioningService.GetPendingReviewAsync(entityType, skip, take, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var dtos = result.Value.Select(ContentVersionDto.FromEntity);
        return Ok(dtos);
    }

    /// <summary>
    /// Approve a version
    /// </summary>
    [HttpPost("{versionId:guid}/approve")]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Approve(Guid versionId, [FromBody] ReviewRequest? request, CancellationToken ct)
    {
        var result = await _sender.Send(new ApproveContentVersionCommand(versionId, request?.ReviewNotes), ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// Reject a version
    /// </summary>
    [HttpPost("{versionId:guid}/reject")]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Reject(Guid versionId, [FromBody] ReviewRequest? request, CancellationToken ct)
    {
        var result = await _sender.Send(new RejectContentVersionCommand(versionId, request?.ReviewNotes), ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// Publish a version
    /// </summary>
    [HttpPost("{versionId:guid}/publish")]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Publish(Guid versionId, CancellationToken ct)
    {
        var result = await _sender.Send(new PublishContentVersionCommand(versionId), ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// Schedule a version for publishing
    /// </summary>
    [HttpPost("{versionId:guid}/schedule")]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SchedulePublish(Guid versionId, [FromBody] ScheduleRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new ScheduleContentPublishCommand(versionId, request.ScheduledAt), ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// Cancel scheduled publishing
    /// </summary>
    [HttpPost("{versionId:guid}/cancel-schedule")]
    [ProducesResponseType(typeof(ContentVersionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CancelSchedule(Guid versionId, CancellationToken ct)
    {
        var result = await _sender.Send(new CancelContentPublishCommand(versionId), ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(ContentVersionDto.FromEntity(result.Value))
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// Compare two versions
    /// </summary>
    [HttpGet("compare")]
    [ProducesResponseType(typeof(ContentVersionDiff), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Compare(
        [FromQuery] Guid versionId1,
        [FromQuery] Guid versionId2,
        CancellationToken ct)
    {
        var result = await _versioningService.CompareVersionsAsync(versionId1, versionId2, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// Rollback to a previous version
    /// </summary>
    [HttpPost("entity/{entityType}/{entityId:guid}/rollback")]
    [ProducesResponseType(typeof(ContentVersionDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Rollback(
        string entityType,
        Guid entityId,
        [FromBody] RollbackRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new RollbackContentVersionCommand(entityId, entityType, request.TargetVersionNumber, request.Reason),
            ct).ConfigureAwait(false);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetVersion), new { versionId = result.Value.Id }, ContentVersionDto.FromEntity(result.Value))
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// Add a review to a version
    /// </summary>
    [HttpPost("{versionId:guid}/reviews")]
    [ProducesResponseType(typeof(ContentVersionReviewDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddReview(Guid versionId, [FromBody] AddReviewRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new AddContentVersionReviewCommand(versionId, request), ct).ConfigureAwait(false);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetVersion), new { versionId }, ContentVersionReviewDto.FromEntity(result.Value))
            : result.Error.Code == "ContentVersioning.NotFound" ? NotFound(result.Error) : BadRequest(result.Error);
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed record CreateDraftRequest(
    Guid EntityId,
    string EntityType,
    string Title,
    Guid CreatedBy,
    string? Summary = null,
    string? Body = null,
    string? Metadata = null,
    string? ChangeNotes = null
);

public sealed record UpdateDraftRequest(
    string? Title = null,
    string? Summary = null,
    string? Body = null,
    string? Metadata = null,
    string? ChangeNotes = null
);

public sealed record ReviewRequest(string? ReviewNotes = null);

public sealed record ScheduleRequest(DateTime ScheduledAt);

public sealed record RollbackRequest(int TargetVersionNumber, string? Reason = null);

public sealed record AddReviewRequest(
    ContentReviewDecision Decision,
    string? Feedback = null,
    string? Suggestions = null
);

public sealed record ContentVersionDto(
    Guid Id,
    Guid EntityId,
    string EntityType,
    int VersionNumber,
    string Title,
    string? Summary,
    string? Body,
    string? Metadata,
    ContentVersionStatus Status,
    Guid CreatedBy,
    string? ChangeNotes,
    DateTime CreatedAt,
    DateTime? SubmittedForReviewAt,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    DateTime? PublishedAt,
    Guid? PublishedBy,
    DateTime? ScheduledPublishAt,
    bool IsCurrentVersion
)
{
    public static ContentVersionDto FromEntity(ContentVersion v) => new(
        v.Id,
        v.EntityId,
        v.EntityType,
        v.VersionNumber,
        v.Title,
        v.Summary,
        v.Body,
        v.Metadata,
        v.Status,
        v.CreatedBy,
        v.ChangeNotes,
        v.CreatedAt,
        v.SubmittedForReviewAt,
        v.ReviewedBy,
        v.ReviewedAt,
        v.ReviewNotes,
        v.PublishedAt,
        v.PublishedBy,
        v.ScheduledPublishAt,
        v.IsCurrentVersion
    );
}

public sealed record ContentVersionReviewDto(
    Guid Id,
    Guid ContentVersionId,
    Guid ReviewerId,
    ContentReviewDecision Decision,
    string? Feedback,
    string? Suggestions,
    DateTime CreatedAt
)
{
    public static ContentVersionReviewDto FromEntity(ContentVersionReview r) => new(
        r.Id,
        r.ContentVersionId,
        r.ReviewerId,
        r.Decision,
        r.Feedback,
        r.Suggestions,
        r.CreatedAt
    );
}

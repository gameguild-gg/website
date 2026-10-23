using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Before/after value snapshots for all mutations (issue #176): searchable value history with
///     field-level difference analysis, large-object checksums and truncation, structured
///     comparison of nested data, delta-compressed storage with deduplication accounting,
///     configurable retention with an archive tier and legal holds, change impact and value trend
///     analysis, and plan-based rollback for critical data recovery.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/audit/value-snapshots")]
[Route("api/audit/value-snapshots")]
[Tags("compliance/audit/value-snapshots")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ValueSnapshotController(
    IValueSnapshotQueryService queryService,
    IValueSnapshotAnalysisService analysisService,
    IValueSnapshotRetentionService retentionService,
    IValueSnapshotRollbackService rollbackService,
    ISender sender) : ControllerBase
{
    /// <summary>Searches the value snapshot store by entity, key, changed field, value pattern, actor, and time window.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ValueSnapshotSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ValueSnapshotSearchResult>> Search(
        [FromQuery] ValueSnapshotSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await queryService.SearchAsync(request, cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Returns one snapshot with its structured field-level difference analysis.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ValueSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ValueSnapshotResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await queryService.GetAsync(id, cancellationToken).ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Point-in-time value history for one entity field (all fields default to the most changed one).</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ValueHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ValueHistoryResponse>> GetHistory(
        [FromQuery(Name = "entityName")] string entityName,
        [FromQuery(Name = "keyId")] string keyId,
        [FromQuery(Name = "field")] string? field,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await queryService.GetHistoryAsync(entityName, keyId, field, cancellationToken).ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Value trend analysis for one field over a time window with statistical reporting.</summary>
    [HttpGet("trends")]
    [ProducesResponseType(typeof(ValueTrendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ValueTrendResponse>> GetTrend(
        [FromQuery(Name = "entityName")] string entityName,
        [FromQuery(Name = "keyId")] string keyId,
        [FromQuery(Name = "field")] string field,
        [FromQuery(Name = "fromUtc")] DateTime fromUtc,
        [FromQuery(Name = "toUtc")] DateTime toUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await analysisService.GetTrendAsync(entityName, keyId, field, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Change impact analysis: the cascade of one mutation across its operation and entity lineage.</summary>
    [HttpGet("impact/{snapshotId:guid}")]
    [ProducesResponseType(typeof(ChangeImpactResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChangeImpactResponse>> GetImpact(Guid snapshotId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await analysisService.GetImpactAsync(snapshotId, cancellationToken).ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Storage accounting: delta/full share, truncation counts, archive tier, deduplication savings.</summary>
    [HttpGet("storage")]
    [ProducesResponseType(typeof(ValueSnapshotStorageOverview), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValueSnapshotStorageOverview>> GetStorage(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await queryService.GetStorageOverviewAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Returns the tenant's value snapshot retention policy, when configured.</summary>
    [HttpGet("retention/policy")]
    [ProducesResponseType(typeof(ValueSnapshotRetentionPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ValueSnapshotRetentionPolicyResponse>> GetRetentionPolicy(CancellationToken cancellationToken)
    {
        try
        {
            var response = await retentionService.GetPolicyAsync(cancellationToken).ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Creates or updates the value snapshot retention policy (retention days, archive tier, entity overrides, legal hold).</summary>
    [HttpPut("retention/policy")]
    [ProducesResponseType(typeof(ValueSnapshotRetentionPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ValueSnapshotRetentionPolicyResponse>> ConfigureRetentionPolicy(
        [FromBody] ConfigureValueSnapshotRetentionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new ConfigureValueSnapshotRetentionCommand(request), cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Runs one retention pass now: deletion (respecting legal holds), archive-tier stripping, and deduplication compaction. Use <c>DryRun</c> to preview.</summary>
    [HttpPost("retention/enforce")]
    [ProducesResponseType(typeof(ValueSnapshotRetentionExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ValueSnapshotRetentionExecutionResponse>> EnforceRetention(
        [FromBody] EnforceValueSnapshotRetentionRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await sender.Send(
                new EnforceValueSnapshotRetentionCommand(request ?? new EnforceValueSnapshotRetentionRequest()), cancellationToken)
                .ConfigureAwait(false);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Builds a rollback plan restoring one entity to its recorded state at a point in time.</summary>
    [HttpPost("rollback-plans")]
    [ProducesResponseType(typeof(ValueSnapshotRollbackPlanResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ValueSnapshotRollbackPlanResponse>> CreateRollbackPlan(
        [FromBody] CreateValueSnapshotRollbackPlanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await sender.Send(new CreateValueSnapshotRollbackPlanCommand(request), cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetRollbackPlan), new { planId = plan.Id }, plan);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotRollbackException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Rollback plan rejected",
                Detail = exception.Reason
            });
        }
    }

    /// <summary>Returns one rollback plan with its reconstructed basis state.</summary>
    [HttpGet("rollback-plans/{planId:guid}")]
    [ProducesResponseType(typeof(ValueSnapshotRollbackPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ValueSnapshotRollbackPlanResponse>> GetRollbackPlan(Guid planId, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await rollbackService.GetPlanAsync(planId, cancellationToken).ConfigureAwait(false);
            return plan is null ? NotFound() : Ok(plan);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Executes a rollback plan. Fails closed while newer changes exist after the basis snapshot unless explicitly accepted.</summary>
    [HttpPost("rollback-plans/{planId:guid}:execute")]
    [ProducesResponseType(typeof(ValueSnapshotRollbackPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ValueSnapshotRollbackPlanResponse>> ExecuteRollbackPlan(
        Guid planId, [FromBody] ExecuteValueSnapshotRollbackRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(
                new ExecuteValueSnapshotRollbackCommand(planId, request ?? new ExecuteValueSnapshotRollbackRequest()), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotRollbackException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Rollback execution failed",
                Detail = exception.Reason
            });
        }
    }

    private ActionResult ValidationProblem(ValueSnapshotValidationException exception)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var (key, messages) in exception.Errors)
        {
            modelState.AddModelError(key, string.Join(" ", messages));
        }

        return BadRequest(new ValidationProblemDetails(modelState) { Status = StatusCodes.Status400BadRequest });
    }
}

using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Audit query DSL endpoints: execute typed filter queries over the audit stores
///     (authentication attempts, permission audit logs, general audit logs), manage saved
///     views with role-based access, and list built-in investigation templates.
///     Every action requires the SystemAdmin policy, matching the sibling audit controllers.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("compliance/audit/queries")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/audit-queries")]
[Authorize(Policy = Policies.AuditRead)]
public class AuditQueryController(
    ISender sender,
    IAuditQueryDslParser parser,
    IAuditQueryDslExecutor executor,
    ISavedAuditQueryService savedQueryService,
    IAuditService auditService,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     Execute an audit query DSL expression (JSON or light text syntax) and return a
    ///     data-masked, paginated page of matching audit entries plus aggregates over the full
    ///     matched set. Execution is bounded by a row-scan cap and a wall-clock timeout.
    /// </summary>
    [HttpPost(":execute")]
    [ProducesResponseType(typeof(AuditQueryDslExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditQueryDslExecutionResult>> ExecuteQuery(
        [FromBody] AuditQueryExecuteApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = RequireUserId();
        if (adminUserId is null)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await sender.Send(
            new ExecuteAuditQueryCommand(
                adminUserId.Value,
                ResolveTenantScope(),
                request.QueryJson,
                request.QueryText,
                request.Skip,
                request.Take,
                request.TimeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        return ToActionResult(result);
    }

    /// <summary>
    ///     Paged-results GET variant of the DSL executor for the light text syntax
    ///     (e.g. GET /v1/audit-queries:results?query=actor:alice AND severity:Critical).
    /// </summary>
    [HttpGet(":results")]
    [ProducesResponseType(typeof(AuditQueryDslExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditQueryDslExecutionResult>> GetQueryResults(
        [FromQuery(Name = "query")] string? queryText,
        [FromQuery] AuditQueryPagingParameters paging,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = RequireUserId();
        if (adminUserId is null)
        {
            return Unauthorized("User not authenticated");
        }

        if (string.IsNullOrWhiteSpace(queryText))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing query",
                Detail = "Provide the light-syntax query in the 'query' query-string parameter.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var parsed = parser.ParseText(queryText);
        if (parsed.IsFailure)
        {
            return BadRequest(ProblemDetailsMapper.ToProblemDetails(parsed.Error));
        }

        var execution = await executor.ExecuteAsync(
            new AuditQueryDslExecutionRequest
            {
                Query = parsed.Value,
                Skip = paging.Skip,
                Take = paging.Take,
                ActingUserId = adminUserId.Value,
                TenantScope = ResolveTenantScope()
            },
            cancellationToken).ConfigureAwait(false);

        if (execution.IsSuccess)
        {
            await auditService.LogAdminActionAsync(
                adminUserId.Value,
                "AuditQueryExecuted",
                $"Audit query executed (GET): {execution.Value.TotalCount} matching events from {execution.Value.RowsScanned} scanned rows",
                new { execution.Value.TotalCount, execution.Value.RowsScanned, execution.Value.DurationMs, QueryText = queryText }).ConfigureAwait(false);
        }

        return ToActionResult(execution);
    }

    /// <summary>
    ///     Validate a DSL query without executing it and return its canonical JSON form.
    ///     Saved-view editors use this to fail closed before persisting a query.
    /// </summary>
    [HttpPost(":validate")]
    [ProducesResponseType(typeof(AuditQueryValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditQueryValidationResponse>> ValidateQuery(
        [FromBody] AuditQueryValidateApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (RequireUserId() is null)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await sender.Send(
            new ValidateAuditQueryCommand(request.QueryJson, request.QueryText),
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Built-in role-based query templates for common investigation and compliance scenarios.
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditQueryTemplate>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<IReadOnlyList<AuditQueryTemplate>> GetTemplates() => Ok(AuditQueryTemplateCatalog.Templates);

    /// <summary>
    ///     List saved audit query views visible to the acting user: their own private views
    ///     plus every shared view within the caller's tenant.
    /// </summary>
    [HttpGet("saved")]
    [ProducesResponseType(typeof(IReadOnlyList<SavedAuditQueryViewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SavedAuditQueryViewResponse>>> ListSavedQueries(
        CancellationToken cancellationToken = default)
    {
        var adminUserId = RequireUserId();
        if (adminUserId is null)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await savedQueryService.ListAsync(adminUserId.Value, ResolveTenantId(), cancellationToken).ConfigureAwait(false);
        return ToActionResult(result);
    }

    /// <summary>
    ///     Create a saved audit query view. The query is validated (and tenant-scope checked)
    ///     before persistence; a creation revision snapshot is recorded.
    /// </summary>
    [HttpPost("saved")]
    [ProducesResponseType(typeof(SavedAuditQueryViewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SavedAuditQueryViewResponse>> CreateSavedQuery(
        [FromBody] SaveSavedAuditQueryApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = RequireUserId();
        if (adminUserId is null)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await sender.Send(
            new CreateSavedAuditQueryCommand(adminUserId.Value, ResolveTenantId(), ToSaveInput(request)),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ToActionResult(result);
    }

    /// <summary>
    ///     Get one saved view. Private views are only visible to their owner.
    /// </summary>
    [HttpGet("saved/{id:guid}")]
    [ProducesResponseType(typeof(SavedAuditQueryViewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedAuditQueryViewResponse>> GetSavedQuery(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = RequireUserId();
        if (adminUserId is null)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await savedQueryService.GetAsync(id, adminUserId.Value, cancellationToken).ConfigureAwait(false);
        return ToActionResult(result);
    }

    /// <summary>
    ///     Full revision history of a saved view: who changed what, when, and the exact query
    ///     payload of every revision. Revisions survive deletion of the view itself.
    /// </summary>
    [HttpGet("saved/{id:guid}/revisions")]
    [ProducesResponseType(typeof(IReadOnlyList<SavedAuditQueryRevisionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SavedAuditQueryRevisionResponse>>> GetSavedQueryRevisions(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = RequireUserId();
        if (adminUserId is null)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await savedQueryService.GetRevisionsAsync(id, adminUserId.Value, cancellationToken).ConfigureAwait(false);
        return ToActionResult(result);
    }

    /// <summary>
    ///     Update a saved view (owner only). A new revision snapshot is recorded.
    /// </summary>
    [HttpPut("saved/{id:guid}")]
    [ProducesResponseType(typeof(SavedAuditQueryViewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedAuditQueryViewResponse>> UpdateSavedQuery(
        Guid id,
        [FromBody] SaveSavedAuditQueryApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = RequireUserId();
        if (adminUserId is null)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await sender.Send(
            new UpdateSavedAuditQueryCommand(id, adminUserId.Value, ToSaveInput(request)),
            cancellationToken).ConfigureAwait(false);

        return ToActionResult(result);
    }

    /// <summary>
    ///     Delete a saved view (owner only). Its revision history is retained for audit.
    /// </summary>
    [HttpDelete("saved/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSavedQuery(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = RequireUserId();
        if (adminUserId is null)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await sender.Send(
            new DeleteSavedAuditQueryCommand(id, adminUserId.Value),
            cancellationToken).ConfigureAwait(false);

        return ToActionResult(result);
    }

    /// <summary>
    ///     Execute a saved view by identifier (owner or shared), paging over its canonical query.
    ///     Execution usage and attribution are recorded on the view and in the audit log.
    /// </summary>
    [HttpPost("saved/{id:guid}:execute")]
    [ProducesResponseType(typeof(AuditQueryDslExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditQueryDslExecutionResult>> ExecuteSavedQuery(
        Guid id,
        [FromBody] AuditQueryPagingApiRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = RequireUserId();
        if (adminUserId is null)
        {
            return Unauthorized("User not authenticated");
        }

        var result = await sender.Send(
            new ExecuteSavedAuditQueryCommand(
                id,
                adminUserId.Value,
                ResolveTenantScope(),
                request?.Skip ?? 0,
                request?.Take ?? 50),
            cancellationToken).ConfigureAwait(false);

        return ToActionResult(result);
    }

    private Guid? RequireUserId() => actorContextAccessor.ActorContext.SubjectIdAsGuid;

    private Guid? ResolveTenantId() => actorContextAccessor.ActorContext.TenantId;

    /// <summary>
    ///     Tenant scope clamp for defense in depth: even if this controller's policy were widened,
    ///     a non-system-admin caller would still be pinned to their own tenant's audit rows.
    /// </summary>
    private Guid? ResolveTenantScope() =>
        actorContextAccessor.ActorContext.IsSystemAdmin
            ? null
            : actorContextAccessor.ActorContext.TenantId;

    private static SaveSavedAuditQueryInput ToSaveInput(SaveSavedAuditQueryApiRequest request) => new()
    {
        Name = request.Name,
        Description = request.Description,
        IsShared = request.IsShared,
        QueryJson = request.QueryJson,
        QueryText = request.QueryText
    };
}

/// <summary>API request for POST /audit-queries:execute.</summary>
public sealed class AuditQueryExecuteApiRequest
{
    /// <summary>Structured query DSL JSON: {"filter":{"and":[{"field":"actor","op":"eq","value":"alice"}]}}.</summary>
    public string? QueryJson { get; set; }

    /// <summary>Light text syntax: actor:alice AND action:granted AFTER 2026-01-01.</summary>
    public string? QueryText { get; set; }

    public int Skip { get; set; }

    public int Take { get; set; } = 50;

    /// <summary>Requested execution timeout in seconds (1-30, default 10).</summary>
    public int? TimeoutSeconds { get; set; }
}

/// <summary>API request for POST /audit-queries:validate.</summary>
public sealed class AuditQueryValidateApiRequest
{
    public string? QueryJson { get; set; }

    public string? QueryText { get; set; }
}

/// <summary>Paging-only payload for executing a saved view.</summary>
public sealed class AuditQueryPagingApiRequest
{
    public int Skip { get; set; }

    public int Take { get; set; } = 50;
}

/// <summary>Paging parameters for the GET paged-results variant.</summary>
public sealed class AuditQueryPagingParameters
{
    public int Skip { get; set; }

    public int Take { get; set; } = 50;
}

/// <summary>API request for creating or updating a saved audit query view.</summary>
public sealed class SaveSavedAuditQueryApiRequest
{
    /// <summary>Display name (1-200 characters, unique within the tenant).</summary>
    public required string Name { get; set; }

    /// <summary>Optional description (max 1000 characters).</summary>
    public string? Description { get; set; }

    /// <summary>When true, every audit-reader in the tenant can see and execute the view.</summary>
    public bool IsShared { get; set; }

    /// <summary>Structured query DSL JSON to persist.</summary>
    public string? QueryJson { get; set; }

    /// <summary>Light text syntax to persist (stored canonically after validation).</summary>
    public string? QueryText { get; set; }
}

/// <summary>Outcome of query validation.</summary>
public sealed class AuditQueryValidationResponse
{
    public bool IsValid { get; set; }

    /// <summary>Canonical JSON form of the parsed query (what a saved view would store).</summary>
    public string CanonicalJson { get; set; } = string.Empty;

    /// <summary>Field names referenced by the query.</summary>
    public List<string> Fields { get; set; } = [];

    /// <summary>Validation failure description when <see cref="IsValid"/> is false.</summary>
    public string? Error { get; set; }
}

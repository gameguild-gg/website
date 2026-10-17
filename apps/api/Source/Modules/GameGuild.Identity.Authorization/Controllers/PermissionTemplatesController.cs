using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     API controller for the permission template lifecycle (issues #369/#370):
///     template CRUD (guarded + versioned + audited), role templates, tenant
///     instantiation with dry-run previews, version history, batched migrations with
///     durable progress reporting and rollback.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/permission-templates")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/permission-templates")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionTemplatesController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Lists permission templates with their role keys and current version.
    /// </summary>
    /// <param name="includeInactive">Include inactive templates.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The templates, ordered by category then name.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Requires tenant or system administration.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionTemplateSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListTemplates(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var templates = await sender.Send(
            new ListPermissionTemplatesQuery { IncludeInactive = includeInactive, Category = category },
            cancellationToken).ConfigureAwait(false);

        return Ok(templates);
    }

    /// <summary>
    ///     Creates a permission template. The initial permission/role set is snapshotted
    ///     as version 1 and the creation is audited.
    /// </summary>
    /// <param name="command">The create command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The created template with its version.</response>
    /// <response code="400">Invalid request (empty set, duplicate name).</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">System templates require system administration.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PermissionTemplateMutationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTemplate(
        [FromBody] CreatePermissionTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Updates a permission template. Every accepted update appends a new version
    ///     snapshot with the computed diff and bumps the security version of every
    ///     tenant holding an active instantiation.
    /// </summary>
    /// <param name="templateId">The template id.</param>
    /// <param name="command">The update command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The updated template with its new version.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">System templates are immutable; tenant templates require administration.</response>
    [HttpPut("{templateId:guid}")]
    [ProducesResponseType(typeof(PermissionTemplateMutationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateTemplate(
        Guid templateId,
        [FromBody] UpdatePermissionTemplateCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TemplateId != templateId)
        {
            return BadRequest("The route template id must match the command template id.");
        }

        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Deletes a permission template. Templates with active instantiations are
    ///     pinned (fail closed) and must be migrated away first.
    /// </summary>
    /// <param name="templateId">The template id.</param>
    /// <param name="reason">Reason recorded in the audit log.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Deletion succeeded.</response>
    /// <response code="404">Template not found.</response>
    /// <response code="409">The template has active instantiations.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">System templates cannot be deleted.</response>
    [HttpDelete("{templateId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteTemplate(
        Guid templateId,
        [FromQuery] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var deleted = await sender.Send(
            new DeletePermissionTemplateCommand { TemplateId = templateId, Reason = reason },
            cancellationToken).ConfigureAwait(false);

        return deleted ? Ok(new { Deleted = true }) : NotFound(new { Message = $"Permission template {templateId} not found." });
    }

    /// <summary>
    ///     Lists the append-only version history of a template (issue #370), newest first.
    /// </summary>
    /// <param name="templateId">The template id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The version snapshots.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Requires tenant or system administration.</response>
    [HttpGet("{templateId:guid}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionTemplateVersionSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetVersions(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var versions = await sender.Send(
            new GetPermissionTemplateVersionsQuery { TemplateId = templateId },
            cancellationToken).ConfigureAwait(false);

        return Ok(versions);
    }

    /// <summary>
    ///     Dry-run preview of instantiating a template into a tenant: per-target
    ///     permission grants and role assignments that would apply, without mutating
    /// anything.
    /// </summary>
    /// <param name="templateId">The template id.</param>
    /// <param name="query">The preview targets.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The instantiation preview (nothing applied).</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Requires same-tenant or system administration.</response>
    [HttpPost("{templateId:guid}:instantiate-preview")]
    [NoBusinessMutationEndpoint("This dry-run preview computes the instantiation plan without granting or revoking anything.")]
    [ProducesResponseType(typeof(PermissionTemplateInstantiationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PreviewInstantiation(
        Guid templateId,
        [FromBody] PreviewInstantiationRequest query,
        CancellationToken cancellationToken)
    {
        var preview = await sender.Send(
            new PreviewPermissionTemplateInstantiationQuery
            {
                TemplateId = templateId,
                TenantId = query.TenantId,
                TargetUserIds = query.TargetUserIds
            },
            cancellationToken).ConfigureAwait(false);

        return Ok(preview);
    }

    /// <summary>
    ///     Instantiates a template into a tenant: grants the template permissions to the
    ///     target users and assigns the template's roles (role templates). Use
    ///     <c>dryRun</c> for the preview-only form.
    /// </summary>
    /// <param name="templateId">The template id.</param>
    /// <param name="request">The instantiation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The instantiation result (or preview when dryRun).</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Requires same-tenant or system administration.</response>
    [HttpPost("{templateId:guid}:instantiate")]
    [ProducesResponseType(typeof(PermissionTemplateInstantiationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Instantiate(
        Guid templateId,
        [FromBody] InstantiateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new InstantiatePermissionTemplateCommand
            {
                TemplateId = templateId,
                TenantId = request.TenantId,
                TargetUserIds = request.TargetUserIds,
                DryRun = request.DryRun,
                Reason = request.Reason
            },
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Migrates a template's instantiated targets to a target version (issue #370):
    ///     applies the version delta across the tenants that instantiated the template,
    ///     batched, with durable per-batch progress. Supports dry-run.
    /// </summary>
    /// <param name="templateId">The template id.</param>
    /// <param name="request">The migration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The migration result with progress counters.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Requires system administration.</response>
    [HttpPost("{templateId:guid}/migrations")]
    [ProducesResponseType(typeof(PermissionTemplateMigrationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Migrate(
        Guid templateId,
        [FromBody] MigrateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new MigratePermissionTemplateCommand
            {
                TemplateId = templateId,
                ToVersion = request.ToVersion,
                TenantIds = request.TenantIds,
                BatchSize = request.BatchSize,
                DryRun = request.DryRun,
                Reason = request.Reason
            },
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Reads the durable progress of one migration (issue #370).
    /// </summary>
    /// <param name="templateId">The template id.</param>
    /// <param name="migrationId">The migration id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The migration status and progress counters.</response>
    /// <response code="404">Migration not found.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Requires system administration.</response>
    [HttpGet("{templateId:guid}/migrations/{migrationId:guid}")]
    [ProducesResponseType(typeof(PermissionTemplateMigrationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMigration(
        Guid templateId,
        Guid migrationId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetPermissionTemplateMigrationQuery { MigrationId = migrationId },
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Rolls back a completed template migration (issue #370): restores every
    ///     affected target to its recorded pre-migration state.
    /// </summary>
    /// <param name="templateId">The template id.</param>
    /// <param name="migrationId">The migration id.</param>
    /// <param name="request">The rollback reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The migration after rollback.</response>
    /// <response code="400">The migration cannot be rolled back.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Requires system administration.</response>
    [HttpPost("{templateId:guid}/migrations/{migrationId:guid}:rollback")]
    [ProducesResponseType(typeof(PermissionTemplateMigrationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RollbackMigration(
        Guid templateId,
        Guid migrationId,
        [FromBody] RollbackMigrationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RollbackPermissionTemplateMigrationCommand { MigrationId = migrationId, Reason = request.Reason },
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }
}

/// <summary>Request body for the instantiation dry-run preview.</summary>
public sealed record PreviewInstantiationRequest
{
    /// <summary>The tenant receiving the template (defaults to the acting tenant).</summary>
    public Guid? TenantId { get; init; }

    /// <summary>The target users.</summary>
    public Guid[] TargetUserIds { get; init; } = [];
}

/// <summary>Request body for template instantiation.</summary>
public sealed record InstantiateTemplateRequest
{
    /// <summary>The tenant receiving the template (defaults to the acting tenant).</summary>
    public Guid? TenantId { get; init; }

    /// <summary>The target users.</summary>
    public Guid[] TargetUserIds { get; init; } = [];

    /// <summary>When true, return the preview without applying anything.</summary>
    public bool DryRun { get; init; }

    /// <summary>Reason recorded in the audit log.</summary>
    public string? Reason { get; init; }
}

/// <summary>Request body for template migration.</summary>
public sealed record MigrateTemplateRequest
{
    /// <summary>The target version number.</summary>
    public required int ToVersion { get; init; }

    /// <summary>Optional tenant filter (null = every tenant that instantiated the template).</summary>
    public Guid[]? TenantIds { get; init; }

    /// <summary>Batch size for per-batch progress commits (default 25).</summary>
    public int BatchSize { get; init; } = 25;

    /// <summary>When true, return the plan without applying anything.</summary>
    public bool DryRun { get; init; }

    /// <summary>Reason recorded in the audit log.</summary>
    public string? Reason { get; init; }
}

/// <summary>Request body for migration rollback.</summary>
public sealed record RollbackMigrationRequest
{
    /// <summary>Reason recorded in the audit log.</summary>
    public required string Reason { get; init; }
}

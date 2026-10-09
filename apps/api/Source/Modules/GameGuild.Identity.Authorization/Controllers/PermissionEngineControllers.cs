using Asp.Versioning;
using GameGuild.CQRS;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     External system permission synchronization endpoints (issue #358): export the
///     permission state of a tenant as a portable JSON document and import (or dry-run)
///     a document back. Admin-guarded; imports are fail-closed and dry-runnable.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/permission-sync")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/sync")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionSyncController(ISender sender, ILogger<PermissionSyncController> logger) : BaseApiController
{
    /// <summary>
    ///     Exports the permission state (roles, tenant defaults, user grants) of a tenant
    ///     as a synchronization document. Omit <paramref name="tenantId"/> for the global
    ///     scope (system admins only).
    /// </summary>
    /// <param name="tenantId">Optional tenant scope; defaults to the caller's tenant context rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Synchronization document.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">Caller lacks admin rights for the requested scope.</response>
    [HttpGet("export")]
    [ProducesResponseType(typeof(ExternalPermissionSyncDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        var document = await sender.Send(new ExportPermissionSyncCommand { TenantId = tenantId }, cancellationToken).ConfigureAwait(false);
        return Ok(document);
    }

    /// <summary>
    ///     Imports (or dry-runs) an external permission synchronization document. Invalid
    ///     documents are rejected in full with their validation errors; nothing is
    ///     partially applied.
    /// </summary>
    /// <param name="request">The document plus the target tenant and dry-run flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Validation outcome and change plan (plus apply result when not a dry-run).</response>
    /// <response code="400">Invalid request body.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">Caller lacks admin rights for the requested scope.</response>
    [HttpPost("import")]
    [ProducesResponseType(typeof(PermissionSyncImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Import([FromBody] ImportPermissionSyncRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Permission sync import requested ({Mode})",
            request.DryRun ? "dry-run" : "apply");

        var result = await sender.Send(
            new ImportPermissionSyncCommand
            {
                Document = request.Document,
                TenantId = request.TenantId,
                DryRun = request.DryRun
            },
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }
}

/// <summary>
///     Request body for the permission sync import endpoint.
/// </summary>
/// <param name="Document">Synchronization document.</param>
/// <param name="TenantId">Optional target tenant (null/empty = global scope, system admins only).</param>
/// <param name="DryRun">Validate and report without applying.</param>
public sealed record ImportPermissionSyncRequest(
    ExternalPermissionSyncDocument Document,
    Guid? TenantId,
    bool DryRun);

/// <summary>
///     Permission restoration endpoints (issue #358): undo permission changes inside the
///     configured retention window. Guards and tenant scoping come from the restored
///     records and the actor context, never from the route.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/permission-restoration")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/restorations")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionRestorationController(ISender sender, ILogger<PermissionRestorationController> logger) : BaseApiController
{
    /// <summary>
    ///     Restores a soft-deleted tenant permission row inside the retention window.
    /// </summary>
    /// <param name="permissionId">The soft-deleted permission row id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Restoration outcome.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">Caller lacks admin rights for the restored record's tenant.</response>
    [HttpPost("deleted/{permissionId:guid}")]
    [ProducesResponseType(typeof(PermissionRestorationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RestoreDeleted([FromRoute] Guid permissionId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Permission restoration requested for deleted permission {PermissionId}", permissionId);
        var result = await sender.Send(
            new RestoreDeletedPermissionCommand { PermissionId = permissionId },
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Reverses a Grant/Revoke/Deny audit-log entry inside the retention window.
    /// </summary>
    /// <param name="auditLogId">The audit-log entry id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Undo outcome.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">Caller lacks admin rights for the audit entry's tenant.</response>
    [HttpPost("undo/{auditLogId:guid}")]
    [ProducesResponseType(typeof(PermissionRestorationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UndoChange([FromRoute] Guid auditLogId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Permission change undo requested for audit entry {AuditLogId}", auditLogId);
        var result = await sender.Send(
            new UndoPermissionChangeCommand { AuditLogId = auditLogId },
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}

/// <summary>
///     Permission compliance reporting endpoint (issue #358): allow/deny effectiveness
///     summary built from the durable permission evaluation log. Admin-guarded.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/permission-compliance")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/compliance")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionComplianceController(ISender sender, ILogger<PermissionComplianceController> logger) : BaseApiController
{
    /// <summary>
    ///     Builds the permission effectiveness compliance report (overall and per
    ///     permission / evaluation surface / operation allow-deny rates) for a time range.
    /// </summary>
    /// <param name="tenantId">Optional tenant scope (null = global, system admins only).</param>
    /// <param name="fromUtc">Inclusive range start (defaults to the trailing 24 hours).</param>
    /// <param name="toUtc">Inclusive range end (defaults to now).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Compliance report.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">Caller lacks admin rights for the requested scope.</response>
    [HttpGet("report")]
    [ProducesResponseType(typeof(PermissionComplianceReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReport(
        [FromQuery] Guid? tenantId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var report = await sender.Send(
            new GetPermissionComplianceReportQuery { TenantId = tenantId, FromUtc = fromUtc, ToUtc = toUtc },
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Permission compliance report served: {Total} evaluations, deny rate {DenyRate:P1}.",
            report.TotalEvaluations,
            report.DenyRate);

        return Ok(report);
    }
}

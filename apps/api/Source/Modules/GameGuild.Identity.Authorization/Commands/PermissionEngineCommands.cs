using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

// ============================================================================
// Permission engine commands (issue #358): external sync, restoration, compliance
// ============================================================================

/// <summary>
///     Command: export the permission state of a tenant (or the global state) as a
///     portable JSON document for external system synchronization (issue #358).
/// </summary>
public sealed record ExportPermissionSyncCommand : ICommand<ExternalPermissionSyncDocument>
{
    /// <summary>
    ///     Optional target tenant. Null (or empty) exports the global state — system
    ///     admins only. Tenant admins may only export their own tenant.
    /// </summary>
    public Guid? TenantId { get; init; }
}

/// <summary>
///     Command: import (or dry-run) an external permission synchronization document
///     (issue #358). Invalid documents are rejected in full (fail-closed).
/// </summary>
public sealed record ImportPermissionSyncCommand : ICommand<PermissionSyncImportResult>
{
    /// <summary>The document to import.</summary>
    public required ExternalPermissionSyncDocument Document { get; init; }

    /// <summary>
    ///     Optional target tenant. Null (or empty) targets the global scope — system
    ///     admins only. Tenant admins may only import into their own tenant.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>When true, validate and report the plan without applying anything.</summary>
    public bool DryRun { get; init; }
}

/// <summary>
///     Command: restore a soft-deleted tenant permission row inside the retention
///     window (issue #358).
/// </summary>
public sealed record RestoreDeletedPermissionCommand : ICommand<PermissionRestorationResult>
{
    /// <summary>The id of the soft-deleted <see cref="TenantPermission"/> row.</summary>
    public required Guid PermissionId { get; init; }
}

/// <summary>
///     Command: undo a Grant/Revoke/Deny audit-log entry inside the retention window
///     (issue #358).
/// </summary>
public sealed record UndoPermissionChangeCommand : ICommand<PermissionRestorationResult>
{
    /// <summary>The id of the audit-log entry to reverse.</summary>
    public required Guid AuditLogId { get; init; }
}

/// <summary>
///     Query: permission effectiveness compliance report built from the durable
///     permission evaluation log (issue #358).
/// </summary>
public sealed record GetPermissionComplianceReportQuery : IQuery<PermissionComplianceReport>
{
    /// <summary>
    ///     Optional tenant scope. Null (or empty) reports globally — system admins only.
    ///     Tenant admins may only read their own tenant.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>Inclusive range start (defaults to the last 24 hours).</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Inclusive range end (defaults to now).</summary>
    public DateTime? ToUtc { get; init; }
}

/// <summary>
///     Shared tenant-scope guard for the permission engine admin surfaces (issue #358).
///     A null (global) scope requires the system admin role; a tenant scope requires the
///     system admin role or tenant-admin rights for exactly that tenant. The acting user
///     always comes from <see cref="IActorContextAccessor"/>, never from the request body.
/// </summary>
public sealed class PermissionEngineTenantGuard(IActorContextAccessor actorContextAccessor)
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <summary>
    ///     Resolves and authorizes the effective tenant scope for a request. Returns the
    ///     authorized tenant id, or null for the global scope.
    /// </summary>
    /// <param name="requestedTenantId">Tenant id requested by the caller (null/empty = global).</param>
    /// <param name="action">Action description used in error messages.</param>
    /// <exception cref="UnauthorizedAccessException">When the actor lacks rights for the scope.</exception>
    public Guid? ResolveAuthorizedTenant(Guid? requestedTenantId, string action)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException($"Authentication is required to {action}.");
        }

        Guid? requested = requestedTenantId is { } value && value != Guid.Empty ? value : null;

        if (requested is null)
        {
            // Global scope: system admins only.
            if (!Actor.IsSystemAdmin)
            {
                throw new UnauthorizedAccessException($"The requested {action} targets the global scope, which requires system admin rights.");
            }

            return null;
        }

        if (Actor.IsSystemAdmin)
        {
            return requested;
        }

        if (Actor.IsTenantAdmin && Actor.TenantId == requested)
        {
            return requested;
        }

        throw new UnauthorizedAccessException($"Tenant admin rights for tenant {requested} are required to {action}.");
    }
}

/// <summary>
///     Handler for <see cref="ExportPermissionSyncCommand"/>. Admin-guarded: tenant
///     admins may export their own tenant; the global scope requires system admin.
///     The tenant scope is validated against the acting user's tenant context.
/// </summary>
public sealed class ExportPermissionSyncCommandHandler(
    IPermissionSyncService syncService,
    PermissionEngineTenantGuard tenantGuard,
    IActorContextAccessor actorContextAccessor,
    ILogger<ExportPermissionSyncCommandHandler> logger)
    : ICommandHandler<ExportPermissionSyncCommand, ExternalPermissionSyncDocument>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<ExternalPermissionSyncDocument> Handle(ExportPermissionSyncCommand request, CancellationToken cancellationToken)
    {
        var targetTenant = tenantGuard.ResolveAuthorizedTenant(request.TenantId, "export permission sync documents");
        var document = await syncService.ExportAsync(targetTenant, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Exported permission sync document for tenant {TenantId} ({RoleCount} roles) by actor {ActorId}.",
            targetTenant,
            document.Roles.Count,
            Actor.SubjectId);
        return document;
    }
}

/// <summary>
///     Handler for <see cref="ImportPermissionSyncCommand"/>. Admin-guarded exactly like
///     the export; imports additionally run through the guarded, versioned and audited
///     mutation paths, and invalid documents are rejected in full.
/// </summary>
public sealed class ImportPermissionSyncCommandHandler(
    IPermissionSyncService syncService,
    PermissionEngineTenantGuard guard,
    IActorContextAccessor actorContextAccessor,
    ILogger<ImportPermissionSyncCommandHandler> logger)
    : ICommandHandler<ImportPermissionSyncCommand, PermissionSyncImportResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionSyncImportResult> Handle(ImportPermissionSyncCommand request, CancellationToken cancellationToken)
    {
        var targetTenant = guard.ResolveAuthorizedTenant(request.TenantId, "import permission sync documents");

        var result = request.DryRun
            ? await syncService.PreviewImportAsync(request.Document, targetTenant, cancellationToken).ConfigureAwait(false)
            : await syncService.ImportAsync(request.Document, targetTenant, dryRun: false, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Permission sync import ({Mode}) for tenant {TenantId} by actor {ActorId}: valid={IsValid} applied={Applied} changes={ChangeCount} errors={ErrorCount}.",
            request.DryRun ? "dry-run" : "apply",
            targetTenant,
            Actor.SubjectId,
            result.IsValid,
            result.Applied,
            result.Changes.Count,
            result.ValidationErrors.Count);
        return result;
    }
}

/// <summary>
///     Handler for <see cref="RestoreDeletedPermissionCommand"/>. Guards live inside
///     <see cref="PermissionRestorationService"/> (tenant scope from the restored record).
/// </summary>
public sealed class RestoreDeletedPermissionCommandHandler(
    IPermissionRestorationService restorationService,
    ILogger<RestoreDeletedPermissionCommandHandler> logger)
    : ICommandHandler<RestoreDeletedPermissionCommand, PermissionRestorationResult>
{
    public async Task<PermissionRestorationResult> Handle(RestoreDeletedPermissionCommand request, CancellationToken cancellationToken)
    {
        var result = await restorationService.RestoreDeletedPermissionAsync(request.PermissionId, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Permission restoration of {PermissionId}: succeeded={Succeeded} ({Message}).",
            request.PermissionId,
            result.Succeeded,
            result.Message);
        return result;
    }
}

/// <summary>
///     Handler for <see cref="UndoPermissionChangeCommand"/>. Guards live inside
///     <see cref="PermissionRestorationService"/> (tenant scope from the audit record).
/// </summary>
public sealed class UndoPermissionChangeCommandHandler(
    IPermissionRestorationService restorationService,
    ILogger<UndoPermissionChangeCommandHandler> logger)
    : ICommandHandler<UndoPermissionChangeCommand, PermissionRestorationResult>
{
    public async Task<PermissionRestorationResult> Handle(UndoPermissionChangeCommand request, CancellationToken cancellationToken)
    {
        var result = await restorationService.UndoAuditEntryAsync(request.AuditLogId, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Permission undo of audit entry {AuditLogId}: succeeded={Succeeded} ({Message}).",
            request.AuditLogId,
            result.Succeeded,
            result.Message);
        return result;
    }
}

/// <summary>
///     Handler for <see cref="GetPermissionComplianceReportQuery"/>. Admin-guarded like
///     the sync commands; the default window is the trailing 24 hours.
/// </summary>
public sealed class GetPermissionComplianceReportHandler(
    IPermissionComplianceReportService reportService,
    PermissionEngineTenantGuard guard,
    ILogger<GetPermissionComplianceReportHandler> logger)
    : IQueryHandler<GetPermissionComplianceReportQuery, PermissionComplianceReport>
{
    public async Task<PermissionComplianceReport> Handle(GetPermissionComplianceReportQuery request, CancellationToken cancellationToken)
    {
        var tenant = guard.ResolveAuthorizedTenant(request.TenantId, "read permission compliance reports");

        var toUtc = request.ToUtc ?? SystemClock.UtcNow;
        var fromUtc = request.FromUtc ?? toUtc.AddHours(-24);

        var report = await reportService.BuildReportAsync(tenant, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Permission compliance report for tenant {TenantId} over [{FromUtc}, {ToUtc}]: {Total} evaluations.",
            tenant,
            fromUtc,
            toUtc,
            report.TotalEvaluations);
        return report;
    }
}

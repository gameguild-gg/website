using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Result of a permission restoration attempt.
/// </summary>
/// <param name="Succeeded">Whether the restoration was applied.</param>
/// <param name="RestoredPermissionId">The affected TenantPermission id (when known).</param>
/// <param name="TenantId">Tenant scope of the restoration (from the restored/undone record, never from the caller).</param>
/// <param name="Message">Human-readable outcome description.</param>
public sealed record PermissionRestorationResult(
    bool Succeeded,
    Guid? RestoredPermissionId,
    Guid? TenantId,
    string Message);

/// <summary>
///     Permission restoration (issue #358): undoes permission changes within a
///     configurable retention window.
///     <list type="bullet">
///         <item><b>Soft-delete restoration</b>: a soft-deleted <see cref="TenantPermission"/> row is
///         undeleted when its deletion age is inside <c>PermissionEngine:Restoration:RetentionDays</c>.</item>
///         <item><b>Change undo</b>: a Grant/Revoke/Deny audit-log entry is reversed through the
///         guarded mutation paths (grant service), restoring the recorded previous state.</item>
///     </list>
///     Every restoration is guarded (actor must be system admin or admin of the tenant the
///     restored record belongs to — the tenant comes from the record, never from the
///     caller), versioned (tenant security version increment) and audited
///     (<see cref="PermissionOperationType.Restore"/>), and it fans out a webhook
///     notification like any other permission change.
/// </summary>
public interface IPermissionRestorationService
{
    /// <summary>Restores a soft-deleted tenant permission row inside the retention window.</summary>
    Task<PermissionRestorationResult> RestoreDeletedPermissionAsync(
        Guid permissionId,
        CancellationToken cancellationToken = default);

    /// <summary>Reverses a Grant/Revoke/Deny audit-log entry inside the retention window.</summary>
    Task<PermissionRestorationResult> UndoAuditEntryAsync(
        Guid auditLogId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Default <see cref="IPermissionRestorationService"/> implementation.
/// </summary>
public sealed class PermissionRestorationService(
    IApplicationDbContext context,
    IPermissionAuditLogRepository auditLogRepository,
    IPermissionGrantService grantService,
    ITenantSecurityVersionStore securityVersionStore,
    IActorContextAccessor actorContextAccessor,
    IEnumerable<IPermissionChangeNotifier> changeNotifiers,
    IOptions<PermissionEngineOptions> engineOptions,
    ILogger<PermissionRestorationService> logger) : IPermissionRestorationService
{
    private readonly PermissionEngineOptions _options = engineOptions.Value;

    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<PermissionRestorationResult> RestoreDeletedPermissionAsync(
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await context.Set<TenantPermission>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == permissionId && p.DeletedAt != null, cancellationToken)
            .ConfigureAwait(false);

        if (deleted is null)
        {
            logger.LogWarning("Permission restoration requested for unknown or non-deleted permission {PermissionId}.", permissionId);
            return new PermissionRestorationResult(false, permissionId, null, "Permission not found or not deleted.");
        }

        // Tenant scope comes from the restored record, never from the caller.
        var tenantId = deleted.TenantId;
        var performedBy = Actor.SubjectIdAsGuid ?? Guid.Empty;

        EnsureRestorationAuthorized(tenantId, $"restore permission {permissionId}");

        var deletedAt = deleted.DeletedAt ?? deleted.UpdatedAt;
        if (!IsWithinRetention(deletedAt, out var retentionCutoff))
        {
            logger.LogWarning(
                "Permission restoration of {PermissionId} rejected: deleted at {DeletedAt} is outside the {RetentionDays}-day retention window.",
                permissionId,
                deletedAt,
                _options.Restoration.RetentionDays);
            return new PermissionRestorationResult(
                false,
                permissionId,
                tenantId,
                $"Restoration window ({_options.Restoration.RetentionDays} days, until {retentionCutoff:O}) expired.");
        }

        var restoredPermissions = deleted.Permissions.ToArray();
        var restoredDenyPermissions = deleted.DenyPermissions.ToArray();
        deleted.Restore();
        deleted.Touch();
        context.Set<TenantPermission>().Update(deleted);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await VersionAndAuditAsync(
            PermissionOperationType.Restore,
            deleted.UserId,
            tenantId,
            performedBy,
            oldValue: null,
            newValue: SerializeState(restoredPermissions, restoredDenyPermissions),
            reason: "Soft-deleted permission restored (issue #358)",
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Restored soft-deleted permission {PermissionId} for user {UserId} in tenant {TenantId} by actor {PerformedBy}.",
            permissionId,
            deleted.UserId,
            tenantId,
            performedBy);

        return new PermissionRestorationResult(true, permissionId, tenantId, "Permission restored.");
    }

    /// <inheritdoc />
    public async Task<PermissionRestorationResult> UndoAuditEntryAsync(
        Guid auditLogId,
        CancellationToken cancellationToken = default)
    {
        var entry = await auditLogRepository.GetByIdAsync(auditLogId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            logger.LogWarning("Permission undo requested for unknown audit entry {AuditLogId}.", auditLogId);
            return new PermissionRestorationResult(false, null, null, "Audit entry not found.");
        }

        if (entry is { Success: false })
        {
            return new PermissionRestorationResult(false, entry.ResourceId, entry.TenantId?.Value, "Only successful operations can be undone.");
        }

        if (entry.OperationType is not (PermissionOperationType.Grant or PermissionOperationType.Revoke or PermissionOperationType.Deny))
        {
            return new PermissionRestorationResult(
                false,
                entry.ResourceId,
                entry.TenantId?.Value,
                $"Operation type {entry.OperationType} cannot be undone.");
        }

        // Tenant scope comes from the audit record, never from the caller.
        var tenantId = entry.TenantId?.Value;
        var performedBy = Actor.SubjectIdAsGuid ?? Guid.Empty;

        EnsureRestorationAuthorized(tenantId, $"undo audit entry {auditLogId}");

        if (!IsWithinRetention(entry.Timestamp, out var retentionCutoff))
        {
            logger.LogWarning(
                "Undo of audit entry {AuditLogId} rejected: recorded at {Timestamp} is outside the {RetentionDays}-day retention window.",
                auditLogId,
                entry.Timestamp,
                _options.Restoration.RetentionDays);
            return new PermissionRestorationResult(
                false,
                entry.ResourceId,
                tenantId,
                $"Restoration window ({_options.Restoration.RetentionDays} days, until {retentionCutoff:O}) expired.");
        }

        // Undo semantics: restore the recorded previous state (OldValue) by applying the
        // inverse mutation through the guarded grant service.
        var targetUserId = entry.UserId;
        var previous = ParsePermissions(entry.OldValue);
        var current = ParsePermissions(entry.NewValue);

        switch (entry.OperationType)
        {
            case PermissionOperationType.Grant:
            {
                // Granted permissions (NewValue - OldValue) are revoked again.
                var delta = current.Except(previous, StringComparer.OrdinalIgnoreCase).ToArray();
                if (delta.Length == 0)
                {
                    return new PermissionRestorationResult(true, entry.ResourceId, tenantId, "Nothing to undo.");
                }

                await grantService.RevokeTenantPermissionAsync(targetUserId, tenantId, delta, cancellationToken).ConfigureAwait(false);
                break;
            }

            case PermissionOperationType.Revoke:
            {
                // Revoked permissions (OldValue - NewValue) are granted again.
                var delta = previous.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
                if (delta.Length == 0)
                {
                    return new PermissionRestorationResult(true, entry.ResourceId, tenantId, "Nothing to undo.");
                }

                await grantService.GrantTenantPermissionAsync(
                    targetUserId,
                    tenantId,
                    delta,
                    performedBy,
                    expiresAt: null,
                    reason: $"Undo of audit entry {auditLogId}",
                    cancellationToken).ConfigureAwait(false);
                break;
            }

            case PermissionOperationType.Deny:
            default:
            {
                // Denied permissions (NewValue - OldValue) are un-denied again.
                var delta = current.Except(previous, StringComparer.OrdinalIgnoreCase).ToArray();
                if (delta.Length == 0)
                {
                    return new PermissionRestorationResult(true, entry.ResourceId, tenantId, "Nothing to undo.");
                }

                await grantService.RemoveDenyPermissionsAsync(targetUserId, tenantId, delta, cancellationToken).ConfigureAwait(false);
                break;
            }
        }

        await VersionAndAuditAsync(
            PermissionOperationType.Restore,
            targetUserId,
            tenantId,
            performedBy,
            oldValue: entry.NewValue,
            newValue: entry.OldValue,
            reason: $"Undo of audit entry {auditLogId} ({entry.OperationType})",
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Undid audit entry {AuditLogId} ({OperationType}) for user {UserId} in tenant {TenantId} by actor {PerformedBy}.",
            auditLogId,
            entry.OperationType,
            targetUserId,
            tenantId,
            performedBy);

        return new PermissionRestorationResult(true, entry.ResourceId, tenantId, "Change undone.");
    }

    /// <summary>
    ///     Guard: the acting user must be authenticated and be either a system admin or a
    ///     tenant admin of the tenant the restored record belongs to. Global (tenant-less)
    ///     records additionally require the system admin role — delegating global defaults
    ///     is never allowed.
    /// </summary>
    private void EnsureRestorationAuthorized(Guid? tenantId, string action)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Permission restoration requires an authenticated actor.");
        }

        if (tenantId is null || tenantId == Guid.Empty)
        {
            if (!Actor.IsSystemAdmin)
            {
                logger.LogWarning("Actor {ActorId} attempted to {Action} on a global record without system admin rights.", Actor.SubjectId, action);
                throw new UnauthorizedAccessException("Restoring global permission records requires system admin rights.");
            }

            return;
        }

        if (Actor.IsSystemAdmin || (Actor.IsTenantAdmin && Actor.TenantId == tenantId))
        {
            return;
        }

        logger.LogWarning(
            "Actor {ActorId} attempted to {Action} in tenant {TenantId} without admin rights for that tenant.",
            Actor.SubjectId,
            action,
            tenantId);
        throw new UnauthorizedAccessException("Only tenant or system administrators can restore permissions.");
    }

    private bool IsWithinRetention(DateTime recordedAt, out DateTime cutoff)
    {
        cutoff = SystemClock.UtcNow.AddDays(-_options.Restoration.RetentionDays);
        return recordedAt >= cutoff;
    }

    private async Task VersionAndAuditAsync(
        PermissionOperationType operationType,
        Guid? userId,
        Guid? tenantId,
        Guid performedBy,
        string? oldValue,
        string? newValue,
        string reason,
        CancellationToken cancellationToken)
    {
        // Versioned: bump the tenant security version so caches invalidate immediately.
        var tenantKey = tenantId?.ToString() ?? Guid.Empty.ToString();
        await securityVersionStore.IncrementVersionAsync(tenantKey, cancellationToken).ConfigureAwait(false);

        // Audited.
        var auditLog = new PermissionAuditLog
        {
            TenantId = tenantId is null ? null : new TenantId(tenantId.Value),
            OperationType = operationType,
            UserId = userId,
            ResourceId = null,
            ResourceType = "TenantPermission",
            PermissionType = "Tenant",
            OldValue = oldValue,
            NewValue = newValue,
            PerformedBy = performedBy,
            Reason = reason,
            Success = true
        };
        await auditLogRepository.CreateAsync(auditLog, cancellationToken).ConfigureAwait(false);

        // Notified (webhooks, issue #358): delivery failures never affect the restoration.
        var change = new PermissionChangeEvent(
            PermissionChangeEventType.Restored,
            tenantId,
            userId,
            "Tenant",
            ParsePermissions(newValue),
            performedBy);
        foreach (var notifier in changeNotifiers)
        {
            try
            {
                await notifier.NotifyAsync(change, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Permission change notifier {NotifierType} failed during a restoration; the restoration is unaffected.",
                    notifier.GetType().Name);
            }
        }
    }

    private static string[] ParsePermissions(string? commaJoined)
        => string.IsNullOrWhiteSpace(commaJoined)
            ? Array.Empty<string>()
            : commaJoined.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                .ToArray();

    private static string SerializeState(string[] permissions, string[] denyPermissions)
    {
        var allowPart = string.Join(",", permissions);
        return denyPermissions.Length == 0
            ? allowPart
            : $"{allowPart}|denied:{string.Join(",", denyPermissions)}";
    }
}

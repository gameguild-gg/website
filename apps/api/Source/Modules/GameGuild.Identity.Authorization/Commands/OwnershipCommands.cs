using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

// ============================================================================
// Resource-ownership commands (issue #372): guarded + versioned + audited
// ownership transfer with a soft-delete restore point.
// ============================================================================

/// <summary>
///     Result of a <see cref="TransferOwnershipCommand"/>.
/// </summary>
/// <param name="Succeeded">Whether the transfer was applied (or was a same-owner no-op).</param>
/// <param name="TenantId">Tenant scope of the resource (from the owner record, never from the caller).</param>
/// <param name="ResourceType">Resource type of the transferred resource.</param>
/// <param name="ResourceId">Resource id of the transferred resource.</param>
/// <param name="PreviousOwnerUserId">The outgoing owner (null when previously unrecorded).</param>
/// <param name="NewOwnerUserId">The incoming owner.</param>
/// <param name="RestorePointAuditLogId">
///     Id of the durable audit entry whose OldValue/NewValue snapshots allow reversing
///     the transfer; the previous owner row itself is soft-revoked (never hard-deleted),
///     so both halves of the restore point survive.
/// </param>
/// <param name="Message">Human-readable outcome.</param>
public sealed record OwnershipTransferResult(
    bool Succeeded,
    Guid? TenantId,
    string ResourceType,
    string ResourceId,
    Guid? PreviousOwnerUserId,
    Guid NewOwnerUserId,
    Guid RestorePointAuditLogId,
    string Message);

/// <summary>
///     Command: transfer the ownership designation of a resource to another user
///     (issue #372). Ownership is recorded on resource creation (the creator auto-grant —
///     the <c>ResourceUserPermission.IsOwner</c> row whose <c>CreatedBy</c> mapping points
///     at the creator) and moves only through this command.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> guarded, versioned and audited like every permission
///     mutation. Authorized actors: the current owner of the resource (within their own
///     tenant), tenant admins of the resource's tenant, and system admins. The acting
///     user always comes from <see cref="IActorContextAccessor"/>, never from the
///     request body; the tenant scope is validated against the owner record.</para>
///     <para><b>Soft-delete parity (issue #360 alignment):</b> the previous owner row is
///     soft-revoked (<c>Revoke</c>), never physically removed, and the durable audit
///     entry doubles as a restore point snapshot. This command performs no hard deletes.</para>
/// </remarks>
public sealed record TransferOwnershipCommand : ICommand<OwnershipTransferResult>
{
    /// <summary>Tenant scope of the resource. For non-admin callers this must equal the actor's tenant.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>The resource type (e.g. "Project", "Document").</summary>
    public required string ResourceType { get; init; }

    /// <summary>The resource id.</summary>
    public required string ResourceId { get; init; }

    /// <summary>The incoming owner. The acting user comes from the actor context, never from this command.</summary>
    public required Guid NewOwnerUserId { get; init; }

    /// <summary>Optional reason recorded on the revocation and the audit entry.</summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for <see cref="TransferOwnershipCommand"/>. Guarded (owner / tenant
///     admin / system admin), versioned (<see cref="ITenantSecurityVersionStore"/> bump
///     for the resource's tenant) and audited (<see cref="PermissionOperationType.TransferOwnership"/>
///     entry carrying the pre/post snapshots — the restore point). Soft-revokes the
///     outgoing owner row and upserts the incoming owner row; never hard-deletes.
/// </summary>
public sealed class TransferOwnershipCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService,
    ILogger<TransferOwnershipCommandHandler> logger
) : ICommandHandler<TransferOwnershipCommand, OwnershipTransferResult>
{
    private static readonly JsonSerializerOptions SnapshotSerializer = new(JsonSerializerDefaults.General);

    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<OwnershipTransferResult> Handle(TransferOwnershipCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResourceType) || string.IsNullOrWhiteSpace(request.ResourceId)
            || request.TenantId == Guid.Empty || request.NewOwnerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Ownership transfer requires a tenant, resource type, resource id and a non-empty new owner.");
        }

        var tenantKey = new TenantId(request.TenantId);
        var ownerRows = await dbContext.Set<ResourceUserPermission>()
            .Where(p =>
                p.TenantId == tenantKey
                && p.ResourceType == request.ResourceType
                && p.ResourceId == request.ResourceId
                && p.IsOwner
                && p.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Fail closed: without a recorded owner designation there is nothing to transfer
        // through this command (ownership is recorded on creation; it is never guessed).
        if (ownerRows.Count == 0)
        {
            logger.LogWarning(
                "Ownership transfer requested for {ResourceType}/{ResourceId} in tenant {TenantId} with no recorded owner - rejected (fail-closed).",
                request.ResourceType, request.ResourceId, request.TenantId);
            throw new InvalidOperationException(
                $"Resource '{request.ResourceType}/{request.ResourceId}' has no recorded owner designation to transfer.");
        }

        EnsureAuthorized(request, ownerRows);

        var performedBy = Actor.SubjectIdAsGuid ?? Guid.Empty;
        var outgoingOwner = ownerRows[0];
        var previousOwnerUserId = outgoingOwner.UserId;
        var ownerPermissionSet = outgoingOwner.Permissions.ToArray();

        if (previousOwnerUserId == request.NewOwnerUserId)
        {
            // Idempotent no-op: nothing changes, nothing is audited as a transfer.
            return new OwnershipTransferResult(
                true, request.TenantId, request.ResourceType, request.ResourceId,
                previousOwnerUserId, request.NewOwnerUserId, Guid.Empty,
                "The requested user already owns the resource; nothing to transfer.");
        }

        // Soft-revoke every in-force owner row (single-owner invariant) — no hard deletes.
        foreach (var row in ownerRows)
        {
            row.Revoke(performedBy, string.IsNullOrWhiteSpace(request.Reason)
                ? "ownership transferred"
                : request.Reason);
        }

        // Upsert the incoming owner row with the recorded owner permission set.
        var incoming = await dbContext.Set<ResourceUserPermission>()
            .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantKey
                    && p.UserId == request.NewOwnerUserId
                    && p.ResourceType == request.ResourceType
                    && p.ResourceId == request.ResourceId
                    && p.RevokedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (incoming is null)
        {
            incoming = new ResourceUserPermission
            {
                TenantId = tenantKey,
                UserId = request.NewOwnerUserId,
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                Permissions = ownerPermissionSet,
                GrantedByUserId = performedBy,
                GrantedByUserName = "Ownership transfer",
                ExpiresAt = outgoingOwner.ExpiresAt
            };
            dbContext.Set<ResourceUserPermission>().Add(incoming);
        }
        else
        {
            incoming.Permissions = ownerPermissionSet;
            if (incoming.ExpiresAt is null)
            {
                incoming.ExpiresAt = outgoingOwner.ExpiresAt;
            }
        }

        incoming.IsOwner = true;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Versioned: invalidate cached permission state for the resource's tenant.
        await securityVersionStore
            .IncrementVersionAsync(request.TenantId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        // Audited: the durable restore point (pre/post snapshots of the owner rows).
        var auditEntry = await auditService.LogPermissionChangeAsync(
            PermissionOperationType.TransferOwnership,
            request.NewOwnerUserId,
            performedBy,
            request.TenantId,
            permissionType: string.Join(",", ownerPermissionSet),
            resourceId: null,
            resourceType: request.ResourceType,
            oldValue: SerializeSnapshot(request.ResourceType, request.ResourceId, previousOwnerUserId, outgoingOwner.Id, ownerPermissionSet, outgoingOwner.ExpiresAt),
            newValue: SerializeSnapshot(request.ResourceType, request.ResourceId, request.NewOwnerUserId, incoming.Id, ownerPermissionSet, incoming.ExpiresAt),
            reason: string.IsNullOrWhiteSpace(request.Reason) ? "Ownership transferred" : request.Reason,
            success: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Transferred ownership of {ResourceType}/{ResourceId} in tenant {TenantId} from {PreviousOwnerUserId} to {NewOwnerUserId} by actor {ActorId}; restore point audit {AuditLogId}.",
            request.ResourceType, request.ResourceId, request.TenantId, previousOwnerUserId, request.NewOwnerUserId,
            performedBy, auditEntry.Id);

        return new OwnershipTransferResult(
            true, request.TenantId, request.ResourceType, request.ResourceId,
            previousOwnerUserId, request.NewOwnerUserId, auditEntry.Id,
            $"Ownership transferred to {request.NewOwnerUserId}; previous owner row soft-revoked (restore point {auditEntry.Id}).");
    }

    /// <summary>
    ///     Guard: system admins may transfer any recorded ownership; tenant admins only
    ///     inside their own tenant; everyone else must be the current owner inside their
    ///     own tenant. Anonymous actors are always rejected.
    /// </summary>
    private void EnsureAuthorized(TransferOwnershipCommand request, IReadOnlyList<ResourceUserPermission> ownerRows)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required to transfer resource ownership.");
        }

        if (Actor.IsSystemAdmin)
        {
            return;
        }

        var actorId = Actor.SubjectIdAsGuid;
        var ownsResource = actorId is { } id
            && ownerRows.Any(row => row.UserId == id);

        if (Actor.TenantId == request.TenantId
            && (Actor.IsTenantAdmin || ownsResource))
        {
            return;
        }

        logger.LogWarning(
            "Actor {ActorId} attempted to transfer ownership of {ResourceType}/{ResourceId} in tenant {TenantId} without owner or admin rights.",
            Actor.SubjectId, request.ResourceType, request.ResourceId, request.TenantId);
        throw new UnauthorizedAccessException(
            "Transferring resource ownership requires being the current owner, a tenant admin of the resource's tenant, or a system admin.");
    }

    private static string SerializeSnapshot(
        string resourceType,
        string resourceId,
        Guid userId,
        Guid grantId,
        string[] permissions,
        DateTime? expiresAt)
        => JsonSerializer.Serialize(
            new OwnerSnapshot(resourceType, resourceId, userId, grantId, permissions, expiresAt),
            SnapshotSerializer);

    private sealed record OwnerSnapshot(
        string ResourceType,
        string ResourceId,
        Guid UserId,
        Guid GrantId,
        string[] Permissions,
        DateTime? ExpiresAt);
}

using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for <see cref="GrantResourcePermissionCommand"/> (issue #298): grants a
///     resource-scoped permission set to one user on one resource. The endpoint existed
///     but its command had no handler; this backs it with the same guarded, versioned and
///     audited flow the bulk resource grant uses.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> guarded like every permission mutation — tenant-admin
///     within the actor's own tenant or SystemAdmin; global scope (empty tenant)
///     additionally requires <c>system:manage-global-defaults</c>. Every mutation bumps
///     the tenant security version and writes a <see cref="PermissionAuditLog"/> entry.</para>
/// </remarks>
public sealed class GrantResourcePermissionCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService,
    ILogger<GrantResourcePermissionCommandHandler> logger
) : ICommandHandler<GrantResourcePermissionCommand, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(GrantResourcePermissionCommand request, CancellationToken cancellationToken)
    {
        EnsureAuthorized(request.TenantId);

        if (request.UserId == Guid.Empty || request.ResourceId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.ResourceType)
            || request.Permissions.Count == 0)
        {
            throw new InvalidOperationException("UserId, ResourceId, ResourceType and at least one permission are required");
        }

        logger.LogInformation(
            "Granting resource permissions {Permissions} to user {UserId} on resource {ResourceType}/{ResourceId}",
            string.Join(",", request.Permissions),
            request.UserId,
            request.ResourceType,
            request.ResourceId);

        // DeletedAt filter: a soft-deleted grant (history preservation, #360) is never
        // reactivated in place — a new grant creates a fresh row.
        var grant = await dbContext.Set<GenericResourcePermission>()
            .FirstOrDefaultAsync(p =>
                    p.UserId == request.UserId &&
                    p.TenantId == (request.TenantId == Guid.Empty ? null : request.TenantId) &&
                    p.ResourceId == request.ResourceId &&
                    p.ResourceType == request.ResourceType &&
                    p.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (grant is null)
        {
            grant = new GenericResourcePermission(
                request.UserId,
                request.TenantId == Guid.Empty ? null : request.TenantId,
                request.ResourceId,
                request.ResourceType)
            {
                ExpiresAt = request.ExpiresAt,
                GrantedBy = Guid.TryParse(request.GrantedBy, out var grantedBy) ? grantedBy : Actor.SubjectIdAsGuid
            };
            dbContext.Set<GenericResourcePermission>().Add(grant);
        }

        grant.IsActive = true;
        grant.ExpiresAt = request.ExpiresAt;
        foreach (var permission in request.Permissions)
        {
            grant.AddPermission(permission);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var tenantKey = request.TenantId == Guid.Empty ? Guid.Empty.ToString() : request.TenantId.ToString();
        await securityVersionStore.IncrementVersionAsync(tenantKey, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            request.UserId,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            request.TenantId == Guid.Empty ? null : request.TenantId,
            permissionType: string.Join(",", request.Permissions),
            resourceId: request.ResourceId,
            resourceType: request.ResourceType,
            reason: request.Reason,
            success: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    private void EnsureAuthorized(Guid tenantId)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        if (Actor.IsSystemAdmin)
        {
            return;
        }

        if (tenantId == Guid.Empty)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to grant global resource permissions without '{Permission}'",
                Actor.SubjectId, SystemPermission.Keys.ManageGlobalDefaults);

            throw new UnauthorizedAccessException(
                $"Granting global resource permissions requires '{SystemPermission.Keys.ManageGlobalDefaults}' permission");
        }

        if (tenantId != Actor.TenantId || !Actor.IsTenantAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to grant resource permissions in tenant {TenantId} without tenant administration",
                Actor.SubjectId, tenantId);

            throw new UnauthorizedAccessException(
                "Granting resource permissions requires system administration or tenant administration of the target tenant");
        }
    }
}

/// <summary>
///     Handler for <see cref="RevokeResourcePermissionCommand"/> (issue #298): revokes a
///     resource-scoped permission set from one user on one resource.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> same guards as the grant. Revocation subtracts only the
///     named permissions; a grant left with none is soft-deleted (history preservation,
///     #360 — a never-persisted row is removed instead). Returns false when no matching
///     grant exists (a no-op revoke is reported, never counted as success).</para>
/// </remarks>
public sealed class RevokeResourcePermissionCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService,
    ILogger<RevokeResourcePermissionCommandHandler> logger
) : ICommandHandler<RevokeResourcePermissionCommand, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(RevokeResourcePermissionCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        if (Actor.IsSystemAdmin)
        {
            // system administrators may revoke anywhere
        }
        else if (request.TenantId == Guid.Empty)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to revoke global resource permissions without '{Permission}'",
                Actor.SubjectId, SystemPermission.Keys.ManageGlobalDefaults);

            throw new UnauthorizedAccessException(
                $"Revoking global resource permissions requires '{SystemPermission.Keys.ManageGlobalDefaults}' permission");
        }
        else if (request.TenantId != Actor.TenantId || !Actor.IsTenantAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to revoke resource permissions in tenant {TenantId} without tenant administration",
                Actor.SubjectId, request.TenantId);

            throw new UnauthorizedAccessException(
                "Revoking resource permissions requires system administration or tenant administration of the target tenant");
        }

        var grant = await dbContext.Set<GenericResourcePermission>()
            .FirstOrDefaultAsync(p =>
                    p.UserId == request.UserId &&
                    p.TenantId == (request.TenantId == Guid.Empty ? null : request.TenantId) &&
                    p.ResourceId == request.ResourceId &&
                    p.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (grant is null)
        {
            return false;
        }

        foreach (var permission in request.Permissions)
        {
            grant.RemovePermission(permission);
        }

        if (grant.GetPermissionsAsEnum().Any())
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (grant.IsNew)
        {
            dbContext.Set<GenericResourcePermission>().Remove(grant);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            grant.IsActive = false;
            grant.SoftDelete();
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var tenantKey = request.TenantId == Guid.Empty ? Guid.Empty.ToString() : request.TenantId.ToString();
        await securityVersionStore.IncrementVersionAsync(tenantKey, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            request.UserId,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            request.TenantId == Guid.Empty ? null : request.TenantId,
            permissionType: string.Join(",", request.Permissions),
            resourceId: request.ResourceId,
            resourceType: grant.ResourceType,
            reason: request.Reason,
            success: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }
}

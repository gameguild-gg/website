using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for <see cref="BulkGrantResourcePermissionsCommand"/>.
///     Grants the same resource-level permission set to multiple users for a single resource.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> guarded like other permission mutations — tenant-admin within
///     the actor's own tenant or SystemAdmin. Every mutation bumps the tenant security version
///     and writes a <see cref="PermissionAuditLog"/> entry.</para>
/// </remarks>
public sealed class BulkGrantResourcePermissionsCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService,
    ILogger<BulkGrantResourcePermissionsCommandHandler> logger
) : ICommandHandler<BulkGrantResourcePermissionsCommand, BulkPermissionResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<BulkPermissionResult> Handle(
        BulkGrantResourcePermissionsCommand request,
        CancellationToken cancellationToken)
    {
        EnsureAuthorized(request);

        var userIds = request.UserIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var result = new ResourceBulkPermissionResult
        {
            TotalRequested = userIds.Length,
            ProcessedAt = SystemClock.UtcNow
        };

        var grantedFor = new List<Guid>();

        foreach (var userId in userIds)
        {
            try
            {
                // DeletedAt filter: a soft-deleted grant (history preservation, #360) is
                // never reactivated in place — a new grant creates a fresh row.
                var grant = await dbContext.Set<GenericResourcePermission>()
                    .FirstOrDefaultAsync(p =>
                            p.UserId == userId &&
                            p.TenantId == (request.TenantId == Guid.Empty ? null : request.TenantId) &&
                            p.ResourceId == request.ResourceId &&
                            p.ResourceType == request.ResourceType &&
                            p.DeletedAt == null,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (grant is null)
                {
                    grant = new GenericResourcePermission(
                        userId,
                        request.TenantId == Guid.Empty ? null : request.TenantId,
                        request.ResourceId,
                        request.ResourceType)
                    {
                        ExpiresAt = request.ExpiresAt
                    };
                    dbContext.Set<GenericResourcePermission>().Add(grant);
                }

                grant.IsActive = true;
                grant.ExpiresAt = request.ExpiresAt;
                foreach (var permission in request.Permissions)
                {
                    grant.AddPermission(permission);
                }

                grantedFor.Add(userId);
                result.Successful++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Bulk resource grant failed for user {UserId} on resource {ResourceType}/{ResourceId}",
                    userId, request.ResourceType, request.ResourceId);
                result.Failed++;
                result.Failures.Add(new UserBulkPermissionFailure
                {
                    UserId = userId,
                    Error = ex.GetType().Name,
                    Details = ex.Message
                });
            }
        }

        if (grantedFor.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var tenantKey = request.TenantId == Guid.Empty ? Guid.Empty.ToString() : request.TenantId.ToString();
            await securityVersionStore.IncrementVersionAsync(tenantKey, cancellationToken).ConfigureAwait(false);

            await auditService.LogPermissionChangeAsync(
                PermissionOperationType.Grant,
                grantedFor.First(),
                Actor.SubjectIdAsGuid ?? Guid.Empty,
                request.TenantId == Guid.Empty ? null : request.TenantId,
                permissionType: string.Join(",", request.Permissions),
                resourceId: request.ResourceId,
                resourceType: request.ResourceType,
                reason: request.Reason,
                success: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private void EnsureAuthorized(BulkGrantResourcePermissionsCommand request)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        if (Actor.IsSystemAdmin)
        {
            return;
        }

        if (request.TenantId == Guid.Empty)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to bulk grant global resource permissions without '{Permission}'",
                Actor.SubjectId, SystemPermission.Keys.ManageGlobalDefaults);

            throw new UnauthorizedAccessException(
                $"Bulk granting global resource permissions requires '{SystemPermission.Keys.ManageGlobalDefaults}' permission");
        }

        if (request.TenantId != Actor.TenantId || !Actor.IsTenantAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to bulk grant resource permissions in tenant {TenantId} without tenant administration",
                Actor.SubjectId, request.TenantId);

            throw new UnauthorizedAccessException(
                "Bulk granting resource permissions requires system administration or tenant administration of the target tenant");
        }
    }
}

/// <summary>
///     Concrete bulk-result carrying per-user failures for resource permission operations.
/// </summary>
public sealed class ResourceBulkPermissionResult : BulkPermissionResult;

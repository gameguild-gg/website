using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for <see cref="BulkGrantTenantPermissionsCommand"/>.
///     Delegates per-user granting to the Authorization module's
///     <see cref="IPermissionBulkService"/>, which bumps tenant security versions and
///     writes audit entries for each mutation.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> bulk grants are guarded like single grants — tenant-admin
///     (within the actor's own tenant) or SystemAdmin; <see cref="Guid.Empty"/> tenant IDs
///     (global defaults) additionally require <c>system:manage-global-defaults</c>.</para>
///     <para><b>Transactionality (issues #302/#365):</b> the whole bulk runs inside the
///     host pipeline's ambient transaction. Default (per-item) mode reports each user's
///     outcome on <see cref="BulkPermissionResult"/> and commits the successful items —
///     <see cref="TenantBulkPermissionResult"/> implements
///     <see cref="GameGuild.ICommitOnFailureOutcome"/> so the partial-failure result is
///     recognized by the transactional outcome classifier as a business failure whose
///     completed mutations are deliberately retained. <see cref="BulkGrantTenantPermissionsCommand.Atomic"/>
///     mode instead throws on the first failure, rolling the transaction back
///     (all-or-nothing).</para>
///     <para><b>Issue #310:</b> <see cref="BulkGrantTenantPermissionsCommand.ExpiresAt"/> and
///     <see cref="BulkGrantTenantPermissionsCommand.Reason"/> are forwarded to every per-user
///     grant (expiry envelope + audit reason) instead of being discarded.</para>
/// </remarks>
public sealed class BulkGrantTenantPermissionsCommandHandler(
    IPermissionBulkService bulkService,
    IActorContextAccessor actorContextAccessor,
    ILogger<BulkGrantTenantPermissionsCommandHandler> logger
) : ICommandHandler<BulkGrantTenantPermissionsCommand, BulkPermissionResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<BulkPermissionResult> Handle(
        BulkGrantTenantPermissionsCommand request,
        CancellationToken cancellationToken)
    {
        BulkTenantPermissionGuards.EnsureGrantAuthorized(Actor, request.TenantId, logger, request.GrantedBy);

        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= SystemClock.UtcNow)
        {
            throw new InvalidOperationException("Bulk grant expiration must be in the future");
        }

        var permissions = ToPermissionNames(request.Permissions);
        var userIds = request.UserIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var result = new TenantBulkPermissionResult
        {
            TotalRequested = userIds.Length,
            ProcessedAt = SystemClock.UtcNow
        };

        foreach (var userId in userIds)
        {
            try
            {
                await bulkService.BulkGrantTenantPermissionAsync(
                        [userId],
                        request.TenantId,
                        permissions,
                        Guid.TryParse(request.GrantedBy, out var grantedBy) ? grantedBy : null,
                        request.ExpiresAt,
                        request.Reason,
                        cancellationToken)
                    .ConfigureAwait(false);

                result.Successful++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Bulk grant failed for user {UserId} in tenant {TenantId}",
                    userId, request.TenantId);

                if (request.Atomic)
                {
                    // All-or-nothing: let the exception escape so the ambient transaction
                    // (UseCaseOperationBehavior) rolls back every already-granted user.
                    throw new InvalidOperationException(
                        $"Atomic bulk grant aborted after a per-user failure (user {userId}, tenant {request.TenantId}); the transaction is rolled back and no user keeps a grant.",
                        ex);
                }

                result.Failed++;
                result.Failures.Add(new UserBulkPermissionFailure
                {
                    UserId = userId,
                    Error = ex.GetType().Name,
                    Details = ex.Message
                });
            }
        }

        return result;
    }

    internal static string[] ToPermissionNames(IEnumerable<PermissionType> permissions) =>
        permissions.Select(p => p.ToString()).ToArray();
}

/// <summary>
///     Handler for <see cref="BulkRevokeTenantPermissionsCommand"/>.
///     Delegates per-user revocation to <see cref="IPermissionGrantService"/>, which bumps
///     tenant security versions and writes audit entries for each mutation.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> same guards as bulk grant — tenant-admin within the actor's
///     own tenant or SystemAdmin; global defaults additionally require
///     <c>system:manage-global-defaults</c>.</para>
///     <para><b>Issue #310:</b> revocation outcomes are counted by the service's boolean
///     result — a user with no matching grant is reported as a per-user failure
///     (<c>NoMatchingGrant</c>) instead of being counted as a successful revoke.</para>
///     <para><b>Transactionality (issues #302/#365):</b> same contract as the bulk grant
///     handler — per-item outcomes commit by default; <see cref="BulkRevokeTenantPermissionsCommand.Atomic"/>
///     mode throws on the first failure and rolls the ambient transaction back.</para>
/// </remarks>
public sealed class BulkRevokeTenantPermissionsCommandHandler(
    IPermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor,
    ILogger<BulkRevokeTenantPermissionsCommandHandler> logger
) : ICommandHandler<BulkRevokeTenantPermissionsCommand, BulkPermissionResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<BulkPermissionResult> Handle(
        BulkRevokeTenantPermissionsCommand request,
        CancellationToken cancellationToken)
    {
        BulkTenantPermissionGuards.EnsureRevokeAuthorized(Actor, request.TenantId, logger, request.RevokedBy);

        var permissions = BulkGrantTenantPermissionsCommandHandler.ToPermissionNames(request.Permissions);
        var userIds = request.UserIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var result = new TenantBulkPermissionResult
        {
            TotalRequested = userIds.Length,
            ProcessedAt = SystemClock.UtcNow
        };

        foreach (var userId in userIds)
        {
            try
            {
                var revoked = await grantService.RevokeTenantPermissionAsync(
                        userId,
                        request.TenantId,
                        permissions,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (revoked)
                {
                    result.Successful++;
                }
                else
                {
                    if (request.Atomic)
                    {
                        throw new InvalidOperationException(
                            $"Atomic bulk revoke aborted: user {userId} in tenant {request.TenantId} has no matching grant; the transaction is rolled back.");
                    }

                    result.Failed++;
                    result.Failures.Add(new UserBulkPermissionFailure
                    {
                        UserId = userId,
                        Error = "NoMatchingGrant",
                        Details = "The user has no matching permission grant to revoke"
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Bulk revoke failed for user {UserId} in tenant {TenantId}",
                    userId, request.TenantId);

                if (request.Atomic)
                {
                    // All-or-nothing: let the exception escape so the ambient transaction
                    // (UseCaseOperationBehavior) rolls back every already-revoked user.
                    throw new InvalidOperationException(
                        $"Atomic bulk revoke aborted after a per-user failure (user {userId}, tenant {request.TenantId}); the transaction is rolled back.",
                        ex);
                }

                result.Failed++;
                result.Failures.Add(new UserBulkPermissionFailure
                {
                    UserId = userId,
                    Error = ex.GetType().Name,
                    Details = ex.Message
                });
            }
        }

        return result;
    }
}

/// <summary>
///     Shared authorization guards for bulk tenant permission mutations.
///     Mirrors the guards used by the Authorization module's single-grant handlers.
/// </summary>
internal static class BulkTenantPermissionGuards
{
    public static void EnsureGrantAuthorized(ActorContext actor, Guid tenantId, ILogger logger, string? requestedBy)
    {
        EnsureAuthorized(actor, tenantId, logger, requestedBy, "grant");
    }

    public static void EnsureRevokeAuthorized(ActorContext actor, Guid tenantId, ILogger logger, string? requestedBy)
    {
        EnsureAuthorized(actor, tenantId, logger, requestedBy, "revoke");
    }

    private static void EnsureAuthorized(ActorContext actor, Guid tenantId, ILogger logger, string? requestedBy, string operation)
    {
        if (!actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        if (actor.IsSystemAdmin)
        {
            return;
        }

        // Global defaults (Guid.Empty tenant) affect every tenant.
        if (tenantId == Guid.Empty)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to bulk {Operation} global default permissions without '{Permission}'",
                actor.SubjectId, operation, SystemPermission.Keys.ManageGlobalDefaults);

            throw new UnauthorizedAccessException(
                $"Bulk {operation} of global default permissions requires '{SystemPermission.Keys.ManageGlobalDefaults}' permission");
        }

        if (tenantId != actor.TenantId || !actor.IsTenantAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to bulk {Operation} permissions in tenant {TenantId} without tenant administration",
                actor.SubjectId, operation, tenantId);

            throw new UnauthorizedAccessException(
                $"Bulk {operation} of tenant permissions requires system administration or tenant administration of the target tenant");
        }
    }
}

/// <summary>
///     Concrete bulk-result carrying per-user failures for tenant permission operations.
/// </summary>
/// <remarks>
///     <para><b>Issues #302/#310:</b> implements <see cref="GameGuild.ICommitOnFailureOutcome"/>
///     and exposes <see cref="BulkPermissionResult.IsFailure"/>/
///     <see cref="BulkPermissionResult.IsSuccess"/>, so the host's transactional outcome
///     classifier recognizes the partial-failure contract: a per-item (non-atomic) bulk
///     command with collected failures is reported as a business failure whose completed
///     mutations are deliberately retained — successful items commit while every failure is
///     reported with its per-user entry. Atomic-mode failures never reach this result: the
///     handler throws and the ambient transaction rolls everything back.</para>
/// </remarks>
public sealed class TenantBulkPermissionResult : BulkPermissionResult, GameGuild.ICommitOnFailureOutcome;

/// <summary>
///     A per-user failure inside a bulk tenant permission operation.
/// </summary>
public sealed class UserBulkPermissionFailure : BulkPermissionFailure;

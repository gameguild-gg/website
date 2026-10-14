using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Admin-assisted MFA reset: disables the target user's MFA through the shared
///     disable path (clears secrets, revokes all tokens, terminates sessions) and
///     cancels any live lost-device recovery request.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> the guard runs inside this handler — SystemAdmin
///     (any tenant), or a tenant administrator holding <c>permissions:manage</c>
///     whose tenant matches the target user's tenant. Cross-tenant resets are
///     denied unless the caller is a SystemAdmin. Every success bumps the tenant
///     security version and writes a permission audit entry; the acting
///     administrator is taken from the actor context, never from the request.</para>
/// </remarks>
public sealed class AdminResetUserMfaCommandHandler(
    IMfaAttemptTrackingService mfaTrackingService,
    IMfaRecoveryRequestStore recoveryStore,
    IUserRepository userRepository,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService,
    ILogger<AdminResetUserMfaCommandHandler> logger) : ICommandHandler<AdminResetUserMfaCommand, AdminResetUserMfaResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<AdminResetUserMfaResult> Handle(AdminResetUserMfaCommand request, CancellationToken cancellationToken)
    {
        var actor = Actor;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } actorId)
        {
            throw new UnauthorizedAccessException("An authenticated administrator is required to reset a user's MFA");
        }

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", request.UserId);

        EnsureAuthorized(actor, user.TenantId);

        var disabled = await mfaTrackingService.DisableMfaAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        var cancelledRequest = await recoveryStore.CancelLiveByUserAsync(
            request.UserId.ToString(), cancellationToken).ConfigureAwait(false);

        var tenantKey = user.TenantId?.ToString() ?? Guid.Empty.ToString();
        await securityVersionStore.IncrementVersionAsync(tenantKey, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            request.UserId,
            actorId,
            user.TenantId,
            permissionType: "Mfa",
            resourceType: "UserMfaConfiguration",
            oldValue: disabled ? "Enabled" : null,
            newValue: "Disabled",
            reason: cancelledRequest is null
                ? "Admin-assisted MFA reset"
                : "Admin-assisted MFA reset; live recovery request cancelled",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Admin {ActorId} reset MFA for user {UserId} in tenant {TenantId}; MfaWasEnabled: {Disabled}, LiveRecoveryCancelled: {Cancelled}",
            actorId, request.UserId, tenantKey, disabled, cancelledRequest is not null);

        return new AdminResetUserMfaResult
        {
            Success = true,
            Message = disabled
                ? "Multi-factor authentication has been reset for the user."
                : "Multi-factor authentication was not enabled; any live recovery request has been cancelled.",
            MfaWasEnabled = disabled,
            LiveRecoveryCancelled = cancelledRequest is not null
        };
    }

    private static void EnsureAuthorized(ActorContext actor, Guid? targetTenantId)
    {
        if (actor.IsSystemAdmin)
        {
            return;
        }

        var callerTenant = actor.TenantId;
        var isTenantManager = actor.IsTenantAdmin || actor.HasPermission(SystemPermission.Keys.ManagePermissions);

        if (!isTenantManager || callerTenant is null || targetTenantId is null || callerTenant != targetTenantId)
        {
            throw new UnauthorizedAccessException(
                "Resetting a user's multi-factor authentication requires system administration, or tenant administration of the user's tenant");
        }
    }
}

using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;

using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Command to grant tenant-level permissions to a user.
/// </summary>
public sealed record GrantTenantPermissionCommand : ICommand<Guid>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the user ID to grant permissions to.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    ///     Gets the permissions to grant.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the ID of the user granting the permissions.
    /// </summary>
    public required Guid GrantedBy { get; init; }

    /// <summary>
    ///     Gets the optional expiration date for the permissions.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    ///     Gets the optional reason for granting permissions.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for GrantTenantPermissionCommand.
/// </summary>
public sealed class GrantTenantPermissionCommandHandler(
    IPermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor,
    ILogger<GrantTenantPermissionCommandHandler> logger)
    : ICommandHandler<GrantTenantPermissionCommand, Guid>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<Guid> Handle(GrantTenantPermissionCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Granting tenant permissions {Permissions} to user {UserId} in tenant {TenantId}",
            string.Join(", ", request.Permissions),
            request.UserId,
            request.TenantId);

        // SECURITY: Global defaults (tenantId=null or Empty) require ManageGlobalDefaults permission
        var isGlobalDefault = request.TenantId.Value == Guid.Empty;
        if (isGlobalDefault)
        {
            if (!Actor.IsAuthenticated ||
                (!Actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults) && !Actor.IsSystemAdmin))
            {
                logger.LogWarning(
                    "Actor {ActorId} attempted to modify global default permissions without ManageGlobalDefaults permission",
                    Actor.SubjectId);

                throw new UnauthorizedAccessException(
                    "Modifying global default permissions requires 'system:manage-global-defaults' permission");
            }
        }
        else
        {
            // Check if current user is tenant admin for tenant-specific grants
            if (!Actor.IsAuthenticated ||
                (!Actor.IsSystemAdmin && (!Actor.IsTenantAdmin || Actor.TenantId != request.TenantId.Value)))
            {
                logger.LogWarning(
                    "Actor {ActorId} attempted to grant tenant permissions without same-tenant admin privileges",
                    Actor.SubjectId);

                throw new UnauthorizedAccessException("Only tenant or system administrators can grant tenant permissions");
            }
        }

        var tenantPermission = await grantService.GrantTenantPermissionAsync(
                request.UserId,
                request.TenantId,
                request.Permissions,
                Actor.SubjectIdAsGuid,
                request.ExpiresAt,
                request.Reason,
                cancellationToken)
            ;

        logger.LogInformation(
            "Successfully granted tenant permissions to user {UserId}: {PermissionId}",
            request.UserId,
            tenantPermission.Id);

        return tenantPermission.Id;
    }
}

/// <summary>
///     Command to revoke tenant-level permissions from a user.
/// </summary>
public sealed record RevokeTenantPermissionCommand : ICommand<bool>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the user ID to revoke permissions from.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    ///     Gets the permissions to revoke.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the ID of the user revoking the permissions.
    /// </summary>
    public required Guid RevokedBy { get; init; }

    /// <summary>
    ///     Gets the optional reason for revoking permissions.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for RevokeTenantPermissionCommand.
/// </summary>
public sealed class RevokeTenantPermissionCommandHandler(
    IPermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor,
    ILogger<RevokeTenantPermissionCommandHandler> logger)
    : ICommandHandler<RevokeTenantPermissionCommand, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(RevokeTenantPermissionCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Revoking tenant permissions {Permissions} from user {UserId} in tenant {TenantId}. Reason: {Reason}",
            string.Join(", ", request.Permissions),
            request.UserId,
            request.TenantId,
            request.Reason ?? "Not specified");

        // SECURITY: Global defaults (tenantId=null or Empty) require ManageGlobalDefaults permission
        var isGlobalDefault = request.TenantId.Value == Guid.Empty;
        if (isGlobalDefault)
        {
            if (!Actor.IsAuthenticated ||
                (!Actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults) && !Actor.IsSystemAdmin))
            {
                logger.LogWarning(
                    "Actor {ActorId} attempted to modify global default permissions without ManageGlobalDefaults permission",
                    Actor.SubjectId);

                throw new UnauthorizedAccessException(
                    "Modifying global default permissions requires 'system:manage-global-defaults' permission");
            }
        }
        else
        {
            // Check if current user is tenant admin for tenant-specific revocations
            if (!Actor.IsAuthenticated ||
                (!Actor.IsSystemAdmin && (!Actor.IsTenantAdmin || Actor.TenantId != request.TenantId.Value)))
            {
                logger.LogWarning(
                    "Actor {ActorId} attempted to revoke tenant permissions without same-tenant admin privileges",
                    Actor.SubjectId);

                throw new UnauthorizedAccessException("Only tenant or system administrators can revoke tenant permissions");
            }
        }

        // Prevent revoking own admin permissions
        if (request.UserId == Actor.SubjectIdAsGuid &&
            (request.Permissions.Contains("TenantAdmin") || request.Permissions.Contains("Admin")))
        {
            logger.LogWarning("User {UserId} attempted to revoke their own admin permissions", request.UserId);

            throw new InvalidOperationException("Cannot revoke your own admin permissions");
        }

        var success = await grantService.RevokeTenantPermissionAsync(
                request.UserId,
                request.TenantId,
                request.Permissions,
                cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Revoke tenant permissions completed for user {UserId}: {Success}",
            request.UserId,
            success);

        return success;
    }
}

// ========================================================================
// GLOBAL/TENANT DEFAULT PERMISSIONS COMMANDS
// ========================================================================

/// <summary>
///     Command to set global default permissions.
///     These are baseline permissions applied to all users across all tenants.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires <c>system:manage-global-defaults</c> permission.</para>
/// </remarks>
public sealed record SetGlobalDefaultPermissionsCommand : ICommand<bool>
{
    /// <summary>
    ///     Gets the permissions to set as global defaults.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the ID of the user setting the permissions.
    /// </summary>
    public required Guid SetBy { get; init; }
}

/// <summary>
///     Handler for SetGlobalDefaultPermissionsCommand.
/// </summary>
public sealed class SetGlobalDefaultPermissionsCommandHandler(
    IPermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor,
    ILogger<SetGlobalDefaultPermissionsCommandHandler> logger)
    : ICommandHandler<SetGlobalDefaultPermissionsCommand, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(SetGlobalDefaultPermissionsCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Setting global default permissions: {Permissions}",
            string.Join(", ", request.Permissions));

        // SECURITY: Global defaults require ManageGlobalDefaults permission
        if (!Actor.IsAuthenticated ||
            (!Actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults) && !Actor.IsSystemAdmin))
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to set global default permissions without ManageGlobalDefaults permission",
                Actor.SubjectId);

            throw new UnauthorizedAccessException(
                "Setting global default permissions requires 'system:manage-global-defaults' permission");
        }

        await grantService.SetGlobalDefaultPermissionsAsync(
                request.Permissions,
                Actor.SubjectIdAsGuid,
                cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Successfully set global default permissions by actor {ActorId}",
            Actor.SubjectId);

        return true;
    }
}

/// <summary>
///     Command to set tenant default permissions.
///     These are baseline permissions applied to all users in a specific tenant.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires tenant admin or system admin privileges.</para>
/// </remarks>
public sealed record SetTenantDefaultPermissionsCommand : ICommand<bool>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the permissions to set as tenant defaults.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the ID of the user setting the permissions.
    /// </summary>
    public required Guid SetBy { get; init; }
}

/// <summary>
///     Handler for SetTenantDefaultPermissionsCommand.
/// </summary>
public sealed class SetTenantDefaultPermissionsCommandHandler(
    IPermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor,
    ILogger<SetTenantDefaultPermissionsCommandHandler> logger)
    : ICommandHandler<SetTenantDefaultPermissionsCommand, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(SetTenantDefaultPermissionsCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Setting tenant {TenantId} default permissions: {Permissions}",
            request.TenantId,
            string.Join(", ", request.Permissions));

        // SECURITY: Tenant defaults require tenant admin or system admin
        if (!Actor.IsAuthenticated ||
            (!Actor.IsSystemAdmin && (!Actor.IsTenantAdmin || Actor.TenantId != request.TenantId.Value)))
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to set tenant default permissions without same-tenant admin privileges",
                Actor.SubjectId);

            throw new UnauthorizedAccessException(
                "Setting tenant default permissions requires tenant admin or system admin privileges");
        }

        await grantService.SetTenantDefaultPermissionsAsync(
                request.TenantId,
                request.Permissions,
                Actor.SubjectIdAsGuid,
                cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Successfully set tenant {TenantId} default permissions by actor {ActorId}",
            request.TenantId,
            Actor.SubjectId);

        return true;
    }
}

/// <summary>
///     Command to deny tenant-level permissions from a user.
///     Denied permissions take precedence over allowed permissions (DENY-WINS).
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires tenant admin or system admin privileges.</para>
///     <para>For global defaults (tenantId=Empty), requires <c>system:manage-global-defaults</c> permission.</para>
/// </remarks>
public sealed record DenyTenantPermissionCommand : ICommand<Guid>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the user ID to deny permissions for.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    ///     Gets the permissions to deny.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the ID of the user denying the permissions.
    /// </summary>
    public required Guid DeniedBy { get; init; }

    /// <summary>
    ///     Gets the optional reason for denying permissions.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for DenyTenantPermissionCommand.
/// </summary>
public sealed class DenyTenantPermissionCommandHandler(
    IPermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor,
    ILogger<DenyTenantPermissionCommandHandler> logger)
    : ICommandHandler<DenyTenantPermissionCommand, Guid>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<Guid> Handle(DenyTenantPermissionCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Denying tenant permissions {Permissions} for user {UserId} in tenant {TenantId}. Reason: {Reason}",
            string.Join(", ", request.Permissions),
            request.UserId,
            request.TenantId,
            request.Reason ?? "Not specified");

        // SECURITY: Global defaults (tenantId=null or Empty) require ManageGlobalDefaults permission
        var isGlobalDefault = request.TenantId.Value == Guid.Empty;
        if (isGlobalDefault)
        {
            if (!Actor.IsAuthenticated ||
                (!Actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults) && !Actor.IsSystemAdmin))
            {
                logger.LogWarning(
                    "Actor {ActorId} attempted to modify global default deny permissions without ManageGlobalDefaults permission",
                    Actor.SubjectId);

                throw new UnauthorizedAccessException(
                    "Modifying global default permissions requires 'system:manage-global-defaults' permission");
            }
        }
        else
        {
            // Check if current user is tenant admin for tenant-specific denials
            if (!Actor.IsAuthenticated ||
                (!Actor.IsSystemAdmin && (!Actor.IsTenantAdmin || Actor.TenantId != request.TenantId.Value)))
            {
                logger.LogWarning(
                    "Actor {ActorId} attempted to deny tenant permissions without same-tenant admin privileges",
                    Actor.SubjectId);

                throw new UnauthorizedAccessException("Only tenant or system administrators can deny tenant permissions");
            }
        }

        var tenantPermission = await grantService.DenyTenantPermissionAsync(
                request.UserId,
                request.TenantId,
                request.Permissions,
                Actor.SubjectIdAsGuid,
                request.Reason,
                cancellationToken)
            ;

        logger.LogInformation(
            "Successfully denied tenant permissions for user {UserId}: {PermissionId}",
            request.UserId,
            tenantPermission.Id);

        return tenantPermission.Id;
    }
}

/// <summary>
///     Command to remove deny entries from a user's permissions.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires tenant admin or system admin privileges.</para>
///     <para>For global defaults (tenantId=Empty), requires <c>system:manage-global-defaults</c> permission.</para>
/// </remarks>
public sealed record RemoveDenyPermissionsCommand : ICommand<bool>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the user ID to remove deny permissions from.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    ///     Gets the deny permissions to remove.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the ID of the user removing the deny permissions.
    /// </summary>
    public required Guid RemovedBy { get; init; }
}

/// <summary>
///     Handler for RemoveDenyPermissionsCommand.
/// </summary>
public sealed class RemoveDenyPermissionsCommandHandler(
    IPermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor,
    ILogger<RemoveDenyPermissionsCommandHandler> logger)
    : ICommandHandler<RemoveDenyPermissionsCommand, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(RemoveDenyPermissionsCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Removing deny permissions {Permissions} from user {UserId} in tenant {TenantId}",
            string.Join(", ", request.Permissions),
            request.UserId,
            request.TenantId);

        // SECURITY: Global defaults (tenantId=null or Empty) require ManageGlobalDefaults permission
        var isGlobalDefault = request.TenantId.Value == Guid.Empty;
        if (isGlobalDefault)
        {
            if (!Actor.IsAuthenticated ||
                (!Actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults) && !Actor.IsSystemAdmin))
            {
                logger.LogWarning(
                    "Actor {ActorId} attempted to modify global default deny permissions without ManageGlobalDefaults permission",
                    Actor.SubjectId);

                throw new UnauthorizedAccessException(
                    "Modifying global default permissions requires 'system:manage-global-defaults' permission");
            }
        }
        else
        {
            // Check if current user is tenant admin for tenant-specific removals
            if (!Actor.IsAuthenticated ||
                (!Actor.IsSystemAdmin && (!Actor.IsTenantAdmin || Actor.TenantId != request.TenantId.Value)))
            {
                logger.LogWarning(
                    "Actor {ActorId} attempted to remove deny permissions without same-tenant admin privileges",
                    Actor.SubjectId);

                throw new UnauthorizedAccessException("Only tenant or system administrators can remove deny permissions");
            }
        }

        var success = await grantService.RemoveDenyPermissionsAsync(
                request.UserId,
                request.TenantId,
                request.Permissions,
                cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Remove deny permissions completed for user {UserId}: {Success}",
            request.UserId,
            success);

        return success;
    }
}

// ========================================================================
// PERMISSION EXPIRATION COMMANDS (issue #331)
// ========================================================================

/// <summary>
///     Command to bulk-set an absolute expiration for permission grants in one tenant.
///     A <c>null</c> <see cref="ExpiresAt"/> clears the expiration.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires tenant admin for the target tenant or system admin.
///     For global defaults (tenantId=Empty), requires <c>system:manage-global-defaults</c> permission.</para>
/// </remarks>
public sealed record SetTenantPermissionExpirationCommand : ICommand<int>
{
    /// <summary>
    ///     Gets the tenant ID that owns the grants.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the IDs of the permission grants to update.
    /// </summary>
    public required Guid[] PermissionIds { get; init; }

    /// <summary>
    ///     Gets the new expiration instant (null = permanent).
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    ///     Gets the optional reason recorded in the audit log.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for SetTenantPermissionExpirationCommand.
/// </summary>
public sealed class SetTenantPermissionExpirationCommandHandler(
    IPermissionExpirationService expirationService,
    IActorContextAccessor actorContextAccessor,
    ILogger<SetTenantPermissionExpirationCommandHandler> logger)
    : ICommandHandler<SetTenantPermissionExpirationCommand, int>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<int> Handle(SetTenantPermissionExpirationCommand request, CancellationToken cancellationToken)
    {
        PermissionExpirationCommandGuards.EnsureCanManageTenant(Actor, request.TenantId.Value, "set permission expirations");

        logger.LogInformation(
            "Setting expiration {ExpiresAt} for {Count} permission grants in tenant {TenantId}",
            request.ExpiresAt,
            request.PermissionIds.Length,
            request.TenantId);

        var updated = await expirationService.SetExpirationAsync(
                request.TenantId.Value,
                request.PermissionIds,
                request.ExpiresAt,
                request.Reason,
                cancellationToken)
            .ConfigureAwait(false);

        return updated.Count;
    }
}

/// <summary>
///     Command to bulk-extend the expiration of permission grants in one tenant by a
///     positive time period.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires tenant admin for the target tenant or system admin.
///     For global defaults (tenantId=Empty), requires <c>system:manage-global-defaults</c> permission.</para>
/// </remarks>
public sealed record ExtendTenantPermissionExpirationCommand : ICommand<int>
{
    /// <summary>
    ///     Gets the tenant ID that owns the grants.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the IDs of the permission grants to extend.
    /// </summary>
    public required Guid[] PermissionIds { get; init; }

    /// <summary>
    ///     Gets the positive extension period.
    /// </summary>
    public required TimeSpan Extension { get; init; }

    /// <summary>
    ///     Gets the optional reason recorded in the audit log.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for ExtendTenantPermissionExpirationCommand.
/// </summary>
public sealed class ExtendTenantPermissionExpirationCommandHandler(
    IPermissionExpirationService expirationService,
    IActorContextAccessor actorContextAccessor,
    ILogger<ExtendTenantPermissionExpirationCommandHandler> logger)
    : ICommandHandler<ExtendTenantPermissionExpirationCommand, int>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<int> Handle(ExtendTenantPermissionExpirationCommand request, CancellationToken cancellationToken)
    {
        PermissionExpirationCommandGuards.EnsureCanManageTenant(Actor, request.TenantId.Value, "extend permission expirations");

        logger.LogInformation(
            "Extending expiration by {Extension} for {Count} permission grants in tenant {TenantId}",
            request.Extension,
            request.PermissionIds.Length,
            request.TenantId);

        var updated = await expirationService.ExtendExpirationAsync(
                request.TenantId.Value,
                request.PermissionIds,
                request.Extension,
                request.Reason,
                cancellationToken)
            .ConfigureAwait(false);

        return updated.Count;
    }
}

/// <summary>
///     Command to immediately process (deactivate, audit, notify) all expired
///     permission grants, without waiting for the background worker cycle.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires system admin privileges (operates across tenants).</para>
/// </remarks>
public sealed record ProcessExpiredPermissionsCommand : ICommand<int>;

/// <summary>
///     Handler for ProcessExpiredPermissionsCommand.
/// </summary>
public sealed class ProcessExpiredPermissionsCommandHandler(
    IPermissionExpirationService expirationService,
    IActorContextAccessor actorContextAccessor,
    ILogger<ProcessExpiredPermissionsCommandHandler> logger)
    : ICommandHandler<ProcessExpiredPermissionsCommand, int>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<int> Handle(ProcessExpiredPermissionsCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated || !Actor.IsSystemAdmin)
        {
            throw new UnauthorizedAccessException(
                "Processing expired permissions across tenants requires system administrator privileges");
        }

        var processed = await expirationService.ProcessExpiredAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Manually processed {Count} expired permission grants.", processed);

        return processed;
    }
}

/// <summary>
///     Command to immediately publish upcoming-expiration notifications for grants
///     expiring within the configured window, without waiting for the background
///     worker cycle.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires system admin privileges (operates across tenants).</para>
/// </remarks>
public sealed record SendExpirationRemindersCommand : ICommand<int>;

/// <summary>
///     Handler for SendExpirationRemindersCommand.
/// </summary>
public sealed class SendExpirationRemindersCommandHandler(
    IPermissionExpirationService expirationService,
    IActorContextAccessor actorContextAccessor,
    ILogger<SendExpirationRemindersCommandHandler> logger)
    : ICommandHandler<SendExpirationRemindersCommand, int>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<int> Handle(SendExpirationRemindersCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated || !Actor.IsSystemAdmin)
        {
            throw new UnauthorizedAccessException(
                "Sending expiration reminders across tenants requires system administrator privileges");
        }

        var published = await expirationService
            .SendUpcomingExpirationRemindersAsync(cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Manually published {Count} expiration reminders.", published);

        return published;
    }
}

/// <summary>
///     Shared authorization guard for the bulk expiration command handlers: mirrors the
///     Grant/Revoke guard (global defaults require ManageGlobalDefaults; tenant grants
///     require same-tenant admin or system admin).
/// </summary>
internal static class PermissionExpirationCommandGuards
{
    public static void EnsureCanManageTenant(ActorContext actor, Guid tenantId, string operation)
    {
        var isGlobalDefault = tenantId == Guid.Empty;
        if (isGlobalDefault)
        {
            if (!actor.IsAuthenticated ||
                (!actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults) && !actor.IsSystemAdmin))
            {
                throw new UnauthorizedAccessException(
                    $"Modifying global default permissions requires '{SystemPermission.Keys.ManageGlobalDefaults}' permission. Attempted operation: {operation}");
            }

            return;
        }

        if (!actor.IsAuthenticated ||
            (!actor.IsSystemAdmin && (!actor.IsTenantAdmin || actor.TenantId != tenantId)))
        {
            throw new UnauthorizedAccessException(
                $"Only tenant or system administrators can {operation} for the target tenant");
        }
    }
}

using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Command to update a specific user's permissions on a resource.
/// </summary>
public sealed record UpdateUserPermissionsCommand : ICommand<PermissionUpdateResult>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the type of resource.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    ///     Gets the ID of the resource.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    ///     Gets the ID of the user whose permissions are being updated.
    /// </summary>
    public required Guid TargetUserId { get; init; }

    /// <summary>
    ///     Gets the new set of permissions to grant to the user.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the ID of the user making the update.
    /// </summary>
    public required Guid UpdatedByUserId { get; init; }

    /// <summary>
    ///     Gets the optional expiration date for the permissions.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
///     Handler for UpdateUserPermissionsCommand.
/// </summary>
public sealed class UpdateUserPermissionsCommandHandler(
    IResourcePermissionService resourcePermissionService,
    IActorContextAccessor actorContextAccessor,
    IPermissionQueryService queryService,
    ILogger<UpdateUserPermissionsCommandHandler> logger)
    : ICommandHandler<UpdateUserPermissionsCommand, PermissionUpdateResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionUpdateResult> Handle(UpdateUserPermissionsCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Updating permissions for user {TargetUserId} on resource {ResourceType}/{ResourceId}",
            request.TargetUserId,
            request.ResourceType,
            request.ResourceId);

        // Check if the current user has permission to manage permissions for this resource
        var resourceIdGuid = Guid.Parse(request.ResourceId);
        var resourcePermission = $"{request.ResourceType}.{resourceIdGuid}.Admin";
        var hasManagePermission = Actor.TenantId.HasValue && await queryService.HasTenantPermissionAsync(
            Actor.SubjectIdAsGuid!.Value,
            Actor.TenantId.Value,
            resourcePermission,
            cancellationToken).ConfigureAwait(false);

        if (!hasManagePermission)
        {
            logger.LogWarning(
                "User {UserId} attempted to update permissions on resource {ResourceType}/{ResourceId} without Admin permission",
                request.UpdatedByUserId,
                request.ResourceType,
                request.ResourceId);

            return new PermissionUpdateResult
            {
                Success = false,
                ErrorMessage = "You don't have permission to update user permissions for this resource"
            };
        }

        var result = await resourcePermissionService.UpdateUserPermissionsAsync(
            request.TenantId,
            request.ResourceType,
            request.ResourceId,
            new UpdatePermissionsRequest(request.TargetUserId, request.Permissions, request.ExpiresAt),
            request.UpdatedByUserId,
            cancellationToken);

        logger.LogInformation(
            "Permission update completed for user {TargetUserId}: {Success}",
            request.TargetUserId,
            result.Success);

        return result;
    }
}

/// <summary>
///     Command to share a resource with one or more users by granting them permissions.
/// </summary>
public sealed record ShareResourceCommand : ICommand<ShareResult>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the type of resource being shared.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    ///     Gets the ID of the resource being shared.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    ///     Gets the IDs of users to share the resource with.
    /// </summary>
    public required Guid[] UserIds { get; init; }

    /// <summary>
    ///     Gets the email addresses of users to share with (alternative to UserIds).
    /// </summary>
    public string[]? UserEmails { get; init; }

    /// <summary>
    ///     Gets the permissions to grant to the users.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the ID of the user who is sharing the resource.
    /// </summary>
    public required Guid GrantedByUserId { get; init; }

    /// <summary>
    ///     Gets the optional expiration date for the granted permissions.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    ///     Gets the optional message to include with the share.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    ///     Gets whether the users must accept the share before gaining access.
    /// </summary>
    public bool RequireAcceptance { get; init; } = true;

    /// <summary>
    ///     Gets whether to notify users about the share via email/notification.
    /// </summary>
    public bool NotifyUsers { get; init; } = true;
}

/// <summary>
///     Handler for ShareResourceCommand.
/// </summary>
public sealed class ShareResourceCommandHandler(
    IResourcePermissionService resourcePermissionService,
    IActorContextAccessor actorContextAccessor,
    IPermissionQueryService queryService,
    ILogger<ShareResourceCommandHandler> logger)
    : ICommandHandler<ShareResourceCommand, ShareResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<ShareResult> Handle(ShareResourceCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Sharing resource {ResourceType}/{ResourceId} by user {UserId}",
            request.ResourceType,
            request.ResourceId,
            request.GrantedByUserId);

        // Check if the current user has permission to share this resource
        var resourceIdGuid = Guid.Parse(request.ResourceId);
        var resourcePermission = $"{request.ResourceType}.{resourceIdGuid}.Share";
        var hasSharePermission = Actor.TenantId.HasValue && await queryService.HasTenantPermissionAsync(
            Actor.SubjectIdAsGuid!.Value,
            Actor.TenantId.Value,
            resourcePermission,
            cancellationToken).ConfigureAwait(false);

        if (!hasSharePermission)
        {
            logger.LogWarning(
                "User {UserId} attempted to share resource {ResourceType}/{ResourceId} without Share permission",
                request.GrantedByUserId,
                request.ResourceType,
                request.ResourceId);

            return ShareResult.Failure("You don't have permission to share this resource");
        }

        // Share with user emails if provided
        if (request.UserEmails != null && request.UserEmails.Length > 0)
        {
            // For simplicity, share with the first email only
            // BulkShareResourceAsync could be used for multiple emails
            var email = request.UserEmails[0];
            var shareRequest = new ShareResourceRequest(
                email,
                request.Permissions,
                request.ExpiresAt,
                request.Message);

            var result = await resourcePermissionService.ShareResourceAsync(
                request.TenantId,
                request.ResourceType,
                request.ResourceId,
                shareRequest,
                request.GrantedByUserId,
                cancellationToken);

            logger.LogInformation(
                "Share resource completed: Success={Success}",
                result.Success);

            return result;
        }

        // If no emails, return failure
        return ShareResult.Failure("No user emails provided for sharing");
    }
}

/// <summary>
///     Command to accept a resource invitation addressed to the current user.
/// </summary>
/// <param name="InvitationId">The invitation ID.</param>
public sealed record AcceptResourceInvitationCommand(Guid InvitationId) : ICommand<InvitationActionResult>;

/// <summary>
///     Handler for AcceptResourceInvitationCommand.
/// </summary>
public sealed class AcceptResourceInvitationCommandHandler(
    IApplicationDbContext dbContext,
    IResourcePermissionService resourcePermissionService,
    IActorContextAccessor actorContextAccessor,
    ILogger<AcceptResourceInvitationCommandHandler> logger)
    : ICommandHandler<AcceptResourceInvitationCommand, InvitationActionResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<InvitationActionResult> Handle(AcceptResourceInvitationCommand request, CancellationToken cancellationToken)
    {
        var userId = Actor.SubjectIdAsGuid ?? throw new UnauthorizedAccessException("User not authenticated");
        var email = Actor.TypedAttributes.Email;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UnauthorizedAccessException("Authenticated user must have an email address to accept an invitation");
        }

        var invitation = await dbContext.Set<ResourceInvitation>()
            .FirstOrDefaultAsync(i => i.Id == request.InvitationId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation == null)
        {
            return InvitationActionResult.Failure(request.InvitationId, "Invitation not found");
        }

        if (!EmailsMatch(invitation.Email, email))
        {
            logger.LogWarning(
                "User {UserId} attempted to accept invitation {InvitationId} for another email address",
                userId,
                request.InvitationId);

            throw new UnauthorizedAccessException("This invitation is not addressed to the current user");
        }

        var accepted = await resourcePermissionService
            .AcceptInvitationAsync(request.InvitationId, userId, cancellationToken)
            .ConfigureAwait(false);

        return accepted
            ? InvitationActionResult.SuccessResult(invitation, InvitationStatus.Accepted)
            : InvitationActionResult.Failure(request.InvitationId, "Invitation could not be accepted");
    }

    private static bool EmailsMatch(string left, string right)
    {
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
///     Command to decline a resource invitation addressed to the current user.
/// </summary>
/// <param name="InvitationId">The invitation ID.</param>
/// <param name="Reason">Optional reason for declining.</param>
public sealed record DeclineResourceInvitationCommand(Guid InvitationId, string? Reason = null) : ICommand<InvitationActionResult>;

/// <summary>
///     Handler for DeclineResourceInvitationCommand.
/// </summary>
public sealed class DeclineResourceInvitationCommandHandler(
    IApplicationDbContext dbContext,
    IResourcePermissionService resourcePermissionService,
    IActorContextAccessor actorContextAccessor,
    ILogger<DeclineResourceInvitationCommandHandler> logger)
    : ICommandHandler<DeclineResourceInvitationCommand, InvitationActionResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<InvitationActionResult> Handle(DeclineResourceInvitationCommand request, CancellationToken cancellationToken)
    {
        var email = Actor.TypedAttributes.Email;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UnauthorizedAccessException("Authenticated user must have an email address to decline an invitation");
        }

        var invitation = await dbContext.Set<ResourceInvitation>()
            .FirstOrDefaultAsync(i => i.Id == request.InvitationId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation == null)
        {
            return InvitationActionResult.Failure(request.InvitationId, "Invitation not found");
        }

        if (!EmailsMatch(invitation.Email, email))
        {
            logger.LogWarning(
                "User with email {Email} attempted to decline invitation {InvitationId} for another recipient",
                LogRedaction.MaskEmail(email),
                request.InvitationId);

            throw new UnauthorizedAccessException("This invitation is not addressed to the current user");
        }

        var declined = await resourcePermissionService
            .DeclineInvitationAsync(request.InvitationId, request.Reason, cancellationToken)
            .ConfigureAwait(false);

        return declined
            ? InvitationActionResult.SuccessResult(invitation, InvitationStatus.Declined)
            : InvitationActionResult.Failure(request.InvitationId, "Invitation could not be declined");
    }

    private static bool EmailsMatch(string left, string right)
    {
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
///     Command to revoke a pending invitation.
/// </summary>
/// <param name="InvitationId">The invitation ID.</param>
public sealed record RevokeResourceInvitationCommand(Guid InvitationId) : ICommand<InvitationActionResult>;

/// <summary>
///     Handler for RevokeResourceInvitationCommand.
/// </summary>
public sealed class RevokeResourceInvitationCommandHandler(
    IApplicationDbContext dbContext,
    IResourcePermissionService resourcePermissionService,
    IActorContextAccessor actorContextAccessor,
    ILogger<RevokeResourceInvitationCommandHandler> logger)
    : ICommandHandler<RevokeResourceInvitationCommand, InvitationActionResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<InvitationActionResult> Handle(RevokeResourceInvitationCommand request, CancellationToken cancellationToken)
    {
        var userId = Actor.SubjectIdAsGuid ?? throw new UnauthorizedAccessException("User not authenticated");

        var invitation = await dbContext.Set<ResourceInvitation>()
            .FirstOrDefaultAsync(i => i.Id == request.InvitationId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation == null)
        {
            return InvitationActionResult.Failure(request.InvitationId, "Invitation not found");
        }

        var canRevoke = Actor.IsSystemAdmin ||
                        invitation.InvitedByUserId == userId ||
                        (Actor.IsTenantAdmin && Actor.TenantId.HasValue && Actor.TenantId.Value == invitation.TenantId.Value);

        if (!canRevoke)
        {
            logger.LogWarning(
                "User {UserId} attempted to revoke invitation {InvitationId} without authorization",
                userId,
                request.InvitationId);

            throw new UnauthorizedAccessException("You do not have permission to revoke this invitation");
        }

        var revoked = await resourcePermissionService
            .RevokeInvitationAsync(request.InvitationId, userId, cancellationToken)
            .ConfigureAwait(false);

        return revoked
            ? InvitationActionResult.SuccessResult(invitation, InvitationStatus.Revoked)
            : InvitationActionResult.Failure(request.InvitationId, "Invitation could not be revoked");
    }
}

/// <summary>
///     Command to remove a user's access to a resource by revoking all their permissions.
/// </summary>
public sealed record RemoveUserAccessCommand : ICommand<PermissionUpdateResult>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the type of resource.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    ///     Gets the ID of the resource.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    ///     Gets the ID of the user whose access is being removed.
    /// </summary>
    public required Guid TargetUserId { get; init; }

    /// <summary>
    ///     Gets the ID of the user removing the access.
    /// </summary>
    public required Guid RemovedByUserId { get; init; }

    /// <summary>
    ///     Gets the optional reason for removing access.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for RemoveUserAccessCommand.
/// </summary>
public sealed class RemoveUserAccessCommandHandler(
    IResourcePermissionService resourcePermissionService,
    IActorContextAccessor actorContextAccessor,
    IPermissionQueryService queryService,
    ILogger<RemoveUserAccessCommandHandler> logger)
    : ICommandHandler<RemoveUserAccessCommand, PermissionUpdateResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionUpdateResult> Handle(RemoveUserAccessCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Removing user access for user {TargetUserId} from resource {ResourceType}/{ResourceId}. Reason: {Reason}",
            request.TargetUserId,
            request.ResourceType,
            request.ResourceId,
            request.Reason ?? "Not specified");

        // Check if the current user has permission to manage permissions for this resource
        var resourceIdGuid = Guid.Parse(request.ResourceId);
        var resourcePermission = $"{request.ResourceType}.{resourceIdGuid}.Admin";
        var hasManagePermission = Actor.TenantId.HasValue && await queryService.HasTenantPermissionAsync(
            Actor.SubjectIdAsGuid!.Value,
            Actor.TenantId.Value,
            resourcePermission,
            cancellationToken).ConfigureAwait(false);

        if (!hasManagePermission)
        {
            logger.LogWarning(
                "User {UserId} attempted to remove user access on resource {ResourceType}/{ResourceId} without Admin permission",
                request.RemovedByUserId,
                request.ResourceType,
                request.ResourceId);

            return new PermissionUpdateResult
            {
                Success = false,
                ErrorMessage = "You don't have permission to remove user access from this resource"
            };
        }

        // Prevent users from removing their own access
        if (request.TargetUserId == request.RemovedByUserId)
        {
            logger.LogWarning(
                "User {UserId} attempted to remove their own access to resource {ResourceType}/{ResourceId}",
                request.RemovedByUserId,
                request.ResourceType,
                request.ResourceId);

            return new PermissionUpdateResult
            {
                Success = false,
                ErrorMessage = "You cannot remove your own access to a resource"
            };
        }

        // Check if target user is the owner (prevent removing owner)
        var isOwner = Actor.SubjectIdAsGuid == request.TargetUserId;

        if (isOwner)
        {
            logger.LogWarning(
                "User {UserId} attempted to remove owner {TargetUserId} access to resource {ResourceType}/{ResourceId}",
                request.RemovedByUserId,
                request.TargetUserId,
                request.ResourceType,
                request.ResourceId);

            return new PermissionUpdateResult
            {
                Success = false,
                ErrorMessage = "Cannot remove resource owner's access"
            };
        }

        var success = await resourcePermissionService.RemoveUserAccessAsync(
            request.TenantId,
            request.ResourceType,
            request.ResourceId,
            request.TargetUserId,
            request.RemovedByUserId,
            request.Reason,
            cancellationToken);

        var result = new PermissionUpdateResult
        {
            Success = success,
            ErrorMessage = success ? null : "Failed to remove user access"
        };

        logger.LogInformation(
            "Access removal completed for user {TargetUserId}: {Success}",
            request.TargetUserId,
            result.Success);

        return result;
    }
}

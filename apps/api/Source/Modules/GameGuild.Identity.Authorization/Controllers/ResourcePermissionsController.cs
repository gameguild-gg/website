using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.CQRS.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Controller for managing resource-level permissions.
///     Provides REST endpoints for sharing resources, checking permissions, and managing user access.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/resource-permissions")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/resources")]
[Authorize]
[Produces("application/json")]
public sealed class ResourcePermissionsController(ISender sender, ILogger<ResourcePermissionsController> logger) : BaseApiController
{
    /// <summary>
    ///     Gets a specific invitation if it is visible to the current user.
    /// </summary>
    [HttpGet("invitations/{invitationId:guid}")]
    [ProducesResponseType(typeof(GetResourceInvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvitation([FromRoute] Guid invitationId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetResourceInvitationQuery(invitationId), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Gets all pending invitations addressed to the current user.
    /// </summary>
    [HttpGet("invitations/pending")]
    [ProducesResponseType(typeof(GetPendingResourceInvitationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPendingInvitations(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPendingResourceInvitationsQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Accepts an invitation addressed to the current user.
    /// </summary>
    [HttpPost("invitations/{invitationId:guid}/accept")]
    [ProducesResponseType(typeof(InvitationActionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AcceptInvitation([FromRoute] Guid invitationId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AcceptResourceInvitationCommand(invitationId), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Declines an invitation addressed to the current user.
    /// </summary>
    [HttpPost("invitations/{invitationId:guid}/decline")]
    [ProducesResponseType(typeof(InvitationActionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeclineInvitation(
        [FromRoute] Guid invitationId,
        [FromBody] DeclineInvitationRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeclineResourceInvitationCommand(invitationId, request?.Reason),
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Revokes a pending invitation.
    /// </summary>
    [HttpDelete("invitations/{invitationId:guid}")]
    [ProducesResponseType(typeof(InvitationActionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeInvitation([FromRoute] Guid invitationId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RevokeResourceInvitationCommand(invitationId), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Shares a resource with one or more users by granting them permissions.
    /// </summary>
    /// <param name="command">The share resource command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success/failure for each user.</returns>
    /// <response code="200">Resource shared successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User doesn't have permission to share this resource.</response>
    [HttpPost("share")]
    [ProducesResponseType(typeof(ShareResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ShareResource([FromBody] ShareResourceCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Sharing resource {ResourceType}/{ResourceId} with {UserCount} users",
            LogRedaction.Sanitize(command.ResourceType),
            LogRedaction.RedactId(command.ResourceId, "rid"),
            command.UserIds.Length);

        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Updates a specific user's permissions on a resource.
    /// </summary>
    /// <param name="command">The update permissions command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success/failure with permission details.</returns>
    /// <response code="200">Permissions updated successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User doesn't have permission to manage this resource.</response>
    [HttpPut("users/permissions")]
    [ProducesResponseType(typeof(PermissionUpdateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUserPermissions([FromBody] UpdateUserPermissionsCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Updating permissions for user {TargetUserId} on resource {ResourceType}/{ResourceId}",
            LogRedaction.RedactId(command.TargetUserId, "uid"),
            LogRedaction.Sanitize(command.ResourceType),
            LogRedaction.RedactId(command.ResourceId, "rid"));

        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Removes all access for a user on a specific resource.
    /// </summary>
    /// <param name="command">The remove access command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success/failure.</returns>
    /// <response code="200">Access removed successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User doesn't have permission to manage this resource.</response>
    [HttpDelete("users/access")]
    [ProducesResponseType(typeof(PermissionUpdateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveUserAccess([FromBody] RemoveUserAccessCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Removing access for user {TargetUserId} from resource {ResourceType}/{ResourceId}",
            LogRedaction.RedactId(command.TargetUserId, "uid"),
            LogRedaction.Sanitize(command.ResourceType),
            LogRedaction.RedactId(command.ResourceId, "rid"));

        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Gets all effective permissions for a user on a specific resource.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="resourceType">The type of resource.</param>
    /// <param name="resourceId">The ID of the resource.</param>
    /// <param name="userId">Optional user ID (defaults to current user).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of effective permissions.</returns>
    /// <response code="200">Effective permissions retrieved successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User doesn't have permission to view permissions for this resource.</response>
    [HttpGet("{resourceType}/{resourceId}/permissions")]
    [ProducesResponseType(typeof(EffectivePermissionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEffectivePermissions(
        [FromQuery] Guid tenantId,
        [FromRoute] string resourceType,
        [FromRoute] Guid resourceId,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var query = new GetEffectivePermissionsQuery
        {
            TenantId = new TenantId(tenantId),
            ResourceType = resourceType,
            ResourceId = resourceId,
            UserId = userId
        };

        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Checks if a user has a specific permission on a resource.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="resourceType">The type of resource.</param>
    /// <param name="resourceId">The ID of the resource.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="userId">Optional user ID (defaults to current user).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Boolean indicating if the user has the permission.</returns>
    /// <response code="200">Permission check completed.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("{resourceType}/{resourceId}/has-permission")]
    [ProducesResponseType(typeof(HasPermissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> HasPermission(
        [FromQuery] Guid tenantId,
        [FromRoute] string resourceType,
        [FromRoute] Guid resourceId,
        [FromQuery] string permission,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var query = new HasPermissionQuery
        {
            TenantId = new TenantId(tenantId),
            ResourceType = resourceType,
            ResourceId = resourceId,
            Permission = permission,
            UserId = userId
        };

        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Gets all users who have access to a specific resource.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="resourceType">The type of resource.</param>
    /// <param name="resourceId">The ID of the resource.</param>
    /// <param name="includeInherited">Whether to include inherited permissions.</param>
    /// <param name="includeExpired">Whether to include expired permissions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of users with their permissions.</returns>
    /// <response code="200">Users retrieved successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User doesn't have permission to view users for this resource.</response>
    [HttpGet("{resourceType}/{resourceId}/users")]
    [ProducesResponseType(typeof(GetResourceUsersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetResourceUsers(
        [FromQuery] Guid tenantId,
        [FromRoute] string resourceType,
        [FromRoute] string resourceId,
        [FromQuery] bool includeInherited = true,
        [FromQuery] bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetResourceUsersQuery
        {
            TenantId = new TenantId(tenantId),
            ResourceType = resourceType,
            ResourceId = resourceId,
            IncludeInherited = includeInherited,
            IncludeExpired = includeExpired
        };

        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }
}

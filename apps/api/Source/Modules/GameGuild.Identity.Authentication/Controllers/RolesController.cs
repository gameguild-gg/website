using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Controller for role management endpoints
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/roles")]
[Microsoft.AspNetCore.Http.Tags("auth/roles")]
[Produces("application/json")]
public class RolesController(ILogger<RolesController> logger, ISender sender) : AuthControllerBase
{
    /// <summary>
    ///     Get all roles in the system
    /// </summary>
    /// <param name="tenantId">Optional tenant ID filter</param>
    /// <param name="includeInactive">Whether to include inactive roles</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of roles</returns>
    [HttpGet]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? tenantId, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting all roles");

        var query = new GetRolesQuery
        {
            TenantId = tenantId,
            IncludeInactive = includeInactive
        };

        var roles = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(roles);
    }

    /// <summary>
    ///     Get a specific role by ID
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{roleId:guid}")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid roleId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting role by ID: {RoleId}", LogRedaction.Sanitize(roleId.ToString()));

        var query = new GetRoleByIdQuery { RoleId = roleId };
        var role = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        if (role == null)
        {
            return NotFound($"Role with ID '{roleId}' not found");
        }

        return Ok(role);
    }

    /// <summary>
    ///     Create a new role
    /// </summary>
    /// <param name="request">Create role request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating new role: {RoleName}", LogRedaction.Sanitize(request.Name));

        var command = new CreateRoleCommand
        {
            Name = request.Name,
            Description = request.Description,
            Permissions = request.Permissions,
            TenantId = request.TenantId
        };

        var role = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { roleId = role.Id }, role);
    }

    /// <summary>
    ///     Update an existing role
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="request">Update role request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPut("{roleId:guid}")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(Guid roleId, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating role: {RoleId}", LogRedaction.Sanitize(roleId.ToString()));

        var command = new UpdateRoleCommand
        {
            RoleId = roleId,
            Name = request.Name,
            Description = request.Description,
            Permissions = request.Permissions,
            IsActive = request.IsActive
        };

        var role = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(role);
    }

    /// <summary>
    ///     Delete a role
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("{roleId:guid}")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid roleId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting role: {RoleId}", LogRedaction.Sanitize(roleId.ToString()));

        var command = new DeleteRoleCommand { RoleId = roleId };
        await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Get all roles assigned to a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="includeExpired">Whether to include expired role assignments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("user/{userId:guid}")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserRoles(Guid userId, [FromQuery] bool includeExpired = false, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting roles for user: {UserId}", LogRedaction.Sanitize(userId.ToString()));

        var query = new GetUserRolesQuery
        {
            UserId = userId,
            IncludeExpired = includeExpired
        };

        var roles = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(roles);
    }

    /// <summary>
    ///     Assign a role to a user
    /// </summary>
    /// <param name="request">Assign role request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost(":assign")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignRoleToUser([FromBody] AssignRoleToUserRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Assigning role {RoleId} to user {UserId}", LogRedaction.Sanitize(request.RoleId.ToString()), LogRedaction.Sanitize(request.UserId.ToString()));

        var command = new AssignRoleToUserCommand
        {
            UserId = request.UserId,
            RoleId = request.RoleId,
            AssignedBy = GetCurrentUserId(),
            ExpiresAt = request.ExpiresAt
        };

        var userRole = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetUserRoles), new { userId = request.UserId }, userRole);
    }

    /// <summary>
    ///     Assign one active role to multiple users in a single bounded operation.
    /// </summary>
    [HttpPost(":bulk-assign")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(typeof(BulkRoleAssignmentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BulkRoleAssignmentResult>> BulkAssignRoles(
        [FromBody] BulkAssignRolesCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Remove a role from a user
    /// </summary>
    /// <param name="request">Remove role request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost(":remove")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveRoleFromUser([FromBody] RemoveRoleFromUserRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Removing role {RoleId} from user {UserId}", LogRedaction.Sanitize(request.RoleId.ToString()), LogRedaction.Sanitize(request.UserId.ToString()));

        var command = new RemoveRoleFromUserCommand
        {
            UserId = request.UserId,
            RoleId = request.RoleId
        };

        await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}

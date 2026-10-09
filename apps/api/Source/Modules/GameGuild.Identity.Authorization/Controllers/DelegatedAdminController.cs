using Asp.Versioning;
using GameGuild.Identity.Authorization.Commands;
using GameGuild.Identity.Authorization.Queries;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authorization.Controllers;

/// <summary>
///     API controller for Delegated Administration operations
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/delegated-admin")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/delegated-admin")]
[Authorize]
[Produces("application/json")]
public class DelegatedAdminController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Grant delegated admin scope to a user
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DelegatedAdminScope), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GrantAdmin(
        [FromBody] GrantDelegatedAdminCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    ///     Revoke delegated admin scope
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAdmin(Guid id, CancellationToken cancellationToken)
    {
        var command = new RevokeDelegatedAdminCommand(id);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    ///     Get a delegated admin scope by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DelegatedAdminScope), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetDelegatedAdminScopeByIdQuery(id);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    ///     Get admin scopes for a user
    /// </summary>
    [HttpGet("user/{adminUserId:guid}/scopes")]
    [ProducesResponseType(typeof(List<DelegatedAdminScope>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminScopes(
        Guid adminUserId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetAdminScopesQuery(adminUserId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get managed users for an admin
    /// </summary>
    [HttpGet("user/{adminUserId:guid}/managed-users")]
    [ProducesResponseType(typeof(List<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManagedUsers(
        Guid adminUserId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetManagedUsersQuery(adminUserId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get managed resource types for an admin
    /// </summary>
    [HttpGet("user/{adminUserId:guid}/managed-resources")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManagedResourceTypes(
        Guid adminUserId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetManagedResourceTypesQuery(adminUserId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Check if admin can manage a user
    /// </summary>
    [HttpGet("user/{adminUserId:guid}/can-manage-user/{targetUserId:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> CanManageUser(
        Guid adminUserId,
        Guid targetUserId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new CanManageUserQuery(adminUserId, targetUserId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Check if admin can manage a resource type
    /// </summary>
    [HttpGet("user/{adminUserId:guid}/can-manage-resource")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> CanManageResource(
        Guid adminUserId,
        [FromQuery] string resourceType,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new CanManageResourceQuery(adminUserId, resourceType, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}

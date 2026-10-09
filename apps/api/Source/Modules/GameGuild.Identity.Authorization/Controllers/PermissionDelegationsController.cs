using Asp.Versioning;
using GameGuild.Identity.Authorization.Commands;
using GameGuild.Identity.Authorization.Queries;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authorization.Controllers;

/// <summary>
///     API controller for permission delegation operations
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/permission-delegations")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/permission-delegations")]
[Authorize]
[Produces("application/json")]
public class PermissionDelegationsController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Delegate permissions to another user
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PermissionDelegation), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DelegatePermissions(
        [FromBody] DelegatePermissionsCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    ///     Revoke a permission delegation
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var command = new RevokeDelegationCommand(id);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    ///     Get a delegation by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PermissionDelegation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetDelegationByIdQuery(id);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    ///     Get active delegations for a delegate user
    /// </summary>
    [HttpGet("delegate/{delegateUserId:guid}")]
    [ProducesResponseType(typeof(List<PermissionDelegation>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveDelegations(
        Guid delegateUserId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetActiveDelegationsQuery(delegateUserId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get delegations made by a delegator
    /// </summary>
    [HttpGet("delegator/{delegatorUserId:guid}")]
    [ProducesResponseType(typeof(List<PermissionDelegation>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDelegationsByDelegator(
        Guid delegatorUserId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetDelegationsByDelegatorQuery(delegatorUserId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Check if user has a delegated permission
    /// </summary>
    [HttpGet("check")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckDelegatedPermission(
        [FromQuery] Guid delegateUserId,
        [FromQuery] string permission,
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? resourceId,
        CancellationToken cancellationToken
    )
    {
        var query = new CheckDelegatedPermissionQuery(delegateUserId, permission, tenantId, resourceId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Cleanup expired delegations (admin only)
    /// </summary>
    [HttpPost(":cleanup")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> CleanupExpired(CancellationToken cancellationToken)
    {
        var command = new CleanupExpiredDelegationsCommand();
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new { Count = result });
    }
}

using Asp.Versioning;
using GameGuild.Identity.Authorization.Commands;
using GameGuild.Identity.Authorization.Queries;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authorization.Controllers;

/// <summary>
///     API controller for JIT (Just-in-Time) elevation operations
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/jit-elevations")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/jit-elevations")]
[Authorize]
[Produces("application/json")]
public class JitElevationsController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Request a JIT elevation for a permission
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(JitElevationRequest), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestElevation(
        [FromBody] RequestJitElevationCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    ///     Approve a pending JIT elevation request
    /// </summary>
    [HttpPost("{id:guid}:approve")]
    [ProducesResponseType(typeof(JitElevationRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveElevationRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new ApproveJitElevationCommand(id, request.ReviewerId, request.Comments);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Deny a pending JIT elevation request
    /// </summary>
    [HttpPost("{id:guid}:deny")]
    [ProducesResponseType(typeof(JitElevationRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deny(
        Guid id,
        [FromBody] DenyElevationRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new DenyJitElevationCommand(id, request.ReviewerId, request.Comments);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Revoke an active JIT elevation
    /// </summary>
    [HttpPost("{id:guid}:revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid id,
        [FromBody] RevokeElevationRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new RevokeJitElevationCommand(id, request.RevokedBy, request.Reason);
        await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Get a JIT elevation request by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JitElevationRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetJitElevationByIdQuery(id);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    ///     Get pending JIT elevation requests
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<JitElevationRequest>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetPendingJitElevationsQuery(tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get JIT elevation requests for a user
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(List<JitElevationRequest>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByUser(
        Guid userId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetUserJitElevationsQuery(userId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get active JIT elevations for a user
    /// </summary>
    [HttpGet("user/{userId:guid}/active")]
    [ProducesResponseType(typeof(List<JitElevationRequest>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveByUser(
        Guid userId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetActiveJitElevationsQuery(userId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Check if user has active elevation for a permission
    /// </summary>
    [HttpGet("user/{userId:guid}/check")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> HasActiveElevation(
        Guid userId,
        [FromQuery] string permission,
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? resourceId,
        CancellationToken cancellationToken
    )
    {
        var query = new HasActiveJitElevationQuery(userId, permission, tenantId, resourceId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Cleanup expired elevations (admin only)
    /// </summary>
    [HttpPost(":cleanup")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> CleanupExpired(CancellationToken cancellationToken)
    {
        var command = new CleanupExpiredElevationsCommand();
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new { Count = result });
    }
}

// Request DTOs
public sealed record ApproveElevationRequest(Guid ReviewerId, string? Comments = null);
public sealed record DenyElevationRequest(Guid ReviewerId, string Comments);
public sealed record RevokeElevationRequest(Guid RevokedBy, string Reason);

using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     API controller for what-if permission simulation (issue #374). The
/// <c>:simulate</c> custom method computes the before/after effective permission diff
/// for a hypothetical grant/revoke delta against a user, role or tenant-default subject
/// — a pure dry-run through the canonical effective-permission resolver that never
/// mutates state.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/permission-simulation")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionSimulationController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Simulates a permission change (issue #374): given a subject (user, role or
    /// tenant), a grant/revoke delta and an optional resource scope, returns the
    /// before/after effective permission diff. Nothing is applied — no grants are
    /// written, no security version is bumped, no audit entry is recorded.
    /// </summary>
    /// <param name="command">The simulation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The before/after diff (always unapplied).</response>
    /// <response code="400">Invalid request (empty delta, grant/revoke overlap).</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Simulating another subject requires administration.</response>
    [HttpPost("permissions:simulate")]
    [NoBusinessMutationEndpoint("Policy simulation is a read-only what-if evaluation: it computes the before/after effective permission diff without applying any change.")]
    [ProducesResponseType(typeof(PermissionSimulationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Simulate(
        [FromBody] SimulatePermissionChangeCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }
}

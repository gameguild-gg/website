using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authorization.Controllers;

/// <summary>
///     API controller for permission history: point-in-time reconstruction of effective
///     grants (issue #189), role-inheritance change history (issue #189) and permission
///     policy version history (issue #188). The tenant scope is resolved from the request
///     context, never from the route.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/permission-history")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/permissions")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionHistoryController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Reconstructs a subject's effective permission state at a past instant
    ///     (issue #189: access rights at any point in time).
    /// </summary>
    /// <param name="timestamp">The requested instant (UTC, inclusive boundary).</param>
    /// <param name="userId">Subject user; defaults to the caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The reconstructed permission state at the instant.</response>
    /// <response code="400">The timestamp is in the future or unparseable.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Viewing another user's history requires system administration.</response>
    /// <response code="422">History for the instant cannot be proven from the available data (fail closed; the body lists the coverage gaps).</response>
    [HttpGet("history/at")]
    [ProducesResponseType(typeof(PermissionHistoryAtSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetHistoryAt(
        [FromQuery] DateTime timestamp,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await sender.Send(
                new GetPermissionHistoryAtQuery { Timestamp = timestamp, UserId = userId },
                cancellationToken).ConfigureAwait(false);

            return Ok(snapshot);
        }
        catch (PermissionHistoryIncompleteException exception)
        {
            return UnprocessableEntity(new
            {
                exception.Message,
                exception.RequestedTimestamp,
                exception.Gaps,
                Reason = "Permission history for the requested instant cannot be reconstructed from the available audit data (fail closed)."
            });
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest("timestamp must be a past UTC instant.");
        }
    }

    /// <summary>
    ///     Lists role-inheritance change events in a window (issue #189: parent-child
    ///     relationship changes over time), each with the pre/post hierarchy state and
    ///     the cascading effects recorded at mutation time.
    /// </summary>
    /// <param name="fromTimestamp">Window start (inclusive).</param>
    /// <param name="toTimestamp">Window end (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The inheritance change events, ascending by time.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Requires system administration.</response>
    [HttpGet("history/inheritance")]
    [ProducesResponseType(typeof(IReadOnlyList<InheritanceHistoryEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInheritanceHistory(
        [FromQuery] DateTime? fromTimestamp,
        [FromQuery] DateTime? toTimestamp,
        CancellationToken cancellationToken)
    {
        var events = await sender.Send(
            new GetInheritanceHistoryQuery { FromTimestamp = fromTimestamp, ToTimestamp = toTimestamp },
            cancellationToken).ConfigureAwait(false);

        return Ok(events);
    }

    /// <summary>
    ///     Lists the append-only version history of one permission policy (issue #188).
    /// </summary>
    /// <param name="policyId">The policy id.</param>
    /// <param name="kind">The policy family, to disambiguate ids shared across kinds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The policy's version snapshots, oldest first.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">Requires system administration.</response>
    [HttpGet("/api/v{version:apiVersion}/authorization/permission-policies/{policyId:guid}/versions")]
    [ProducesResponseType(typeof(List<PolicyVersionHistory>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPolicyVersions(
        Guid policyId,
        [FromQuery] PolicyVersionKind? kind,
        CancellationToken cancellationToken)
    {
        var versions = await sender.Send(
            new GetPolicyVersionHistoryQuery { PolicyId = policyId, Kind = kind },
            cancellationToken).ConfigureAwait(false);

        return Ok(versions);
    }
}

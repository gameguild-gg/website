using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Authorization.Queries;
using GameGuild.Identity.Authorization.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authorization.Controllers;

/// <summary>
///     API controller for graph-based permission visualization and impact analysis (issue #334).
///     All endpoints are read-only simulations; the tenant scope is resolved from the
///     request context, never from the route.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/permission-graph")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/permission-graph")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionGraphController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Builds the permission graph of the caller's tenant: users, dynamic roles, and
    ///     permission keys connected by assignment, inheritance, grant, and deny edges,
    ///     with a data-quality summary (cycles, unregistered keys, orphaned roles).
    /// </summary>
    /// <param name="includeUsers">Whether to include user nodes and their edges (default true).</param>
    /// <param name="format">Output format: JSON (default), DOT (Graphviz), or GraphML.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The permission graph.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PermissionGraph), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetGraph(
        [FromQuery] bool includeUsers = true,
        [FromQuery] GraphExportFormat format = GraphExportFormat.JSON,
        CancellationToken cancellationToken = default)
    {
        var graph = await sender.Send(new GetPermissionGraphQuery(includeUsers), cancellationToken).ConfigureAwait(false);

        return format switch
        {
            GraphExportFormat.DOT => Content(PermissionGraphExporter.ToDot(graph), "text/vnd.graphviz; charset=utf-8"),
            GraphExportFormat.GraphML => Content(PermissionGraphExporter.ToGraphMl(graph), "application/xml; charset=utf-8"),
            _ => Ok(graph)
        };
    }

    /// <summary>
    ///     Simulates deleting a dynamic role and reports which users would lose permissions.
    /// </summary>
    /// <param name="roleId">The role to simulate deleting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The simulated deletion impact.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("roles/{roleId:guid}/deletion-impact")]
    [ProducesResponseType(typeof(RoleDeletionImpact), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AnalyzeRoleDeletion(Guid roleId, CancellationToken cancellationToken = default)
    {
        var impact = await sender.Send(new AnalyzeRoleDeletionQuery(roleId), cancellationToken).ConfigureAwait(false);
        return Ok(impact);
    }

    /// <summary>
    ///     Simulates removing a permission key from a dynamic role and reports which users
    ///     would lose or retain the key.
    /// </summary>
    /// <param name="roleId">The role the key would be removed from.</param>
    /// <param name="permissionKey">The concrete permission key under analysis (e.g. <c>content:read</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The simulated removal impact.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("roles/{roleId:guid}/permission-removal-impact/{permissionKey}")]
    [ProducesResponseType(typeof(PermissionRemovalImpact), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AnalyzePermissionRemoval(
        Guid roleId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        var impact = await sender.Send(
            new AnalyzePermissionRemovalQuery(roleId, permissionKey),
            cancellationToken).ConfigureAwait(false);
        return Ok(impact);
    }
}

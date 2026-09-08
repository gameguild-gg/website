using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.Identity.Authorization;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     API controller for permission evaluation and querying operations.
///     Handles permission checks across all layers (tenant, content-type, resource),
///     aggregated user permission views, effective permissions, and hierarchy resolution.
/// </summary>
/// <remarks>
///     Rate limited to 100 requests per minute per client to prevent DoS attacks on permission evaluation.
/// </remarks>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/permissions")]
[Microsoft.AspNetCore.Http.Tags("auth/permissions")]
[ApiExplorerSettings(IgnoreApi = true)]
[EnableRateLimiting(RateLimitPolicies.Authorization)]
[Authorize]
public class PermissionEvaluationController(IMediator mediator, ILogger<PermissionEvaluationController> logger) : BaseApiController
{
    private readonly ILogger<PermissionEvaluationController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    #region Tenant Permission Queries

    /// <summary>
    ///     Check if user has a specific tenant permission
    /// </summary>
    [HttpPost("tenant:check")]
    [NoBusinessMutationEndpoint("This POST-shaped permission evaluation is read-only and does not grant or revoke access.")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<bool>> CheckTenantPermission([FromBody] HasTenantPermissionQuery query)
    {
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get all tenant permissions for a user
    /// </summary>
    [HttpGet("tenant")]
    [ProducesResponseType(typeof(IEnumerable<PermissionType>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PermissionType>>> GetTenantPermissions(
        [FromQuery] Guid userId,
        [FromQuery] Guid tenantId)
    {
        var query = new GetTenantPermissionsQuery { UserId = userId, TenantId = tenantId };
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion

    #region Content Type Permission Queries

    /// <summary>
    ///     Check if user has a specific content type permission
    /// </summary>
    [HttpPost("content-type:check")]
    [NoBusinessMutationEndpoint("This POST-shaped permission evaluation is read-only and does not grant or revoke access.")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<bool>> CheckContentTypePermission([FromBody] HasContentTypePermissionQuery query)
    {
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get all content type permissions for a user
    /// </summary>
    [HttpGet("content-type")]
    [ProducesResponseType(typeof(IEnumerable<PermissionType>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PermissionType>>> GetContentTypePermissions(
        [FromQuery] Guid userId,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? contentType = null)
    {
        var query = new GetContentTypePermissionsQuery { UserId = userId, TenantId = tenantId, ContentType = contentType };
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion

    #region Resource Permission Queries

    /// <summary>
    ///     Check if user has a specific resource permission
    /// </summary>
    [HttpPost("resource:check")]
    [NoBusinessMutationEndpoint("This POST-shaped permission evaluation is read-only and does not grant or revoke access.")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<bool>> CheckResourcePermission([FromBody] HasResourcePermissionQuery query)
    {
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get all resource permissions for a user
    /// </summary>
    [HttpGet("resource")]
    [ProducesResponseType(typeof(IEnumerable<PermissionType>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PermissionType>>> GetResourcePermissions(
        [FromQuery] Guid userId,
        [FromQuery] Guid resourceId)
    {
        var query = new GetResourcePermissionsQuery { UserId = userId, ResourceId = resourceId };
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion

    #region User Permissions (Aggregated Views)

    /// <summary>
    ///     Get all permissions for a user across all layers (tenant + content-type + resource)
    /// </summary>
    [HttpGet("~/v{version:apiVersion}/users/{userId:guid}/permissions")]
    [ProducesResponseType(typeof(UserPermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserPermissionsDto>> GetUserPermissions(Guid userId, [FromQuery] Guid? tenantId = null)
    {
        var query = new GetUserPermissionsQuery { UserId = userId, TenantId = tenantId };
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get effective permissions for a user (resolved through all layers with inheritance)
    /// </summary>
    [HttpGet("~/v{version:apiVersion}/users/{userId:guid}/permissions/effective")]
    [ProducesResponseType(typeof(EffectivePermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EffectivePermissionsDto>> GetEffectivePermissions(
        Guid userId,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? resourceId = null)
    {
        var query = new GetEffectivePermissionsQuery { UserId = userId, TenantId = tenantId, ResourceId = resourceId };
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Resolve permission hierarchy for a specific permission check
    /// </summary>
    [HttpPost(":resolve-hierarchy")]
    [NoBusinessMutationEndpoint("Permission hierarchy resolution is a read-only calculation and does not change authorization state.")]
    [ProducesResponseType(typeof(PermissionHierarchyResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionHierarchyResult>> ResolvePermissionHierarchy([FromBody] ResolvePermissionHierarchyQuery query)
    {
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion
}

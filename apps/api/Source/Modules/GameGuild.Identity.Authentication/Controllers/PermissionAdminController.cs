using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     API controller for permission administration operations including
///     analytics, audit trail, cache management, and permission templates.
/// </summary>
/// <remarks>
///     <para>
///         Rate limited to 100 requests per minute per client to prevent DoS attacks on permission evaluation.
///     </para>
///     <para>
///         <b>Route reconciliation (issue #353):</b> the issue's requested
///         <c>/api/cache/permissions/*</c> surface is implemented by this controller under the
///         platform's versioned API convention as <c>v{version}/permissions/cache/*</c>
///         (<c>cache/stats</c>, <c>cache:clear</c>, <c>cache:warm</c>, <c>cache/keys</c>,
///         <c>cache/keys:inspect</c>). No <c>/api/cache/permissions</c> route alias is registered:
///         the repository has no precedent for unversioned duplicate routes, and every public
///         endpoint is versioned under <c>v{version}</c>. The mapping is also documented in
///         <c>docs/api/authorization-cache-configuration.md</c>.
///     </para>
/// </remarks>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/permissions")]
[Microsoft.AspNetCore.Http.Tags("auth/permissions")]
[ApiExplorerSettings(IgnoreApi = true)]
[EnableRateLimiting(RateLimitPolicies.Authorization)]
[Authorize]
public class PermissionAdminController(IMediator mediator, ILogger<PermissionAdminController> logger) : BaseApiController
{
    private readonly ILogger<PermissionAdminController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    #region Permission Analytics

    /// <summary>
    ///     Get permission usage analytics for a tenant
    /// </summary>
    [HttpGet("~/v{version:apiVersion}/tenants/{tenantId:guid}/permissions/analytics")]
    [ProducesResponseType(typeof(PermissionAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionAnalyticsDto>> GetPermissionAnalytics(
        Guid tenantId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = new GetPermissionAnalyticsQuery { TenantId = tenantId, FromDate = fromDate ?? SystemClock.UtcNow.AddDays(-30), ToDate = toDate ?? SystemClock.UtcNow };
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get permission audit trail for compliance reporting
    /// </summary>
    [HttpGet("audit-trail")]
    [ProducesResponseType(typeof(PermissionAuditTrailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionAuditTrailDto>> GetPermissionAuditTrail(
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new GetPermissionAuditTrailQuery
        {
            UserId = userId,
            TenantId = tenantId,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = pageSize
        };
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion

    #region Cache Management

    /// <summary>
    ///     Get cache statistics for permission system performance monitoring
    /// </summary>
    [HttpGet("cache/stats")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(typeof(PermissionCacheStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionCacheStatsDto>> GetCacheStatistics()
    {
        var query = new GetPermissionCacheStatsQuery();
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Clear permission cache for a specific user or tenant
    /// </summary>
    [HttpPost("cache:clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ClearPermissionCache([FromQuery] Guid? userId = null, [FromQuery] Guid? tenantId = null)
    {
        var command = new ClearPermissionCacheCommand { UserId = userId, TenantId = tenantId };
        await _mediator.Send(command).ConfigureAwait(false);

        return Ok(new { message = "Permission cache cleared successfully" });
    }

    /// <summary>Precompute selected ACL decisions before a known high-traffic period.</summary>
    [HttpPost("cache:warm")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(typeof(PermissionCacheWarmupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PermissionCacheWarmupResult>> WarmPermissionCache(
        [FromBody] WarmPermissionCacheCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     List tracked authorization L1 cache keys for cache-key debugging, most recently used first.
    /// </summary>
    /// <remarks>
    ///     System-administrator only. Read-only inspection of the process-local key index: it
    ///     exposes key shape, cache type, first-tracked/last-access timestamps, and L1 presence.
    /// </remarks>
    [HttpGet("cache/keys")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(typeof(PermissionCacheKeysDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionCacheKeysDto>> GetTrackedCacheKeys(
        [FromQuery] string? search = null,
        [FromQuery] string? cacheType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new GetPermissionCacheKeysQuery
        {
            Search = search,
            CacheType = cacheType,
            Page = page,
            PageSize = pageSize
        };
        var result = await _mediator.Send(query).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get tracking metadata for one authorization L1 cache key (cache-key debugging).
    /// </summary>
    /// <remarks>System-administrator only; returns 404 when the key is not tracked in this process.</remarks>
    [HttpGet("cache/keys:inspect")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [ProducesResponseType(typeof(PermissionCacheKeyInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionCacheKeyInfoDto>> InspectTrackedCacheKey(
        [FromQuery(Name = "key")] string key)
    {
        var query = new GetPermissionCacheKeyQuery { Key = key };
        var result = await _mediator.Send(query).ConfigureAwait(false);
        if (result is null)
        {
            return NotFound(new { message = "Cache key is not tracked in this process." });
        }

        return Ok(result);
    }

    #endregion

    #region Permission Templates

    /// <summary>
    ///     Get available permission templates for common roles
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IEnumerable<PermissionTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PermissionTemplateDto>>> GetPermissionTemplates()
    {
        var query = new GetPermissionTemplatesQuery();
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Apply a permission template to a user
    /// </summary>
    [HttpPost("templates/{templateId:guid}:apply")]
    [ProducesResponseType(typeof(ApplyPermissionTemplateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApplyPermissionTemplateResult>> ApplyPermissionTemplate(
        Guid templateId,
        [FromBody] ApplyPermissionTemplateRequest request)
    {
        var command = new ApplyPermissionTemplateCommand
        {
            TemplateId = templateId,
            UserId = request.UserId,
            TenantId = request.TenantId
        };
        var result = await _mediator.Send(command).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion
}

/// <summary>
///     Request body for applying a permission template
/// </summary>
public sealed record ApplyPermissionTemplateRequest(Guid UserId, Guid? TenantId = null);

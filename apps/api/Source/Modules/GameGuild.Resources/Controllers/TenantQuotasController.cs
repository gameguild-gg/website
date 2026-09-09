using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Utilities;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Resources;

/// <summary>
///     Tenant Quotas API Controller - RESTful API for tenant-level resource quota management
/// </summary>
/// <remarks>
///     All endpoints require authentication. Tenant membership validation is enforced.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("tenants/quotas")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.PerTenant)]
public sealed class TenantQuotasController(
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    ITenantMembershipChecker tenantMembershipChecker) : BaseApiController
{
    /// <summary>
    ///     Validates that the current actor is a member of the specified tenant.
    ///     Fail-closed: Returns false if actor is not authenticated or not a member.
    /// </summary>
    private async Task<bool> ValidateTenantMembershipAsync(Guid tenantId, CancellationToken ct)
    {
        var actor = actorContextAccessor.ActorContext;
        
        // Fail-closed: No actor means no access
        if (actor is null || !actor.IsAuthenticated || !actor.SubjectIdAsGuid.HasValue)
            return false;
        
        // System admins bypass tenant membership check
        if (actor.IsSystemAdmin)
            return true;
        
        // If actor's current tenant matches, allow access
        if (actor.TenantId.HasValue && actor.TenantId.Value == tenantId)
            return true;

        // JWT and API-key authentication carry the selected tenant in a trusted claim.
        // Use it only when request-level tenant resolution did not produce a context.
        if (!actor.TenantId.HasValue &&
            Guid.TryParse(ClaimsExtractor.GetTenantId(User), out var claimedTenantId) &&
            claimedTenantId != Guid.Empty &&
            claimedTenantId == tenantId)
            return true;
        
        // Check actual tenant membership in database
        return await tenantMembershipChecker.IsUserMemberOfTenantAsync(
            actor.SubjectIdAsGuid.Value, 
            tenantId, 
            ct);
    }
    #region Collection Operations - /v1/tenants/{tenantId}/quotas

    /// <summary>
    ///     Get all quotas for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of configured resource quotas for the tenant</returns>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/quotas")]
    [EndpointSummary("Get all quotas for a tenant")]
    [EndpointDescription("Retrieves all configured resource quotas for a specific tenant organization.")]
    [ProducesResponseType<IEnumerable<ResourceQuotaResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantQuotas(Guid tenantId, CancellationToken ct = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        var query = new GetTenantResourceQuotasQuery(tenantId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get specific quota for a resource type
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="type">Resource usage type to get quota for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Resource quota configuration</returns>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/quotas/{type}")]
    [EndpointSummary("Get specific quota for a resource type")]
    [EndpointDescription("Retrieves the quota configuration for a specific resource type for a tenant.")]
    [ProducesResponseType<ResourceQuotaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuota(Guid tenantId, ResourceUsageType type, CancellationToken ct = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        var query = new GetResourceQuotaQuery(tenantId, type);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        if (result == null) return NotFound($"Quota not found for tenant {tenantId} and type {type}");

        return Ok(result);
    }

    #endregion

    #region Item Operations - /v1/tenants/{tenantId}/quotas/{type}

    /// <summary>
    ///     Set or update a quota for a resource type
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="type">Resource usage type to configure</param>
    /// <param name="body">Quota configuration settings</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPut("v{version:apiVersion}/tenants/{tenantId:guid}/quotas/{type}")]
    [EndpointSummary("Set or update a quota for a resource type")]
    [EndpointDescription("Creates or updates the quota configuration for a specific resource type for a tenant.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetQuota(Guid tenantId, ResourceUsageType type, [FromBody] SetQuotaRequest body, CancellationToken ct = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        ArgumentNullException.ThrowIfNull(body);

        var command = new SetResourceQuotaCommand(tenantId, type, body.SoftLimit, body.HardLimit, body.Period, body.IsActive, body.ResetTime);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Delete a quota for a resource type
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="type">Resource usage type to delete quota for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("v{version:apiVersion}/tenants/{tenantId:guid}/quotas/{type}")]
    [EndpointSummary("Delete a quota for a resource type")]
    [EndpointDescription("Removes the quota configuration for a specific resource type for a tenant.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteQuota(Guid tenantId, ResourceUsageType type, CancellationToken ct = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        var command = new DeleteResourceQuotaCommand(tenantId, type);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    #endregion

    #region Quota Actions - /v1/tenants/{tenantId}/quotas/{type}:action

    /// <summary>
    ///     Reset quota usage to zero
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="type">Resource usage type to reset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}/quotas/{type}:reset")]
    [EndpointSummary("Reset quota usage to zero")]
    [EndpointDescription("Resets the current usage counter for a specific resource quota to zero without changing the quota limits.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetQuota(Guid tenantId, ResourceUsageType type, CancellationToken ct = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        var command = new ResetResourceQuotaCommand(tenantId, type);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Toggle quota activation status
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="type">Resource usage type to toggle</param>
    /// <param name="body">Toggle request with desired active state</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}/quotas/{type}:toggle")]
    [EndpointSummary("Toggle quota activation status")]
    [EndpointDescription("Activates or deactivates a resource quota. Inactive quotas are not enforced.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleQuota(Guid tenantId, ResourceUsageType type, [FromBody] ToggleResourceQuotaRequest body, CancellationToken ct = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        ArgumentNullException.ThrowIfNull(body);

        var command = new ToggleResourceQuotaCommand(tenantId, type, body.IsActive);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Check if a usage amount would exceed quota
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="type">Resource usage type to check</param>
    /// <param name="body">Check request with amount to validate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Quota enforcement result indicating if usage is allowed</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}/quotas/{type}:check")]
    [NoBusinessMutationEndpoint("This POST-shaped quota check is read-only and records no usage.")]
    [EndpointSummary("Check if a usage amount would exceed quota")]
    [EndpointDescription("Validates whether a proposed usage amount would exceed the configured quota limits without recording any usage.")]
    [ProducesResponseType<ResourceQuotaEnforcementResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckQuota(Guid tenantId, ResourceUsageType type, [FromBody] CheckResourceQuotaRequest body, CancellationToken ct = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        ArgumentNullException.ThrowIfNull(body);

        var query = new CheckResourceQuotaQuery(tenantId, type, body.Amount);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion
}

/// <summary>
///     Request model for setting a quota (without tenantId and type in body)
/// </summary>
public sealed record SetQuotaRequest(
    int? SoftLimit,
    int? HardLimit,
    ResourceQuotaPeriod Period,
    bool IsActive = true,
    TimeSpan? ResetTime = null
);

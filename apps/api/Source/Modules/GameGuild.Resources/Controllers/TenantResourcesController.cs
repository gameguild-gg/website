using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Resources;

/// <summary>
///     Tenant Resources API Controller - RESTful API for tenant-level resource usage tracking
/// </summary>
/// <remarks>
///     All endpoints require authentication. Tenant membership validation is enforced.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("tenants/resources")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.PerTenant)]
public sealed class TenantResourcesController(
    ISender sender,
    IResourceQuotaService quotaService,
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
        {
            return false;
        }

        // System admins bypass tenant membership check
        if (actor.IsSystemAdmin)
        {
            return true;
        }

        // If actor's current tenant matches, allow access
        if (actor.TenantId.HasValue && actor.TenantId.Value == tenantId)
        {
            return true;
        }

        // Check actual tenant membership in database
        return await tenantMembershipChecker.IsUserMemberOfTenantAsync(
            actor.SubjectIdAsGuid.Value, 
            tenantId, 
            ct);
    }
    #region Collection Operations - /v1/tenants/{tenantId}/resources

    /// <summary>
    ///     Get usage records for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="usageType">Optional filter by resource usage type</param>
    /// <param name="startDate">Optional filter by start date</param>
    /// <param name="endDate">Optional filter by end date</param>
    /// <param name="pageNumber">Page number (1-based), defaults to 1</param>
    /// <param name="pageSize">Number of records per page (1-200), defaults to 50</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of resource usage records</returns>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/resources/usage-records")]
    [EndpointSummary("Get usage records for a tenant")]
    [EndpointDescription("Retrieves paginated resource usage records for a specific tenant with optional filtering by type and date range.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsageRecords(
        Guid tenantId, 
        [FromQuery] ResourceUsageType? usageType, 
        [FromQuery] DateTime? startDate, 
        [FromQuery] DateTime? endDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
        {
            return Forbid();
        }

        return Ok(await sender.Send(new GetResourceUsageRecordsQuery(tenantId, usageType, startDate, endDate, pageNumber, pageSize), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Get current usage summary for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Current resource usage summary</returns>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/resources/usage-summary")]
    [EndpointSummary("Get current usage summary for a tenant")]
    [EndpointDescription("Retrieves the current aggregated resource usage summary for a specific tenant.")]
    [ProducesResponseType<Dictionary<ResourceUsageType, int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUsageSummary(Guid tenantId, CancellationToken ct)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
        {
            return Forbid();
        }

        return Ok(await sender.Send(new GetCurrentResourceUsageSummaryQuery(tenantId), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Check resource limits for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="usageType">Optional filter by specific resource usage type</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Resource limit check results</returns>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/resources/limits")]
    [EndpointSummary("Check resource limits for a tenant")]
    [EndpointDescription("Checks current resource usage against configured limits for a specific tenant.")]
    [ProducesResponseType<Dictionary<ResourceUsageType, bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckLimits(Guid tenantId, [FromQuery] ResourceUsageType? usageType, CancellationToken ct)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
        {
            return Forbid();
        }

        return Ok(await sender.Send(new CheckResourceUsageLimitsQuery(tenantId, usageType), ct).ConfigureAwait(false));
    }

    #endregion

    #region Resource Operations - /v1/tenants/{tenantId}/resources:action

    /// <summary>
    ///     Record resource usage for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="body">Resource usage record request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created usage record identifier</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}/resources:record")]
    [EndpointSummary("Record resource usage for a tenant")]
    [EndpointDescription("Records a new resource usage entry for the specified tenant.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Record(Guid tenantId, [FromBody] RecordTenantResourceUsageRequest body, CancellationToken ct)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
        {
            return Forbid();
        }

        ArgumentNullException.ThrowIfNull(body);

        var metadata = body.Metadata != null ? System.Text.Json.JsonSerializer.Serialize(body.Metadata) : null;
        var id = await sender.Send(new RecordResourceUsageCommand(tenantId, body.ResourceUsageType, body.Count, body.PeriodStart, body.PeriodEnd, metadata), ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetUsageRecords), new { tenantId }, new { id });
    }

    /// <summary>
    ///     Record resource usage with quota enforcement for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="body">Resource usage record request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created usage record identifier with quota status</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}/resources:record-with-quota-check")]
    [EndpointSummary("Record resource usage with quota enforcement for a tenant")]
    [EndpointDescription("Records a new resource usage entry after verifying it doesn't exceed configured quotas. Returns 429 if quota would be exceeded.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RecordWithQuotaCheck(Guid tenantId, [FromBody] RecordTenantResourceUsageRequest body, CancellationToken ct)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
        {
            return Forbid();
        }

        ArgumentNullException.ThrowIfNull(body);

        // ATOMIC: Use TryAtomicConsumeAsync to avoid TOCTOU race condition
        // This performs an atomic check-and-increment operation
        var (success, currentUsage, hardLimit) = await quotaService.TryAtomicConsumeAsync(
            tenantId,
            body.ResourceUsageType,
            body.Count,
            ct).ConfigureAwait(false);

        if (!success)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "Quota exceeded",
                details = new
                {
                    isAllowed = false,
                    currentUsage,
                    hardLimit,
                    requested = body.Count
                }
            });
        }

        // Quota was atomically consumed - now create the usage record for audit trail
        var metadata = body.Metadata != null ? System.Text.Json.JsonSerializer.Serialize(body.Metadata) : null;
        var id = await sender.Send(
            new RecordResourceUsageCommand(tenantId, body.ResourceUsageType, body.Count, body.PeriodStart, body.PeriodEnd, metadata, SkipQuotaIncrement: true),
            ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetUsageRecords), new { tenantId }, new
        {
            id,
            quotaInfo = new
            {
                isAllowed = true,
                currentUsage,
                hardLimit
            }
        });
    }

    /// <summary>
    ///     Reset resource usage for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="usageType">Resource usage type to reset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}/resources:reset")]
    [EndpointSummary("Reset resource usage for a tenant")]
    [EndpointDescription("Resets the resource usage counters for a specific tenant and resource type to zero.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reset(Guid tenantId, [FromQuery] ResourceUsageType usageType, CancellationToken ct)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
        {
            return Forbid();
        }

        await sender.Send(new ResetResourceUsageCommand(tenantId, usageType), ct).ConfigureAwait(false);

        return NoContent();
    }

    #endregion
}

/// <summary>
///     Request model for recording tenant resource usage (without tenantId in body)
/// </summary>
public sealed record RecordTenantResourceUsageRequest(
    ResourceUsageType ResourceUsageType,
    int Count,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    Dictionary<string, string>? Metadata = null
);

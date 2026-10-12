using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Resources;

/// <summary>
///     User Resources API Controller - RESTful API for user-level resource usage tracking
/// </summary>
/// <remarks>
///     All endpoints require authentication. User ownership or admin role is enforced.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("users/resources")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.PerUser)]
public sealed class UserResourcesController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     Validates that the current actor owns the user resource or is an admin.
    ///     Fail-closed: Returns false if actor is not authenticated or not authorized.
    /// </summary>
    private bool ValidateUserOwnership(Guid userId)
    {
        var actor = actorContextAccessor.ActorContext;
        
        // Fail-closed: No actor means no access
        if (actor is null || !actor.IsAuthenticated || !actor.SubjectIdAsGuid.HasValue)
        {
            return false;
        }

        // System admins bypass ownership check
        if (actor.IsSystemAdmin)
        {
            return true;
        }

        // User can only access their own resources
        return actor.SubjectIdAsGuid.Value == userId;
    }

    #region Collection Operations - /v1/users/{userId}/resources

    /// <summary>
    ///     Get usage records for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="usageType">Optional filter by resource usage type</param>
    /// <param name="startDate">Optional filter by start date</param>
    /// <param name="endDate">Optional filter by end date</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of resource usage records</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/resources/usage-records")]
    [EndpointSummary("Get usage records for a user")]
    [EndpointDescription("Retrieves resource usage records for a specific user with optional filtering by type and date range.")]
    [ProducesResponseType<IEnumerable<UsageRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsageRecords(Guid userId, [FromQuery] ResourceUsageType? usageType, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
        {
            return Forbid();
        }

        return Ok(await sender.Send(new GetUserResourceUsageRecordsQuery(userId, usageType, startDate, endDate), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Get current usage summary for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Current resource usage summary</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/resources/usage-summary")]
    [EndpointSummary("Get current usage summary for a user")]
    [EndpointDescription("Retrieves the current aggregated resource usage summary for a specific user.")]
    [ProducesResponseType<Dictionary<ResourceUsageType, long>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUsageSummary(Guid userId, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
        {
            return Forbid();
        }

        return Ok(await sender.Send(new GetCurrentUserResourceUsageSummaryQuery(userId), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Check resource limits for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="usageType">Optional filter by specific resource usage type</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Resource limit check results</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/resources/limits")]
    [EndpointSummary("Check resource limits for a user")]
    [EndpointDescription("Checks current resource usage against configured limits for a specific user.")]
    [ProducesResponseType<Dictionary<ResourceUsageType, bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckLimits(Guid userId, [FromQuery] ResourceUsageType? usageType, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
        {
            return Forbid();
        }

        return Ok(await sender.Send(new CheckUserResourceUsageLimitsQuery(userId, usageType), ct).ConfigureAwait(false));
    }

    #endregion

    #region Resource Operations - /v1/users/{userId}/resources:action

    /// <summary>
    ///     Record resource usage for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="body">Resource usage record request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created usage record identifier</returns>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/resources:record")]
    [EndpointSummary("Record resource usage for a user")]
    [EndpointDescription("Records a new resource usage entry for the specified user.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Record(Guid userId, [FromBody] RecordUserResourceUsageRequest body, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
        {
            return Forbid();
        }

        ArgumentNullException.ThrowIfNull(body);

        var metadata = body.Metadata != null ? System.Text.Json.JsonSerializer.Serialize(body.Metadata) : null;
        var id = await sender.Send(new RecordUserResourceUsageCommand(userId, body.ResourceUsageType, body.Count, body.PeriodStart, body.PeriodEnd, metadata), ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetUsageRecords), new { userId }, new { id });
    }

    /// <summary>
    ///     Record resource usage with quota enforcement for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="body">Resource usage record request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created usage record identifier with quota status</returns>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/resources:record-with-quota-check")]
    [EndpointSummary("Record resource usage with quota enforcement for a user")]
    [EndpointDescription("Records a new resource usage entry after verifying it doesn't exceed configured quotas. Returns 429 if quota would be exceeded.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RecordWithQuotaCheck(Guid userId, [FromBody] RecordUserResourceUsageRequest body, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
        {
            return Forbid();
        }

        ArgumentNullException.ThrowIfNull(body);

        // Check quota before recording
        var quotaCheck = await sender.Send(new CheckUserResourceQuotaQuery(userId, body.ResourceUsageType, body.Count), ct).ConfigureAwait(false);

        if (!quotaCheck.IsAllowed) { return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Quota exceeded", details = quotaCheck }); }

        // Record usage
        var metadata = body.Metadata != null ? System.Text.Json.JsonSerializer.Serialize(body.Metadata) : null;
        var id = await sender.Send(new RecordUserResourceUsageCommand(userId, body.ResourceUsageType, body.Count, body.PeriodStart, body.PeriodEnd, metadata), ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetUsageRecords), new { userId }, new { id, quotaInfo = quotaCheck });
    }

    /// <summary>
    ///     Reset resource usage for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="usageType">Resource usage type to reset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/resources:reset")]
    [EndpointSummary("Reset resource usage for a user")]
    [EndpointDescription("Resets the resource usage counters for a specific user and resource type to zero.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reset(Guid userId, [FromQuery] ResourceUsageType usageType, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
        {
            return Forbid();
        }

        await sender.Send(new ResetUserResourceUsageCommand(userId, usageType), ct).ConfigureAwait(false);

        return NoContent();
    }

    #endregion
}

/// <summary>
///     Request model for recording user resource usage (without userId in body)
/// </summary>
public sealed record RecordUserResourceUsageRequest(
    ResourceUsageType ResourceUsageType,
    int Count,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    Dictionary<string, string>? Metadata = null
);

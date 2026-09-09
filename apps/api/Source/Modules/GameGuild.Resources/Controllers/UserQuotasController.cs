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
///     User Quotas API Controller - RESTful API for user-level resource quota management
/// </summary>
/// <remarks>
///     All endpoints require authentication. User ownership or admin role is enforced.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("users/quotas")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.PerUser)]
public sealed class UserQuotasController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     Validates that the current actor is the owner of the resource or a system admin.
    ///     Fail-closed: Returns false if actor is not authenticated or not authorized.
    /// </summary>
    private bool ValidateUserOwnership(Guid userId)
    {
        var actor = actorContextAccessor.ActorContext;
        
        // Fail-closed: No actor means no access
        if (actor is null || !actor.IsAuthenticated || !actor.SubjectIdAsGuid.HasValue)
            return false;
        
        // System admins bypass ownership check
        if (actor.IsSystemAdmin)
            return true;
        
        // Check if actor owns this resource
        return actor.SubjectIdAsGuid.Value == userId;
    }
    #region Collection Operations - /v1/users/{userId}/quotas

    /// <summary>
    ///     Get all quotas for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of configured resource quotas for the user</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/quotas")]
    [EndpointSummary("Get all quotas for a user")]
    [EndpointDescription("Retrieves all configured resource quotas for a specific user.")]
    [ProducesResponseType<IEnumerable<ResourceQuotaResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserQuotas(Guid userId, CancellationToken ct = default)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        var query = new GetUserResourceQuotasQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get specific quota for a resource type
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="type">Resource usage type to get quota for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Resource quota configuration</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/quotas/{type}")]
    [EndpointSummary("Get specific quota for a resource type")]
    [EndpointDescription("Retrieves the quota configuration for a specific resource type for a user.")]
    [ProducesResponseType<ResourceQuotaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuota(Guid userId, ResourceUsageType type, CancellationToken ct = default)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        var query = new GetUserResourceQuotaQuery(userId, type);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        if (result == null) return NotFound($"Quota not found for user {userId} and type {type}");

        return Ok(result);
    }

    #endregion

    #region Item Operations - /v1/users/{userId}/quotas/{type}

    /// <summary>
    ///     Set or update a quota for a resource type
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="type">Resource usage type to configure</param>
    /// <param name="body">Quota configuration settings</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPut("v{version:apiVersion}/users/{userId:guid}/quotas/{type}")]
    [EndpointSummary("Set or update a quota for a resource type")]
    [EndpointDescription("Creates or updates the quota configuration for a specific resource type for a user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetQuota(Guid userId, ResourceUsageType type, [FromBody] SetQuotaRequest body, CancellationToken ct = default)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        ArgumentNullException.ThrowIfNull(body);

        var command = new SetUserResourceQuotaCommand(userId, type, body.SoftLimit, body.HardLimit, body.Period, body.IsActive, body.ResetTime);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Delete a quota for a resource type
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="type">Resource usage type to delete quota for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("v{version:apiVersion}/users/{userId:guid}/quotas/{type}")]
    [EndpointSummary("Delete a quota for a resource type")]
    [EndpointDescription("Removes the quota configuration for a specific resource type for a user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteQuota(Guid userId, ResourceUsageType type, CancellationToken ct = default)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        var command = new DeleteUserResourceQuotaCommand(userId, type);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    #endregion

    #region Quota Actions - /v1/users/{userId}/quotas/{type}:action

    /// <summary>
    ///     Reset quota usage to zero
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="type">Resource usage type to reset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/quotas/{type}:reset")]
    [EndpointSummary("Reset quota usage to zero")]
    [EndpointDescription("Resets the current usage counter for a specific resource quota to zero without changing the quota limits.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetQuota(Guid userId, ResourceUsageType type, CancellationToken ct = default)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        var command = new ResetUserResourceQuotaCommand(userId, type);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Toggle quota activation status
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="type">Resource usage type to toggle</param>
    /// <param name="body">Toggle request with desired active state</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/quotas/{type}:toggle")]
    [EndpointSummary("Toggle quota activation status")]
    [EndpointDescription("Activates or deactivates a resource quota. Inactive quotas are not enforced.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleQuota(Guid userId, ResourceUsageType type, [FromBody] ToggleResourceQuotaRequest body, CancellationToken ct = default)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        ArgumentNullException.ThrowIfNull(body);

        var command = new ToggleUserResourceQuotaCommand(userId, type, body.IsActive);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Check if a usage amount would exceed quota
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="type">Resource usage type to check</param>
    /// <param name="body">Check request with amount to validate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Quota enforcement result indicating if usage is allowed</returns>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/quotas/{type}:check")]
    [NoBusinessMutationEndpoint("This POST-shaped quota check is read-only and records no usage.")]
    [EndpointSummary("Check if a usage amount would exceed quota")]
    [EndpointDescription("Validates whether a proposed usage amount would exceed the configured quota limits without recording any usage.")]
    [ProducesResponseType<ResourceQuotaEnforcementResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckQuota(Guid userId, ResourceUsageType type, [FromBody] CheckResourceQuotaRequest body, CancellationToken ct = default)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        ArgumentNullException.ThrowIfNull(body);

        var query = new CheckUserResourceQuotaQuery(userId, type, body.Amount);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion
}

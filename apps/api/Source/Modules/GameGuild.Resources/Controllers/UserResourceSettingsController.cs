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
///     User Resource Settings API Controller - RESTful API for managing user-level resource settings overrides
/// </summary>
/// <remarks>
///     All endpoints require authentication. User ownership or admin role is enforced.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("users/resources/settings")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.PerUser)]
public sealed class UserResourceSettingsController(
    IResourceSettingsRepository settingsRepository,
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
            return false;
        
        // System admins bypass ownership check
        if (actor.IsSystemAdmin)
            return true;
        
        // User can only access their own resources
        return actor.SubjectIdAsGuid.Value == userId;
    }

    /// <summary>
    ///     Get all setting overrides for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of user setting overrides</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/resources/settings")]
    [EndpointSummary("Get all setting overrides for a user")]
    [EndpointDescription("Retrieves all resource setting overrides for a specific user.")]
    [ProducesResponseType<IEnumerable<ResourceSettings>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserSettings(Guid userId, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        return Ok(await settingsRepository.GetByUserAsync(userId, ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Get a specific setting override by key for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="key">Setting key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Setting override</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/resources/settings/{key}")]
    [EndpointSummary("Get a specific setting override by key for a user")]
    [EndpointDescription("Retrieves a specific resource setting override by its key for a user.")]
    [ProducesResponseType<ResourceSettings>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserSettingByKey(Guid userId, string key, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        var setting = await settingsRepository.GetByUserKeyAsync(userId, key, ct).ConfigureAwait(false);

        if (setting == null) return NotFound($"Setting override not found for user {userId} and key: {key}");

        return Ok(setting);
    }

    /// <summary>
    ///     Create or update a setting override for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="key">Setting key</param>
    /// <param name="body">Setting data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created or updated setting</returns>
    [HttpPut("v{version:apiVersion}/users/{userId:guid}/resources/settings/{key}")]
    [EndpointSummary("Create or update a setting override for a user")]
    [EndpointDescription("Creates a new setting override or updates an existing one for a user.")]
    [ProducesResponseType<ResourceSettings>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetUserSetting(Guid userId, string key, [FromBody] SetUserResourceSettingsRequest body, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        ArgumentNullException.ThrowIfNull(body);

        var setting = await sender.Send(new SetUserResourceSettingCommand(userId, key, body), ct)
            .ConfigureAwait(false);
        return Ok(setting);
    }
}

/// <summary>
///     Request model for setting user resource settings override
/// </summary>
public sealed record SetUserResourceSettingsRequest(string? Value);

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
///     User Resource Metadata API Controller - RESTful API for managing user-level resource metadata
/// </summary>
/// <remarks>
///     All endpoints require authentication. User ownership or admin role is enforced.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("users/resources/metadata")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.PerUser)]
public sealed class UserResourceMetadataController(
    IResourceMetadataRepository metadataRepository,
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
    ///     Get all metadata entries for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of metadata entries</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/resources/metadata")]
    [EndpointSummary("Get all metadata entries for a user")]
    [EndpointDescription("Retrieves all resource metadata entries for a specific user.")]
    [ProducesResponseType<IEnumerable<ResourceMetadata>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserMetadata(Guid userId, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        return Ok(await metadataRepository.GetByUserAsync(userId, ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Get a specific metadata entry by key for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="key">Metadata key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Metadata entry</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/resources/metadata/{key}")]
    [EndpointSummary("Get a specific metadata entry by key for a user")]
    [EndpointDescription("Retrieves a specific resource metadata entry by its key for a user.")]
    [ProducesResponseType<ResourceMetadata>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserMetadataByKey(Guid userId, string key, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        var metadata = await metadataRepository.GetByUserKeyAsync(userId, key, ct).ConfigureAwait(false);

        if (metadata == null) return NotFound($"Metadata not found for user {userId} and key: {key}");

        return Ok(metadata);
    }

    /// <summary>
    ///     Create or update a metadata entry for a user
    /// </summary>
    /// <param name="userId">User unique identifier</param>
    /// <param name="key">Metadata key</param>
    /// <param name="body">Metadata entry data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created or updated metadata entry</returns>
    [HttpPut("v{version:apiVersion}/users/{userId:guid}/resources/metadata/{key}")]
    [EndpointSummary("Create or update a metadata entry for a user")]
    [EndpointDescription("Creates a new metadata entry or updates an existing one for a user.")]
    [ProducesResponseType<ResourceMetadata>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetUserMetadata(Guid userId, string key, [FromBody] SetResourceMetadataRequest body, CancellationToken ct)
    {
        if (!ValidateUserOwnership(userId))
            return Forbid();
        
        ArgumentNullException.ThrowIfNull(body);

        var metadata = await sender.Send(new SetUserResourceMetadataCommand(userId, key, body), ct)
            .ConfigureAwait(false);
        return Ok(metadata);
    }
}

/// <summary>
///     Request model for setting resource metadata
/// </summary>
public sealed record SetResourceMetadataRequest(
    string? Value,
    string? DataType = null,
    string? Description = null,
    string? Category = null,
    int? DisplayOrder = null
);

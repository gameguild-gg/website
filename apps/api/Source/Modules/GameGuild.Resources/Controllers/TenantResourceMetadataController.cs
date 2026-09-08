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
///     Tenant Resource Metadata API Controller - RESTful API for managing tenant-level resource metadata
/// </summary>
/// <remarks>
///     All endpoints require authentication. Tenant membership validation is enforced.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("tenants/resources/metadata")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.PerTenant)]
public sealed class TenantResourceMetadataController(
    IResourceMetadataRepository metadataRepository,
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
        
        // Check actual tenant membership in database
        return await tenantMembershipChecker.IsUserMemberOfTenantAsync(
            actor.SubjectIdAsGuid.Value, 
            tenantId, 
            ct);
    }

    /// <summary>
    ///     Get all metadata entries for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="category">Optional filter by category</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of metadata entries</returns>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/resources/metadata")]
    [EndpointSummary("Get all metadata entries for a tenant")]
    [EndpointDescription("Retrieves all resource metadata entries for a specific tenant, optionally filtered by category.")]
    [ProducesResponseType<IEnumerable<ResourceMetadata>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenantMetadata(Guid tenantId, [FromQuery] string? category, CancellationToken ct)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        if (!string.IsNullOrEmpty(category)) { return Ok(await metadataRepository.GetByCategoryAsync(tenantId, category, ct).ConfigureAwait(false)); }

        return Ok(await metadataRepository.GetByTenantAsync(tenantId, ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Get a specific metadata entry by key
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="key">Metadata key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Metadata entry</returns>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/resources/metadata/{key}")]
    [EndpointSummary("Get a specific metadata entry by key")]
    [EndpointDescription("Retrieves a specific resource metadata entry by its key for a tenant.")]
    [ProducesResponseType<ResourceMetadata>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantMetadataByKey(Guid tenantId, string key, CancellationToken ct)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        var metadata = await metadataRepository.GetByKeyAsync(tenantId, key, ct).ConfigureAwait(false);

        if (metadata == null) return NotFound($"Metadata not found for key: {key}");

        return Ok(metadata);
    }

    /// <summary>
    ///     Create or update a metadata entry
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="key">Metadata key</param>
    /// <param name="body">Metadata entry data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created or updated metadata entry</returns>
    [HttpPut("v{version:apiVersion}/tenants/{tenantId:guid}/resources/metadata/{key}")]
    [EndpointSummary("Create or update a metadata entry")]
    [EndpointDescription("Creates a new metadata entry or updates an existing one for a tenant.")]
    [ProducesResponseType<ResourceMetadata>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetTenantMetadata(Guid tenantId, string key, [FromBody] SetResourceMetadataRequest body, CancellationToken ct)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        ArgumentNullException.ThrowIfNull(body);

        var metadata = await sender.Send(new SetTenantResourceMetadataCommand(tenantId, key, body), ct)
            .ConfigureAwait(false);
        return Ok(metadata);
    }

    /// <summary>
    ///     Delete a metadata entry
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="key">Metadata key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("v{version:apiVersion}/tenants/{tenantId:guid}/resources/metadata/{key}")]
    [EndpointSummary("Delete a metadata entry")]
    [EndpointDescription("Removes a resource metadata entry for a tenant.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTenantMetadata(Guid tenantId, string key, CancellationToken ct)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, ct))
            return Forbid();
        
        var deleted = await sender.Send(new DeleteTenantResourceMetadataCommand(tenantId, key), ct)
            .ConfigureAwait(false);

        if (!deleted) return NotFound($"Metadata not found for key: {key}");

        return NoContent();
    }
}

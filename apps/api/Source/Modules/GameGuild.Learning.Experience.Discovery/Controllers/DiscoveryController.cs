using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Attributes;
using GameGuild.Learning.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Experience.Discovery;

/// <summary>
/// REST API controller for LXP Discovery operations
/// </summary>
/// <remarks>
/// DiscoveryController implements a complete API for learning content discovery:
/// - Featured content management (hero banners, highlights, promotions)
/// - Course collections (curated lists, categories, skills)
/// - Search analytics (tracking queries, clicks, popular searches)
/// 
/// Public endpoints for discovery experience
/// Admin endpoints for content curation
/// </remarks>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/discovery")]
[LxpCapabilityFilter]
[LxpCapability(LxpCapabilities.Discovery)]
[Authorize]
public class DiscoveryController(IDiscoveryService discoveryService, ISender sender) : BaseApiController
{
    // ===== PUBLIC FEATURED CONTENT ENDPOINTS =====

    /// <summary>
    /// Get all currently active featured content
    /// </summary>
    [HttpGet("featured")]
    public async Task<ActionResult<IEnumerable<FeaturedContentDto>>> GetActiveFeaturedContent(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var content = await discoveryService.GetActiveFeaturedContentAsync(tenantId, skip, take).ConfigureAwait(false);
        return Ok(content.Select(c => c.ToDto()));
    }

    /// <summary>
    /// Get featured content by type (e.g., HeroBanner, NewRelease)
    /// </summary>
    [HttpGet("featured/type/{type}")]
    public async Task<ActionResult<IEnumerable<FeaturedContentDto>>> GetFeaturedContentByType(
        FeaturedContentType type,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var content = await discoveryService.GetFeaturedContentByTypeAsync(type, tenantId, skip, take).ConfigureAwait(false);
        return Ok(content.Select(c => c.ToDto()));
    }

    /// <summary>
    /// Get a specific featured content item by ID
    /// </summary>
    [HttpGet("featured/{id}")]
    public async Task<ActionResult<FeaturedContentDto>> GetFeaturedContentById(Guid id)
    {
        var content = await discoveryService.GetFeaturedContentByIdAsync(id).ConfigureAwait(false);
        if (content == null) return NotFound();
        return Ok(content.ToDto());
    }

    // ===== ADMIN FEATURED CONTENT ENDPOINTS =====

    /// <summary>
    /// Create new featured content (admin)
    /// </summary>
    [HttpPost("featured")]
    [RequireContentTypePermission<FeaturedContent>(PermissionType.Create)]
    public async Task<ActionResult<FeaturedContentDto>> CreateFeaturedContent(
        [FromBody] CreateFeaturedContentDto dto,
        [FromQuery] Guid? tenantId = null)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var content = await sender.Send(new CreateFeaturedContentCommand(
            dto.Type,
            dto.Title,
            dto.DisplayOrder,
            dto.CourseId,
            dto.LearningPathId,
            tenantId,
            dto.Subtitle,
            dto.ImageUrl,
            dto.LinkUrl,
            dto.StartsAt,
            dto.EndsAt,
            dto.TargetAudience)).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetFeaturedContentById), new { id = content.Id }, content.ToDto());
    }

    /// <summary>
    /// Update featured content (admin)
    /// </summary>
    [HttpPut("featured/{id}")]
    [RequireResourcePermission<PermissionType, FeaturedContent>(PermissionType.Edit)]
    public async Task<ActionResult<FeaturedContentDto>> UpdateFeaturedContent(
        Guid id,
        [FromBody] UpdateFeaturedContentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var content = await sender.Send(new UpdateFeaturedContentCommand(
            id,
            dto.Title,
            dto.Subtitle,
            dto.ImageUrl,
            dto.LinkUrl,
            dto.DisplayOrder,
            dto.StartsAt,
            dto.EndsAt,
            dto.IsActive,
            dto.TargetAudience)).ConfigureAwait(false);
        if (content == null) return NotFound();
        return Ok(content.ToDto());
    }

    /// <summary>
    /// Toggle featured content active state (admin)
    /// </summary>
    [HttpPatch("featured/{id}/toggle")]
    [RequireResourcePermission<PermissionType, FeaturedContent>(PermissionType.Edit)]
    public async Task<ActionResult<FeaturedContentDto>> ToggleFeaturedContent(
        Guid id,
        [FromQuery] bool isActive)
    {
        var content = await sender.Send(new ToggleFeaturedContentCommand(id, isActive)).ConfigureAwait(false);
        if (content == null) return NotFound();
        return Ok(content.ToDto());
    }

    /// <summary>
    /// Delete featured content (admin)
    /// </summary>
    [HttpDelete("featured/{id}")]
    [RequireResourcePermission<PermissionType, FeaturedContent>(PermissionType.Delete)]
    public async Task<IActionResult> DeleteFeaturedContent(Guid id)
    {
        var success = await sender.Send(new DeleteFeaturedContentCommand(id)).ConfigureAwait(false);
        if (!success) return NotFound();
        return NoContent();
    }

    // ===== PUBLIC COLLECTION ENDPOINTS =====

    /// <summary>
    /// Get all published course collections
    /// </summary>
    [HttpGet("collections")]
    public async Task<ActionResult<IEnumerable<CourseCollectionDto>>> GetPublishedCollections(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] CollectionType? type = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var collections = await discoveryService.GetPublishedCollectionsAsync(tenantId, type, skip, take).ConfigureAwait(false);
        return Ok(collections.Select(c => c.ToDto()));
    }

    /// <summary>
    /// Get featured course collections
    /// </summary>
    [HttpGet("collections/featured")]
    public async Task<ActionResult<IEnumerable<CourseCollectionDto>>> GetFeaturedCollections(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int take = 10)
    {
        var collections = await discoveryService.GetFeaturedCollectionsAsync(tenantId, take).ConfigureAwait(false);
        return Ok(collections.Select(c => c.ToDto()));
    }

    /// <summary>
    /// Get a course collection by slug
    /// </summary>
    [HttpGet("collections/slug/{slug}")]
    public async Task<ActionResult<CourseCollectionDto>> GetCollectionBySlug(
        string slug,
        [FromQuery] Guid? tenantId = null)
    {
        var collection = await discoveryService.GetCollectionBySlugAsync(slug, tenantId).ConfigureAwait(false);
        if (collection == null) return NotFound();
        return Ok(collection.ToDto());
    }

    /// <summary>
    /// Get a course collection by ID
    /// </summary>
    [HttpGet("collections/{id}")]
    public async Task<ActionResult<CourseCollectionDto>> GetCollectionById(Guid id)
    {
        var collection = await discoveryService.GetCollectionByIdAsync(id).ConfigureAwait(false);
        if (collection == null) return NotFound();
        return Ok(collection.ToDto());
    }

    // ===== ADMIN/CURATOR COLLECTION ENDPOINTS =====

    /// <summary>
    /// Get collections created by a specific curator
    /// </summary>
    [HttpGet("collections/curator/{curatorId}")]
    public async Task<ActionResult<IEnumerable<CourseCollectionDto>>> GetCollectionsByCurator(
        Guid curatorId,
        [FromQuery] bool includeUnpublished = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var collections = await discoveryService.GetCollectionsByCuratorAsync(curatorId, includeUnpublished, skip, take).ConfigureAwait(false);
        return Ok(collections.Select(c => c.ToDto()));
    }

    /// <summary>
    /// Create a new course collection
    /// </summary>
    [HttpPost("collections")]
    [RequireContentTypePermission<CourseCollection>(PermissionType.Create)]
    public async Task<ActionResult<CourseCollectionDto>> CreateCollection(
        [FromBody] CreateCourseCollectionDto dto,
        [FromQuery] Guid curatorId,
        [FromQuery] Guid? tenantId = null)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var collection = await sender.Send(new CreateCourseCollectionCommand(
            curatorId,
            dto.Title,
            dto.Type,
            tenantId,
            dto.Description,
            dto.ImageUrl)).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetCollectionById), new { id = collection.Id }, collection.ToDto());
    }

    /// <summary>
    /// Update a course collection
    /// </summary>
    [HttpPut("collections/{id}")]
    [RequireResourcePermission<PermissionType, CourseCollection>(PermissionType.Edit)]
    public async Task<ActionResult<CourseCollectionDto>> UpdateCollection(
        Guid id,
        [FromBody] UpdateCourseCollectionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var collection = await sender.Send(new UpdateCourseCollectionCommand(
            id,
            dto.Title,
            dto.Description,
            dto.ImageUrl,
            dto.IsFeatured)).ConfigureAwait(false);
        if (collection == null) return NotFound();
        return Ok(collection.ToDto());
    }

    /// <summary>
    /// Publish a course collection
    /// </summary>
    [HttpPost("collections/{id}/publish")]
    [RequireResourcePermission<PermissionType, CourseCollection>(PermissionType.Publish)]
    public async Task<ActionResult<CourseCollectionDto>> PublishCollection(Guid id)
    {
        var collection = await sender.Send(new PublishCourseCollectionCommand(id)).ConfigureAwait(false);
        if (collection == null) return NotFound();
        return Ok(collection.ToDto());
    }

    /// <summary>
    /// Unpublish a course collection
    /// </summary>
    [HttpPost("collections/{id}/unpublish")]
    [RequireResourcePermission<PermissionType, CourseCollection>(PermissionType.Unpublish)]
    public async Task<ActionResult<CourseCollectionDto>> UnpublishCollection(Guid id)
    {
        var collection = await sender.Send(new UnpublishCourseCollectionCommand(id)).ConfigureAwait(false);
        if (collection == null) return NotFound();
        return Ok(collection.ToDto());
    }

    /// <summary>
    /// Delete a course collection
    /// </summary>
    [HttpDelete("collections/{id}")]
    [RequireResourcePermission<PermissionType, CourseCollection>(PermissionType.Delete)]
    public async Task<IActionResult> DeleteCollection(Guid id)
    {
        var success = await sender.Send(new DeleteCourseCollectionCommand(id)).ConfigureAwait(false);
        if (!success) return NotFound();
        return NoContent();
    }

    // ===== SEARCH ANALYTICS ENDPOINTS =====

    /// <summary>
    /// Record a search query (for analytics)
    /// </summary>
    [HttpPost("search/record")]
    public async Task<ActionResult<SearchHistoryDto>> RecordSearch(
        [FromBody] RecordSearchDto dto,
        [FromQuery] Guid? userId = null)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var searchHistory = await sender.Send(new RecordSearchCommand(
            dto.Query,
            dto.ResultCount,
            userId,
            dto.Filters)).ConfigureAwait(false);
        return Ok(searchHistory.ToDto());
    }

    /// <summary>
    /// Record a click from search results
    /// </summary>
    [HttpPost("search/{searchId}/click")]
    public async Task<IActionResult> RecordSearchClick(
        Guid searchId,
        [FromBody] RecordSearchClickDto dto)
    {
        var success = await sender.Send(new RecordSearchClickCommand(searchId, dto.ClickedCourseId)).ConfigureAwait(false);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Get search history for a user
    /// </summary>
    [HttpGet("search/history/{userId}")]
    public async Task<ActionResult<IEnumerable<SearchHistoryDto>>> GetUserSearchHistory(
        Guid userId,
        [FromQuery] int take = 20)
    {
        var history = await discoveryService.GetUserSearchHistoryAsync(userId, take).ConfigureAwait(false);
        return Ok(history.Select(h => h.ToDto()));
    }

    /// <summary>
    /// Get popular searches (admin analytics)
    /// </summary>
    [HttpGet("search/popular")]
    [RequireContentTypePermission<SearchHistory>(PermissionType.Read)]
    public async Task<ActionResult<IEnumerable<PopularSearchResult>>> GetPopularSearches(
        [FromQuery] int daysBack = 30,
        [FromQuery] int take = 20)
    {
        var popularSearches = await discoveryService.GetPopularSearchesAsync(daysBack, take).ConfigureAwait(false);
        return Ok(popularSearches);
    }
}

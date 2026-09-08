using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Abstractions;
using GameGuild.Learning.Attributes;
using GameGuild.Learning.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Experience.Recommendations;

/// <summary>
/// REST API controller for LXP Recommendations operations
/// </summary>
/// <remarks>
/// RecommendationsController implements a complete API for personalized learning recommendations:
/// - Get personalized recommendations for users
/// - User learning profile management (preferences, skills, goals)
/// - Popular and trending course discovery
/// - Similar course suggestions
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/recommendations")]
[LxpCapabilityFilter]
[LxpCapability(LxpCapabilities.RecommendationsBasic)]
public class RecommendationsController(
    IRecommendationService recommendationService,
    IActorContextAccessor actorContextAccessor,
    ISender sender)
    : LearningControllerBase(actorContextAccessor)
{
    // ===== RECOMMENDATIONS ENDPOINTS =====

    /// <summary>
    /// Get personalized recommendations for the current user
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<RecommendationDto>>> GetMyRecommendations(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] RecommendationType? type = null,
        [FromQuery] bool includeViewed = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10)
    {
        var userId = GetRequiredUserId();
        var recommendations = await recommendationService.GetUserRecommendationsAsync(
            userId, tenantId, type, includeViewed, skip, take).ConfigureAwait(false);
        return Ok(recommendations.Select(r => r.ToDto()));
    }

    /// <summary>
    /// Generate new recommendations for the current user
    /// </summary>
    [HttpPost("me/generate")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<RecommendationDto>>> GenerateRecommendations(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int maxResults = 10)
    {
        var userId = GetRequiredUserId();
        var recommendations = await sender.Send(
            new GenerateRecommendationsCommand(userId, tenantId, maxResults)).ConfigureAwait(false);
        return Ok(recommendations.Select(r => r.ToDto()));
    }

    /// <summary>
    /// Mark a recommendation as viewed
    /// </summary>
    [HttpPost("{id}/viewed")]
    [Authorize]
    public async Task<ActionResult> MarkRecommendationViewed(Guid id)
    {
        var userId = GetRequiredUserId();
        await sender.Send(new MarkRecommendationViewedCommand(id, userId)).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Dismiss a recommendation
    /// </summary>
    [HttpPost("{id}/dismiss")]
    [Authorize]
    public async Task<ActionResult> DismissRecommendation(Guid id)
    {
        var userId = GetRequiredUserId();
        await sender.Send(new DismissRecommendationCommand(id, userId)).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Refresh recommendations (clear expired, generate new)
    /// </summary>
    [HttpPost("me/refresh")]
    [Authorize]
    public async Task<ActionResult> RefreshRecommendations(
        [FromQuery] Guid? tenantId = null)
    {
        var userId = GetRequiredUserId();
        await sender.Send(new RefreshRecommendationsCommand(userId, tenantId)).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Get recommendation statistics for the current user
    /// </summary>
    [HttpGet("me/statistics")]
    [Authorize]
    public async Task<ActionResult<RecommendationStatisticsDto>> GetMyStatistics()
    {
        var userId = GetRequiredUserId();
        var stats = await recommendationService.GetStatisticsAsync(userId).ConfigureAwait(false);
        return Ok(stats);
    }

    // ===== LEARNING PROFILE ENDPOINTS =====

    /// <summary>
    /// Get the current user's learning profile
    /// </summary>
    [HttpGet("me/profile")]
    [Authorize]
    public async Task<ActionResult<UserLearningProfileDto>> GetMyProfile()
    {
        var userId = GetRequiredUserId();
        var profile = await recommendationService.GetOrCreateUserProfileAsync(userId).ConfigureAwait(false);
        return Ok(profile.ToDto());
    }

    /// <summary>
    /// Update the current user's learning profile
    /// </summary>
    [HttpPut("me/profile")]
    [Authorize]
    public async Task<ActionResult<UserLearningProfileDto>> UpdateMyProfile(
        [FromBody] CreateOrUpdateLearningProfileDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetRequiredUserId();
        var profile = await sender.Send(new CreateOrUpdateLearningProfileCommand(
            userId,
            dto.PreferredCategories,
            dto.PreferredDifficulty,
            dto.PreferredDuration,
            dto.LearningGoals,
            dto.Skills)).ConfigureAwait(false);
        return Ok(profile.ToDto());
    }

    /// <summary>
    /// Add a skill to the current user's profile
    /// </summary>
    [HttpPost("me/profile/skills")]
    [Authorize]
    public async Task<ActionResult<UserLearningProfileDto>> AddSkillToProfile(
        [FromBody] AddSkillRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Skill))
        {
            return BadRequest("Skill is required");
        }

        var userId = GetRequiredUserId();
        var profile = await sender.Send(new AddSkillToProfileCommand(userId, request.Skill)).ConfigureAwait(false);
        return Ok(profile.ToDto());
    }

    /// <summary>
    /// Remove a skill from the current user's profile
    /// </summary>
    [HttpDelete("me/profile/skills/{skill}")]
    [Authorize]
    public async Task<ActionResult<UserLearningProfileDto>> RemoveSkillFromProfile(
        string skill)
    {
        var userId = GetRequiredUserId();
        var profile = await sender.Send(new RemoveSkillFromProfileCommand(userId, skill)).ConfigureAwait(false);
        return Ok(profile.ToDto());
    }

    // ===== DISCOVERY ENDPOINTS =====

    /// <summary>
    /// Get popular courses across the platform
    /// </summary>
    [HttpGet("popular")]
    public async Task<ActionResult<IEnumerable<PopularCourseDto>>> GetPopularCourses(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? category = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10)
    {
        var courses = await recommendationService.GetPopularCoursesAsync(tenantId, category, skip, take).ConfigureAwait(false);
        return Ok(courses);
    }

    /// <summary>
    /// Get trending courses (high recent enrollment velocity)
    /// </summary>
    [HttpGet("trending")]
    public async Task<ActionResult<IEnumerable<TrendingCourseDto>>> GetTrendingCourses(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int daysWindow = 7,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10)
    {
        var courses = await recommendationService.GetTrendingCoursesAsync(tenantId, daysWindow, skip, take).ConfigureAwait(false);
        return Ok(courses);
    }

    /// <summary>
    /// Get courses similar to a specific course
    /// </summary>
    [HttpGet("courses/{courseId}/similar")]
    public async Task<ActionResult<IEnumerable<SimilarCourseDto>>> GetSimilarCourses(
        Guid courseId,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int maxResults = 5)
    {
        var courses = await recommendationService.GetSimilarCoursesAsync(courseId, tenantId, maxResults).ConfigureAwait(false);
        return Ok(courses);
    }
}

/// <summary>
/// Request DTO for adding a skill
/// </summary>
public sealed record AddSkillRequest(string Skill);

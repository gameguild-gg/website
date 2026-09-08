using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Abstractions;
using GameGuild.Learning.Attributes;
using GameGuild.Learning.Experience.Social.Services;
using GameGuild.Learning.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Experience.Social.Controllers;

/// <summary>
/// API controller for personalized feed operations
/// </summary>
[ApiController]
[Route("api/social")]
[Authorize]
[LxpCapabilityFilter]
[LxpCapability(LxpCapabilities.Social)]
public class FeedController : LearningControllerBase
{
    private readonly IFeedService _feedService;
    private readonly ISender _sender;

    public FeedController(
        IFeedService feedService,
        ISender sender,
        IActorContextAccessor actorContextAccessor) : base(actorContextAccessor)
    {
        _feedService = feedService;
        _sender = sender;
    }

    /// <summary>
    /// Gets the current user's personalized feed
    /// </summary>
    [HttpGet("feed/me")]
    [ProducesResponseType(typeof(IEnumerable<PersonalizedFeedItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPersonalizedFeed(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] FeedItemType? filterByType = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _feedService.GetPersonalizedFeedAsync(userId, skip, take, filterByType, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(MapToFeedItemDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Generates new feed items for the current user
    /// </summary>
    [HttpPost("feed/me/generate")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateFeedItems(CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new GenerateFeedItemsCommand(userId, null), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(new { generatedCount = result.Value });
    }

    /// <summary>
    /// Marks a feed item as viewed
    /// </summary>
    [HttpPost("feed/{id:guid}/viewed")]
    [ProducesResponseType(typeof(PersonalizedFeedItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkFeedItemViewed(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new MarkFeedItemViewedCommand(id), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToFeedItemDto(result.Value));
    }

    /// <summary>
    /// Dismisses a feed item
    /// </summary>
    [HttpPost("feed/{id:guid}/dismiss")]
    [ProducesResponseType(typeof(PersonalizedFeedItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DismissFeedItem(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new DismissFeedItemCommand(id, userId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToFeedItemDto(result.Value));
    }

    private static PersonalizedFeedItemDto MapToFeedItemDto(PersonalizedFeedItem item) =>
        new(item.Id,
            item.ItemType,
            item.CourseId,
            item.DiscussionId,
            item.ReviewId,
            item.LearningPathId,
            item.RelevanceScore,
            item.Reason,
            item.IsViewed,
            item.ExpiresAt,
            item.CreatedAt);
}

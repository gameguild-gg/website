using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Gamification.Achievements;

/// <summary>
/// REST API controller for achievement management and gamification.
/// </summary>
[Route("api/[controller]")]
[Authorize]
public class AchievementsController : BaseApiController
{
    private readonly IAchievementService _achievementService;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly ISender _sender;

    public AchievementsController(
        IAchievementService achievementService,
        IActorContextAccessor actorContextAccessor,
        ISender sender)
    {
        _achievementService = achievementService;
        _actorContextAccessor = actorContextAccessor;
        _sender = sender;
    }

    /// <summary>
    /// Get all achievements for the current user.
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<IEnumerable<UserAchievementDto>>> GetMyAchievements(
        [FromQuery] string? category = null)
    {
        var actorContext = _actorContextAccessor.ActorContext;
        if (actorContext.SubjectIdAsGuid == null)
            return Unauthorized();
            
        var achievements = await _achievementService.GetUserAchievementsAsync(
            actorContext.SubjectIdAsGuid.Value,
            category,
            actorContext.TenantId).ConfigureAwait(false);

        var dtos = achievements.Select(ua => new UserAchievementDto
        {
            Id = ua.Id,
            AchievementId = ua.AchievementId,
            AchievementName = ua.Achievement?.Name ?? "Unknown",
            AchievementDescription = ua.Achievement?.Description,
            Category = ua.Achievement?.Category,
            IconUrl = ua.Achievement?.IconUrl,
            EarnedAt = ua.EarnedAt,
            Level = ua.Level ?? 0,
            Progress = ua.Progress,
            MaxProgress = ua.MaxProgress,
            ProgressPercentage = (decimal)ua.ProgressPercentage,
            IsCompleted = ua.IsCompleted,
            PointsEarned = ua.PointsEarned
        });

        return Ok(dtos);
    }

    /// <summary>
    /// Get total achievement points for the current user.
    /// </summary>
    [HttpGet("my/points")]
    public async Task<ActionResult<int>> GetMyTotalPoints()
    {
        var actorContext = _actorContextAccessor.ActorContext;
        if (actorContext.SubjectIdAsGuid == null)
            return Unauthorized();
            
        var points = await _achievementService.GetUserTotalPointsAsync(
            actorContext.SubjectIdAsGuid.Value,
            actorContext.TenantId).ConfigureAwait(false);

        return Ok(new { totalPoints = points });
    }

    /// <summary>
    /// Get unnotified achievements for the current user.
    /// </summary>
    [HttpGet("my/unnotified")]
    public async Task<ActionResult<IEnumerable<UserAchievementDto>>> GetUnnotifiedAchievements()
    {
        var actorContext = _actorContextAccessor.ActorContext;
        if (actorContext.SubjectIdAsGuid == null)
            return Unauthorized();
            
        var result = await _achievementService.GetUnnotifiedAchievementsAsync(
            actorContext.SubjectIdAsGuid.Value,
            actorContext.TenantId).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return StatusCode(500, result.Error);
        }

        var dtos = result.Value!.Select(ua => new UserAchievementDto
        {
            Id = ua.Id,
            AchievementId = ua.AchievementId,
            AchievementName = ua.Achievement?.Name ?? "Unknown",
            AchievementDescription = ua.Achievement?.Description,
            Category = ua.Achievement?.Category,
            IconUrl = ua.Achievement?.IconUrl,
            EarnedAt = ua.EarnedAt,
            Level = ua.Level ?? 0,
            PointsEarned = ua.PointsEarned,
            IsCompleted = ua.IsCompleted
        });

        return Ok(dtos);
    }

    /// <summary>
    /// Mark an achievement notification as read.
    /// </summary>
    [HttpPost("my/{userAchievementId}/mark-notified")]
    public async Task<ActionResult> MarkAsNotified(Guid userAchievementId)
    {
        var result = await _sender.Send(new MarkAchievementNotifiedCommand(userAchievementId)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return result.Error.Code == "NotFound"
                ? NotFound(result.Error)
                : StatusCode(500, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Get eligible achievements the current user can earn.
    /// </summary>
    [HttpGet("eligible")]
    public async Task<ActionResult<IEnumerable<AchievementDto>>> GetEligibleAchievements()
    {
        var actorContext = _actorContextAccessor.ActorContext;
        if (actorContext.SubjectIdAsGuid == null)
            return Unauthorized();
            
        var result = await _achievementService.GetEligibleAchievementsAsync(
            actorContext.SubjectIdAsGuid.Value,
            actorContext.TenantId).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return StatusCode(500, result.Error);
        }

        var dtos = result.Value!.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Get all available achievements.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<AchievementDto>>> GetAchievements(
        [FromQuery] string? category = null,
        [FromQuery] bool? isActive = true)
    {
        var actorContext = _actorContextAccessor.ActorContext;
        var achievements = await _achievementService.GetAchievementsAsync(
            category,
            isActive,
            includeSecrets: false,
            actorContext.TenantId).ConfigureAwait(false);

        var dtos = achievements.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Get a specific achievement by ID.
    /// </summary>
    [HttpGet("{achievementId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<AchievementDto>> GetAchievement(Guid achievementId)
    {
        var achievement = await _achievementService.GetAchievementByIdAsync(achievementId).ConfigureAwait(false);

        if (achievement == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(achievement));
    }

    /// <summary>
    /// Create a new achievement (admin only).
    /// </summary>
    [HttpPost]
    [RequirePermission(AchievementsPermission.Keys.Create)]
    public async Task<ActionResult<AchievementDto>> CreateAchievement([FromBody] CreateAchievementRequest request)
    {
        var result = await _sender.Send(new CreateAchievementCommand(
            request.Name,
            request.Description,
            request.Category,
            request.Type,
            request.Points,
            request.IconUrl,
            request.Color,
            request.IsSecret,
            request.IsRepeatable,
            request.DisplayOrder)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return StatusCode(500, result.Error);
        }

        var achievement = result.Value!;
        return CreatedAtAction(nameof(GetAchievement), new { achievementId = achievement.Id }, MapToDto(achievement));
    }

    /// <summary>
    /// Update an existing achievement (admin only).
    /// </summary>
    [HttpPut("{achievementId:guid}")]
    [RequirePermission(AchievementsPermission.Keys.Update)]
    public async Task<ActionResult<AchievementDto>> UpdateAchievement(
        Guid achievementId,
        [FromBody] UpdateAchievementRequest request)
    {
        var result = await _sender.Send(new UpdateAchievementCommand(
            achievementId,
            request.Name,
            request.Description,
            request.Category,
            request.Points,
            request.IconUrl,
            request.Color,
            request.IsActive,
            request.IsSecret,
            request.IsRepeatable,
            request.DisplayOrder)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            if (result.Error.Code == "NotFound")
                return NotFound();
            return StatusCode(500, result.Error);
        }

        return Ok(MapToDto(result.Value!));
    }

    /// <summary>
    /// Delete an achievement (admin only).
    /// </summary>
    [HttpDelete("{achievementId:guid}")]
    [RequirePermission(AchievementsPermission.Keys.Delete)]
    public async Task<ActionResult> DeleteAchievement(Guid achievementId)
    {
        var result = await _sender.Send(new DeleteAchievementCommand(achievementId)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return result.Error.Code == "NotFound"
                ? NotFound(result.Error)
                : StatusCode(500, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Award an achievement to a user (admin only, for manual awards).
    /// </summary>
    [HttpPost("{achievementId:guid}/award")]
    [RequirePermission(AchievementsPermission.Keys.Award)]
    public async Task<ActionResult<UserAchievementDto>> AwardAchievement(
        Guid achievementId,
        [FromBody] AwardAchievementRequest request)
    {
        var result = await _sender.Send(new AwardAchievementCommand(
            request.UserId,
            achievementId,
            request.Context)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return result.Error.Code switch
            {
                "NotFound" => NotFound(result.Error),
                "Conflict" => Conflict(result.Error),
                "Validation" => BadRequest(result.Error),
                _ => StatusCode(500, result.Error)
            };
        }

        var ua = result.Value!;
        return Ok(new UserAchievementDto
        {
            Id = ua.Id,
            AchievementId = ua.AchievementId,
            EarnedAt = ua.EarnedAt,
            PointsEarned = ua.PointsEarned,
            IsCompleted = ua.IsCompleted
        });
    }

    private static AchievementDto MapToDto(Achievement achievement)
    {
        return new AchievementDto
        {
            Id = achievement.Id,
            Name = achievement.Name,
            Description = achievement.Description,
            Category = achievement.Category,
            Type = achievement.Type,
            IconUrl = achievement.IconUrl,
            Color = achievement.Color,
            Points = achievement.Points,
            IsActive = achievement.IsActive,
            IsSecret = achievement.IsSecret,
            IsRepeatable = achievement.IsRepeatable,
            DisplayOrder = achievement.DisplayOrder,
            Levels = achievement.Levels.Select(l => new AchievementLevelDto
            {
                Id = l.Id,
                Level = l.Level,
                Name = l.Name,
                RequiredProgress = l.RequiredProgress,
                PointsAwarded = l.Points,
                IconUrl = l.IconUrl
            }).ToList()
        };
    }
}

#region DTOs

public sealed record AchievementDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Type { get; init; }
    public string? IconUrl { get; init; }
    public string? Color { get; init; }
    public int Points { get; init; }
    public bool IsActive { get; init; }
    public bool IsSecret { get; init; }
    public bool IsRepeatable { get; init; }
    public int DisplayOrder { get; init; }
    public List<AchievementLevelDto> Levels { get; init; } = new();
}

public sealed record AchievementLevelDto
{
    public Guid Id { get; init; }
    public int Level { get; init; }
    public string? Name { get; init; }
    public int RequiredProgress { get; init; }
    public int PointsAwarded { get; init; }
    public string? IconUrl { get; init; }
}

public sealed record UserAchievementDto
{
    public Guid Id { get; init; }
    public Guid AchievementId { get; init; }
    public string AchievementName { get; init; } = string.Empty;
    public string? AchievementDescription { get; init; }
    public string? Category { get; init; }
    public string? IconUrl { get; init; }
    public DateTime EarnedAt { get; init; }
    public int Level { get; init; }
    public int Progress { get; init; }
    public int MaxProgress { get; init; }
    public decimal ProgressPercentage { get; init; }
    public bool IsCompleted { get; init; }
    public int PointsEarned { get; init; }
}

public sealed record CreateAchievementRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Type { get; init; }
    public int Points { get; init; }
    public string? IconUrl { get; init; }
    public string? Color { get; init; }
    public bool IsSecret { get; init; }
    public bool IsRepeatable { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record UpdateAchievementRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public int? Points { get; init; }
    public string? IconUrl { get; init; }
    public string? Color { get; init; }
    public bool? IsActive { get; init; }
    public bool? IsSecret { get; init; }
    public bool? IsRepeatable { get; init; }
    public int? DisplayOrder { get; init; }
}

public sealed record AwardAchievementRequest
{
    public Guid UserId { get; init; }
    public string? Context { get; init; }
}

#endregion

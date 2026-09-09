using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace GameGuild.Learning.Cohorts;

/// <summary>
/// Controller for cohort management
/// </summary>
[Route("api/cohorts")]
[Authorize]
public class CohortsController : BaseApiController
{
    private readonly ICohortService _cohortService;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly ILogger<CohortsController> _logger;
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public CohortsController(
        ICohortService cohortService,
        IActorContextAccessor actorContextAccessor,
        IApplicationDbContext context,
        ISender sender,
        ILogger<CohortsController> logger)
    {
        _cohortService = cohortService;
        _actorContextAccessor = actorContextAccessor;
        _context = context;
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Create a new cohort
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CohortDto>> CreateCohort([FromBody] CreateCohortRequest request)
    {
        var actor = _actorContextAccessor.ActorContext;
        
        // Use tenant from actor if not specified in request
        var effectiveRequest = request with { TenantId = request.TenantId ?? actor.TenantId };

        var result = await _sender.Send(new CreateCohortCommand(effectiveRequest)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetCohort), new { id = result.Value.Id }, CohortDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Get a cohort by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CohortDto>> GetCohort(Guid id)
    {
        var cohort = await _cohortService.GetCohortByIdAsync(id).ConfigureAwait(false);
        if (cohort == null)
        {
            return NotFound();
        }

        return Ok(await EnrichAsync(cohort).ConfigureAwait(false));
    }

    /// <summary>
    /// Get all cohorts for a course
    /// </summary>
    [HttpGet("course/{courseId:guid}")]
    public async Task<ActionResult<IEnumerable<CohortDto>>> GetCourseCohorts(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var cohorts = await _cohortService.GetCoursCohortsAsync(courseId, actor.TenantId).ConfigureAwait(false);
        return Ok(await EnrichAsync(cohorts).ConfigureAwait(false));
    }

    /// <summary>
    /// Get active cohorts for a course
    /// </summary>
    [HttpGet("course/{courseId:guid}/active")]
    public async Task<ActionResult<IEnumerable<CohortDto>>> GetActiveCohorts(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var cohorts = await _cohortService.GetActiveCohortsAsync(courseId, actor.TenantId).ConfigureAwait(false);
        return Ok(await EnrichAsync(cohorts).ConfigureAwait(false));
    }

    /// <summary>
    /// Get enrollable cohorts for a course (open with capacity)
    /// </summary>
    [HttpGet("course/{courseId:guid}/enrollable")]
    public async Task<ActionResult<IEnumerable<CohortDto>>> GetEnrollableCohorts(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var cohorts = await _cohortService.GetEnrollableCohortsAsync(courseId, actor.TenantId).ConfigureAwait(false);
        return Ok(await EnrichAsync(cohorts).ConfigureAwait(false));
    }

    /// <summary>
    /// Update a cohort
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CohortDto>> UpdateCohort(Guid id, [FromBody] UpdateCohortRequest request)
    {
        var result = await _sender.Send(new UpdateCohortCommand(id, request)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound 
                ? NotFound(result.Error) 
                : BadRequest(result.Error);
        }

        return Ok(CohortDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Open a cohort for enrollment
    /// </summary>
    [HttpPost("{id:guid}/open")]
    public async Task<ActionResult<CohortDto>> OpenCohort(Guid id)
    {
        var result = await _sender.Send(new OpenCohortCommand(id)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound 
                ? NotFound(result.Error) 
                : BadRequest(result.Error);
        }

        return Ok(CohortDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Close a cohort for enrollment
    /// </summary>
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<CohortDto>> CloseCohort(Guid id)
    {
        var result = await _sender.Send(new CloseCohortCommand(id)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound 
                ? NotFound(result.Error) 
                : BadRequest(result.Error);
        }

        return Ok(CohortDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Mark a cohort as completed
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<CohortDto>> CompleteCohort(Guid id)
    {
        var result = await _sender.Send(new CompleteCohortCommand(id)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound 
                ? NotFound(result.Error) 
                : BadRequest(result.Error);
        }

        return Ok(CohortDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Cancel a cohort
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<CohortDto>> CancelCohort(Guid id)
    {
        var result = await _sender.Send(new CancelCohortCommand(id)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound 
                ? NotFound(result.Error) 
                : BadRequest(result.Error);
        }

        return Ok(CohortDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Delete a cohort
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteCohort(Guid id)
    {
        var result = await _sender.Send(new DeleteCohortCommand(id)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound 
                ? NotFound(result.Error) 
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    private async Task<CohortDto> EnrichAsync(Cohort cohort)
    {
        var results = await EnrichAsync([cohort]).ConfigureAwait(false);
        return results.Single();
    }

    private async Task<IReadOnlyList<CohortDto>> EnrichAsync(IEnumerable<Cohort> source)
    {
        var cohorts = source.ToArray();
        if (cohorts.Length == 0)
        {
            return [];
        }

        var cohortIds = cohorts.Select(cohort => cohort.Id).ToArray();
        var schedules = await _context.Set<CohortSchedule>()
            .AsNoTracking()
            .Where(schedule => cohortIds.Contains(schedule.CohortId))
            .ToArrayAsync()
            .ConfigureAwait(false);
        var scheduleItems = await _context.Set<CohortScheduleItem>()
            .AsNoTracking()
            .Where(item => cohortIds.Contains(item.CohortId))
            .ToArrayAsync()
            .ConfigureAwait(false);
        var instructorIds = cohorts
            .Where(cohort => cohort.InstructorId.HasValue)
            .Select(cohort => cohort.InstructorId!.Value)
            .Distinct()
            .ToArray();
        var instructorCohorts = instructorIds.Length == 0
            ? []
            : await _context.Set<Cohort>()
                .AsNoTracking()
                .Where(cohort => cohort.InstructorId.HasValue && instructorIds.Contains(cohort.InstructorId.Value))
                .ToArrayAsync()
                .ConfigureAwait(false);
        var instructorCohortIds = instructorCohorts.Select(cohort => cohort.Id).ToArray();
        var instructorMeetings = instructorCohortIds.Length == 0
            ? []
            : await _context.Set<CohortScheduleItem>()
                .AsNoTracking()
                .Where(item =>
                    instructorCohortIds.Contains(item.CohortId) &&
                    item.Type == CohortScheduleItemType.LiveSession &&
                    item.Status != CohortScheduleItemStatus.Cancelled &&
                    item.StartsAt.HasValue &&
                    item.EndsAt.HasValue)
                .ToArrayAsync()
                .ConfigureAwait(false);
        var now = SystemClock.UtcNow;

        return cohorts.Select(cohort =>
        {
            var schedule = schedules.SingleOrDefault(candidate => candidate.CohortId == cohort.Id);
            var items = scheduleItems.Where(item => item.CohortId == cohort.Id).ToArray();
            var nextMeetingAt = items
                .Where(item =>
                    item.Type == CohortScheduleItemType.LiveSession &&
                    item.Status != CohortScheduleItemStatus.Cancelled &&
                    item.StartsAt >= now)
                .Select(item => item.StartsAt)
                .Min();
            var conflictCount = cohort.InstructorId.HasValue
                ? CountInstructorConflicts(cohort, items, instructorCohorts, instructorMeetings)
                : 0;
            var summary = schedule is null
                ? null
                : new CohortScheduleSummaryDto(
                    schedule.Version,
                    schedule.TimezoneId,
                    schedule.MeetingDays,
                    schedule.MeetingStartTime,
                    schedule.PacingMode,
                    schedule.ReleasePolicy,
                    items.Length);

            return CohortDto.FromEntity(cohort) with
            {
                NextMeetingAt = nextMeetingAt,
                ConflictCount = conflictCount,
                Schedule = summary
            };
        }).ToArray();
    }

    private static int CountInstructorConflicts(
        Cohort cohort,
        IEnumerable<CohortScheduleItem> ownItems,
        IReadOnlyCollection<Cohort> instructorCohorts,
        IReadOnlyCollection<CohortScheduleItem> instructorMeetings)
    {
        var otherCohortIds = instructorCohorts
            .Where(candidate => candidate.Id != cohort.Id && candidate.InstructorId == cohort.InstructorId)
            .Select(candidate => candidate.Id)
            .ToHashSet();
        if (otherCohortIds.Count == 0)
        {
            return 0;
        }

        var otherMeetings = instructorMeetings
            .Where(item => otherCohortIds.Contains(item.CohortId))
            .ToArray();
        return ownItems.Count(item =>
            item.Type == CohortScheduleItemType.LiveSession &&
            item.Status != CohortScheduleItemStatus.Cancelled &&
            item.StartsAt.HasValue &&
            item.EndsAt.HasValue &&
            otherMeetings.Any(other =>
                item.StartsAt.Value < other.EndsAt!.Value &&
                item.EndsAt.Value > other.StartsAt!.Value));
    }
}

// ===== DTOs =====

public sealed record CohortDto(
    Guid Id,
    Guid CourseId,
    Guid? TenantId,
    string Name,
    string? Description,
    DateTime StartDate,
    DateTime EndDate,
    int MaxCapacity,
    int CurrentEnrollmentCount,
    int AvailableSpots,
    CohortStatus Status,
    bool IsOpen,
    bool CanEnroll,
    Guid? InstructorId,
    string? MeetingSchedule,
    DateTime CreatedAt,
    DateTime? NextMeetingAt = null,
    int ConflictCount = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CohortScheduleSummaryDto? Schedule = null)
{
    public static CohortDto FromEntity(Cohort entity) => new(
        entity.Id,
        entity.CourseId,
        entity.TenantId,
        entity.Name,
        entity.Description,
        entity.StartDate,
        entity.EndDate,
        entity.MaxCapacity,
        entity.CurrentEnrollmentCount,
        entity.MaxCapacity - entity.CurrentEnrollmentCount,
        entity.Status,
        entity.IsOpen,
        entity.CanEnroll(),
        entity.InstructorId,
        entity.MeetingSchedule,
        entity.CreatedAt);
}

public sealed record CohortScheduleSummaryDto(
    int Version,
    string TimezoneId,
    IReadOnlyCollection<DayOfWeek> MeetingDays,
    TimeOnly MeetingStartTime,
    CohortPacingMode PacingMode,
    CohortReleasePolicy ReleasePolicy,
    int ItemCount);

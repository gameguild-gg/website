using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Cohorts;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses/{courseId:guid}/cohorts/{cohortId:guid}/schedule")]
[Authorize]
public sealed class CohortSchedulesController(ISender sender) : BaseApiController
{
    [HttpGet("available-content")]
    [ProducesResponseType<IReadOnlyList<AvailableCohortContentDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AvailableCohortContentDto>> GetAvailableContent(
        Guid courseId,
        Guid cohortId,
        CancellationToken cancellationToken) =>
        sender.Send<IReadOnlyList<AvailableCohortContentDto>>(
            new GetAvailableCohortContentQuery(courseId, cohortId),
            cancellationToken);

    [HttpGet]
    [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "courseId")]
    [ProducesResponseType<CohortScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CohortScheduleDto>> Get(
        Guid courseId,
        Guid cohortId,
        CancellationToken cancellationToken)
    {
        var schedule = await sender.Send(
            new GetCohortScheduleQuery(courseId, cohortId),
            cancellationToken).ConfigureAwait(false);
        return schedule is null ? NotFound() : Ok(schedule);
    }

    [HttpPost("preview")]
    [NoBusinessMutationEndpoint("Schedule preview is a read-only calculation and does not persist cohort state.")]
    [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "courseId")]
    [ProducesResponseType<CohortSchedulePreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CohortSchedulePreviewDto>> Preview(
        Guid courseId,
        Guid cohortId,
        [FromBody] PreviewCohortScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var preview = await sender.Send(
            new PreviewCohortScheduleQuery(courseId, cohortId, request),
            cancellationToken).ConfigureAwait(false);
        return Ok(preview);
    }

    [HttpPut]
    [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "courseId")]
    [ProducesResponseType<CohortScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CohortScheduleDto>> Apply(
        Guid courseId,
        Guid cohortId,
        [FromBody] ApplyCohortScheduleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var schedule = await sender.Send(
                new ApplyCohortScheduleCommand(
                    courseId,
                    cohortId,
                    request.ExpectedVersion,
                    request.Rules,
                    request.ConfirmAdvisories),
                cancellationToken).ConfigureAwait(false);
            return Ok(schedule);
        }
        catch (CohortScheduleVersionConflictException exception)
        {
            return VersionConflict(exception);
        }
    }

    [HttpPatch("items/{itemId:guid}")]
    [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "courseId")]
    [ProducesResponseType<CohortScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CohortScheduleDto>> UpdateItem(
        Guid courseId,
        Guid cohortId,
        Guid itemId,
        [FromBody] UpdateCohortScheduleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var schedule = await sender.Send(
                new UpdateCohortScheduleItemCommand(
                    courseId,
                    cohortId,
                    itemId,
                    request.ExpectedVersion,
                    request.Item),
                cancellationToken).ConfigureAwait(false);
            return Ok(schedule);
        }
        catch (CohortScheduleVersionConflictException exception)
        {
            return VersionConflict(exception);
        }
    }

    [HttpPost("items/{itemId:guid}/shift")]
    [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "courseId")]
    [ProducesResponseType<CohortScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CohortScheduleDto>> ShiftItems(
        Guid courseId,
        Guid cohortId,
        Guid itemId,
        [FromBody] ShiftCohortScheduleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var schedule = await sender.Send(
                new ShiftCohortScheduleItemsCommand(
                    courseId,
                    cohortId,
                    itemId,
                    request.ExpectedVersion,
                    request.Days,
                    request.Scope),
                cancellationToken).ConfigureAwait(false);
            return Ok(schedule);
        }
        catch (CohortScheduleVersionConflictException exception)
        {
            return VersionConflict(exception);
        }
    }

    [HttpGet("~/v{version:apiVersion}/courses/{courseId:guid}/cohorts/calendar")]
    [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "courseId")]
    [ProducesResponseType<CourseCohortCalendarDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CourseCohortCalendarDto>> Calendar(
        Guid courseId,
        [FromQuery] Guid? cohortId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var calendar = await sender.Send(
            new GetCourseCohortCalendarQuery(courseId, cohortId, from, to),
            cancellationToken).ConfigureAwait(false);
        return Ok(calendar);
    }

    private ConflictObjectResult VersionConflict(CohortScheduleVersionConflictException exception)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The class schedule changed",
            Detail = exception.Message,
            Type = "https://httpstatuses.com/409"
        };
        problem.Extensions["expectedVersion"] = exception.ExpectedVersion;
        problem.Extensions["actualVersion"] = exception.ActualVersion;
        return Conflict(problem);
    }
}

public sealed record ApplyCohortScheduleRequest(
    int ExpectedVersion,
    PreviewCohortScheduleRequest Rules,
    bool ConfirmAdvisories = false);

public sealed record UpdateCohortScheduleRequest(
    int ExpectedVersion,
    UpdateCohortScheduleItemRequest Item);

public sealed record ShiftCohortScheduleRequest(
    int ExpectedVersion,
    int Days,
    ScheduleShiftScope Scope);

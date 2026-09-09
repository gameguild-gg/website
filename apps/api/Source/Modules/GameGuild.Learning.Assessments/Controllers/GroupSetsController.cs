using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Controller for course group sets: student self-signup and instructor manual membership.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses/{courseId:guid}/group-sets")]
[Authorize]
public class GroupSetsController : BaseApiController
{
    private readonly IGroupSetService _groupSetService;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly IProgramCrudService _programService;
    private readonly IPermissionQueryService _permissionQueryService;
    private readonly ILogger<GroupSetsController> _logger;
    private readonly ISender _sender;

    public GroupSetsController(
        IGroupSetService groupSetService,
        IActorContextAccessor actorContextAccessor,
        IProgramCrudService programService,
        IPermissionQueryService permissionQueryService,
        ILogger<GroupSetsController> logger,
        ISender sender)
    {
        _groupSetService = groupSetService;
        _actorContextAccessor = actorContextAccessor;
        _programService = programService;
        _permissionQueryService = permissionQueryService;
        _logger = logger;
        _sender = sender;
    }

    /// <summary>
    /// List a course's group sets with per-group summaries. Open to any active course member.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupSetSummaryDto>>> GetGroupSets(Guid courseId)
    {
        if (!await CanAccessCourseMembershipAsync(courseId).ConfigureAwait(false)) return Forbid();

        var sets = await _groupSetService.GetCourseGroupSetsAsync(courseId).ConfigureAwait(false);
        return Ok(sets);
    }

    /// <summary>
    /// Create a group set for a course. Instructor only.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<GroupSetDto>> CreateGroupSet(
        Guid courseId,
        [FromBody] CreateGroupSetRequest request)
    {
        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program == null) return NotFound();
        if (!await CanManageCourseAsync(courseId).ConfigureAwait(false)) return Forbid();

        var result = await _sender.Send(new CreateCourseGroupSetEndpointCommand(courseId, request.Name)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetGroupSets),
            new { courseId },
            GroupSetDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Create a group inside a group set. Instructor only.
    /// </summary>
    [HttpPost("{setId:guid}/groups")]
    public async Task<ActionResult<GroupDto>> CreateGroup(
        Guid courseId,
        Guid setId,
        [FromBody] CreateGroupRequest request)
    {
        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program == null) return NotFound();
        if (!await CanManageCourseAsync(courseId).ConfigureAwait(false)) return Forbid();

        var result = await _sender.Send(new CreateCourseGroupEndpointCommand(courseId, setId, request.Name, request.Capacity))
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetGroupSetGroups),
            new { courseId, setId },
            GroupDto.FromEntity(result.Value));
    }

    /// <summary>
    /// List the groups of one group set with member display names. Open to any active course member.
    /// </summary>
    [HttpGet("{setId:guid}/groups")]
    public async Task<ActionResult<IReadOnlyList<GroupDetailDto>>> GetGroupSetGroups(Guid courseId, Guid setId)
    {
        if (!await CanAccessCourseMembershipAsync(courseId).ConfigureAwait(false)) return Forbid();

        var result = await _groupSetService.GetGroupSetGroupsAsync(courseId, setId).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Student self-signup into a group.
    /// </summary>
    [HttpPost("groups/{groupId:guid}/join")]
    public async Task<ActionResult<GroupMembershipDto>> JoinGroup(Guid courseId, Guid groupId)
    {
        var actorUserId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorUserId.HasValue) return Unauthorized();

        var result = await _sender.Send(new JoinCourseGroupEndpointCommand(courseId, groupId, actorUserId.Value)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return MembershipRejection(result.Error, "Group join rejected");
        }

        return Ok(GroupMembershipDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Student leaves their own membership in a group.
    /// </summary>
    [HttpDelete("groups/{groupId:guid}/membership")]
    public async Task<ActionResult> LeaveGroup(Guid courseId, Guid groupId)
    {
        var actorUserId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorUserId.HasValue) return Unauthorized();

        var result = await _sender.Send(new LeaveCourseGroupEndpointCommand(courseId, groupId, actorUserId.Value)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : MembershipRejection(result.Error, "Group leave rejected");
        }

        return NoContent();
    }

    /// <summary>
    /// Instructor manual add of a user to a group (bypasses the lock-at-due rule, not capacity).
    /// </summary>
    [HttpPost("groups/{groupId:guid}/members/{userId:guid}")]
    public async Task<ActionResult<GroupMembershipDto>> AddMember(Guid courseId, Guid groupId, Guid userId)
    {
        if (!await CanManageCourseAsync(courseId).ConfigureAwait(false)) return Forbid();

        var result = await _sender.Send(new AddCourseGroupMemberEndpointCommand(courseId, groupId, userId)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(GroupMembershipDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Instructor manual remove of a member from a group (bypasses the lock-at-due rule).
    /// </summary>
    [HttpDelete("groups/{groupId:guid}/members/{userId:guid}")]
    public async Task<ActionResult> RemoveMember(Guid courseId, Guid groupId, Guid userId)
    {
        if (!await CanManageCourseAsync(courseId).ConfigureAwait(false)) return Forbid();

        var result = await _sender.Send(new RemoveCourseGroupMemberEndpointCommand(courseId, groupId, userId)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    private ActionResult MembershipRejection(Error error, string title)
    {
        _logger.LogWarning("Group membership rejected: {ErrorCode} {ErrorDescription}", error.Code, error.Description);
        return BadRequest(new ProblemDetails
        {
            Title = title,
            Detail = error.Description
        });
    }

    private async Task<bool> CanAccessCourseMembershipAsync(Guid courseId)
    {
        if (await CanManageCourseAsync(courseId).ConfigureAwait(false)) return true;

        var actor = _actorContextAccessor.ActorContext;
        if (!actor.SubjectIdAsGuid.HasValue) return false;
        if (!await IsActorInProgramTenantAsync(courseId).ConfigureAwait(false)) return false;

        return await _groupSetService.HasActiveEnrollmentAsync(courseId, actor.SubjectIdAsGuid.Value)
            .ConfigureAwait(false);
    }

    private async Task<bool> CanManageCourseAsync(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.IsSystemAdmin) return true;
        if (!actor.SubjectIdAsGuid.HasValue) return false;

        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program == null) return false;
        if (!actor.TenantId.HasValue) return false;
        if (program.TenantId.HasValue && program.TenantId != actor.TenantId) return false;
        if (program.CreatorId == actor.SubjectIdAsGuid.Value) return true;

        foreach (var permission in new[] { PermissionType.Edit, PermissionType.Create, PermissionType.Delete })
        {
            var permissionName = $"{nameof(Program)}.{courseId}.{permission}";
            if (await _permissionQueryService.HasTenantPermissionAsync(
                    actor.SubjectIdAsGuid.Value,
                    actor.TenantId,
                    permissionName).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> IsActorInProgramTenantAsync(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program == null) return false;
        if (actor.IsSystemAdmin) return true;

        return actor.TenantId.HasValue &&
               (!program.TenantId.HasValue || program.TenantId == actor.TenantId);
    }
}

// ===== DTOs =====

public sealed record GroupSetDto(
    Guid Id,
    Guid CourseId,
    string Name)
{
    public static GroupSetDto FromEntity(CourseGroupSet entity) => new(entity.Id, entity.CourseId, entity.Name);
}

public sealed record GroupDto(
    Guid Id,
    Guid GroupSetId,
    string Name,
    int Capacity)
{
    public static GroupDto FromEntity(CourseGroup entity) => new(entity.Id, entity.GroupSetId, entity.Name, entity.Capacity);
}

public sealed record GroupMembershipDto(
    Guid Id,
    Guid GroupId,
    Guid UserId,
    DateTime JoinedAt)
{
    public static GroupMembershipDto FromEntity(CourseGroupMember entity) => new(
        entity.Id,
        entity.GroupId,
        entity.UserId,
        entity.JoinedAt);
}

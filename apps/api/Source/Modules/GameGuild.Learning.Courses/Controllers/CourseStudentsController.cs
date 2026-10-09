using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GameGuild.Learning.Courses;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses/{courseId:guid}/students")]
[Authorize]
public sealed class CourseStudentsController(ISender sender, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpPost("message")]
    [RequireCourseCapability(CourseCapability.Edit, "courseId")]
    public async Task<ActionResult<SendCourseStudentMessageResponse>> SendMessage(
        Guid courseId,
        [FromBody] SendCourseStudentMessageRequest request,
        CancellationToken cancellationToken)
    {
        var sent = await sender.Send<int>(new SendCourseStudentMessageCommand(
                courseId,
                request.UserIds.Distinct().ToArray(),
                request.Subject.Trim(),
                request.Message.Trim(),
                actorContextAccessor.ActorContext.TenantId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(new SendCourseStudentMessageResponse(sent));
    }
}

public sealed record SendCourseStudentMessageRequest(
    [MinLength(1)] IReadOnlyCollection<Guid> UserIds,
    [Required, MinLength(3), MaxLength(160)] string Subject,
    [Required, MinLength(2), MaxLength(4000)] string Message);

public sealed record SendCourseStudentMessageResponse(int Sent);

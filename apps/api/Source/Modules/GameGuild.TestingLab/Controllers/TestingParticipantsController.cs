using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.TestingLab;

public sealed record TestingParticipantMutationProjection(
    Guid Id,
    Guid TestingRequestId,
    Guid UserId,
    ParticipationStatus Status,
    DateTime StartedAt);

/// <summary>
/// Controller for participant management, session registration, and waitlist operations.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/testing")]
[Authorize]
public class TestingParticipantsController(
    ITestingParticipantOperations participantService,
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    #region Participant Management

    // POST: testing/requests/{requestId}/participants/{userId}
    [HttpPost("requests/{requestId}/participants/{userId}")]
    [RequireTestingLabPermission(TestingLabActions.Manage, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult<TestingParticipantMutationProjection>> AddParticipant(Guid requestId, Guid userId)
    {
        return await Execute(async () =>
        {
            var participant = await sender.Send(new AddTestingParticipantEndpointCommand(requestId, userId)).ConfigureAwait(false);
            return new TestingParticipantMutationProjection(
                participant.Id,
                participant.TestingRequestId,
                participant.UserId,
                participant.Status,
                participant.StartedAt);
        }).ConfigureAwait(false);
    }

    // DELETE: testing/requests/{requestId}/participants/{userId}
    [HttpDelete("requests/{requestId}/participants/{userId}")]
    [RequireTestingLabPermission(TestingLabActions.Manage, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult> RemoveParticipant(Guid requestId, Guid userId)
    {
        var result = await sender.Send(new RemoveTestingParticipantEndpointCommand(requestId, userId)).ConfigureAwait(false);
        if (!result) return NotFound();
        return NoContent();
    }

    // GET: testing/requests/{requestId}/participants
    [HttpGet("requests/{requestId}/participants")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult<IEnumerable<TestingParticipant>>> GetTestingRequestParticipants(Guid requestId)
    {
        var participants = await participantService.GetTestingRequestParticipantsAsync(requestId).ConfigureAwait(false);
        return Ok(participants);
    }

    // GET: testing/requests/{requestId}/participants/{userId}/check
    [HttpGet("requests/{requestId}/participants/{userId}/check")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult<bool>> CheckUserParticipation(Guid requestId, Guid userId)
    {
        var isParticipant = await participantService.IsUserParticipantAsync(requestId, userId).ConfigureAwait(false);
        return Ok(isParticipant);
    }

    #endregion

    #region Session Registration

    // POST: testing/sessions/{sessionId}/register
    [HttpPost("sessions/{sessionId}/register")]
    public async Task<ActionResult<SessionRegistration>> RegisterForSession(Guid sessionId, [FromBody] SessionRegistrationRequest request)
    {
        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (userId == null)
            return Unauthorized("User ID not found in token");

        return await Execute(() => sender.Send(new RegisterTestingSessionEndpointCommand(
            sessionId, userId.Value, request.RegistrationType, request.Notes))).ConfigureAwait(false);
    }

    // DELETE: testing/sessions/{sessionId}/register
    [HttpDelete("sessions/{sessionId}/register")]
    public async Task<ActionResult> UnregisterFromSession(Guid sessionId)
    {
        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (userId == null)
            return Unauthorized("User ID not found in token");

        try
        {
            var result = await sender.Send(new UnregisterTestingSessionEndpointCommand(sessionId, userId.Value)).ConfigureAwait(false);
            return result ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // GET: testing/sessions/{sessionId}/registrations
    [HttpGet("sessions/{sessionId}/registrations")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult<IEnumerable<SessionRegistration>>> GetSessionRegistrations(Guid sessionId)
    {
        var registrations = await participantService.GetSessionRegistrationsAsync(sessionId).ConfigureAwait(false);
        return Ok(registrations);
    }

    #endregion

    #region Waitlist

    // POST: testing/sessions/{sessionId}/waitlist
    [HttpPost("sessions/{sessionId}/waitlist")]
    public async Task<ActionResult<SessionWaitlist>> AddToWaitlist(Guid sessionId, [FromBody] SessionRegistrationRequest request)
    {
        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (userId == null)
            return Unauthorized("User ID not found in token");

        return await Execute(() => sender.Send(new AddTestingSessionWaitlistEndpointCommand(
            sessionId, userId.Value, request.RegistrationType, request.Notes))).ConfigureAwait(false);
    }

    // DELETE: testing/sessions/{sessionId}/waitlist
    [HttpDelete("sessions/{sessionId}/waitlist")]
    public async Task<ActionResult> RemoveFromWaitlist(Guid sessionId)
    {
        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (userId == null)
            return Unauthorized("User ID not found in token");

        try
        {
            var result = await sender.Send(new RemoveTestingSessionWaitlistEndpointCommand(sessionId, userId.Value)).ConfigureAwait(false);
            return result ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // GET: testing/sessions/{sessionId}/waitlist
    [HttpGet("sessions/{sessionId}/waitlist")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult<IEnumerable<SessionWaitlist>>> GetSessionWaitlist(Guid sessionId)
    {
        var waitlist = await participantService.GetSessionWaitlistAsync(sessionId).ConfigureAwait(false);
        return Ok(waitlist);
    }

    #endregion

    #region User Activity & Attendance

    // GET: testing/users/{userId}/activity
    [HttpGet("users/{userId}/activity")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult<object>> GetUserTestingActivity(Guid userId)
    {
        var activity = await participantService.GetUserTestingActivityAsync(userId).ConfigureAwait(false);
        return Ok(activity);
    }

    // GET: testing/attendance/students
    [HttpGet("attendance/students")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult<object>> GetStudentAttendanceReport()
    {
        var report = await participantService.GetStudentAttendanceReportAsync().ConfigureAwait(false);
        return Ok(report);
    }

    #endregion

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> operation)
    {
        try { return Ok(await operation().ConfigureAwait(false)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "TestingLab.ParticipationConflict",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
    }
}

using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameGuild.TestingLab;

/// <summary>
/// Controller for testing session CRUD operations, queries, search, and attendance.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/testing")]
[Authorize]
public class TestingSessionsController(
    ITestingSessionOperations sessionService,
    IMediator mediator,
    IActorContextAccessor actorContextAccessor,
    ILogger<TestingSessionsController> _logger) : BaseApiController
{
    // GET: testing/sessions
    [HttpGet("sessions")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Session)]
    public async Task<ActionResult<IEnumerable<TestingSession>>> GetTestingSessions([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var sessions = await sessionService.GetTestingSessionsAsync(skip, take).ConfigureAwait(false);
        return Ok(sessions);
    }

    // GET: testing/sessions/{id}
    [HttpGet("sessions/{id}")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Session, "id")]
    public async Task<ActionResult<TestingSession>> GetTestingSession(Guid id)
    {
        var session = await sessionService.GetTestingSessionByIdAsync(id).ConfigureAwait(false);
        if (session == null) return NotFound();
        return Ok(session);
    }

    // GET: testing/sessions/{id}/details
    [HttpGet("sessions/{id}/details")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Session, "id")]
    public async Task<ActionResult<TestingSession>> GetTestingSessionWithDetails(Guid id)
    {
        var session = await sessionService.GetTestingSessionByIdWithDetailsAsync(id).ConfigureAwait(false);
        if (session == null) return NotFound();
        return Ok(session);
    }

    // POST: testing/sessions
    [HttpPost("sessions")]
    [RequireTestingLabPermission(TestingLabActions.Create, TestingLabResourceTypes.Session)]
    public async Task<ActionResult<TestingSession>> CreateTestingSession(CreateTestingSessionDto sessionDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (userId == null)
            return Unauthorized("User ID not found in token");

        var createdSession = await mediator.Send(new CreateTestingSessionEndpointCommand(sessionDto, userId.Value)).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetTestingSession), new { id = createdSession.Id }, createdSession);
    }

    // PUT: testing/sessions/{id}
    [HttpPut("sessions/{id}")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Session, "id")]
    public async Task<ActionResult<TestingSession>> UpdateTestingSession(Guid id, TestingSession session)
    {
        if (id != session.Id) return BadRequest("ID mismatch");

        try
        {
            var updatedSession = await mediator.Send(new UpdateTestingSessionEndpointCommand(id, session)).ConfigureAwait(false);
            return Ok(updatedSession);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Testing session {SessionId} not found or could not be updated", id);
            return NotFound("The requested testing session was not found or could not be updated.");
        }
    }

    // DELETE: testing/sessions/{id}
    [HttpDelete("sessions/{id}")]
    [RequireTestingLabPermission(TestingLabActions.Delete, TestingLabResourceTypes.Session, "id")]
    public async Task<ActionResult> DeleteTestingSession(Guid id)
    {
        var result = await mediator.Send(new DeleteTestingSessionEndpointCommand(id)).ConfigureAwait(false);
        if (!result) return NotFound();
        return NoContent();
    }

    // POST: testing/sessions/{id}:restore
    [HttpPost("sessions/{id}:restore")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Session, "id")]
    public async Task<ActionResult> RestoreTestingSession(Guid id)
    {
        var result = await mediator.Send(new RestoreTestingSessionEndpointCommand(id)).ConfigureAwait(false);
        if (!result) return NotFound();
        return Ok();
    }

    // GET: testing/public/sessions
    /// <summary>
    /// Public endpoint returning "published" testing sessions (Scheduled or Active). No authentication required.
    /// </summary>
    [HttpGet("public/sessions")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TestingSession>>> GetPublicTestingSessions([FromQuery] int take = 100)
    {
        var sessions = await sessionService.GetPublicTestingSessionsAsync(take).ConfigureAwait(false);
        return Ok(sessions);
    }

    // GET: testing/sessions/by-request/{testingRequestId}
    [HttpGet("sessions/by-request/{testingRequestId}")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Session)]
    public async Task<ActionResult<IEnumerable<TestingSession>>> GetTestingSessionsByRequest(Guid testingRequestId)
    {
        var sessions = await sessionService.GetTestingSessionsByRequestAsync(testingRequestId).ConfigureAwait(false);
        return Ok(sessions);
    }

    // GET: testing/sessions/by-location/{locationId}
    [HttpGet("sessions/by-location/{locationId}")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Session)]
    public async Task<ActionResult<IEnumerable<TestingSession>>> GetTestingSessionsByLocation(Guid locationId)
    {
        var sessions = await sessionService.GetTestingSessionsByLocationAsync(locationId).ConfigureAwait(false);
        return Ok(sessions);
    }

    // GET: testing/sessions/by-status/{status}
    [HttpGet("sessions/by-status/{status}")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Session)]
    public async Task<ActionResult<IEnumerable<TestingSession>>> GetTestingSessionsByStatus(SessionStatus status)
    {
        var sessions = await sessionService.GetTestingSessionsByStatusAsync(status).ConfigureAwait(false);
        return Ok(sessions);
    }

    // GET: testing/sessions/by-manager/{managerId}
    [HttpGet("sessions/by-manager/{managerId}")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Session)]
    public async Task<ActionResult<IEnumerable<TestingSession>>> GetTestingSessionsByManager(Guid managerId)
    {
        var sessions = await sessionService.GetTestingSessionsByManagerAsync(managerId).ConfigureAwait(false);
        return Ok(sessions);
    }

    // GET: testing/sessions/search
    [HttpGet("sessions/search")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Session)]
    public async Task<ActionResult<IEnumerable<TestingSession>>> SearchTestingSessions([FromQuery] string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return BadRequest("Search term is required");

        var sessions = await sessionService.SearchTestingSessionsAsync(searchTerm).ConfigureAwait(false);
        return Ok(sessions);
    }

    // GET: testing/sessions/{sessionId}/statistics
    [HttpGet("sessions/{sessionId}/statistics")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Session, "sessionId")]
    public async Task<ActionResult<object>> GetTestingSessionStatistics(Guid sessionId)
    {
        var statistics = await sessionService.GetTestingSessionStatisticsAsync(sessionId).ConfigureAwait(false);
        return Ok(statistics);
    }

    // GET: testing/attendance/sessions
    [HttpGet("attendance/sessions")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult<object>> GetSessionAttendanceReport()
    {
        var report = await sessionService.GetSessionAttendanceReportAsync().ConfigureAwait(false);
        return Ok(report);
    }

    // POST: testing/sessions/{id}/attendance
    [HttpPost("sessions/{sessionId}/attendance")]
    [RequireTestingLabPermission(TestingLabActions.Manage, TestingLabResourceTypes.Participant)]
    public async Task<ActionResult> UpdateAttendance(Guid sessionId, UpdateAttendanceDto attendanceDto)
    {
        var currentUserId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (currentUserId == null)
            return Unauthorized("User ID not found in token");

        await mediator.Send(new UpdateTestingSessionAttendanceEndpointCommand(
            sessionId,
            attendanceDto.UserId,
            attendanceDto.AttendanceStatus,
            currentUserId.Value)).ConfigureAwait(false);

        return Ok(new { message = "Attendance updated successfully" });
    }

    [HttpGet("sessions/{sessionId:guid}/projects")]
    public async Task<ActionResult<IReadOnlyList<SessionProjectProjection>>> GetSessionProjects(
        Guid sessionId,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetSessionProjectLinksQuery(sessionId, includeInactive), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ToChannelActionResult(result);
    }

    [HttpPost("sessions/{sessionId:guid}/projects")]
    public async Task<ActionResult<SessionProjectProjection>> LinkSessionProject(
        Guid sessionId,
        LinkSessionProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new LinkSessionProjectCommand(sessionId, request.ProjectId, request.ProjectVersionId, request.Notes),
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return ToChannelActionResult(result);
        return CreatedAtAction(nameof(GetSessionProjects), new { sessionId }, result.Value);
    }

    [HttpDelete("sessions/{sessionId:guid}/projects/{projectId:guid}")]
    public async Task<ActionResult> UnlinkSessionProject(
        Guid sessionId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new UnlinkSessionProjectCommand(sessionId, projectId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : ToChannelActionResult(result);
    }

    private ObjectResult ToChannelActionResult(Result result)
        => result.Error.Type switch
        {
            ErrorType.Unauthorized => StatusCode(401, result.Error),
            ErrorType.Forbidden => StatusCode(403, result.Error),
            ErrorType.NotFound => NotFound(result.Error),
            ErrorType.Conflict => Conflict(result.Error),
            ErrorType.Validation => BadRequest(result.Error),
            _ => StatusCode(500, result.Error)
        };
}

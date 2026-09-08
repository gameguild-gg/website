using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameGuild.TestingLab;

/// <summary>
/// Controller for testing request CRUD operations, queries, and search.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/testing")]
[Authorize]
public class TestingRequestsController(
    ITestingRequestOperations requestService,
    IActorContextAccessor actorContextAccessor,
    ILogger<TestingRequestsController> _logger,
    IMediator mediator) : BaseApiController
{
    // GET: testing/requests
    [HttpGet("requests")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request)]
    public async Task<ActionResult<IEnumerable<TestingRequestDetailProjection>>> GetTestingRequests(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] bool includeArchived = false)
    {
        var requests = await requestService.GetTestingRequestsAsync(skip, take, includeArchived).ConfigureAwait(false);
        return Ok(requests.Select(TestingRequestDetailProjection.FromEntity));
    }

    // GET: testing/requests/{id}
    [HttpGet("requests/{id}")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request, "id")]
    public async Task<ActionResult<TestingRequestDetailProjection>> GetTestingRequest(
        Guid id,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new GetTestingRequestDetailQuery(id), cancellationToken).ConfigureAwait(false));

    // GET: testing/requests/{id}/details
    [HttpGet("requests/{id}/details")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request, "id")]
    public async Task<ActionResult<TestingRequest>> GetTestingRequestWithDetails(Guid id)
    {
        var request = await requestService.GetTestingRequestByIdWithDetailsAsync(id).ConfigureAwait(false);
        if (request == null) return NotFound();
        return Ok(request);
    }

    // POST: testing/requests
    [HttpPost("requests")]
    [RequireTestingLabPermission(TestingLabActions.Create, TestingLabResourceTypes.Request)]
    public async Task<ActionResult<TestingRequest>> CreateTestingRequest(CreateTestingRequestDto requestDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (userId == null)
            return Unauthorized("User ID not found in token");

        try
        {
            var createdRequest = await mediator.Send(
                new CreateTestingRequestEndpointCommand(requestDto, userId.Value)).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetTestingRequest), new { id = createdRequest.Id }, createdRequest);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Testing request project not found for user {UserId}", userId);
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Testing request creation forbidden for user {UserId}", userId);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Testing request creation rejected for user {UserId}", userId);
            return BadRequest(ex.Message);
        }
    }

    // PUT: testing/requests/{id}
    [HttpPut("requests/{id}")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Request, "id")]
    public async Task<ActionResult<TestingRequestDetailProjection>> UpdateTestingRequest(
        Guid id,
        UpdateTestingRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updatedRequest = await mediator.Send(
                new UpdateTestingRequestEndpointCommand(id, requestDto),
                cancellationToken).ConfigureAwait(false);
            if (updatedRequest is null) return NotFound("The requested testing request was not found.");
            return ToActionResult(await mediator.Send(
                new GetTestingRequestDetailQuery(id),
                cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Testing request {RequestId} could not be updated", id);
            return BadRequest("The requested testing request could not be updated.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Testing request project not found while updating request {RequestId}", id);
            return NotFound();
        }
    }

    // DELETE: testing/requests/{id}
    [HttpDelete("requests/{id}")]
    [RequireTestingLabPermission(TestingLabActions.Delete, TestingLabResourceTypes.Request, "id")]
    public async Task<ActionResult> DeleteTestingRequest(Guid id)
    {
        var result = await mediator.Send(new DeleteTestingRequestEndpointCommand(id)).ConfigureAwait(false);
        if (!result) return NotFound();
        return NoContent();
    }

    // POST: testing/requests/{id}:restore
    [HttpPost("requests/{id}:restore")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Request, "id")]
    public async Task<ActionResult> RestoreTestingRequest(Guid id)
    {
        try
        {
            var result = await mediator.Send(new RestoreTestingRequestEndpointCommand(id)).ConfigureAwait(false);
            if (!result) return NotFound();
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Testing request {RequestId} cannot be restored because its project is unavailable", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Testing request {RequestId} could not be restored", id);
            return BadRequest("The requested testing request could not be restored.");
        }
    }

    // GET: testing/requests/by-project-version/{projectVersionId}
    [HttpGet("requests/by-project-version/{projectVersionId}")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request)]
    public async Task<ActionResult<IEnumerable<TestingRequest>>> GetTestingRequestsByProjectVersion(Guid projectVersionId)
    {
        var requests = await requestService.GetTestingRequestsByProjectVersionAsync(projectVersionId).ConfigureAwait(false);
        return Ok(requests);
    }

    // GET: testing/requests/by-creator/{creatorId}
    [HttpGet("requests/by-creator/{creatorId}")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request)]
    public async Task<ActionResult<IEnumerable<TestingRequest>>> GetTestingRequestsByCreator(Guid creatorId)
    {
        var requests = await requestService.GetTestingRequestsByCreatorAsync(creatorId).ConfigureAwait(false);
        return Ok(requests);
    }

    // GET: testing/requests/by-status/{status}
    [HttpGet("requests/by-status/{status}")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request)]
    public async Task<ActionResult<IEnumerable<TestingRequest>>> GetTestingRequestsByStatus(TestingRequestStatus status)
    {
        var requests = await requestService.GetTestingRequestsByStatusAsync(status).ConfigureAwait(false);
        return Ok(requests);
    }

    // GET: testing/requests/search
    [HttpGet("requests/search")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request)]
    public async Task<ActionResult<IEnumerable<TestingRequest>>> SearchTestingRequests([FromQuery] string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return BadRequest("Search term is required");

        var requests = await requestService.SearchTestingRequestsAsync(searchTerm).ConfigureAwait(false);
        return Ok(requests);
    }

    // POST: testing/submit-simple
    [HttpPost("submit-simple")]
    public async Task<ActionResult<TestingRequestDetailProjection>> SubmitSimpleTestingRequest(
        CreateSimpleTestingRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (userId == null)
            return Unauthorized("User ID not found in token");

        TestingRequest request;
        try
        {
            request = await mediator.Send(
                new CreateSimpleTestingRequestEndpointCommand(requestDto, userId.Value),
                cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Testing Lab submission forbidden for user {UserId}", userId);
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Testing Lab submission project not found for user {UserId}", userId);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Testing Lab submission rejected for user {UserId}", userId);
            return BadRequest(ex.Message);
        }

        var detail = await mediator.Send(
            new GetTestingRequestDetailQuery(request.Id),
            cancellationToken).ConfigureAwait(false);

        return detail.IsSuccess
            ? CreatedAtAction(nameof(GetTestingRequest), new { id = request.Id }, detail.Value)
            : ToActionResult(detail);
    }

    // GET: testing/my-requests
    [HttpGet("my-requests")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request)]
    public async Task<ActionResult<IEnumerable<TestingRequest>>> GetMyTestingRequests()
    {
        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (userId == null)
            return Unauthorized("User ID not found in token");

        var requests = await requestService.GetTestingRequestsByCreatorAsync(userId.Value).ConfigureAwait(false);
        return Ok(requests);
    }

    // GET: testing/available-for-testing
    [HttpGet("available-for-testing")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request)]
    public async Task<ActionResult<IEnumerable<TestingRequest>>> GetAvailableTestingRequests()
    {
        var requests = await requestService.GetActiveTestingRequestsAsync().ConfigureAwait(false);
        return Ok(requests);
    }

    // GET: testing/requests/{requestId}/statistics
    [HttpGet("requests/{requestId}/statistics")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Request, "requestId")]
    public async Task<ActionResult<object>> GetTestingRequestStatistics(Guid requestId, [FromServices] ITestingFeedbackOperations feedbackService)
    {
        var statistics = await feedbackService.GetTestingRequestStatisticsAsync(requestId).ConfigureAwait(false);
        return Ok(statistics);
    }
}

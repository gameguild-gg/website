using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.TestingLab;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/testing/events")]
[Authorize]
public sealed class TestingEventsController(IMediator mediator) : BaseApiController
{
    [HttpGet]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Event)]
    public async Task<ActionResult<IReadOnlyList<TestingEventProjection>>> GetEvents(
        [FromQuery] TestingEventStatus? status = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new GetTestingEventsQuery(status, skip, take), cancellationToken).ConfigureAwait(false));

    [HttpGet("archived")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Event)]
    public async Task<ActionResult<IReadOnlyList<TestingEventProjection>>> GetArchivedEvents(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new GetArchivedTestingEventsQuery(skip, take), cancellationToken).ConfigureAwait(false));

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PublicTestingEventProjection>>> GetPublicEvents(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new GetPublicTestingEventsQuery(skip, take),
            cancellationToken).ConfigureAwait(false));

    [HttpGet("public/{eventId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicTestingEventProjection>> GetPublicEvent(
        Guid eventId,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new GetPublicTestingEventQuery(eventId),
            cancellationToken).ConfigureAwait(false));

    [HttpGet("{eventId:guid}")]
    public async Task<ActionResult<TestingEventProjection>> GetEvent(Guid eventId, CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new GetTestingEventQuery(eventId), cancellationToken).ConfigureAwait(false));

    [HttpPost]
    [RequireTestingLabPermission(TestingLabActions.Create, TestingLabResourceTypes.Event)]
    public async Task<ActionResult<TestingEventProjection>> CreateEvent(
        CreateTestingEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TemplateRevisionId != null && request.Configuration != null)
            return UnprocessableEntity(new
            {
                code = "TestingLab.EventConfigurationSourceConflict",
                message = "Choose either a template or a custom event configuration."
            });
        var result = await mediator.Send(new CreateTestingEventCommand(
            request.Name,
            request.Description,
            request.Mode,
            request.ApprovalMode,
            request.ApplicationsOpenAt,
            request.ApplicationsCloseAt,
            request.StartsAt,
            request.EndsAt,
            request.RequiresFeedback,
            request.Recurrence,
            request.TemplateRevisionId,
            request.Configuration,
            request.TimeZoneId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetEvent), new { eventId = result.Value.Id }, result.Value)
            : ToActionResult(result);
    }

    [HttpPut("{eventId:guid}/configuration")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventProjection>> ConfigureEvent(
        Guid eventId,
        ConfigureTestingEventRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new ConfigureTestingEventCommand(
            eventId,
            request.GeneralRules,
            request.CandidateInstructions,
            request.TesterInstructions,
            request.ProjectApplicationSchema,
            request.TesterRegistrationSchema), cancellationToken).ConfigureAwait(false));

    [HttpPut("{eventId:guid}")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventProjection>> UpdateEvent(
        Guid eventId,
        UpdateTestingEventRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new UpdateTestingEventCommand(
            eventId,
            request.Name,
            request.Description,
            request.Mode,
            request.ApprovalMode,
            request.ApplicationsOpenAt,
            request.ApplicationsCloseAt,
            request.StartsAt,
            request.EndsAt,
            request.RequiresFeedback,
            request.TimeZoneId), cancellationToken).ConfigureAwait(false));

    [HttpDelete("{eventId:guid}")]
    [RequireTestingLabPermission(TestingLabActions.Delete, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<bool>> DeleteEvent(Guid eventId, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new DeleteTestingEventCommand(eventId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("{eventId:guid}:archive")]
    [RequireTestingLabPermission(TestingLabActions.Delete, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<bool>> ArchiveEvent(Guid eventId, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ArchiveTestingEventCommand(eventId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("{eventId:guid}:restore")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<bool>> RestoreEvent(Guid eventId, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new RestoreTestingEventCommand(eventId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("{eventId:guid}:open-applications")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventProjection>> OpenApplications(Guid eventId, CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new OpenTestingEventApplicationsCommand(eventId), cancellationToken).ConfigureAwait(false));

    [HttpPost("{eventId:guid}:close-applications")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventProjection>> CloseApplications(Guid eventId, CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new CloseTestingEventApplicationsCommand(eventId), cancellationToken).ConfigureAwait(false));

    [HttpPost("{eventId:guid}:schedule")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventProjection>> Schedule(Guid eventId, CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new ScheduleTestingEventCommand(eventId), cancellationToken).ConfigureAwait(false));

    [HttpPost("{eventId:guid}:activate")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventProjection>> Activate(Guid eventId, CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new ActivateTestingEventCommand(eventId), cancellationToken).ConfigureAwait(false));

    [HttpPost("{eventId:guid}:complete")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventProjection>> Complete(Guid eventId, CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new CompleteTestingEventCommand(eventId), cancellationToken).ConfigureAwait(false));

    [HttpPost("{eventId:guid}:cancel")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventProjection>> Cancel(
        Guid eventId,
        CancelTestingEventRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new CancelTestingEventCommand(eventId, request.Reason),
            cancellationToken).ConfigureAwait(false));

    [HttpPut("{eventId:guid}/learning")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventProjection>> ConfigureLearning(
        Guid eventId,
        ConfigureTestingEventLearningRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new ConfigureTestingEventLearningCommand(
            eventId,
            request.CourseId,
            request.CohortId,
            request.LearningActivityId,
            request.Requirement),
            cancellationToken).ConfigureAwait(false));

    [HttpGet("{eventId:guid}/slots")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<IReadOnlyList<TestingEventSlotProjection>>> GetSlots(
        Guid eventId,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new GetTestingEventSlotsQuery(eventId), cancellationToken).ConfigureAwait(false));

    [HttpPost("{eventId:guid}/slots")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventSlotProjection>> CreateSlot(
        Guid eventId,
        UpsertTestingEventSlotRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new CreateTestingEventSlotCommand(
            eventId,
            request.Mode,
            request.StartsAt,
            request.EndsAt,
            request.MaxTesters,
            request.MaxProjects,
            request.CampusName,
            request.RoomName,
            request.MeetingUrl,
            request.LocationId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetSlots), new { eventId }, result.Value)
            : ToActionResult(result);
    }

    [HttpPut("{eventId:guid}/slots/{slotId:guid}")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventSlotProjection>> UpdateSlot(
        Guid eventId,
        Guid slotId,
        UpsertTestingEventSlotRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new UpdateTestingEventSlotCommand(
            eventId,
            slotId,
            request.Mode,
            request.StartsAt,
            request.EndsAt,
            request.MaxTesters,
            request.MaxProjects,
            request.CampusName,
            request.RoomName,
            request.MeetingUrl,
            request.LocationId), cancellationToken).ConfigureAwait(false));

    [HttpDelete("{eventId:guid}/slots/{slotId:guid}")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<bool>> DeleteSlot(Guid eventId, Guid slotId, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new DeleteTestingEventSlotCommand(eventId, slotId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpGet("{eventId:guid}/committee")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<IReadOnlyList<TestingEventCommitteeMemberProjection>>> GetCommittee(
        Guid eventId,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new GetTestingEventCommitteeQuery(eventId),
            cancellationToken).ConfigureAwait(false));

    [HttpPost("{eventId:guid}/committee")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<TestingEventCommitteeMemberProjection>> AddCommitteeMember(
        Guid eventId,
        AddTestingEventCommitteeMemberRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new AddTestingEventCommitteeMemberCommand(eventId, request.UserId, request.IsChair),
            cancellationToken).ConfigureAwait(false));

    [HttpDelete("{eventId:guid}/committee/{userId:guid}")]
    [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<bool>> RemoveCommitteeMember(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new RemoveTestingEventCommitteeMemberCommand(eventId, userId),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpPost("{eventId:guid}/applications")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> SubmitApplication(
        Guid eventId,
        SubmitTestingProjectApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new SubmitTestingProjectApplicationCommand(
            eventId,
            request.ProjectId,
            request.ProjectVersionId,
            request.PreferredAvailability,
            request.SubmittedAssetReferenceIds,
            request.Brief,
            request.FeedbackQuestionnaire,
            request.EventApplicationResponse,
            request.AcceptedRules), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetApplication), new { applicationId = result.Value.Id }, result.Value)
            : ToActionResult(result);
    }

    [HttpPost("{eventId:guid}/applications/drafts")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> CreateApplicationDraft(
        Guid eventId,
        CreateTestingProjectApplicationDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new CreateTestingProjectApplicationDraftCommand(eventId, request.ProjectId),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetApplication), new { applicationId = result.Value.Id }, result.Value)
            : ToActionResult(result);
    }

    [HttpPut("applications/{applicationId:guid}/draft")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> SaveApplicationDraft(
        Guid applicationId,
        SaveTestingProjectApplicationDraftRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new SaveTestingProjectApplicationDraftCommand(
            applicationId,
            request.ProjectVersionId,
            request.Brief,
            request.FeedbackQuestionnaire,
            request.EventApplicationResponse,
            request.AcceptedRules,
            request.PreferredAvailability,
            request.SubmittedAssetReferenceIds), cancellationToken).ConfigureAwait(false));

    [HttpPost("applications/{applicationId:guid}:submit")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> SubmitApplicationDraft(
        Guid applicationId,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new SubmitTestingProjectApplicationDraftCommand(applicationId),
            cancellationToken).ConfigureAwait(false));

    [HttpGet("{eventId:guid}/applications")]
    public async Task<ActionResult<IReadOnlyList<TestingProjectApplicationProjection>>> GetApplications(
        Guid eventId,
        [FromQuery] TestingApplicationStatus? status = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new GetTestingEventApplicationsQuery(eventId, status, skip, take), cancellationToken).ConfigureAwait(false));

    [HttpGet("{eventId:guid}/applications/access")]
    public async Task<ActionResult<TestingEventApplicationAccessProjection>> GetApplicationAccess(
        Guid eventId,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new GetTestingEventApplicationAccessQuery(eventId),
            cancellationToken).ConfigureAwait(false));

    [HttpGet("{eventId:guid}/applications/tester-eligibility")]
    [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Event, "eventId")]
    public async Task<ActionResult<IReadOnlyList<TestingApplicationTesterEligibilityProjection>>> GetTesterEligibility(
        Guid eventId,
        [FromQuery] Guid[] testerUserIds,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new GetTestingApplicationTesterEligibilityQuery(eventId, testerUserIds),
            cancellationToken).ConfigureAwait(false));

    [HttpGet("applications/me")]
    public async Task<ActionResult<IReadOnlyList<TestingProjectApplicationProjection>>> GetMyApplications(
        [FromQuery] Guid? eventId = null,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new GetMyTestingProjectApplicationsQuery(eventId),
            cancellationToken).ConfigureAwait(false));
    [HttpGet("applications/{applicationId:guid}")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> GetApplication(
        Guid applicationId,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new GetTestingProjectApplicationQuery(applicationId), cancellationToken).ConfigureAwait(false));

    [HttpPut("applications/{applicationId:guid}")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> UpdateApplication(
        Guid applicationId,
        UpdateTestingProjectApplicationRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new UpdateTestingProjectApplicationCommand(
                applicationId,
                request.ProjectVersionId,
                request.PreferredAvailability,
                request.SubmittedAssetReferenceIds),
            cancellationToken).ConfigureAwait(false));

    [HttpGet("applications/{applicationId:guid}/review-package")]
    public async Task<ActionResult<TestingApplicationReviewPackageProjection>> GetApplicationReviewPackage(
        Guid applicationId,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(
            new GetTestingApplicationReviewPackageQuery(applicationId),
            cancellationToken).ConfigureAwait(false));

    [HttpPost("applications/{applicationId:guid}:withdraw")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> WithdrawApplication(
        Guid applicationId,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new WithdrawTestingProjectApplicationCommand(applicationId), cancellationToken).ConfigureAwait(false));

    [HttpPost("applications/{applicationId:guid}:review")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> BeginReview(
        Guid applicationId,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new BeginReviewTestingProjectApplicationCommand(applicationId), cancellationToken).ConfigureAwait(false));

    [HttpPost("applications/{applicationId:guid}/votes")]
    public async Task<ActionResult<TestingApplicationVoteProjection>> CastVote(
        Guid applicationId,
        CastTestingApplicationVoteRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new CastTestingApplicationVoteCommand(
            applicationId,
            request.Decision,
            request.Comments), cancellationToken).ConfigureAwait(false));

    [HttpPost("applications/{applicationId:guid}:approve")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> ApproveApplication(
        Guid applicationId,
        DecideTestingProjectApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.SlotId.HasValue)
            return BadRequest(Error.Validation("TestingLab.SlotRequired", "A slot is required to approve an application."));
        return ToActionResult(await mediator.Send(new ApproveTestingProjectApplicationCommand(
            applicationId,
            request.SlotId.Value,
            request.Rationale), cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("applications/{applicationId:guid}:reject")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> RejectApplication(
        Guid applicationId,
        DecideTestingProjectApplicationRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new RejectTestingProjectApplicationCommand(
            applicationId,
            request.Rationale ?? string.Empty), cancellationToken).ConfigureAwait(false));

    [HttpPost("applications/{applicationId:guid}:waitlist")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> WaitlistApplication(
        Guid applicationId,
        DecideTestingProjectApplicationRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new WaitlistTestingProjectApplicationCommand(
            applicationId,
            request.Rationale), cancellationToken).ConfigureAwait(false));

    [HttpPut("applications/{applicationId:guid}/slot")]
    public async Task<ActionResult<TestingProjectApplicationProjection>> AssignSlot(
        Guid applicationId,
        AssignTestingProjectApplicationSlotRequest request,
        CancellationToken cancellationToken = default)
        => ToActionResult(await mediator.Send(new AssignTestingProjectApplicationSlotCommand(
            applicationId,
            request.SlotId), cancellationToken).ConfigureAwait(false));

}

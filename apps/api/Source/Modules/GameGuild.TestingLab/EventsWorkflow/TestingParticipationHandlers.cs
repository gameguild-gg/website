using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Projects;
using GameGuild.Teams;
using Microsoft.Extensions.Logging;

namespace GameGuild.TestingLab;

public sealed class TestingParticipationHandlers(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    ILogger<TestingParticipationHandlers> logger,
    IProjectLifecycleLock? capacityLock = null,
    IPublisher? publisher = null,
    ITestingLabPermissionService? testingLabPermissionService = null,
    IProjectAuthorizationService? projectAuthorizationService = null) :
    ICommandHandler<RegisterTestingEventSlotCommand, Result<TestingSlotRegistrationProjection>>,
    ICommandHandler<CancelTestingEventSlotRegistrationCommand, Result<TestingSlotRegistrationProjection>>,
    ICommandHandler<CheckInTestingEventRegistrationCommand, Result<TestingSlotRegistrationProjection>>,
    ICommandHandler<CheckOutTestingEventRegistrationCommand, Result<TestingSlotRegistrationProjection>>,
    ICommandHandler<MarkTestingEventNoShowCommand, Result<TestingSlotRegistrationProjection>>,
    ICommandHandler<AssignTestingProjectToTesterCommand, Result<TestingFeedbackObligationProjection>>,
    ICommandHandler<SubmitTestingEventFeedbackCommand, Result<TestingEventFeedbackProjection>>,
    ICommandHandler<CompleteTestingEventParticipationCommand, Result<TestingSlotRegistrationProjection>>,
    IQueryHandler<GetMyTestingSlotRegistrationsQuery, Result<IReadOnlyList<TestingSlotRegistrationProjection>>>,
    IQueryHandler<GetTestingEventSlotRegistrationsQuery, Result<IReadOnlyList<TestingSlotRegistrationProjection>>>,
    IQueryHandler<GetTestingParticipantDirectoryQuery, Result<TestingParticipantDirectoryProjection>>,
    IQueryHandler<GetMyTestingFeedbackObligationsQuery, Result<IReadOnlyList<TestingFeedbackObligationProjection>>>,
    IQueryHandler<GetMyTestingEventFeedbackQuery, Result<IReadOnlyList<TestingEventFeedbackProjection>>>,
    IQueryHandler<GetTestingEventFeedbackQuery, Result<IReadOnlyList<TestingEventFeedbackReviewProjection>>>
{
    private bool IsTenantAdmin => actorContextAccessor.ActorContext.IsTenantAdmin;

    private static readonly TestingSlotRegistrationStatus[] CapacityStatuses =
    [
        TestingSlotRegistrationStatus.Registered,
        TestingSlotRegistrationStatus.CheckedIn,
        TestingSlotRegistrationStatus.Attended,
        TestingSlotRegistrationStatus.Completed,
        TestingSlotRegistrationStatus.NoShow,
    ];

    private readonly IProjectLifecycleLock _capacityLock = capacityLock ?? new ProjectLifecycleLock(context);

    public async Task<Result<TestingSlotRegistrationProjection>> Handle(
        RegisterTestingEventSlotCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(actor.Error);
        }

        await using var lockHandle = await _capacityLock.AcquireAsync(request.SlotId, cancellationToken).ConfigureAwait(false);
        var slot = await context.Set<TestingEventSlot>()
            .Include(candidate => candidate.Event)
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.SlotId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (slot == null)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(
                Error.NotFound("TestingLab.EventSlotNotFound", "Testing event slot not found."));
        }

        if (slot.Event.Status is not (
                TestingEventStatus.ApplicationsClosed or
                TestingEventStatus.Scheduled or
                TestingEventStatus.Active))
        {
            return Result.Failure<TestingSlotRegistrationProjection>(
                Validation("Tester registration is not open for this event."));
        }

        if (!request.AcceptedRules)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(Validation("The event rules must be accepted before registration."));
        }

        if (request.RegistrationResponse == null)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(Validation("Tester registration responses are required."));
        }

        var registrationSchema = slot.Event.TesterRegistrationSchema;
        if (registrationSchema == null || !slot.Event.ConfigurationFrozenAt.HasValue)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(Validation("The event registration configuration is not frozen."));
        }

        try
        {
            QuestionnaireResponseValidator.EnsureValid(registrationSchema, request.RegistrationResponse);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(Validation(exception.Message));
        }
        var rulesAcceptedAt = SystemClock.UtcNow;
        var configurationFrozenAt = slot.Event.ConfigurationFrozenAt.Value;

        var existing = await context.Set<TestingSlotRegistration>()
            .FirstOrDefaultAsync(candidate =>
                candidate.SlotId == slot.Id &&
                candidate.UserId == actor.UserId &&
                candidate.Status != TestingSlotRegistrationStatus.Cancelled &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing != null)
        {
            return Result.Success(await ToProjectionAsync(existing, cancellationToken).ConfigureAwait(false));
        }

        var capacityCount = await context.Set<TestingSlotRegistration>().CountAsync(candidate =>
            candidate.SlotId == slot.Id &&
            CapacityStatuses.Contains(candidate.Status) &&
            candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        TestingSlotRegistration registration;
        if (slot.MaxTesters.HasValue && capacityCount >= slot.MaxTesters.Value)
        {
            var nextPosition = (await context.Set<TestingSlotRegistration>()
                .Where(candidate =>
                    candidate.SlotId == slot.Id &&
                    candidate.Status == TestingSlotRegistrationStatus.Waitlisted &&
                    candidate.DeletedAt == null)
                .MaxAsync(candidate => (int?)candidate.WaitlistPosition, cancellationToken)
                .ConfigureAwait(false) ?? 0) + 1;
            registration = TestingSlotRegistration.Waitlist(
                slot.EventId,
                slot.Id,
                actor.UserId,
                nextPosition,
                request.Notes,
                actor.TenantId,
                request.RegistrationResponse,
                rulesAcceptedAt,
                configurationFrozenAt);
        }
        else
        {
            registration = TestingSlotRegistration.Register(
                slot.EventId,
                slot.Id,
                actor.UserId,
                request.Notes,
                actor.TenantId,
                request.RegistrationResponse,
                rulesAcceptedAt,
                configurationFrozenAt);
        }

        context.Set<TestingSlotRegistration>().Add(registration);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await lockHandle.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await ToProjectionAsync(registration, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<TestingSlotRegistrationProjection>> Handle(
        CancelTestingEventSlotRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadRegistrationAsync(request.RegistrationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(loaded.Error);
        }

        var managerOverride = loaded.Registration!.Event.ManagerUserId == loaded.Actor!.UserId ||
                              IsTenantAdmin ||
                              await HasTestingLabPermissionAsync(
                                  loaded.Actor,
                                  TestingLabActions.Manage,
                                  TestingLabResourceTypes.Participant).ConfigureAwait(false);
        if (!managerOverride && loaded.Registration.UserId != loaded.Actor.UserId)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(
                Error.Forbidden("TestingLab.RegistrationOwnerRequired", "Only the tester or event manager can cancel this registration."));
        }

        await using var lockHandle = await _capacityLock.AcquireAsync(loaded.Registration.SlotId, cancellationToken).ConfigureAwait(false);
        var releasedCapacity = loaded.Registration.ConsumesCapacity;
        try
        {
            loaded.Registration.Cancel(loaded.Actor.UserId, managerOverride);
            if (releasedCapacity)
            {
                await PromoteWaitlistAsync(loaded.Registration.SlotId, loaded.Actor.TenantId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ReindexWaitlistAsync(loaded.Registration.SlotId, loaded.Actor.TenantId, cancellationToken).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await lockHandle.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(await ToProjectionAsync(loaded.Registration, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(Validation(exception.Message));
        }
    }

    public Task<Result<TestingSlotRegistrationProjection>> Handle(
        CheckInTestingEventRegistrationCommand request,
        CancellationToken cancellationToken)
        => ChangeAttendanceAsync(request.RegistrationId, static registration => registration.CheckIn(), cancellationToken);

    public Task<Result<TestingSlotRegistrationProjection>> Handle(
        CheckOutTestingEventRegistrationCommand request,
        CancellationToken cancellationToken)
        => ChangeAttendanceAsync(request.RegistrationId, static registration => registration.CheckOut(), cancellationToken);

    public Task<Result<TestingSlotRegistrationProjection>> Handle(
        MarkTestingEventNoShowCommand request,
        CancellationToken cancellationToken)
        => ChangeAttendanceAsync(request.RegistrationId, static registration => registration.MarkNoShow(), cancellationToken);

    public async Task<Result<TestingFeedbackObligationProjection>> Handle(
        AssignTestingProjectToTesterCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadManagedRegistrationAsync(request.RegistrationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingFeedbackObligationProjection>(loaded.Error);
        }

        if (loaded.Registration!.Status is not (
                TestingSlotRegistrationStatus.CheckedIn or
                TestingSlotRegistrationStatus.Attended))
        {
            return Result.Failure<TestingFeedbackObligationProjection>(
                Validation("The tester must attend the slot before a tested project can be assigned."));
        }

        var application = await context.Set<TestingProjectApplication>()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.ApplicationId &&
                candidate.AssignedSlotId == loaded.Registration.SlotId &&
                candidate.Status == TestingApplicationStatus.Approved &&
                candidate.TenantId == loaded.Actor!.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (application == null)
        {
            return Result.Failure<TestingFeedbackObligationProjection>(
                Error.NotFound("TestingLab.ApprovedApplicationNotFound", "An approved project application was not found in this slot."));
        }

        var testerUserId = loaded.Registration.UserId;
        var actorTenantId = loaded.Actor!.TenantId;
        var ownsOrCollaborates = await context.Set<Project>()
            .AsNoTracking()
            .AnyAsync(project =>
                project.Id == application.ProjectId &&
                project.TenantId == actorTenantId &&
                project.DeletedAt == null &&
                (project.CreatedById == testerUserId ||
                 context.Set<ProjectCollaborator>().Any(collaborator =>
                     collaborator.ProjectId == project.Id &&
                     collaborator.UserId == testerUserId &&
                     collaborator.IsActive &&
                     collaborator.DeletedAt == null &&
                     collaborator.LeftAt == null) ||
                 context.Set<ProjectTeam>().Any(projectTeam =>
                     projectTeam.ProjectId == project.Id &&
                     projectTeam.TenantId == project.TenantId &&
                     projectTeam.IsActive &&
                     projectTeam.DeletedAt == null &&
                     projectTeam.EndedAt == null &&
                     context.Set<Team>().Any(team =>
                         team.Id == projectTeam.TeamId &&
                         team.TenantId == project.TenantId &&
                         team.IsActive &&
                         team.DeletedAt == null) &&
                     context.Set<TeamMember>().Any(member =>
                         member.TeamId == projectTeam.TeamId &&
                         member.TenantId == project.TenantId &&
                         member.UserId == testerUserId &&
                         member.IsActive &&
                         member.DeletedAt == null &&
                         member.LeftAt == null))),
                cancellationToken)
            .ConfigureAwait(false);
        if (ownsOrCollaborates)
        {
            return Result.Failure<TestingFeedbackObligationProjection>(
                Error.Conflict(
                    "TestingLab.ProjectTesterConflict",
                    "A project owner or active team member cannot test their own project."));
        }

        var existing = await context.Set<TestingFeedbackObligation>()
            .FirstOrDefaultAsync(candidate =>
                candidate.SlotId == loaded.Registration.SlotId &&
                candidate.ApplicationId == application.Id &&
                candidate.TesterUserId == loaded.Registration.UserId &&
                candidate.TenantId == loaded.Actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing != null)
        {
            return Result.Success(ToProjection(existing));
        }

        var obligation = TestingFeedbackObligation.Create(
            loaded.Registration.EventId,
            loaded.Registration.SlotId,
            application.Id,
            loaded.Registration.UserId,
            loaded.Actor!.TenantId,
            application.CurrentQuestionnaireRevisionId);
        if (!loaded.Registration.Event.RequiresFeedback)
        {
            obligation.Waive();
        }

        context.Set<TestingFeedbackObligation>().Add(obligation);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToProjection(obligation));
    }

    public async Task<Result<TestingEventFeedbackProjection>> Handle(
        SubmitTestingEventFeedbackCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<TestingEventFeedbackProjection>(actor.Error);
        }

        var obligation = await context.Set<TestingFeedbackObligation>()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.ObligationId &&
                candidate.TesterUserId == actor.UserId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (obligation == null)
        {
            return Result.Failure<TestingEventFeedbackProjection>(
                Error.NotFound("TestingLab.FeedbackObligationNotFound", "Feedback obligation not found."));
        }

        if (obligation.IsFulfilled)
        {
            return Result.Failure<TestingEventFeedbackProjection>(Validation("The feedback obligation is already complete."));
        }

        var registration = await context.Set<TestingSlotRegistration>()
            .FirstOrDefaultAsync(candidate =>
                candidate.SlotId == obligation.SlotId &&
                candidate.UserId == actor.UserId &&
                candidate.TenantId == actor.TenantId &&
                candidate.Status != TestingSlotRegistrationStatus.Cancelled &&
                candidate.Status != TestingSlotRegistrationStatus.NoShow &&
                candidate.Status != TestingSlotRegistrationStatus.Waitlisted &&
                candidate.Status != TestingSlotRegistrationStatus.Registered &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (registration == null)
        {
            return Result.Failure<TestingEventFeedbackProjection>(
                Error.Forbidden("TestingLab.AttendanceRequired", "Attendance is required before feedback can be submitted."));
        }

        var slotMode = await context.Set<TestingEventSlot>()
            .Where(candidate => candidate.Id == obligation.SlotId && candidate.TenantId == actor.TenantId && candidate.DeletedAt == null)
            .Select(candidate => candidate.Mode)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            TestingFeedback feedback;
            var testingContext = slotMode == TestingEventMode.InPerson
                ? TestingContext.InPerson
                : TestingContext.Online;
            if (obligation.QuestionnaireRevisionId.HasValue)
            {
                if (request.QuestionnaireRevisionId != obligation.QuestionnaireRevisionId || request.Responses == null)
                {
                    throw new ArgumentException("Responses for the assigned questionnaire revision are required.");
                }

                var revision = await context.Set<TestingQuestionnaireRevision>().AsNoTracking()
                    .FirstOrDefaultAsync(candidate =>
                        candidate.Id == obligation.QuestionnaireRevisionId &&
                        candidate.ApplicationId == obligation.ApplicationId &&
                        candidate.TenantId == actor.TenantId &&
                        candidate.DeletedAt == null,
                        cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The assigned questionnaire revision was not found.");
                feedback = TestingFeedback.CreateStructuredForEvent(
                    obligation.EventId,
                    obligation.ApplicationId,
                    actor.UserId,
                    testingContext,
                    revision.Id,
                    revision.Schema,
                    request.Responses,
                    request.OverallRating,
                    request.WouldRecommend,
                    request.AdditionalNotes,
                    actor.TenantId);
            }
            else
            {
                feedback = TestingFeedback.CreateForEvent(
                    obligation.EventId,
                    obligation.ApplicationId,
                    actor.UserId,
                    testingContext,
                    request.FeedbackData ?? throw new ArgumentException("Feedback data is required."),
                    request.OverallRating,
                    request.WouldRecommend,
                    request.AdditionalNotes,
                    actor.TenantId);
            }
            context.Set<TestingFeedback>().Add(feedback);
            obligation.Fulfill(feedback.Id);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(feedback));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingEventFeedbackProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingSlotRegistrationProjection>> Handle(
        CompleteTestingEventParticipationCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadRegistrationAsync(request.RegistrationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(loaded.Error);
        }

        if (loaded.Registration!.UserId != loaded.Actor!.UserId &&
            loaded.Registration.Event.ManagerUserId != loaded.Actor.UserId &&
            !IsTenantAdmin)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(
                Error.Forbidden("TestingLab.RegistrationOwnerRequired", "Only the tester or event manager can complete participation."));
        }

        var pending = await context.Set<TestingFeedbackObligation>().AnyAsync(candidate =>
            candidate.SlotId == loaded.Registration.SlotId &&
            candidate.TesterUserId == loaded.Registration.UserId &&
            candidate.TenantId == loaded.Actor.TenantId &&
            candidate.Status == TestingFeedbackObligationStatus.Pending &&
            candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (pending)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(
                Validation("All required feedback must be submitted before participation can be completed."));
        }

        try
        {
            if (loaded.Registration.Status != TestingSlotRegistrationStatus.Completed)
            {
                loaded.Registration.Complete();
            }

            var evidence = await CreateLearningEvidenceAsync(loaded.Registration, cancellationToken)
                .ConfigureAwait(false);
            if (evidence != null && publisher != null)
            {
                await publisher.Publish(evidence, cancellationToken).ConfigureAwait(false);
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(await ToProjectionAsync(loaded.Registration, cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<IReadOnlyList<TestingSlotRegistrationProjection>>> Handle(
        GetTestingEventSlotRegistrationsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<IReadOnlyList<TestingSlotRegistrationProjection>>(actor.Error);
        }

        var slot = await context.Set<TestingEventSlot>()
            .Include(candidate => candidate.Event)
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.SlotId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (slot == null)
        {
            return Result.Failure<IReadOnlyList<TestingSlotRegistrationProjection>>(
                Error.NotFound("TestingLab.EventSlotNotFound", "Testing event slot not found."));
        }

        var canManageParticipants = await HasTestingLabPermissionAsync(
            actor,
            TestingLabActions.Manage,
            TestingLabResourceTypes.Participant).ConfigureAwait(false);
        if (slot.Event.ManagerUserId != actor.UserId && !IsTenantAdmin && !canManageParticipants)
        {
            return Result.Failure<IReadOnlyList<TestingSlotRegistrationProjection>>(
                Error.Forbidden("TestingLab.EventManagerRequired", "Only the event manager can list slot registrations."));
        }

        var query = context.Set<TestingSlotRegistration>()
            .AsNoTracking()
            .Where(candidate =>
                candidate.SlotId == request.SlotId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null);
        if (request.Status.HasValue)
        {
            query = query.Where(candidate => candidate.Status == request.Status.Value);
        }

        var registrations = await query
            .OrderBy(candidate => candidate.Status == TestingSlotRegistrationStatus.Waitlisted
                ? candidate.WaitlistPosition
                : 0)
            .ThenBy(candidate => candidate.RegisteredAt)
            .ThenBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = new List<TestingSlotRegistrationProjection>(registrations.Count);
        foreach (var registration in registrations)
        {
            result.Add(await ToProjectionAsync(registration, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<TestingSlotRegistrationProjection>>(result);
    }

    public async Task<Result<TestingParticipantDirectoryProjection>> Handle(
        GetTestingParticipantDirectoryQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<TestingParticipantDirectoryProjection>(actor.Error);
        }

        var query = context.Set<TestingSlotRegistration>()
            .AsNoTracking()
            .Where(registration =>
                registration.TenantId == actor.TenantId &&
                registration.DeletedAt == null &&
                registration.Event.DeletedAt == null &&
                registration.Slot.DeletedAt == null &&
                registration.User.DeletedAt == null);

        var canReadAllParticipants = IsTenantAdmin || await HasTestingLabPermissionAsync(
            actor,
            TestingLabActions.Read,
            TestingLabResourceTypes.Participant).ConfigureAwait(false);
        if (!canReadAllParticipants)
        {
            var actorId = actor.UserId;
            query = query.Where(registration => registration.Event.ManagerUserId == actorId);
        }

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(registration =>
                registration.User.Name.ToLower().Contains(search) ||
                registration.User.Email.ToLower().Contains(search) ||
                registration.Event.Name.ToLower().Contains(search) ||
                (registration.Slot.CampusName != null && registration.Slot.CampusName.ToLower().Contains(search)) ||
                (registration.Slot.RoomName != null && registration.Slot.RoomName.ToLower().Contains(search)));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(registration => registration.Status == request.Status.Value);
        }

        var statusCounts = await query
            .GroupBy(registration => registration.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Status, row => row.Count, cancellationToken)
            .ConfigureAwait(false);
        var totalCount = statusCounts.Values.Sum();
        var items = await query
            .OrderBy(registration => registration.Status == TestingSlotRegistrationStatus.Waitlisted
                ? registration.WaitlistPosition
                : 0)
            .ThenByDescending(registration => registration.RegisteredAt)
            .ThenBy(registration => registration.Id)
            .Skip(Math.Max(0, request.Skip))
            .Take(Math.Clamp(request.Take, 1, 100))
            .Select(registration => new TestingParticipantDirectoryItemProjection(
                registration.Id,
                registration.EventId,
                registration.Event.Name,
                registration.SlotId,
                registration.Slot.Mode,
                registration.Slot.StartsAt,
                registration.Slot.EndsAt,
                registration.Slot.CampusName,
                registration.Slot.RoomName,
                registration.UserId,
                registration.User.Name,
                registration.User.Email,
                registration.User.Profile == null ? null : registration.User.Profile.AvatarUrl,
                registration.Status,
                registration.WaitlistPosition,
                registration.Notes,
                registration.RegisteredAt,
                registration.CheckedInAt,
                registration.CheckedOutAt,
                registration.CompletedAt,
                registration.Status == TestingSlotRegistrationStatus.Cancelled ||
                registration.Status == TestingSlotRegistrationStatus.NoShow
                    ? 0
                    : context.Set<TestingFeedbackObligation>().Count(obligation =>
                        obligation.EventId == registration.EventId &&
                        obligation.SlotId == registration.SlotId &&
                        obligation.TesterUserId == registration.UserId &&
                        obligation.TenantId == actor.TenantId &&
                        obligation.Status == TestingFeedbackObligationStatus.Pending &&
                        obligation.DeletedAt == null)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new TestingParticipantDirectoryProjection(
            items,
            totalCount,
            statusCounts.GetValueOrDefault(TestingSlotRegistrationStatus.Registered),
            statusCounts.GetValueOrDefault(TestingSlotRegistrationStatus.Waitlisted),
            statusCounts.GetValueOrDefault(TestingSlotRegistrationStatus.CheckedIn),
            statusCounts.GetValueOrDefault(TestingSlotRegistrationStatus.Attended),
            statusCounts.GetValueOrDefault(TestingSlotRegistrationStatus.Completed),
            statusCounts.GetValueOrDefault(TestingSlotRegistrationStatus.NoShow)));
    }
    public async Task<Result<IReadOnlyList<TestingFeedbackObligationProjection>>> Handle(
        GetMyTestingFeedbackObligationsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<IReadOnlyList<TestingFeedbackObligationProjection>>(actor.Error);
        }

        var query = context.Set<TestingFeedbackObligation>().AsNoTracking().Where(candidate =>
            candidate.TesterUserId == actor.UserId &&
            candidate.TenantId == actor.TenantId &&
            candidate.DeletedAt == null);
        if (request.EventId.HasValue)
        {
            query = query.Where(candidate => candidate.EventId == request.EventId.Value);
        }

        var obligations = await query
            .OrderBy(candidate => candidate.Status)
            .ThenBy(candidate => candidate.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<TestingFeedbackObligationProjection>>(
            obligations.Select(ToProjection).ToList());
    }

    public async Task<Result<IReadOnlyList<TestingSlotRegistrationProjection>>> Handle(
        GetMyTestingSlotRegistrationsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<IReadOnlyList<TestingSlotRegistrationProjection>>(actor.Error);
        }

        var query = context.Set<TestingSlotRegistration>()
            .AsNoTracking()
            .Where(registration =>
                registration.UserId == actor.UserId &&
                registration.TenantId == actor.TenantId &&
                registration.DeletedAt == null);
        if (request.EventId.HasValue)
        {
            query = query.Where(registration => registration.EventId == request.EventId.Value);
        }

        var registrations = await query
            .OrderByDescending(registration => registration.RegisteredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var projections = new List<TestingSlotRegistrationProjection>(registrations.Count);
        foreach (var registration in registrations)
        {
            projections.Add(await ToProjectionAsync(registration, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<TestingSlotRegistrationProjection>>(projections);
    }

    public async Task<Result<IReadOnlyList<TestingEventFeedbackProjection>>> Handle(
        GetMyTestingEventFeedbackQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<IReadOnlyList<TestingEventFeedbackProjection>>(actor.Error);
        }

        var query = context.Set<TestingFeedback>().AsNoTracking().Where(feedback =>
            feedback.UserId == actor.UserId &&
            feedback.TenantId == actor.TenantId &&
            feedback.EventId.HasValue &&
            feedback.ApplicationId.HasValue &&
            feedback.DeletedAt == null);
        if (request.EventId.HasValue)
        {
            query = query.Where(feedback => feedback.EventId == request.EventId.Value);
        }

        var feedback = await query
            .OrderByDescending(candidate => candidate.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<TestingEventFeedbackProjection>>(feedback.Select(ToProjection).ToList());
    }

    public async Task<Result<IReadOnlyList<TestingEventFeedbackReviewProjection>>> Handle(
        GetTestingEventFeedbackQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<IReadOnlyList<TestingEventFeedbackReviewProjection>>(actor.Error);
        }

        var testingEvent = await context.Set<TestingEvent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.EventId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (testingEvent == null)
        {
            return Result.Failure<IReadOnlyList<TestingEventFeedbackReviewProjection>>(
                Error.NotFound("TestingLab.EventNotFound", "Testing event not found."));
        }

        var canReadFeedback = await HasTestingLabPermissionAsync(
            actor,
            TestingLabActions.Read,
            TestingLabResourceTypes.Feedback).ConfigureAwait(false);
        var canReadAllFeedback = testingEvent.ManagerUserId == actor.UserId || IsTenantAdmin || canReadFeedback;
        HashSet<Guid>? projectEditorApplicationIds = null;
        if (!canReadAllFeedback)
        {
            if (projectAuthorizationService == null)
            {
                return Result.Failure<IReadOnlyList<TestingEventFeedbackReviewProjection>>(
                    Error.Forbidden("TestingLab.FeedbackForbidden", "Feedback access is not available."));
            }

            var eventApplications = await context.Set<TestingProjectApplication>().AsNoTracking()
                .Where(application =>
                    application.EventId == request.EventId &&
                    application.TenantId == actor.TenantId &&
                    application.DeletedAt == null)
                .Select(application => new { application.Id, application.ProjectId })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            projectEditorApplicationIds = [];
            foreach (var application in eventApplications)
            {
                if (await projectAuthorizationService.HasPermissionAsync(
                        application.ProjectId,
                        PermissionType.Edit,
                        cancellationToken).ConfigureAwait(false))
                {
                    projectEditorApplicationIds.Add(application.Id);
                }
            }
        }

        var obligationsQuery = context.Set<TestingFeedbackObligation>()
            .AsNoTracking()
            .Where(candidate =>
                candidate.EventId == request.EventId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null);
        if (!canReadAllFeedback)
        {
            obligationsQuery = obligationsQuery.Where(candidate => projectEditorApplicationIds!.Contains(candidate.ApplicationId));
        }

        var obligations = await obligationsQuery
            .OrderBy(candidate => candidate.Status)
            .ThenBy(candidate => candidate.CreatedAt)
            .ThenBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var feedbackIds = obligations
            .Where(candidate => candidate.FeedbackId.HasValue)
            .Select(candidate => candidate.FeedbackId!.Value)
            .ToArray();
        var feedbackById = await context.Set<TestingFeedback>()
            .AsNoTracking()
            .Where(candidate =>
                feedbackIds.Contains(candidate.Id) &&
                candidate.EventId == request.EventId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null)
            .ToDictionaryAsync(candidate => candidate.Id, cancellationToken)
            .ConfigureAwait(false);

        var result = obligations.Select(obligation => new TestingEventFeedbackReviewProjection(
            obligation.Id,
            obligation.EventId,
            obligation.SlotId,
            obligation.ApplicationId,
            obligation.TesterUserId,
            obligation.Status,
            obligation.FulfilledAt,
            obligation.FeedbackId.HasValue &&
            feedbackById.TryGetValue(obligation.FeedbackId.Value, out var feedback)
                ? ToProjection(feedback)
                : null)).ToList();
        return Result.Success<IReadOnlyList<TestingEventFeedbackReviewProjection>>(result);
    }

    private async Task<Result<TestingSlotRegistrationProjection>> ChangeAttendanceAsync(
        Guid registrationId,
        Action<TestingSlotRegistration> transition,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadManagedRegistrationAsync(registrationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(loaded.Error);
        }

        try
        {
            transition(loaded.Registration!);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(await ToProjectionAsync(loaded.Registration!, cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<TestingSlotRegistrationProjection>(Validation(exception.Message));
        }
    }

    private async Task PromoteWaitlistAsync(Guid slotId, Guid tenantId, CancellationToken cancellationToken)
    {
        var promoted = await context.Set<TestingSlotRegistration>()
            .Where(candidate =>
                candidate.SlotId == slotId &&
                candidate.TenantId == tenantId &&
                candidate.Status == TestingSlotRegistrationStatus.Waitlisted &&
                candidate.DeletedAt == null)
            .OrderBy(candidate => candidate.WaitlistPosition)
            .ThenBy(candidate => candidate.RegisteredAt)
            .ThenBy(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (promoted != null)
        {
            promoted.Promote();
            logger.LogInformation(
                "Promoted tester {TesterId} from the waitlist for Testing Lab slot {SlotId}",
                promoted.UserId,
                slotId);
        }
        await ReindexWaitlistAsync(slotId, tenantId, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReindexWaitlistAsync(Guid slotId, Guid tenantId, CancellationToken cancellationToken)
    {
        var waitlist = await context.Set<TestingSlotRegistration>()
            .Where(candidate =>
                candidate.SlotId == slotId &&
                candidate.TenantId == tenantId &&
                candidate.Status == TestingSlotRegistrationStatus.Waitlisted &&
                candidate.DeletedAt == null)
            .OrderBy(candidate => candidate.WaitlistPosition)
            .ThenBy(candidate => candidate.RegisteredAt)
            .ThenBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        waitlist = waitlist.Where(candidate => candidate.Status == TestingSlotRegistrationStatus.Waitlisted).ToList();
        for (var index = 0; index < waitlist.Count; index++)
        {
            var expectedPosition = index + 1;
            if (waitlist[index].WaitlistPosition != expectedPosition)
            {
                waitlist[index].Reposition(expectedPosition);
            }
        }
    }

    private async Task<LoadedRegistration> LoadRegistrationAsync(
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return new(null, null, actor.Error);
        }

        var registration = await context.Set<TestingSlotRegistration>()
            .Include(candidate => candidate.Event)
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == registrationId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        return registration == null
            ? new(null, null, Error.NotFound("TestingLab.SlotRegistrationNotFound", "Testing slot registration not found."))
            : new(registration, actor, null);
    }

    private async Task<LoadedRegistration> LoadManagedRegistrationAsync(
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadRegistrationAsync(registrationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return loaded;
        }

        var canManageParticipants = await HasTestingLabPermissionAsync(
            loaded.Actor!,
            TestingLabActions.Manage,
            TestingLabResourceTypes.Participant).ConfigureAwait(false);
        return loaded.Registration!.Event.ManagerUserId == loaded.Actor!.UserId || IsTenantAdmin || canManageParticipants
            ? loaded
            : new(null, null, Error.Forbidden("TestingLab.EventManagerRequired", "Only the event manager can manage attendance."));
    }

    private Task<bool> HasTestingLabPermissionAsync(
        ActorScope actor,
        string action,
        string resourceType,
        Guid? resourceId = null)
        => testingLabPermissionService?.HasPermissionAsync(
               actor.UserId,
               actor.TenantId,
               action,
               resourceType,
               resourceId) ?? Task.FromResult(false);

    private async Task<ActorScope> RequireActorAsync(CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var userId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || userId == null || actor.TenantId == null)
        {
            return new(Guid.Empty, Guid.Empty, Error.Unauthorized(
                "TestingLab.Unauthenticated",
                "An authenticated tenant actor is required."));
        }

        var hasAccess = await TestingLabActorAccess.IsActiveTenantActorAsync(context, actor, cancellationToken).ConfigureAwait(false);
        return hasAccess
            ? new(userId.Value, actor.TenantId.Value, null)
            : new(Guid.Empty, Guid.Empty, Error.Unauthorized(
                "TestingLab.InactiveActor",
                "An active user and tenant membership are required."));
    }

    private async Task<TestingSlotRegistrationProjection> ToProjectionAsync(
        TestingSlotRegistration registration,
        CancellationToken cancellationToken)
    {
        var pendingFeedback = registration.Status is TestingSlotRegistrationStatus.Cancelled or TestingSlotRegistrationStatus.NoShow
            ? 0
            : await context.Set<TestingFeedbackObligation>().CountAsync(candidate =>
                candidate.SlotId == registration.SlotId &&
                candidate.TesterUserId == registration.UserId &&
                candidate.TenantId == registration.TenantId &&
                candidate.Status == TestingFeedbackObligationStatus.Pending &&
                candidate.DeletedAt == null,
                cancellationToken).ConfigureAwait(false);
        return new TestingSlotRegistrationProjection(
            registration.Id,
            registration.EventId,
            registration.SlotId,
            registration.UserId,
            registration.Status,
            registration.WaitlistPosition,
            registration.Notes,
            registration.RegisteredAt,
            registration.PromotedAt,
            registration.CheckedInAt,
            registration.CheckedOutAt,
            registration.CompletedAt,
            pendingFeedback,
            registration.RegistrationResponse,
            registration.RulesAcceptedAt,
            registration.EventConfigurationFrozenAt);
    }


    private async Task<TestingLearningEvidenceCompletedEvent?> CreateLearningEvidenceAsync(
        TestingSlotRegistration registration,
        CancellationToken cancellationToken)
    {
        var testingEvent = registration.Event;
        if (!testingEvent.CourseId.HasValue ||
            !testingEvent.LearningActivityId.HasValue ||
            testingEvent.LearningCompletionRequirement == TestingLearningCompletionRequirement.None)
        {
            return null;
        }

        var hasSubmittedFeedback = await context.Set<TestingFeedbackObligation>().AnyAsync(candidate =>
            candidate.SlotId == registration.SlotId &&
            candidate.TesterUserId == registration.UserId &&
            candidate.TenantId == registration.TenantId &&
            candidate.Status == TestingFeedbackObligationStatus.Fulfilled &&
            candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        var hasPresentedProject = await context.Set<TestingProjectApplication>().AnyAsync(application =>
            application.EventId == registration.EventId &&
            application.AssignedSlotId == registration.SlotId &&
            application.SubmittedByUserId == registration.UserId &&
            application.Status == TestingApplicationStatus.Approved &&
            application.TenantId == registration.TenantId &&
            application.DeletedAt == null &&
            context.Set<TestingFeedbackObligation>().Any(obligation =>
                obligation.ApplicationId == application.Id &&
                obligation.SlotId == registration.SlotId &&
                obligation.TenantId == registration.TenantId &&
                obligation.DeletedAt == null),
            cancellationToken).ConfigureAwait(false);
        var state = new TestingLearningEvidenceState(
            registration.Status,
            hasSubmittedFeedback,
            hasPresentedProject);
        if (!TestingLearningPolicy.IsSatisfied(testingEvent.LearningCompletionRequirement, state))
        {
            return null;
        }

        return new TestingLearningEvidenceCompletedEvent(
            registration.Id,
            registration.EventId,
            registration.SlotId,
            registration.UserId,
            testingEvent.CourseId.Value,
            testingEvent.CohortId,
            testingEvent.LearningActivityId.Value,
            testingEvent.LearningCompletionRequirement,
            registration.CompletedAt ?? SystemClock.UtcNow,
            registration.TenantId);
    }

    private static TestingFeedbackObligationProjection ToProjection(TestingFeedbackObligation obligation) => new(
        obligation.Id,
        obligation.EventId,
        obligation.SlotId,
        obligation.ApplicationId,
        obligation.TesterUserId,
        obligation.FeedbackId,
        obligation.Status,
        obligation.FulfilledAt,
        obligation.QuestionnaireRevisionId);

    private static TestingEventFeedbackProjection ToProjection(TestingFeedback feedback) => new(
        feedback.Id,
        feedback.EventId!.Value,
        feedback.ApplicationId!.Value,
        feedback.UserId,
        feedback.FeedbackData,
        feedback.OverallRating,
        feedback.WouldRecommend,
        feedback.AdditionalNotes,
        feedback.CreatedAt,
        feedback.QuestionnaireRevisionId,
        feedback.StructuredResponses);

    private static Error Validation(string message) => Error.Validation("TestingLab.Validation", message);

    private sealed record ActorScope(Guid UserId, Guid TenantId, Error? Error);

    private sealed record LoadedRegistration(
        TestingSlotRegistration? Registration,
        ActorScope? Actor,
        Error? Error);
}

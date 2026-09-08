using System.Linq.Expressions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.TestingLab;

public sealed class TestingEventHandlers(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    ITestingLabPermissionService? testingLabPermissionService = null,
    GameGuild.CQRS.IMediator? mediator = null)
    : ICommandHandler<CreateTestingEventCommand, Result<TestingEventProjection>>,
    ICommandHandler<ConfigureTestingEventCommand, Result<TestingEventProjection>>,
    ICommandHandler<UpdateTestingEventCommand, Result<TestingEventProjection>>,
    ICommandHandler<DeleteTestingEventCommand, Result<bool>>,
    ICommandHandler<ArchiveTestingEventCommand, Result<bool>>,
    ICommandHandler<RestoreTestingEventCommand, Result<bool>>,
    ICommandHandler<OpenTestingEventApplicationsCommand, Result<TestingEventProjection>>,
    ICommandHandler<CloseTestingEventApplicationsCommand, Result<TestingEventProjection>>,
    ICommandHandler<ScheduleTestingEventCommand, Result<TestingEventProjection>>,
    ICommandHandler<ActivateTestingEventCommand, Result<TestingEventProjection>>,
    ICommandHandler<CompleteTestingEventCommand, Result<TestingEventProjection>>,
    ICommandHandler<CancelTestingEventCommand, Result<TestingEventProjection>>,
    ICommandHandler<ConfigureTestingEventLearningCommand, Result<TestingEventProjection>>,
    ICommandHandler<CreateTestingEventSlotCommand, Result<TestingEventSlotProjection>>,
    ICommandHandler<UpdateTestingEventSlotCommand, Result<TestingEventSlotProjection>>,
    ICommandHandler<DeleteTestingEventSlotCommand, Result<bool>>,
    ICommandHandler<AddTestingEventCommitteeMemberCommand, Result<TestingEventCommitteeMemberProjection>>,
    ICommandHandler<RemoveTestingEventCommitteeMemberCommand, Result<bool>>,
    IQueryHandler<GetTestingEventQuery, Result<TestingEventProjection>>,
    IQueryHandler<GetTestingEventsQuery, Result<IReadOnlyList<TestingEventProjection>>>,
    IQueryHandler<GetArchivedTestingEventsQuery, Result<IReadOnlyList<TestingEventProjection>>>,
    IQueryHandler<GetPublicTestingEventsQuery, Result<IReadOnlyList<PublicTestingEventProjection>>>,
    IQueryHandler<GetPublicTestingEventQuery, Result<PublicTestingEventProjection>>,
    IQueryHandler<GetTestingEventSlotsQuery, Result<IReadOnlyList<TestingEventSlotProjection>>>,
    IQueryHandler<GetTestingEventCommitteeQuery, Result<IReadOnlyList<TestingEventCommitteeMemberProjection>>>
{
    private bool IsTenantAdmin => actorContextAccessor.ActorContext.IsTenantAdmin;

    public async Task<Result<TestingEventProjection>> Handle(CreateTestingEventCommand request, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null) return Result.Failure<TestingEventProjection>(actor.Error);

        try
        {
            TestingEventTemplateRevision? templateRevision = null;
            if (request.TemplateRevisionId.HasValue)
            {
                templateRevision = await context.Set<TestingEventTemplateRevision>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(revision =>
                        revision.Id == request.TemplateRevisionId.Value &&
                        revision.TenantId == actor.TenantId &&
                        revision.DeletedAt == null,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (templateRevision == null)
                    return Result.Failure<TestingEventProjection>(
                        Error.NotFound("TestingLab.TemplateRevisionNotFound", "Testing event template revision not found."));
            }
            var occurrenceStarts = TestingEventRecurrenceSchedule.Expand(
                request.StartsAt,
                request.Recurrence,
                request.TimeZoneId);
            Guid? recurrenceSeriesId = request.Recurrence == null ? null : Guid.NewGuid();
            var recurrenceDaysOfWeek = request.Recurrence?.DaysOfWeek is { Count: > 0 } days
                ? string.Join(',', days.Distinct().OrderBy(day => day))
                : null;
            var testingEvents = occurrenceStarts
                .Select((occurrenceStart, index) =>
                {
                    var offset = occurrenceStart - request.StartsAt;
                    var testingEvent = TestingEvent.Create(
                        request.Name,
                        request.Mode,
                        actor.UserId,
                        request.ApplicationsOpenAt + offset,
                        request.ApplicationsCloseAt + offset,
                        occurrenceStart,
                        request.EndsAt + offset,
                        request.RequiresFeedback,
                        request.ApprovalMode,
                        actor.TenantId,
                        request.Description,
                        recurrenceSeriesId,
                        request.Recurrence == null ? null : index + 1,
                        request.Recurrence?.Frequency,
                        request.Recurrence?.Interval,
                        recurrenceDaysOfWeek,
                        request.Recurrence?.EndsAt,
                        request.Recurrence?.OccurrenceCount,
                        request.TimeZoneId);
                    if (templateRevision != null)
                    {
                        testingEvent.ConfigureFromTemplate(templateRevision);
                    }
                    else if (request.Configuration is { } configuration)
                    {
                        testingEvent.Configure(
                            configuration.GeneralRules,
                            configuration.CandidateInstructions,
                            configuration.TesterInstructions,
                            configuration.ProjectApplicationSchema,
                            configuration.TesterRegistrationSchema);
                    }
                    return testingEvent;
                })
                .ToArray();
            foreach (var testingEvent in testingEvents)
                context.Set<TestingEvent>().Add(testingEvent);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (mediator is not null)
            {
                await mediator.Send(new GameGuild.Announcements.Contracts.AnnouncePublicationCommand
                {
                    Kind = GameGuild.Announcements.Contracts.PublicationKind.TestingEventCreated,
                    ActorId = actor.UserId,
                    Title = testingEvents[0].Name,
                    EntityId = testingEvents[0].Id,
                    StartsAt = testingEvents[0].StartsAt,
                    TenantId = actor.TenantId,
                }, cancellationToken).ConfigureAwait(false);
            }

            return Result.Success(ToProjection(testingEvents[0]));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<TestingEventProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingEventProjection>> Handle(
        ConfigureTestingEventCommand request,
        CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<TestingEventProjection>(authorization.Error);
        try
        {
            authorization.Event!.Configure(
                request.GeneralRules,
                request.CandidateInstructions,
                request.TesterInstructions,
                request.ProjectApplicationSchema,
                request.TesterRegistrationSchema);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(authorization.Event));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingEventProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingEventProjection>> Handle(UpdateTestingEventCommand request, CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<TestingEventProjection>(authorization.Error);

        try
        {
            authorization.Event!.Update(
                request.Name,
                request.Description,
                request.Mode,
                request.ApprovalMode,
                request.ApplicationsOpenAt,
                request.ApplicationsCloseAt,
                request.StartsAt,
                request.EndsAt,
                request.RequiresFeedback,
                request.TimeZoneId);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(authorization.Event));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingEventProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<bool>> Handle(DeleteTestingEventCommand request, CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<bool>(authorization.Error);
        var testingEvent = authorization.Event!;
        if (testingEvent.Status != TestingEventStatus.Draft)
            return Result.Failure<bool>(Validation("Only draft events can be deleted."));
        var hasApplications = await context.Set<TestingProjectApplication>()
            .AnyAsync(application => application.EventId == testingEvent.Id && application.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (hasApplications)
            return Result.Failure<bool>(Error.Conflict("TestingLab.EventHasApplications", "Events with applications cannot be deleted."));

        testingEvent.DeletedAt = SystemClock.UtcNow;
        testingEvent.Touch();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<bool>> Handle(ArchiveTestingEventCommand request, CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<bool>(authorization.Error);
        var testingEvent = authorization.Event!;
        if (testingEvent.Status is not (TestingEventStatus.Completed or TestingEventStatus.Cancelled))
            return Result.Failure<bool>(Validation("Only completed or cancelled events can be archived."));

        testingEvent.DeletedAt = SystemClock.UtcNow;
        testingEvent.Touch();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<bool>> Handle(RestoreTestingEventCommand request, CancellationToken cancellationToken)
    {
        var authorization = await GetManagedArchivedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<bool>(authorization.Error);

        authorization.Event!.Restore();
        authorization.Event.Touch();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<TestingEventProjection>> Handle(OpenTestingEventApplicationsCommand request, CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<TestingEventProjection>(authorization.Error);
        try
        {
            authorization.Event!.OpenApplications();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(authorization.Event));
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<TestingEventProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingEventProjection>> Handle(CloseTestingEventApplicationsCommand request, CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<TestingEventProjection>(authorization.Error);
        try
        {
            authorization.Event!.CloseApplications();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(authorization.Event));
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<TestingEventProjection>(Validation(exception.Message));
        }
    }

    public Task<Result<TestingEventProjection>> Handle(ScheduleTestingEventCommand request, CancellationToken cancellationToken)
        => TransitionEventAsync(request.EventId, testingEvent => testingEvent.Schedule(), cancellationToken);

    public Task<Result<TestingEventProjection>> Handle(ActivateTestingEventCommand request, CancellationToken cancellationToken)
        => TransitionEventAsync(request.EventId, testingEvent => testingEvent.Activate(), cancellationToken);

    public Task<Result<TestingEventProjection>> Handle(CompleteTestingEventCommand request, CancellationToken cancellationToken)
        => TransitionEventAsync(request.EventId, testingEvent => testingEvent.Complete(), cancellationToken);

    public Task<Result<TestingEventProjection>> Handle(CancelTestingEventCommand request, CancellationToken cancellationToken)
        => TransitionEventAsync(request.EventId, testingEvent => testingEvent.Cancel(request.Reason), cancellationToken);

    public async Task<Result<TestingEventProjection>> Handle(
        ConfigureTestingEventLearningCommand request,
        CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<TestingEventProjection>(authorization.Error);

        try
        {
            authorization.Event!.ConfigureLearning(
                request.CourseId,
                request.CohortId,
                request.LearningActivityId,
                request.Requirement);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(authorization.Event));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingEventProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingEventSlotProjection>> Handle(CreateTestingEventSlotCommand request, CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<TestingEventSlotProjection>(authorization.Error);
        if (!IsWithinEvent(authorization.Event!, request.StartsAt, request.EndsAt))
            return Result.Failure<TestingEventSlotProjection>(Validation("Slot schedule must be inside the event schedule."));

        try
        {
            var slot = TestingEventSlot.Create(
                request.EventId,
                request.Mode,
                request.StartsAt,
                request.EndsAt,
                request.MaxTesters,
                request.MaxProjects,
                request.CampusName,
                request.RoomName,
                request.MeetingUrl,
                authorization.Event!.TenantId,
                request.LocationId);
            context.Set<TestingEventSlot>().Add(slot);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(slot));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<TestingEventSlotProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingEventSlotProjection>> Handle(UpdateTestingEventSlotCommand request, CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<TestingEventSlotProjection>(authorization.Error);
        if (!IsWithinEvent(authorization.Event!, request.StartsAt, request.EndsAt))
            return Result.Failure<TestingEventSlotProjection>(Validation("Slot schedule must be inside the event schedule."));
        var slot = await context.Set<TestingEventSlot>()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.SlotId &&
                candidate.EventId == request.EventId &&
                candidate.TenantId == authorization.Event!.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (slot == null)
            return Result.Failure<TestingEventSlotProjection>(Error.NotFound("TestingLab.EventSlotNotFound", "Testing event slot not found."));

        try
        {
            slot.Update(
                request.Mode,
                request.StartsAt,
                request.EndsAt,
                request.MaxTesters,
                request.MaxProjects,
                request.CampusName,
                request.RoomName,
                request.MeetingUrl,
                request.LocationId);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(slot));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingEventSlotProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<bool>> Handle(DeleteTestingEventSlotCommand request, CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<bool>(authorization.Error);
        var slot = await context.Set<TestingEventSlot>()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.SlotId &&
                candidate.EventId == request.EventId &&
                candidate.TenantId == authorization.Event!.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (slot == null)
            return Result.Failure<bool>(Error.NotFound("TestingLab.EventSlotNotFound", "Testing event slot not found."));
        var assigned = await context.Set<TestingProjectApplication>()
            .AnyAsync(application =>
                application.AssignedSlotId == slot.Id &&
                application.Status == TestingApplicationStatus.Approved &&
                application.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (assigned)
            return Result.Failure<bool>(Error.Conflict("TestingLab.EventSlotAssigned", "A slot with approved projects cannot be deleted."));

        slot.DeletedAt = SystemClock.UtcNow;
        slot.Touch();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<TestingEventProjection>> Handle(GetTestingEventQuery request, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null) return Result.Failure<TestingEventProjection>(actor.Error);
        var canReadAllEvents = IsTenantAdmin || await HasTestingLabPermissionAsync(
            actor,
            TestingLabActions.Read,
            TestingLabResourceTypes.Event).ConfigureAwait(false);
        var canReadApplications = await HasTestingLabPermissionAsync(
            actor,
            TestingLabActions.Read,
            TestingLabResourceTypes.Application).ConfigureAwait(false);
        var canManageApplications = await HasTestingLabPermissionAsync(
            actor,
            TestingLabActions.Approve,
            TestingLabResourceTypes.Application).ConfigureAwait(false) ||
            await HasTestingLabPermissionAsync(
                actor,
                TestingLabActions.Manage,
                TestingLabResourceTypes.Application).ConfigureAwait(false);
        var testingEvent = await context.Set<TestingEvent>()
            .AsNoTracking()
            .Include(candidate => candidate.Slots)
            .Include(candidate => candidate.Applications)
            .Where(testingEvent =>
                testingEvent.Id == request.EventId &&
                testingEvent.TenantId == actor.TenantId &&
                testingEvent.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (testingEvent == null)
            return Result.Failure<TestingEventProjection>(Error.NotFound("TestingLab.EventNotFound", "Testing event not found."));
        var isCommitteeMember = await context.Set<TestingCommitteeMember>().AnyAsync(member =>
            member.EventId == testingEvent.Id &&
            member.UserId == actor.UserId &&
            member.IsActive &&
            member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        return canReadAllEvents || canReadApplications || canManageApplications ||
            testingEvent.ManagerUserId == actor.UserId || isCommitteeMember
            ? Result.Success(ToProjection(testingEvent))
            : Result.Failure<TestingEventProjection>(Error.NotFound("TestingLab.EventNotFound", "Testing event not found."));
    }

    public async Task<Result<IReadOnlyList<TestingEventProjection>>> Handle(GetTestingEventsQuery request, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null) return Result.Failure<IReadOnlyList<TestingEventProjection>>(actor.Error);
        var query = context.Set<TestingEvent>()
            .AsNoTracking()
            .Where(testingEvent => testingEvent.TenantId == actor.TenantId && testingEvent.DeletedAt == null);
        var canReadAllEvents = IsTenantAdmin || await HasTestingLabPermissionAsync(
            actor,
            TestingLabActions.Read,
            TestingLabResourceTypes.Event).ConfigureAwait(false);
        if (!canReadAllEvents)
        {
            var actorId = actor.UserId;
            query = query.Where(testingEvent => testingEvent.ManagerUserId == actorId);
        }
        if (request.Status.HasValue) query = query.Where(testingEvent => testingEvent.Status == request.Status.Value);
        var events = await query
            .OrderByDescending(testingEvent => testingEvent.StartsAt)
            .Skip(Math.Max(0, request.Skip))
            .Take(Math.Clamp(request.Take, 1, 100))
            .Select(EventProjection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<TestingEventProjection>>(events);
    }

    public async Task<Result<IReadOnlyList<TestingEventProjection>>> Handle(
        GetArchivedTestingEventsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null) return Result.Failure<IReadOnlyList<TestingEventProjection>>(actor.Error);
        var query = context.Set<TestingEvent>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(testingEvent =>
                testingEvent.TenantId == actor.TenantId &&
                testingEvent.DeletedAt != null &&
                testingEvent.Status != TestingEventStatus.Draft);
        var canReadAllEvents = IsTenantAdmin || await HasTestingLabPermissionAsync(
            actor,
            TestingLabActions.Read,
            TestingLabResourceTypes.Event).ConfigureAwait(false);
        if (!canReadAllEvents)
        {
            var actorId = actor.UserId;
            query = query.Where(testingEvent => testingEvent.ManagerUserId == actorId);
        }
        var events = await query
            .OrderByDescending(testingEvent => testingEvent.DeletedAt)
            .Skip(Math.Max(0, request.Skip))
            .Take(Math.Clamp(request.Take, 1, 100))
            .Select(EventProjection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<TestingEventProjection>>(events);
    }

    public async Task<Result<IReadOnlyList<PublicTestingEventProjection>>> Handle(
        GetPublicTestingEventsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = actorContextAccessor.ActorContext.IsAuthenticated
            ? actorContextAccessor.ActorContext.TenantId
            : null;
        var events = await LoadPublicEventsAsync(
            null,
            tenantId,
            Math.Max(0, request.Skip),
            Math.Clamp(request.Take, 1, 100),
            cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<PublicTestingEventProjection>>(events);
    }

    public async Task<Result<PublicTestingEventProjection>> Handle(
        GetPublicTestingEventQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = actorContextAccessor.ActorContext.IsAuthenticated
            ? actorContextAccessor.ActorContext.TenantId
            : null;
        var events = await LoadPublicEventsAsync(request.EventId, tenantId, 0, 1, cancellationToken).ConfigureAwait(false);
        return events.Count == 0
            ? Result.Failure<PublicTestingEventProjection>(
                Error.NotFound("TestingLab.PublicEventNotFound", "Public testing event not found."))
            : Result.Success(events[0]);
    }

    private async Task<IReadOnlyList<PublicTestingEventProjection>> LoadPublicEventsAsync(
        Guid? eventId,
        Guid? tenantId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = context.Set<TestingEvent>()
            .AsNoTracking()
            .Include(testingEvent => testingEvent.Slots.Where(slot => slot.DeletedAt == null))
            .Where(testingEvent =>
                testingEvent.DeletedAt == null &&
                (testingEvent.Status == TestingEventStatus.ApplicationsOpen ||
                 testingEvent.Status == TestingEventStatus.ApplicationsClosed ||
                 testingEvent.Status == TestingEventStatus.Scheduled ||
                 testingEvent.Status == TestingEventStatus.Active));
        if (eventId.HasValue)
            query = query.Where(testingEvent => testingEvent.Id == eventId.Value);
        if (tenantId.HasValue)
            query = query.Where(testingEvent => testingEvent.TenantId == tenantId.Value);

        var events = await query
            .OrderBy(testingEvent => testingEvent.StartsAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (events.Count == 0) return [];

        var eventIds = events.Select(testingEvent => testingEvent.Id).ToArray();
        var slotIds = events.SelectMany(testingEvent => testingEvent.Slots).Select(slot => slot.Id).ToArray();
        var applicationCounts = await context.Set<TestingProjectApplication>()
            .AsNoTracking()
            .Where(application => eventIds.Contains(application.EventId) && application.DeletedAt == null)
            .GroupBy(application => application.EventId)
            .Select(group => new { EventId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.EventId, row => row.Count, cancellationToken)
            .ConfigureAwait(false);
        var approvedProjectCounts = await context.Set<TestingProjectApplication>()
            .AsNoTracking()
            .Where(application =>
                application.AssignedSlotId.HasValue &&
                slotIds.Contains(application.AssignedSlotId.Value) &&
                application.Status == TestingApplicationStatus.Approved &&
                application.DeletedAt == null)
            .GroupBy(application => application.AssignedSlotId!.Value)
            .Select(group => new { SlotId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.SlotId, row => row.Count, cancellationToken)
            .ConfigureAwait(false);
        var registeredTesterCounts = await context.Set<TestingSlotRegistration>()
            .AsNoTracking()
            .Where(registration =>
                slotIds.Contains(registration.SlotId) &&
                registration.DeletedAt == null &&
                registration.Status != TestingSlotRegistrationStatus.Waitlisted &&
                registration.Status != TestingSlotRegistrationStatus.Cancelled)
            .GroupBy(registration => registration.SlotId)
            .Select(group => new { SlotId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.SlotId, row => row.Count, cancellationToken)
            .ConfigureAwait(false);

        return events.Select(testingEvent => new PublicTestingEventProjection(
            testingEvent.Id,
            testingEvent.Name,
            testingEvent.Description,
            testingEvent.Mode,
            testingEvent.ApprovalMode,
            testingEvent.Status,
            testingEvent.ApplicationsOpenAt,
            testingEvent.ApplicationsCloseAt,
            testingEvent.StartsAt,
            testingEvent.EndsAt,
            testingEvent.RequiresFeedback,
            applicationCounts.GetValueOrDefault(testingEvent.Id),
            testingEvent.Slots
                .OrderBy(slot => slot.StartsAt)
                .Select(slot => new PublicTestingEventSlotProjection(
                    slot.Id,
                    slot.EventId,
                    slot.Mode,
                    slot.StartsAt,
                    slot.EndsAt,
                    slot.MaxTesters,
                    slot.MaxProjects,
                    slot.CampusName,
                    slot.RoomName,
                    approvedProjectCounts.GetValueOrDefault(slot.Id),
                    registeredTesterCounts.GetValueOrDefault(slot.Id)))
                .ToList(),
            ToConfigurationProjection(testingEvent),
            testingEvent.TimeZoneId))
            .ToList();
    }

    public async Task<Result<IReadOnlyList<TestingEventSlotProjection>>> Handle(GetTestingEventSlotsQuery request, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null) return Result.Failure<IReadOnlyList<TestingEventSlotProjection>>(actor.Error);
        var eventExists = await context.Set<TestingEvent>().AnyAsync(testingEvent =>
            testingEvent.Id == request.EventId &&
            testingEvent.TenantId == actor.TenantId &&
            testingEvent.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (!eventExists)
            return Result.Failure<IReadOnlyList<TestingEventSlotProjection>>(Error.NotFound("TestingLab.EventNotFound", "Testing event not found."));

        var slots = await context.Set<TestingEventSlot>()
            .AsNoTracking()
            .Where(slot => slot.EventId == request.EventId && slot.TenantId == actor.TenantId && slot.DeletedAt == null)
            .OrderBy(slot => slot.StartsAt)
            .Select(slot => new TestingEventSlotProjection(
                slot.Id,
                slot.EventId,
                slot.LocationId,
                slot.Mode,
                slot.StartsAt,
                slot.EndsAt,
                slot.MaxTesters,
                slot.MaxProjects,
                slot.CampusName,
                slot.RoomName,
                slot.MeetingUrl,
                context.Set<TestingProjectApplication>().Count(application =>
                    application.AssignedSlotId == slot.Id &&
                    application.Status == TestingApplicationStatus.Approved &&
                    application.DeletedAt == null),
                context.Set<TestingSession>()
                    .Where(session => session.EventSlotId == slot.Id && session.DeletedAt == null)
                    .SelectMany(session => session.Registrations)
                    .Count(registration => registration.DeletedAt == null)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<TestingEventSlotProjection>>(slots);
    }

    public async Task<Result<TestingEventCommitteeMemberProjection>> Handle(
        AddTestingEventCommitteeMemberCommand request,
        CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null)
            return Result.Failure<TestingEventCommitteeMemberProjection>(authorization.Error);
        if (authorization.Event!.ApprovalMode != TestingEventApprovalMode.Committee)
            return Result.Failure<TestingEventCommitteeMemberProjection>(
                Validation("Committee members can only be assigned to committee-reviewed events."));
        if (authorization.Event.Status is TestingEventStatus.Active or TestingEventStatus.Completed or TestingEventStatus.Cancelled)
            return Result.Failure<TestingEventCommitteeMemberProjection>(
                Validation("Committee membership cannot change for active or terminal events."));

        var user = await context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.UserId &&
                candidate.IsActive &&
                !candidate.IsSuspended &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        var isMember = user != null && await context.Set<TenantMember>()
            .AsNoTracking()
            .AnyAsync(member =>
                member.UserId == request.UserId &&
                member.TenantId == authorization.Event.TenantId &&
                member.IsActive &&
                member.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!isMember)
            return Result.Failure<TestingEventCommitteeMemberProjection>(
                Error.NotFound("TestingLab.CommitteeUserNotFound", "An active tenant member is required."));

        var exists = await context.Set<TestingCommitteeMember>()
            .AnyAsync(member =>
                member.EventId == request.EventId &&
                member.UserId == request.UserId &&
                member.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            return Result.Failure<TestingEventCommitteeMemberProjection>(
                Error.Conflict("TestingLab.CommitteeMemberExists", "This user already has a committee membership for the event."));

        var member = TestingCommitteeMember.Create(
            request.EventId,
            request.UserId,
            request.IsChair,
            authorization.Event.TenantId);
        context.Set<TestingCommitteeMember>().Add(member);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToProjection(member, user!));
    }

    public async Task<Result<bool>> Handle(
        RemoveTestingEventCommitteeMemberCommand request,
        CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<bool>(authorization.Error);
        if (authorization.Event!.Status is TestingEventStatus.Active or TestingEventStatus.Completed or TestingEventStatus.Cancelled)
            return Result.Failure<bool>(Validation("Committee membership cannot change for active or terminal events."));

        var member = await context.Set<TestingCommitteeMember>()
            .FirstOrDefaultAsync(candidate =>
                candidate.EventId == request.EventId &&
                candidate.UserId == request.UserId &&
                candidate.IsActive &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (member == null)
            return Result.Failure<bool>(
                Error.NotFound("TestingLab.CommitteeMemberNotFound", "Active committee member not found."));
        var hasVotes = await context.Set<TestingApplicationVote>()
            .AnyAsync(vote => vote.ReviewerId == request.UserId &&
                              vote.Application.EventId == request.EventId &&
                              vote.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (hasVotes)
            return Result.Failure<bool>(
                Error.Conflict("TestingLab.CommitteeMemberHasVotes", "Committee members with recorded votes cannot be removed."));

        member.Deactivate();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<IReadOnlyList<TestingEventCommitteeMemberProjection>>> Handle(
        GetTestingEventCommitteeQuery request,
        CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null)
            return Result.Failure<IReadOnlyList<TestingEventCommitteeMemberProjection>>(authorization.Error);

        var members = await context.Set<TestingCommitteeMember>()
            .AsNoTracking()
            .Where(member =>
                member.EventId == request.EventId &&
                member.TenantId == authorization.Event!.TenantId &&
                member.IsActive &&
                member.DeletedAt == null)
            .OrderByDescending(member => member.IsChair)
            .ThenBy(member => member.User.Name)
            .Select(member => new TestingEventCommitteeMemberProjection(
                member.Id,
                member.EventId,
                member.UserId,
                member.User.Name,
                member.User.Email,
                member.IsChair,
                member.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<TestingEventCommitteeMemberProjection>>(members);
    }

    private async Task<Result<TestingEventProjection>> TransitionEventAsync(
        Guid eventId,
        Action<TestingEvent> transition,
        CancellationToken cancellationToken)
    {
        var authorization = await GetManagedEventAsync(eventId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null) return Result.Failure<TestingEventProjection>(authorization.Error);
        try
        {
            transition(authorization.Event!);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(authorization.Event!));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingEventProjection>(Validation(exception.Message));
        }
    }

    private async Task<ManagedEvent> GetManagedEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null) return new(null, actor.Error);
        var testingEvent = await context.Set<TestingEvent>()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == eventId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (testingEvent == null)
            return new(null, Error.NotFound("TestingLab.EventNotFound", "Testing event not found."));
        var hasPermission = testingLabPermissionService != null && await testingLabPermissionService.HasPermissionAsync(
            actor.UserId,
            actor.TenantId,
            TestingLabActions.Edit,
            TestingLabResourceTypes.Event,
            testingEvent.Id).ConfigureAwait(false);
        if (testingEvent.ManagerUserId != actor.UserId && !IsTenantAdmin && !hasPermission)
            return new(null, Error.Forbidden("TestingLab.EventManagerRequired", "Only the event manager can perform this operation."));
        return new(testingEvent, null);
    }

    private async Task<ManagedEvent> GetManagedArchivedEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null) return new(null, actor.Error);
        var testingEvent = await context.Set<TestingEvent>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == eventId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt != null &&
                candidate.Status != TestingEventStatus.Draft,
                cancellationToken)
            .ConfigureAwait(false);
        if (testingEvent == null)
            return new(null, Error.NotFound("TestingLab.ArchivedEventNotFound", "Archived testing event not found."));
        var hasPermission = testingLabPermissionService != null && await testingLabPermissionService.HasPermissionAsync(
            actor.UserId,
            actor.TenantId,
            TestingLabActions.Edit,
            TestingLabResourceTypes.Event,
            testingEvent.Id).ConfigureAwait(false);
        if (testingEvent.ManagerUserId != actor.UserId && !IsTenantAdmin && !hasPermission)
            return new(null, Error.Forbidden("TestingLab.EventManagerRequired", "Only the event manager can perform this operation."));
        return new(testingEvent, null);
    }

    private async Task<ActorScope> RequireActorAsync(CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var userId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || userId == null || actor.TenantId == null)
            return new(Guid.Empty, Guid.Empty, Error.Unauthorized("TestingLab.Unauthenticated", "An authenticated tenant actor is required."));
        var hasAccess = await TestingLabActorAccess.IsActiveTenantActorAsync(context, actor, cancellationToken).ConfigureAwait(false);
        return hasAccess
            ? new(userId.Value, actor.TenantId.Value, null)
             : new(Guid.Empty, Guid.Empty, Error.Unauthorized("TestingLab.InactiveActor", "An active user and tenant membership are required."));
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

    private static bool IsWithinEvent(TestingEvent testingEvent, DateTime startsAt, DateTime endsAt) =>
        startsAt >= testingEvent.StartsAt && endsAt <= testingEvent.EndsAt;

    private static Error Validation(string message) => Error.Validation("TestingLab.Validation", message);

    private static TestingEventProjection ToProjection(TestingEvent testingEvent) => new(
        testingEvent.Id,
        testingEvent.Name,
        testingEvent.Description,
        testingEvent.Mode,
        testingEvent.ApprovalMode,
        testingEvent.Status,
        testingEvent.ManagerUserId,
        testingEvent.ApplicationsOpenAt,
        testingEvent.ApplicationsCloseAt,
        testingEvent.StartsAt,
        testingEvent.EndsAt,
        testingEvent.RequiresFeedback,
        testingEvent.LearningCompletionRequirement,
        testingEvent.CourseId,
        testingEvent.CohortId,
        testingEvent.LearningActivityId,
        testingEvent.TenantId,
        testingEvent.Slots.Count(slot => slot.DeletedAt == null),
        testingEvent.Applications.Count(application => application.DeletedAt == null),
        testingEvent.RecurrenceSeriesId,
        testingEvent.RecurrenceOccurrence,
        testingEvent.RecurrenceFrequency,
        testingEvent.RecurrenceInterval,
        ParseRecurrenceDaysOfWeek(testingEvent.RecurrenceDaysOfWeek),
        testingEvent.RecurrenceEndsAt,
        testingEvent.RecurrenceOccurrenceCount,
        ToConfigurationProjection(testingEvent),
        testingEvent.TimeZoneId);

    private static readonly Expression<Func<TestingEvent, TestingEventProjection>> EventProjection = testingEvent => new(
        testingEvent.Id,
        testingEvent.Name,
        testingEvent.Description,
        testingEvent.Mode,
        testingEvent.ApprovalMode,
        testingEvent.Status,
        testingEvent.ManagerUserId,
        testingEvent.ApplicationsOpenAt,
        testingEvent.ApplicationsCloseAt,
        testingEvent.StartsAt,
        testingEvent.EndsAt,
        testingEvent.RequiresFeedback,
        testingEvent.LearningCompletionRequirement,
        testingEvent.CourseId,
        testingEvent.CohortId,
        testingEvent.LearningActivityId,
        testingEvent.TenantId,
        testingEvent.Slots.Count(slot => slot.DeletedAt == null),
        testingEvent.Applications.Count(application => application.DeletedAt == null),
        testingEvent.RecurrenceSeriesId,
        testingEvent.RecurrenceOccurrence,
        testingEvent.RecurrenceFrequency,
        testingEvent.RecurrenceInterval,
        ParseRecurrenceDaysOfWeek(testingEvent.RecurrenceDaysOfWeek),
        testingEvent.RecurrenceEndsAt,
        testingEvent.RecurrenceOccurrenceCount,
        null,
        testingEvent.TimeZoneId);

    private static TestingEventConfigurationProjection? ToConfigurationProjection(TestingEvent testingEvent)
    {
        if (testingEvent.ProjectApplicationSchema == null || testingEvent.TesterRegistrationSchema == null) return null;
        return new TestingEventConfigurationProjection(
            testingEvent.SourceTemplateId,
            testingEvent.SourceTemplateRevisionId,
            testingEvent.GeneralRules!,
            testingEvent.CandidateInstructions!,
            testingEvent.TesterInstructions!,
            testingEvent.ProjectApplicationSchema,
            testingEvent.TesterRegistrationSchema,
            testingEvent.ConfigurationFrozenAt);
    }
    private static IReadOnlyList<DayOfWeek>? ParseRecurrenceDaysOfWeek(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => Enum.TryParse<DayOfWeek>(item, out var day) ? day : (DayOfWeek?)null)
            .Where(day => day.HasValue)
            .Select(day => day!.Value)
            .ToArray();
    }

    private static TestingEventSlotProjection ToProjection(TestingEventSlot slot) => new(
        slot.Id,
        slot.EventId,
        slot.LocationId,
        slot.Mode,
        slot.StartsAt,
        slot.EndsAt,
        slot.MaxTesters,
        slot.MaxProjects,
        slot.CampusName,
        slot.RoomName,
        slot.MeetingUrl,
        0,
        0);


    private static TestingEventCommitteeMemberProjection ToProjection(TestingCommitteeMember member, User user) => new(
        member.Id,
        member.EventId,
        member.UserId,
        user.Name,
        user.Email,
        member.IsChair,
        member.IsActive);
    private sealed record ActorScope(Guid UserId, Guid TenantId, Error? Error);
    private sealed record ManagedEvent(TestingEvent? Event, Error? Error);
}

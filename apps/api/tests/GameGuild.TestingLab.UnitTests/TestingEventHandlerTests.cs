using System.Text.Json;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.TestingLab.UnitTests;

public sealed class TestingEventHandlerTests : IDisposable
{
    private readonly TestContext _context;
    private readonly ActorContextAccessor _actorAccessor = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();

    public TestingEventHandlerTests()
    {
        _context = new TestContext(new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase($"testing-event-handlers-{Guid.NewGuid():N}")
            .Options);
        AddActor(_managerId, TenantRole.Owner);
        SetActor(_managerId);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateEvent_UsesAuthenticatedActorAsTenantManager()
    {
        var handler = CreateEventHandler();

        var result = await handler.Handle(new CreateTestingEventCommand(
            "Campus showcase",
            "Student projects",
            TestingEventMode.InPerson,
            TestingEventApprovalMode.ManagerOnly,
            SystemClock.UtcNow.AddDays(-1),
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(3),
            true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ManagerUserId.Should().Be(_managerId);
        result.Value.TenantId.Should().Be(_tenantId);
        (await _context.Set<TestingEvent>().SingleAsync()).ManagerUserId.Should().Be(_managerId);
    }

    [Fact]
    public async Task CreateEvent_FromTemplateCopiesSelectedRevisionAndDefaults()
    {
        var projectSchema = BasicSchema("Project");
        var testerSchema = BasicSchema("Tester");
        var template = TestingEventTemplate.Create(
            _tenantId,
            "Playtest template",
            "Rules",
            "Candidate instructions",
            "Tester instructions",
            projectSchema,
            testerSchema,
            TestingEventMode.Hybrid,
            TestingEventApprovalMode.Committee,
            false,
            _managerId);
        _context.Add(template);
        await _context.SaveChangesAsync();

        var result = await CreateEventHandler().Handle(new CreateTestingEventCommand(
            "Templated event",
            null,
            TestingEventMode.Online,
            TestingEventApprovalMode.ManagerOnly,
            SystemClock.UtcNow.AddDays(-1),
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(3),
            true,
            TemplateRevisionId: template.CurrentRevision.Id), default);

        result.IsSuccess.Should().BeTrue();
        var testingEvent = await _context.Set<TestingEvent>().SingleAsync();
        testingEvent.SourceTemplateId.Should().Be(template.Id);
        testingEvent.SourceTemplateRevisionId.Should().Be(template.CurrentRevision.Id);
        testingEvent.Mode.Should().Be(TestingEventMode.Hybrid);
        testingEvent.ApprovalMode.Should().Be(TestingEventApprovalMode.Committee);
        testingEvent.RequiresFeedback.Should().BeFalse();
        testingEvent.GeneralRules.Should().Be("Rules");
    }

    [Fact]
    public async Task CreateEvent_WithCustomConfiguration_PersistsAReadyToOpenDraftAtomically()
    {
        var configuration = new ConfigureTestingEventRequest(
            "Respect the code of conduct.",
            "Provide a playable build.",
            "Complete the assigned tasks.",
            BasicSchema("Project"),
            BasicSchema("Tester"));

        var result = await CreateEventHandler().Handle(new CreateTestingEventCommand(
            "Configured event",
            null,
            TestingEventMode.Online,
            TestingEventApprovalMode.ManagerOnly,
            SystemClock.UtcNow.AddDays(-1),
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(3),
            true,
            Configuration: configuration), default);

        result.IsSuccess.Should().BeTrue();
        var testingEvent = await _context.Set<TestingEvent>().SingleAsync();
        testingEvent.GeneralRules.Should().Be(configuration.GeneralRules);
        testingEvent.CandidateInstructions.Should().Be(configuration.CandidateInstructions);
        testingEvent.TesterInstructions.Should().Be(configuration.TesterInstructions);
        testingEvent.ProjectApplicationSchema.Should().NotBeNull();
        testingEvent.TesterRegistrationSchema.Should().NotBeNull();
    }

    [Fact]
    public async Task EventConfiguration_IsRequiredForOpeningAndCannotChangeAfterFreeze()
    {
        var created = await CreateEventHandler().Handle(new CreateTestingEventCommand(
            "Configurable event",
            null,
            TestingEventMode.Online,
            TestingEventApprovalMode.ManagerOnly,
            SystemClock.UtcNow.AddDays(-1),
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(3),
            true), default);
        var handler = CreateEventHandler();

        var rejectedOpen = await handler.Handle(new OpenTestingEventApplicationsCommand(created.Value.Id), default);
        var configured = await handler.Handle(new ConfigureTestingEventCommand(
            created.Value.Id,
            "Rules",
            "Candidate instructions",
            "Tester instructions",
            BasicSchema("Project"),
            BasicSchema("Tester")), default);
        var opened = await handler.Handle(new OpenTestingEventApplicationsCommand(created.Value.Id), default);
        var rejectedEdit = await handler.Handle(new ConfigureTestingEventCommand(
            created.Value.Id,
            "Changed rules",
            "Candidate instructions",
            "Tester instructions",
            BasicSchema("Project"),
            BasicSchema("Tester")), default);

        rejectedOpen.IsFailure.Should().BeTrue();
        configured.IsSuccess.Should().BeTrue();
        opened.IsSuccess.Should().BeTrue();
        rejectedEdit.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateEvent_OmitsNullRecurrenceFrequencyFromJsonContract()
    {
        var result = await CreateEventHandler().Handle(new CreateTestingEventCommand(
            "One-off playtest",
            null,
            TestingEventMode.Online,
            TestingEventApprovalMode.ManagerOnly,
            SystemClock.UtcNow.AddDays(-1),
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(2).AddHours(2),
            true), default);

        result.IsSuccess.Should().BeTrue();
        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().NotContain("\"recurrenceFrequency\":");
    }

    [Fact]
    public async Task CreateEvent_ExpandsWeeklyRecurrenceIntoConcreteOccurrences()
    {
        var handler = CreateEventHandler();
        var startsAt = new DateTime(2026, 8, 3, 18, 0, 0, DateTimeKind.Utc);

        var result = await handler.Handle(new CreateTestingEventCommand(
            "Weekly playtest",
            null,
            TestingEventMode.Online,
            TestingEventApprovalMode.ManagerOnly,
            startsAt.AddDays(-14),
            startsAt.AddDays(-1),
            startsAt,
            startsAt.AddHours(2),
            true,
            new TestingEventRecurrenceRequest(
                TestingEventRecurrenceFrequency.Weekly,
                1,
                [DayOfWeek.Monday],
                null,
                3)), default);

        result.IsSuccess.Should().BeTrue();
        var events = await _context.Set<TestingEvent>().OrderBy(item => item.StartsAt).ToListAsync();
        events.Should().HaveCount(3);
        events.Select(item => item.StartsAt).Should().Equal(startsAt, startsAt.AddDays(7), startsAt.AddDays(14));
        events[0].RecurrenceSeriesId.Should().NotBeNull();
        events.Should().OnlyContain(item => item.RecurrenceSeriesId == events[0].RecurrenceSeriesId);
        events.Select(item => item.RecurrenceOccurrence).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task CreateEvent_KeepsRecurringLocalTimeAcrossDaylightSavingChanges()
    {
        var handler = CreateEventHandler();
        var startsAt = new DateTime(2026, 3, 1, 15, 0, 0, DateTimeKind.Utc);

        var result = await handler.Handle(new CreateTestingEventCommand(
            "Weekly playtest",
            null,
            TestingEventMode.Online,
            TestingEventApprovalMode.ManagerOnly,
            startsAt.AddDays(-14),
            startsAt.AddDays(-1),
            startsAt,
            startsAt.AddHours(2),
            true,
            new TestingEventRecurrenceRequest(
                TestingEventRecurrenceFrequency.Weekly,
                1,
                [DayOfWeek.Sunday],
                null,
                3),
            TimeZoneId: "America/New_York"), default);

        result.IsSuccess.Should().BeTrue();
        var events = await _context.Set<TestingEvent>()
            .OrderBy(item => item.StartsAt)
            .ToListAsync();
        events.Select(item => item.StartsAt).Should().Equal(
            startsAt,
            new DateTime(2026, 3, 8, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 15, 14, 0, 0, DateTimeKind.Utc));
        events.Should().OnlyContain(item => item.TimeZoneId == "America/New_York");
    }

    [Fact]
    public async Task SubmitApplication_DoesNotConsumeSlotBeforeApproval()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var slot = AddSlot(testingEvent.Id, maxProjects: 1);
        var project = AddProject(_managerId);
        var projectVersion = AddProjectVersion(project, _managerId);
        await _context.SaveChangesAsync();

        var result = await CreateApplicationHandler().Handle(
            CompleteApplication(testingEvent.Id, project.Id, projectVersion.Id, "Evening"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TestingApplicationStatus.Pending);
        result.Value.AssignedSlotId.Should().BeNull();
        slot.MaxProjects.Should().Be(1);
    }

    [Fact]
    public async Task ApplicationWizard_CreatesSavesAndSubmitsPrivateDraft()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var version = AddProjectVersion(project, _managerId);
        await _context.SaveChangesAsync();
        var handler = CreateApplicationHandler();

        var created = await handler.Handle(
            new CreateTestingProjectApplicationDraftCommand(testingEvent.Id, project.Id), default);
        var saved = await handler.Handle(new SaveTestingProjectApplicationDraftCommand(
            created.Value.Id,
            version.Id,
            ValidBrief(),
            new QuestionnaireSchema("Developer feedback", [
                new QuestionnaireQuestion("fun", "What was fun?", QuestionnaireQuestionType.FreeText, true)
            ]),
            new QuestionnaireResponse([]),
            true,
            "Evening",
            []), default);
        var submitted = await handler.Handle(
            new SubmitTestingProjectApplicationDraftCommand(created.Value.Id), default);

        created.Value.Status.Should().Be(TestingApplicationStatus.Draft);
        saved.Value.Status.Should().Be(TestingApplicationStatus.Draft);
        saved.Value.CurrentQuestionnaireRevisionId.Should().NotBeNull();
        submitted.Value.Status.Should().Be(TestingApplicationStatus.Pending);
        (await handler.Handle(new GetTestingEventApplicationsQuery(testingEvent.Id), default))
            .Value.Should().ContainSingle(application => application.Id == created.Value.Id);
    }

    [Fact]
    public async Task ApplicationWizard_SavesPartialDraftWithoutMakingItSubmittable()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var version = AddProjectVersion(project, _managerId);
        await _context.SaveChangesAsync();
        var handler = CreateApplicationHandler();
        var created = await handler.Handle(
            new CreateTestingProjectApplicationDraftCommand(testingEvent.Id, project.Id), default);

        var saved = await handler.Handle(new SaveTestingProjectApplicationDraftCommand(
            created.Value.Id,
            version.Id,
            null,
            null,
            null,
            null,
            "Evening",
            []), default);
        var submitted = await handler.Handle(
            new SubmitTestingProjectApplicationDraftCommand(created.Value.Id), default);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Status.Should().Be(TestingApplicationStatus.Draft);
        saved.Value.ProjectVersionId.Should().Be(version.Id);
        saved.Value.Brief.Should().BeNull();
        saved.Value.CurrentQuestionnaireRevisionId.Should().BeNull();
        submitted.IsFailure.Should().BeTrue();
        (await _context.Set<TestingProjectApplication>().SingleAsync()).Status.Should().Be(TestingApplicationStatus.Draft);
    }

    [Fact]
    public async Task ApplicationWizard_ReturnsValidationWhenBriefIsMissingButQuestionnaireExists()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var version = AddProjectVersion(project, _managerId);
        await _context.SaveChangesAsync();
        var handler = CreateApplicationHandler();
        var created = await handler.Handle(
            new CreateTestingProjectApplicationDraftCommand(testingEvent.Id, project.Id), default);
        await handler.Handle(new SaveTestingProjectApplicationDraftCommand(
            created.Value.Id,
            version.Id,
            null,
            new QuestionnaireSchema("Developer feedback", [
                new QuestionnaireQuestion("fun", "What was fun?", QuestionnaireQuestionType.FreeText, true)
            ]),
            new QuestionnaireResponse([]),
            true,
            null,
            []), default);

        var submitted = await handler.Handle(
            new SubmitTestingProjectApplicationDraftCommand(created.Value.Id), default);

        submitted.IsFailure.Should().BeTrue();
        submitted.Error.Type.Should().Be(ErrorType.Validation);
        (await _context.Set<TestingProjectApplication>().SingleAsync()).Status.Should().Be(TestingApplicationStatus.Draft);
    }

    [Fact]
    public async Task ApplicationWizard_RequiresDeveloperQuestionWhenEventRequiresFeedback()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var version = AddProjectVersion(project, _managerId);
        await _context.SaveChangesAsync();
        var handler = CreateApplicationHandler();
        var created = await handler.Handle(
            new CreateTestingProjectApplicationDraftCommand(testingEvent.Id, project.Id), default);
        await handler.Handle(new SaveTestingProjectApplicationDraftCommand(
            created.Value.Id,
            version.Id,
            ValidBrief(),
            new QuestionnaireSchema("Empty", []),
            new QuestionnaireResponse([]),
            true,
            null,
            []), default);

        var submitted = await handler.Handle(
            new SubmitTestingProjectApplicationDraftCommand(created.Value.Id), default);

        submitted.IsFailure.Should().BeTrue();
        (await _context.Set<TestingProjectApplication>().SingleAsync()).Status.Should().Be(TestingApplicationStatus.Draft);
    }

    [Fact]
    public async Task SubmitApplication_RejectsDraftVersionUnderDefaultPolicy()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var draftVersion = AddProjectVersion(project, _managerId, ProjectVersionStatus.Draft);
        await _context.SaveChangesAsync();

        var result = await CreateApplicationHandler().Handle(
            new SubmitTestingProjectApplicationCommand(testingEvent.Id, project.Id, draftVersion.Id, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task SubmitApplication_ReleasedImmutablePolicyRequiresReleasedVersion()
    {
        _context.Add(new TestingLabSettings
        {
            TenantId = _tenantId,
            VersionSubmissionPolicy = VersionSubmissionPolicy.ReleasedImmutable
        });
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var readyVersion = AddProjectVersion(project, _managerId);
        await _context.SaveChangesAsync();

        var result = await CreateApplicationHandler().Handle(
            new SubmitTestingProjectApplicationCommand(testingEvent.Id, project.Id, readyVersion.Id, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task UpdateApplication_ReleasedImmutableSnapshotPreventsVersionReplacement()
    {
        _context.Add(new TestingLabSettings
        {
            TenantId = _tenantId,
            VersionSubmissionPolicy = VersionSubmissionPolicy.ReleasedImmutable
        });
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var submittedVersion = AddProjectVersion(project, _managerId, ProjectVersionStatus.Released, "1.0.0");
        var replacementVersion = AddProjectVersion(project, _managerId, ProjectVersionStatus.Released, "1.0.1");
        await _context.SaveChangesAsync();
        var handler = CreateApplicationHandler();
        var submitted = await handler.Handle(
            CompleteApplication(testingEvent.Id, project.Id, submittedVersion.Id, null),
            default);

        var result = await handler.Handle(
            new UpdateTestingProjectApplicationCommand(submitted.Value.Id, replacementVersion.Id, null),
            default);

        result.IsFailure.Should().BeTrue();
        (await _context.Set<TestingProjectApplication>().SingleAsync()).ProjectVersionId.Should().Be(submittedVersion.Id);
    }

    [Fact]
    public async Task SubmitApplication_RequiresProjectEditPermission()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(Guid.NewGuid());
        await _context.SaveChangesAsync();

        var result = await CreateApplicationHandler().Handle(
            new SubmitTestingProjectApplicationCommand(testingEvent.Id, project.Id, Guid.NewGuid(), null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ApproveApplication_RequiresEventManager()
    {
        var applicantId = Guid.NewGuid();
        AddActor(applicantId, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var slot = AddSlot(testingEvent.Id, 2);
        var project = AddProject(applicantId);
        var application = TestingProjectApplication.Submit(
            testingEvent.Id, project.Id, null, applicantId, null, _tenantId);
        _context.Add(application);
        await _context.SaveChangesAsync();
        SetActor(applicantId);

        var result = await CreateApplicationHandler().Handle(
            new ApproveTestingProjectApplicationCommand(application.Id, slot.Id, "Accepted"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task CommitteeApproval_RequiresMajority()
    {
        var reviewerOne = Guid.NewGuid();
        var reviewerTwo = Guid.NewGuid();
        var reviewerThree = Guid.NewGuid();
        AddActor(reviewerOne, TenantRole.Member);
        AddActor(reviewerTwo, TenantRole.Member);
        AddActor(reviewerThree, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.Committee);
        _context.AddRange(
            TestingCommitteeMember.Create(testingEvent.Id, reviewerOne, false, _tenantId),
            TestingCommitteeMember.Create(testingEvent.Id, reviewerTwo, false, _tenantId),
            TestingCommitteeMember.Create(testingEvent.Id, reviewerThree, false, _tenantId));
        var slot = AddSlot(testingEvent.Id, 2);
        var project = AddProject(_managerId);
        var application = TestingProjectApplication.Submit(testingEvent.Id, project.Id, null, _managerId, null, _tenantId);
        application.BeginReview();
        _context.Add(application);
        await _context.SaveChangesAsync();
        var handler = CreateApplicationHandler();

        SetActor(reviewerOne);
        (await handler.Handle(new CastTestingApplicationVoteCommand(
            application.Id, TestingApplicationVoteDecision.Approve, "Ready"), default)).IsSuccess.Should().BeTrue();
        SetActor(_managerId);
        var beforeMajority = await handler.Handle(
            new ApproveTestingProjectApplicationCommand(application.Id, slot.Id, "Committee review"), default);
        SetActor(reviewerTwo);
        (await handler.Handle(new CastTestingApplicationVoteCommand(
            application.Id, TestingApplicationVoteDecision.Approve, "Ready"), default)).IsSuccess.Should().BeTrue();
        SetActor(_managerId);
        var afterMajority = await handler.Handle(
            new ApproveTestingProjectApplicationCommand(application.Id, slot.Id, "Committee approved"), default);

        beforeMajority.IsFailure.Should().BeTrue();
        beforeMajority.Error.Type.Should().Be(ErrorType.Validation);
        afterMajority.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ApplicationAccess_SeparatesCommitteeVotingFromManagerDecisions()
    {
        var reviewerId = Guid.NewGuid();
        AddActor(reviewerId, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.Committee);
        var project = AddProject(_managerId);
        var application = TestingProjectApplication.Submit(testingEvent.Id, project.Id, null, _managerId, null, _tenantId);
        application.BeginReview();
        _context.AddRange(
            TestingCommitteeMember.Create(testingEvent.Id, reviewerId, false, _tenantId),
            application);
        await _context.SaveChangesAsync();

        var handler = CreateApplicationHandler();

        SetActor(reviewerId);
        var reviewerAccess = await handler.Handle(new GetTestingEventApplicationAccessQuery(testingEvent.Id), default);
        var reviewerEvent = await CreateEventHandler().Handle(new GetTestingEventQuery(testingEvent.Id), default);

        SetActor(_managerId);
        var managerAccess = await handler.Handle(new GetTestingEventApplicationAccessQuery(testingEvent.Id), default);

        reviewerAccess.IsSuccess.Should().BeTrue();
        reviewerAccess.Value.CanViewApplications.Should().BeTrue();
        reviewerAccess.Value.CanManageApplications.Should().BeFalse();
        reviewerAccess.Value.CanVote.Should().BeTrue();
        reviewerEvent.IsSuccess.Should().BeTrue();
        managerAccess.IsSuccess.Should().BeTrue();
        managerAccess.Value.CanManageApplications.Should().BeTrue();
        managerAccess.Value.CanVote.Should().BeFalse();
    }

    [Fact]
    public async Task CommitteeTie_AllowsManagerTieBreakAfterAllVotes()
    {
        var reviewerOne = Guid.NewGuid();
        var reviewerTwo = Guid.NewGuid();
        AddActor(reviewerOne, TenantRole.Member);
        AddActor(reviewerTwo, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.Committee);
        _context.AddRange(
            TestingCommitteeMember.Create(testingEvent.Id, reviewerOne, false, _tenantId),
            TestingCommitteeMember.Create(testingEvent.Id, reviewerTwo, false, _tenantId));
        var slot = AddSlot(testingEvent.Id, 2);
        var project = AddProject(_managerId);
        var application = TestingProjectApplication.Submit(testingEvent.Id, project.Id, null, _managerId, null, _tenantId);
        application.BeginReview();
        _context.Add(application);
        await _context.SaveChangesAsync();
        var handler = CreateApplicationHandler();

        SetActor(reviewerOne);
        await handler.Handle(new CastTestingApplicationVoteCommand(
            application.Id, TestingApplicationVoteDecision.Approve, null), default);
        SetActor(reviewerTwo);
        await handler.Handle(new CastTestingApplicationVoteCommand(
            application.Id, TestingApplicationVoteDecision.Reject, "Needs work"), default);
        SetActor(_managerId);
        var result = await handler.Handle(
            new ApproveTestingProjectApplicationCommand(application.Id, slot.Id, "Manager tie-break"), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RejectApplication_RequiresRationale()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var application = TestingProjectApplication.Submit(testingEvent.Id, project.Id, null, _managerId, null, _tenantId);
        _context.Add(application);
        await _context.SaveChangesAsync();

        var result = await CreateApplicationHandler().Handle(
            new RejectTestingProjectApplicationCommand(application.Id, " "), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task ApproveApplication_RejectsFullProjectSlot()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var slot = AddSlot(testingEvent.Id, 1);
        var firstProject = AddProject(_managerId);
        var secondProject = AddProject(_managerId);
        var first = TestingProjectApplication.Submit(testingEvent.Id, firstProject.Id, null, _managerId, null, _tenantId);
        first.Approve(_managerId, slot.Id, "First");
        var second = TestingProjectApplication.Submit(testingEvent.Id, secondProject.Id, null, _managerId, null, _tenantId);
        _context.AddRange(first, second);
        await _context.SaveChangesAsync();

        var result = await CreateApplicationHandler().Handle(
            new ApproveTestingProjectApplicationCommand(second.Id, slot.Id, "Second"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        second.AssignedSlotId.Should().BeNull();
    }

    [Fact]
    public async Task EventQueries_AreTenantScoped()
    {
        var visible = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var hidden = TestingEvent.Create(
            "Other tenant",
            TestingEventMode.Online,
            _managerId,
            SystemClock.UtcNow.AddDays(-2),
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(3),
            false,
            TestingEventApprovalMode.ManagerOnly,
            Guid.NewGuid());
        _context.Add(hidden);
        await _context.SaveChangesAsync();

        var result = await CreateEventHandler().Handle(new GetTestingEventsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Id.Should().Be(visible.Id);
    }

    [Fact]
    public async Task RequestDetailQuery_ReturnsStableProjectionWhenProjectVersionIsMissing()
    {
        var testingRequest = new TestingRequest
        {
            TenantId = _tenantId,
            CreatedById = _managerId,
            Title = "No version linked",
            StartDate = SystemClock.UtcNow,
            EndDate = SystemClock.UtcNow.AddDays(1),
            Status = TestingRequestStatus.Open,
            InstructionsType = InstructionType.Text,
        };
        _context.Add(testingRequest);
        await _context.SaveChangesAsync();

        var result = await new TestingRequestDetailQueryHandler(_context, _actorAccessor)
            .Handle(new GetTestingRequestDetailQuery(testingRequest.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(testingRequest.Id);
        result.Value.ProjectVersion.Should().BeNull();
    }

    [Fact]
    public async Task RequestDetailQuery_ReturnsArchivedRequestAsReadOnlyProjection()
    {
        var testingRequest = new TestingRequest
        {
            TenantId = _tenantId,
            CreatedById = _managerId,
            Title = "Archived request",
            StartDate = SystemClock.UtcNow,
            EndDate = SystemClock.UtcNow.AddDays(1),
            Status = TestingRequestStatus.Completed,
            InstructionsType = InstructionType.Text,
            Version = 1,
        };
        testingRequest.SoftDelete();
        _context.Add(testingRequest);
        await _context.SaveChangesAsync();

        var result = await new TestingRequestDetailQueryHandler(_context, _actorAccessor)
            .Handle(new GetTestingRequestDetailQuery(testingRequest.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(testingRequest.Id);
        result.Value.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAndListSlots_UsesEventScheduleAndTenant()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        await _context.SaveChangesAsync();
        var handler = CreateEventHandler();

        var created = await handler.Handle(new CreateTestingEventSlotCommand(
            testingEvent.Id,
            TestingEventMode.Online,
            testingEvent.StartsAt,
            testingEvent.StartsAt.AddHours(2),
            null,
            4,
            null,
            null,
            "https://meet.example.com/session"), default);
        var listed = await handler.Handle(new GetTestingEventSlotsQuery(testingEvent.Id), default);

        created.IsSuccess.Should().BeTrue();
        listed.IsSuccess.Should().BeTrue();
        listed.Value.Should().ContainSingle().Which.Id.Should().Be(created.Value.Id);
    }

    [Fact]
    public async Task UpdateEvent_RequiresEventManager()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var memberId = Guid.NewGuid();
        AddActor(memberId, TenantRole.Member);
        await _context.SaveChangesAsync();
        SetActor(memberId);

        var result = await CreateEventHandler().Handle(new UpdateTestingEventCommand(
            testingEvent.Id,
            testingEvent.Name,
            testingEvent.Description,
            testingEvent.Mode,
            testingEvent.ApprovalMode,
            testingEvent.ApplicationsOpenAt,
            testingEvent.ApplicationsCloseAt,
            testingEvent.StartsAt,
            testingEvent.EndsAt,
            testingEvent.RequiresFeedback), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Theory]
    [InlineData("SystemAdmin")]
    [InlineData("TenantAdmin")]
    public async Task Administrators_CanReviewAndManageAnEventOwnedByAnotherMember(string role)
    {
        var systemAdminId = Guid.NewGuid();
        AddActor(systemAdminId, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        await _context.SaveChangesAsync();
        SetActor(systemAdminId, role);

        var applications = await CreateApplicationHandler().Handle(
            new GetTestingEventApplicationsQuery(testingEvent.Id),
            default);
        var committee = await CreateEventHandler().Handle(
            new GetTestingEventCommitteeQuery(testingEvent.Id),
            default);
        var update = await CreateEventHandler().Handle(new UpdateTestingEventCommand(
            testingEvent.Id,
            testingEvent.Name,
            testingEvent.Description,
            testingEvent.Mode,
            testingEvent.ApprovalMode,
            testingEvent.ApplicationsOpenAt,
            testingEvent.ApplicationsCloseAt,
            testingEvent.StartsAt,
            testingEvent.EndsAt,
            testingEvent.RequiresFeedback), default);

        applications.IsSuccess.Should().BeTrue();
        committee.IsSuccess.Should().BeTrue();
        update.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteEvent_RejectsEventAfterApplicationsOpen()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        await _context.SaveChangesAsync();

        var result = await CreateEventHandler().Handle(new DeleteTestingEventCommand(testingEvent.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task ArchiveEvent_HidesCompletedEventFromActiveDirectoryAndListsItAsArchived()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        await _context.SaveChangesAsync();
        var handler = CreateEventHandler();
        await handler.Handle(new CloseTestingEventApplicationsCommand(testingEvent.Id), default);
        await handler.Handle(new ScheduleTestingEventCommand(testingEvent.Id), default);
        await handler.Handle(new ActivateTestingEventCommand(testingEvent.Id), default);
        await handler.Handle(new CompleteTestingEventCommand(testingEvent.Id), default);

        var archived = await handler.Handle(new ArchiveTestingEventCommand(testingEvent.Id), default);
        var activeDirectory = await handler.Handle(new GetTestingEventsQuery(), default);
        var archivedDirectory = await handler.Handle(new GetArchivedTestingEventsQuery(), default);

        archived.IsSuccess.Should().BeTrue();
        testingEvent.DeletedAt.Should().NotBeNull();
        activeDirectory.Value.Should().BeEmpty();
        archivedDirectory.Value.Should().ContainSingle().Which.Id.Should().Be(testingEvent.Id);
    }

    [Fact]
    public async Task RestoreEvent_ReactivatesArchivedTerminalEvent()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        await _context.SaveChangesAsync();
        var handler = CreateEventHandler();
        await handler.Handle(new CancelTestingEventCommand(testingEvent.Id, "QA cleanup"), default);
        await handler.Handle(new ArchiveTestingEventCommand(testingEvent.Id), default);

        var restored = await handler.Handle(new RestoreTestingEventCommand(testingEvent.Id), default);
        var activeDirectory = await handler.Handle(new GetTestingEventsQuery(), default);

        restored.IsSuccess.Should().BeTrue();
        testingEvent.DeletedAt.Should().BeNull();
        activeDirectory.Value.Should().ContainSingle().Which.Id.Should().Be(testingEvent.Id);
    }

    [Fact]
    public async Task ArchiveEvent_RejectsNonTerminalEvent()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        await _context.SaveChangesAsync();

        var result = await CreateEventHandler().Handle(new ArchiveTestingEventCommand(testingEvent.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        testingEvent.DeletedAt.Should().BeNull();
    }
    [Fact]
    public async Task EventLifecycle_AdvancesThroughScheduledActiveAndCompleted()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        await _context.SaveChangesAsync();
        var handler = CreateEventHandler();

        (await handler.Handle(new CloseTestingEventApplicationsCommand(testingEvent.Id), default))
            .Value.Status.Should().Be(TestingEventStatus.ApplicationsClosed);
        (await handler.Handle(new ScheduleTestingEventCommand(testingEvent.Id), default))
            .Value.Status.Should().Be(TestingEventStatus.Scheduled);
        (await handler.Handle(new ActivateTestingEventCommand(testingEvent.Id), default))
            .Value.Status.Should().Be(TestingEventStatus.Active);
        (await handler.Handle(new CompleteTestingEventCommand(testingEvent.Id), default))
            .Value.Status.Should().Be(TestingEventStatus.Completed);
    }

    [Fact]
    public async Task CancelEvent_RejectsCompletedEvents()
    {
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        await _context.SaveChangesAsync();
        var handler = CreateEventHandler();
        await handler.Handle(new CloseTestingEventApplicationsCommand(testingEvent.Id), default);
        await handler.Handle(new ScheduleTestingEventCommand(testingEvent.Id), default);
        await handler.Handle(new ActivateTestingEventCommand(testingEvent.Id), default);
        await handler.Handle(new CompleteTestingEventCommand(testingEvent.Id), default);

        var result = await handler.Handle(new CancelTestingEventCommand(testingEvent.Id, "Too late"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CommitteeManagement_AddsListsAndRemovesActiveTenantMember()
    {
        var reviewerId = Guid.NewGuid();
        AddActor(reviewerId, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.Committee);
        await _context.SaveChangesAsync();
        var handler = CreateEventHandler();

        var added = await handler.Handle(
            new AddTestingEventCommitteeMemberCommand(testingEvent.Id, reviewerId, true),
            default);
        var listed = await handler.Handle(new GetTestingEventCommitteeQuery(testingEvent.Id), default);
        var removed = await handler.Handle(
            new RemoveTestingEventCommitteeMemberCommand(testingEvent.Id, reviewerId),
            default);

        added.IsSuccess.Should().BeTrue();
        added.Value.UserId.Should().Be(reviewerId);
        added.Value.IsChair.Should().BeTrue();
        listed.IsSuccess.Should().BeTrue();
        listed.Value.Should().ContainSingle(member => member.UserId == reviewerId);
        removed.IsSuccess.Should().BeTrue();
        (await _context.Set<TestingCommitteeMember>().SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CommitteeManagement_RejectsDuplicateActiveMember()
    {
        var reviewerId = Guid.NewGuid();
        AddActor(reviewerId, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.Committee);
        _context.Add(TestingCommitteeMember.Create(testingEvent.Id, reviewerId, false, _tenantId));
        await _context.SaveChangesAsync();

        var result = await CreateEventHandler().Handle(
            new AddTestingEventCommitteeMemberCommand(testingEvent.Id, reviewerId, false),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }


    [Fact]
    public async Task GetMyApplications_ReturnsApplicationsOwnedByAccessibleProjects_NotBySubmitter()
    {
        var applicantId = Guid.NewGuid();
        AddActor(applicantId, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var managerProject = AddProject(_managerId);
        var otherProject = AddProject(applicantId);
        _context.AddRange(
            TestingProjectApplication.Submit(testingEvent.Id, managerProject.Id, null, applicantId, null, _tenantId),
            TestingProjectApplication.Submit(testingEvent.Id, otherProject.Id, null, _managerId, null, _tenantId));
        await _context.SaveChangesAsync();
        SetActor(_managerId);

        var result = await CreateApplicationHandler().Handle(
            new GetMyTestingProjectApplicationsQuery(testingEvent.Id),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.ProjectId.Should().Be(managerProject.Id);
        result.Value.Single().SubmittedByUserId.Should().Be(applicantId);
    }

    [Fact]
    public async Task SystemAdmin_CannotManageTestingLabWithoutActiveSelectedTenantMembership()
    {
        var systemAdminId = Guid.NewGuid();
        _context.Add(new User
        {
            Id = systemAdminId,
            Email = $"{systemAdminId:N}@example.com",
            Name = "Stale system admin",
            IsActive = true
        });
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        await _context.SaveChangesAsync();
        SetActor(systemAdminId, "SystemAdmin");

        var result = await CreateEventHandler().Handle(new UpdateTestingEventCommand(
            testingEvent.Id,
            testingEvent.Name,
            testingEvent.Description,
            testingEvent.Mode,
            testingEvent.ApprovalMode,
            testingEvent.ApplicationsOpenAt,
            testingEvent.ApplicationsCloseAt,
            testingEvent.StartsAt,
            testingEvent.EndsAt,
            testingEvent.RequiresFeedback), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task WithdrawApplication_AllowsAnotherAuthorizedProjectMember()
    {
        var applicantId = Guid.NewGuid();
        AddActor(applicantId, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var application = TestingProjectApplication.Submit(
            testingEvent.Id, project.Id, null, applicantId, null, _tenantId);
        _context.Add(application);
        await _context.SaveChangesAsync();
        SetActor(_managerId);

        var result = await CreateApplicationHandler().Handle(
            new WithdrawTestingProjectApplicationCommand(application.Id),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TestingApplicationStatus.Withdrawn);
    }

    [Fact]
    public async Task UpdateApplication_AllowsAnotherAuthorizedProjectMember_WithoutChangingOwnership()
    {
        var originalSubmitterId = Guid.NewGuid();
        AddActor(originalSubmitterId, TenantRole.Member);
        var testingEvent = AddOpenEvent(TestingEventApprovalMode.ManagerOnly);
        var project = AddProject(_managerId);
        var firstVersion = AddProjectVersion(project, originalSubmitterId);
        var replacementVersion = AddProjectVersion(project, _managerId);
        var application = TestingProjectApplication.Submit(
            testingEvent.Id,
            project.Id,
            firstVersion.Id,
            originalSubmitterId,
            "Evening",
            _tenantId);
        _context.Add(application);
        await _context.SaveChangesAsync();
        SetActor(_managerId);

        var result = await CreateApplicationHandler().Handle(
            new UpdateTestingProjectApplicationCommand(
                application.Id,
                replacementVersion.Id,
                "Weekend",
                []),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectVersionId.Should().Be(replacementVersion.Id);
        result.Value.PreferredAvailability.Should().Be("Weekend");
        result.Value.SubmittedByUserId.Should().Be(originalSubmitterId);
    }
    private TestingEventHandlers CreateEventHandler() => new(_context, _actorAccessor);

    private TestingApplicationHandlers CreateApplicationHandler() => new(
        _context,
        _actorAccessor,
        new ProjectAuthorizationService(_context, _actorAccessor),
        NullLogger<TestingApplicationHandlers>.Instance);

    private TestingEvent AddOpenEvent(TestingEventApprovalMode approvalMode)
    {
        var testingEvent = TestingEvent.Create(
            $"Event {Guid.NewGuid():N}",
            TestingEventMode.Online,
            _managerId,
            SystemClock.UtcNow.AddDays(-2),
            SystemClock.UtcNow.AddDays(1),
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(3),
            true,
            approvalMode,
            _tenantId);
        testingEvent.OpenConfiguredApplications();
        _context.Add(testingEvent);
        return testingEvent;
    }

    private TestingEventSlot AddSlot(Guid eventId, int? maxProjects)
    {
        var slot = TestingEventSlot.Create(
            eventId,
            TestingEventMode.Online,
            SystemClock.UtcNow.AddDays(2),
            SystemClock.UtcNow.AddDays(2).AddHours(2),
            null,
            maxProjects,
            null,
            null,
            "https://meet.example.com/testing",
            _tenantId);
        _context.Add(slot);
        return slot;
    }

    private Project AddProject(Guid ownerId)
    {
        var project = new Project
        {
            TenantId = _tenantId,
            Title = $"Project {Guid.NewGuid():N}",
            Slug = $"project-{Guid.NewGuid():N}",
            Status = ContentStatus.Draft,
            Visibility = ContentVisibility.Private,
            CreatedById = ownerId
        };
        _context.Add(project);
        _context.Add(new ProjectCollaborator
        {
            Project = project,
            UserId = ownerId,
            Role = ProjectRoles.Owner,
            IsActive = true
        });
        return project;
    }

    private ProjectVersion AddProjectVersion(
        Project project,
        Guid createdById,
        ProjectVersionStatus status = ProjectVersionStatus.ReadyForTesting,
        string versionNumber = "1.0.0")
    {
        var version = ProjectVersion.Create(project.Id, versionNumber, null, createdById, _tenantId);
        version.Project = project;
        if (status is ProjectVersionStatus.ReadyForTesting or ProjectVersionStatus.Released or ProjectVersionStatus.Archived)
            version.MarkReadyForTesting();
        if (status is ProjectVersionStatus.Released or ProjectVersionStatus.Archived)
            version.Release();
        if (status == ProjectVersionStatus.Archived)
            version.Archive();
        _context.Add(version);
        return version;
    }

    private static QuestionnaireSchema BasicSchema(string title) => new(
        title,
        [new QuestionnaireQuestion("notes", "Notes", QuestionnaireQuestionType.FreeText, false)]);

    private static TestingProjectBrief ValidBrief() => new(
        "Find usability blockers",
        "Download and sign in",
        ["Complete the tutorial"],
        "Keyboard and mouse",
        "Matchmaking may be slow");

    private static SubmitTestingProjectApplicationCommand CompleteApplication(
        Guid eventId,
        Guid projectId,
        Guid projectVersionId,
        string? preferredAvailability) => new(
        eventId,
        projectId,
        projectVersionId,
        preferredAvailability,
        [],
        ValidBrief(),
        new QuestionnaireSchema("Developer feedback", [
            new QuestionnaireQuestion("experience", "How was the experience?", QuestionnaireQuestionType.FreeText, true)
        ]),
        new QuestionnaireResponse([]),
        true);

    private void AddActor(Guid userId, string role)
    {
        _context.Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@example.com",
            Name = "Testing Lab actor",
            IsActive = true
        });
        _context.Add(new TenantMember
        {
            UserId = userId,
            TenantId = _tenantId,
            Role = role,
            IsActive = true
        });
    }

    private void SetActor(Guid userId, string? role = null)
    {
        var builder = ActorContextBuilder.ForUser(userId).WithTenantId(_tenantId);
        if (!string.IsNullOrWhiteSpace(role)) builder.WithRole(role);
        _actorAccessor.SetActorContext(builder.Build());
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options), IApplicationDbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();
        public DbSet<ProjectCollaborator> ProjectCollaborators => Set<ProjectCollaborator>();
        public DbSet<GameGuild.Identity.Authorization.ResourceUserPermission> ResourceUserPermissions => Set<GameGuild.Identity.Authorization.ResourceUserPermission>();
        public DbSet<TestingEvent> TestingEvents => Set<TestingEvent>();
        public DbSet<TestingEventSlot> TestingEventSlots => Set<TestingEventSlot>();
        public DbSet<TestingProjectApplication> TestingProjectApplications => Set<TestingProjectApplication>();
        public DbSet<TestingLabSettings> TestingLabSettings => Set<TestingLabSettings>();
        public DbSet<TestingEventTemplate> TestingEventTemplates => Set<TestingEventTemplate>();
        public DbSet<TestingEventTemplateRevision> TestingEventTemplateRevisions => Set<TestingEventTemplateRevision>();
        public DbSet<TestingQuestionnaireRevision> TestingQuestionnaireRevisions => Set<TestingQuestionnaireRevision>();
        public DbSet<TestingCommitteeMember> TestingCommitteeMembers => Set<TestingCommitteeMember>();
        public DbSet<TestingApplicationVote> TestingApplicationVotes => Set<TestingApplicationVote>();
        public DbSet<TestingSession> TestingSessions => Set<TestingSession>();
        public DbSet<TestingRequest> TestingRequests => Set<TestingRequest>();
        public DbSet<SessionRegistration> SessionRegistrations => Set<SessionRegistration>();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

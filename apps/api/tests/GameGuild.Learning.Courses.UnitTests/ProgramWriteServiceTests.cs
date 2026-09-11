using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class ProgramWriteServiceTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public async Task LearnerReflectionResponses_ShouldRespectPrivateToInstructors(bool privateToInstructors, int expectedCount)
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var learnerId = Guid.NewGuid();
        var peerId = Guid.NewGuid();
        var program = CreateProgram();
        program.TenantId = tenantId;
        var learner = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = learnerId, IsActive = true };
        var peer = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = peerId, IsActive = true };
        var reflection = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Reflection", Type = ProgramContentType.Reflection };
        reflection.SetActivitySettings(new ReflectionActivitySettings(PrivateToInstructors: privateToInstructors));
        context.AddRange(program, learner, peer, reflection,
            ReflectionResponse(learner, reflection),
            ReflectionResponse(peer, reflection));
        await context.SaveChangesAsync();
        dynamic service = new ContentInteractionService(context, new TestRequestContextAccessor(learnerId, tenantId));

        var results = (IEnumerable<object>)await service.GetVisibleReflectionResponsesAsync(program.Id, reflection.Id);

        results.Should().HaveCount(expectedCount);
        results.Should().OnlyContain(result => GetRespondentUserId(result) == null);
    }

    [Fact]
    public async Task ManagerReflectionResponses_ShouldExposeRespondentIdentityWithinTenant()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var program = CreateProgram();
        program.TenantId = tenantId;
        var reflection = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Reflection", Type = ProgramContentType.Reflection };
        var respondent = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = Guid.NewGuid(), IsActive = true };
        context.AddRange(program, reflection, respondent, ReflectionResponse(respondent, reflection));
        await context.SaveChangesAsync();
        dynamic service = new ContentInteractionService(context, new TestRequestContextAccessor(managerId, tenantId), CreatePermissions(PermissionType.Review));

        var result = ((IEnumerable<object>)await service.GetReflectionResponsesAsync(program.Id, reflection.Id)).Single();

        GetRespondentUserId(result).Should().Be(respondent.UserId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManagerActivityResponses_ShouldRejectReadOnlyAuthority(bool survey)
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var program = CreateProgram();
        program.TenantId = tenantId;
        var content = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Activity", Type = survey ? ProgramContentType.Survey : ProgramContentType.Reflection };
        if (survey) content.SetActivitySettings(new SurveyActivitySettings());
        else content.SetActivitySettings(new ReflectionActivitySettings());
        context.AddRange(program, content);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(context, new TestRequestContextAccessor(Guid.NewGuid(), tenantId), CreatePermissions(PermissionType.Read));

        Func<Task> action = survey
            ? () => service.GetSurveyResponsesAsync(program.Id, content.Id)
            : () => service.GetReflectionResponsesAsync(program.Id, content.Id);

        await action.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public async Task GetSurveyResults_WhenServiceRejectsRequestValidation_ShouldReturnBadRequest()
    {
        var programId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        var interactions = new Mock<IContentInteractionService>();
        interactions.Setup(service => service.GetSurveyResponsesAsync(programId, contentId))
            .ThrowsAsync(new RequestValidationException("Program review permission is required."));
        var contentService = new Mock<IProgramContentService>();
        contentService.Setup(service => service.GetContentByIdAsync(contentId))
            .ReturnsAsync(new ProgramContent { Id = contentId, ProgramId = programId, Type = ProgramContentType.Survey });
        var controller = new ContentInteractionController(
            interactions.Object,
            contentService.Object,
            NullLogger<ContentInteractionController>.Instance,
            Mock.Of<ISender>());

        var result = await controller.GetSurveyResults(contentId, programId);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(nameof(ContentInteractionController.GetSurveyResults))]
    [InlineData(nameof(ContentInteractionController.GetReflectionResponses))]
    public void ManagerResponseEndpoints_ShouldRequireProgramReviewPermission(string actionName)
    {
        var action = typeof(ContentInteractionController).GetMethod(actionName);

        var permission = action!.GetCustomAttributes(inherit: true)
            .OfType<IResourcePermissionMarker>()
            .Single();

        permission.ResourceType.Should().Be(typeof(Program));
        permission.RequiredPermission.Should().Be(PermissionType.Review);
        permission.ResourceIdParameterName.Should().Be("programId");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SurveyResponsePaths_ShouldRejectDifferentTenant(bool managerPath)
    {
        await using var context = CreateContext();
        var programTenantId = Guid.NewGuid();
        var requestTenantId = Guid.NewGuid();
        var learnerId = Guid.NewGuid();
        var program = CreateProgram();
        program.TenantId = programTenantId;
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = learnerId, IsActive = true };
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Survey", Type = ProgramContentType.Survey };
        survey.SetActivitySettings(new SurveyActivitySettings(ResultsVisibility: SurveyResultsVisibility.AfterSubmission));
        context.AddRange(program, enrollment, survey, new ContentInteraction { Id = Guid.NewGuid(), ProgramUserId = enrollment.Id, UserId = learnerId, ContentId = survey.Id, SubmittedAt = SystemClock.UtcNow, SubmissionData = """{"kind":"survey","answers":{"a":1}}""" });
        await context.SaveChangesAsync();
        var actorId = managerPath ? Guid.NewGuid() : learnerId;
        var service = new ContentInteractionService(context, new TestRequestContextAccessor(actorId, requestTenantId), CreatePermissions(PermissionType.Read));

        Func<Task> action = managerPath
            ? () => service.GetSurveyResponsesAsync(program.Id, survey.Id)
            : () => service.GetVisibleSurveyResponsesAsync(program.Id, survey.Id);

        await action.Should().ThrowAsync<RequestValidationException>();
    }

    [Theory]
    [InlineData(SurveyResultsVisibility.AfterSubmission, true, false, true)]
    [InlineData(SurveyResultsVisibility.AfterSubmission, false, false, false)]
    [InlineData(SurveyResultsVisibility.AfterClose, false, false, false)]
    [InlineData(SurveyResultsVisibility.AfterClose, false, true, true)]
    [InlineData(SurveyResultsVisibility.Never, true, true, false)]
    public async Task LearnerSurveyResults_ShouldEnforceConfiguredVisibility(
        SurveyResultsVisibility visibility,
        bool learnerSubmitted,
        bool courseClosed,
        bool shouldAllow)
    {
        await using var context = CreateContext();
        var learnerId = Guid.NewGuid();
        var program = CreateProgram();
        program.EnrollmentStatus = courseClosed ? EnrollmentStatus.Closed : EnrollmentStatus.Open;
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = learnerId, IsActive = true };
        var respondentEnrollment = learnerSubmitted
            ? enrollment
            : new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = Guid.NewGuid(), IsActive = true };
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Survey", Type = ProgramContentType.Survey };
        survey.SetActivitySettings(new SurveyActivitySettings(ResultsVisibility: visibility));
        var response = new ContentInteraction { Id = Guid.NewGuid(), ProgramUserId = respondentEnrollment.Id, UserId = respondentEnrollment.UserId, ContentId = survey.Id, SubmittedAt = SystemClock.UtcNow, SubmissionData = """{"kind":"survey","answers":{"a":1}}""" };
        context.AddRange(program, enrollment, respondentEnrollment, survey, response);
        await context.SaveChangesAsync();
        dynamic service = new ContentInteractionService(context, new TestRequestContextAccessor(learnerId, Guid.NewGuid()));

        if (shouldAllow)
        {
            var results = (IEnumerable<SurveyResponseResultDto>)await service.GetVisibleSurveyResponsesAsync(program.Id, survey.Id);
            results.Should().OnlyContain(result => GetRespondentUserId(result) == null);
        }
        else
        {
            Func<Task> action = async () => _ = await service.GetVisibleSurveyResponsesAsync(program.Id, survey.Id);
            await action.Should().ThrowAsync<RequestValidationException>();
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ManagerSurveyResults_ShouldExposeIdentityOnlyForNonAnonymousSurveys(bool anonymous, bool shouldExposeIdentity)
    {
        await using var context = CreateContext();
        var managerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var program = CreateProgram();
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Survey", Type = ProgramContentType.Survey };
        survey.SetActivitySettings(new SurveyActivitySettings(IsAnonymous: anonymous));
        var respondentId = Guid.NewGuid();
        var response = new ContentInteraction { Id = Guid.NewGuid(), UserId = respondentId, ContentId = survey.Id, SubmittedAt = SystemClock.UtcNow, SubmissionData = """{"kind":"survey","answers":{"a":1}}""" };
        context.AddRange(program, survey, response);
        await context.SaveChangesAsync();

        var results = await new ContentInteractionService(context, new TestRequestContextAccessor(managerId, tenantId), CreatePermissions(PermissionType.Review))
            .GetSurveyResponsesAsync(program.Id, survey.Id);

        GetRespondentUserId(results.Should().ContainSingle().Which).Should().Be(shouldExposeIdentity ? respondentId : null);
    }

    [Theory]
    [InlineData(ThreadRootKind.Valid, false)]
    [InlineData(ThreadRootKind.Arbitrary, true)]
    [InlineData(ThreadRootKind.NestedDiscussion, true)]
    [InlineData(ThreadRootKind.OtherContent, true)]
    [InlineData(ThreadRootKind.OtherCourse, true)]
    [InlineData(ThreadRootKind.Deleted, true)]
    [InlineData(ThreadRootKind.NonDiscussion, true)]
    public async Task SubmitUserContentAsync_ShouldRequireAValidDiscussionThreadRoot(
        ThreadRootKind rootKind,
        bool shouldReject)
    {
        await using var context = CreateContext();
        var program = CreateProgram();
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = Guid.NewGuid(), IsActive = true };
        var discussion = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Discussion", Type = ProgramContentType.Discussion };
        var rootContent = rootKind == ThreadRootKind.OtherCourse
            ? new ProgramContent { Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Title = "Other course", Type = ProgramContentType.Discussion }
            : rootKind is ThreadRootKind.OtherContent or ThreadRootKind.NonDiscussion
                ? new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Other content", Type = rootKind == ThreadRootKind.NonDiscussion ? ProgramContentType.Reflection : ProgramContentType.Discussion }
                : discussion;
        var root = new ContentInteraction
        {
            Id = Guid.NewGuid(),
            ProgramUserId = enrollment.Id,
            UserId = enrollment.UserId,
            ContentId = rootContent.Id,
            SubmittedAt = SystemClock.UtcNow,
            SubmissionData = rootKind == ThreadRootKind.NestedDiscussion
                ? $$"""{"kind":"discussion","body":"nested","threadRootId":"{{Guid.NewGuid()}}"}"""
                : rootContent.Type == ProgramContentType.Reflection
                    ? """{"kind":"reflection","body":"reflection"}"""
                    : """{"kind":"discussion","body":"root"}""",
        };
        context.AddRange(program, enrollment, discussion, rootContent, root);
        await context.SaveChangesAsync();
        if (rootKind == ThreadRootKind.Deleted)
        {
            root.SoftDelete();
            await context.SaveChangesAsync();
        }
        var rootId = rootKind == ThreadRootKind.Arbitrary ? Guid.NewGuid() : root.Id;

        Func<Task<ContentInteraction?>> submit = () => CreateSubmissionService(context, enrollment.UserId).SubmitUserContentAsync(
            program.Id, enrollment.UserId, discussion.Id,
            $$"""{"kind":"discussion","body":"reply","threadRootId":"{{rootId}}"}""");

        if (shouldReject)
        {
            Func<Task> action = async () => _ = await submit();
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Discussion thread root is invalid.");
        }
        else
            (await submit()).Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(ActivityContentSettings))]
    public async Task CloneProgramAsync_ShouldPreserveActivitySettings(
        ProgramContentType type,
        ActivitySettings settings)
    {
        await using var context = CreateCloneContext();
        var program = CreateProgram();
        var content = new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Title = type.ToString(),
            Type = type,
        };
        content.SetActivitySettings(settings);
        program.ProgramContents.Add(content);
        context.Add(program);
        await context.SaveChangesAsync();

        var cloned = await new ProgramWriteService(context).CloneProgramAsync(program.Id, "Cloned course");

        cloned.ProgramContents.Should().ContainSingle();
        cloned.ProgramContents.Single().GetActivitySettings().Should().Be(settings);
    }

    public static IEnumerable<object[]> ActivityContentSettings =>
    [
        [ProgramContentType.Discussion, new DiscussionActivitySettings(AllowReplies: false, RequireThreadRoot: false, MinimumBodyLength: 5, MaximumBodyLength: 100)],
        [ProgramContentType.Reflection, new ReflectionActivitySettings(PrivateToInstructors: false, MinimumBodyLength: 5, MaximumBodyLength: 100)],
        [ProgramContentType.Survey, new SurveyActivitySettings(IsAnonymous: true, AllowMultipleResponses: true, ResultsVisibility: SurveyResultsVisibility.AfterClose)],
    ];

    [Fact]
    public async Task AddUserToProgramAsync_ShouldCreateCanonicalAndInteractionEnrollments()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var program = CreateProgram();
        program.TenantId = tenantId;
        context.Add(program);
        await context.SaveChangesAsync();

        var result = await new ProgramWriteService(context).AddUserToProgramAsync(program.Id, userId);

        result.Should().NotBeNull();
        var interactionEnrollment = await context.Set<ProgramUser>().SingleAsync();
        interactionEnrollment.UserId.Should().Be(userId);
        interactionEnrollment.ProgramId.Should().Be(program.Id);
        interactionEnrollment.IsActive.Should().BeTrue();
        interactionEnrollment.TenantId.Should().Be(tenantId);
        var canonicalEnrollment = await context.Set<ProgramEnrollment>().SingleAsync();
        canonicalEnrollment.UserId.Should().Be(userId);
        canonicalEnrollment.ProgramId.Should().Be(program.Id);
        canonicalEnrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
        canonicalEnrollment.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task AddUserToProgramAsync_ShouldRepairMissingCanonicalEnrollment()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var program = CreateProgram();
        var legacyEnrollment = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            UserId = userId,
            IsActive = true,
            JoinedAt = DateTime.UtcNow.AddDays(-2),
        };
        context.AddRange(program, legacyEnrollment);
        await context.SaveChangesAsync();

        var result = await new ProgramWriteService(context).AddUserToProgramAsync(program.Id, userId);

        result.Should().NotBeNull();
        (await context.Set<ProgramUser>().CountAsync()).Should().Be(1);
        var canonicalEnrollment = await context.Set<ProgramEnrollment>().SingleAsync();
        canonicalEnrollment.EnrolledAt.Should().Be(legacyEnrollment.JoinedAt);
        canonicalEnrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task RemoveUserFromProgramAsync_ShouldDeactivateBothEnrollmentModels()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var program = CreateProgram();
        context.Add(program);
        await context.SaveChangesAsync();
        var service = new ProgramWriteService(context);
        await service.AddUserToProgramAsync(program.Id, userId);

        var removed = await service.RemoveUserFromProgramAsync(program.Id, userId);

        removed.Should().BeTrue();
        (await context.Set<ProgramUser>().SingleAsync()).DeletedAt.Should().NotBeNull();
        var canonicalEnrollment = await context.Set<ProgramEnrollment>().SingleAsync();
        canonicalEnrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Cancelled);
    }

    [Fact]
    public async Task UpdateProgramAsync_ShouldClearNullableEnrollmentControls()
    {
        await using var context = CreateContext();
        var program = new Program
        {
            Id = Guid.NewGuid(),
            Title = "Enrollment controls",
            Slug = "enrollment-controls",
            MaxEnrollments = 25,
            EnrollmentDeadline = DateTime.UtcNow.AddDays(10),
        };
        context.Set<Program>().Add(program);
        await context.SaveChangesAsync();

        var service = new ProgramWriteService(context);
        var updated = await service.UpdateProgramAsync(program.Id, new UpdateProgramDto
        {
            ClearMaxEnrollments = true,
            ClearEnrollmentDeadline = true,
        });

        updated.Should().NotBeNull();
        updated!.MaxEnrollments.Should().BeNull();
        updated.EnrollmentDeadline.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProgramAsync_ShouldSetNullableEnrollmentControls()
    {
        await using var context = CreateContext();
        var program = new Program
        {
            Id = Guid.NewGuid(),
            Title = "Enrollment controls",
            Slug = "enrollment-controls",
        };
        context.Set<Program>().Add(program);
        await context.SaveChangesAsync();
        var deadline = DateTime.UtcNow.AddDays(10);

        var service = new ProgramWriteService(context);
        var updated = await service.UpdateProgramAsync(program.Id, new UpdateProgramDto
        {
            MaxEnrollments = 40,
            EnrollmentDeadline = deadline,
        });

        updated.Should().NotBeNull();
        updated!.MaxEnrollments.Should().Be(40);
        updated.EnrollmentDeadline.Should().Be(deadline);
    }

    [Fact]
    public async Task UpdateContentAsync_WhenBodyChanges_ShouldPreserveExplicitLessonFormat()
    {
        await using var context = CreateContext();
        var program = CreateProgram();
        var content = new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Title = "Slides",
            Type = ProgramContentType.Lesson,
            LessonFormat = LessonContentFormat.RevealJs,
            Body = "old slides",
        };
        context.AddRange(program, content);
        await context.SaveChangesAsync();
        var service = new ProgramWriteService(context);

        var updated = await service.UpdateContentAsync(
            program.Id,
            content.Id,
            new UpdateContentDto(Body: "updated slides"));

        updated.Should().NotBeNull();
        updated!.LessonFormat.Should().Be(LessonContentFormat.RevealJs);
    }

    [Fact]
    public async Task SubmitUserContentAsync_ShouldSubmitTheCurrentActiveAttempt()
    {
        await using var context = CreateContext();
        var graph = CreateAttemptGraph();
        graph.OldAttempt.SubmittedAt = graph.OldAttempt.CreatedAt.AddMinutes(1);
        graph.OldAttempt.SubmissionData = "old submission";
        context.AddRange(
            graph.Program,
            graph.Content,
            graph.Enrollment,
            graph.OldAttempt,
            graph.CurrentAttempt);
        await context.SaveChangesAsync();
        var service = CreateSubmissionService(context, graph.Enrollment.UserId);

        var submitted = await service.SubmitUserContentAsync(
            graph.Program.Id,
            graph.Enrollment.UserId,
            graph.Content.Id,
            "current submission");

        submitted!.Id.Should().Be(graph.CurrentAttempt.Id);
        graph.CurrentAttempt.SubmissionData.Should().Be("current submission");
        graph.CurrentAttempt.SubmittedAt.Should().NotBeNull();
        graph.OldAttempt.SubmissionData.Should().Be("old submission");
    }

    [Fact]
    public async Task SubmitUserContentAsync_WhenSurveyAllowsMultipleResponses_ShouldCreateAnotherResponse()
    {
        await using var context = CreateContext();
        var program = CreateProgram();
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = Guid.NewGuid(), IsActive = true };
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Survey", Type = ProgramContentType.Survey };
        survey.SetActivitySettings(new SurveyActivitySettings(AllowMultipleResponses: true));
        context.AddRange(program, enrollment, survey);
        await context.SaveChangesAsync();
        var service = CreateSubmissionService(context, enrollment.UserId);

        var first = await service.SubmitUserContentAsync(program.Id, enrollment.UserId, survey.Id, """{"kind":"survey","answers":{"first":true}}""");
        var second = await service.SubmitUserContentAsync(program.Id, enrollment.UserId, survey.Id, """{"kind":"survey","answers":{"second":true}}""");

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second!.Id.Should().NotBe(first!.Id);
        (await context.Set<ContentInteraction>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SubmitUserContentAsync_WhenActivityPayloadDoesNotMatchContentType_ShouldReject()
    {
        await using var context = CreateContext();
        var program = CreateProgram();
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = Guid.NewGuid(), IsActive = true };
        var reflection = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Reflection", Type = ProgramContentType.Reflection };
        context.AddRange(program, enrollment, reflection);
        await context.SaveChangesAsync();
        var service = CreateSubmissionService(context, enrollment.UserId);

        Func<Task> action = async () => await service.SubmitUserContentAsync(
            program.Id,
            enrollment.UserId,
            reflection.Id,
            """{"kind":"discussion","body":"wrong type"}""");

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SubmitUserContentAsync_WhenActorTargetsAnotherLearnerWithoutManagementPermission_ShouldReject()
    {
        await using var context = CreateContext();
        var graph = CreateAttemptGraph();
        context.AddRange(graph.Program, graph.Content, graph.Enrollment);
        await context.SaveChangesAsync();
        var service = CreateSubmissionService(context, Guid.NewGuid());

        Func<Task> action = () => service.SubmitUserContentAsync(
            graph.Program.Id, graph.Enrollment.UserId, graph.Content.Id, "submission");

        await action.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("Program management permission is required*");
    }

    [Fact]
    public async Task SubmitUserContentAsync_WhenActorHasProgramManagementPermission_ShouldAllowManualSubmission()
    {
        await using var context = CreateContext();
        var graph = CreateAttemptGraph();
        context.AddRange(graph.Program, graph.Content, graph.Enrollment);
        await context.SaveChangesAsync();
        var managerId = Guid.NewGuid();
        var service = CreateSubmissionService(context, managerId, Guid.NewGuid(), CreatePermissions(PermissionType.Edit));

        var submitted = await service.SubmitUserContentAsync(
            graph.Program.Id, graph.Enrollment.UserId, graph.Content.Id, "manual submission");

        submitted.Should().NotBeNull();
        submitted!.UserId.Should().Be(graph.Enrollment.UserId);
    }

    [Theory]
    [InlineData(PermissionType.Read)]
    [InlineData(PermissionType.Create)]
    [InlineData(PermissionType.Delete)]
    public async Task SubmitUserContentAsync_WhenActorOnlyHasNonMutationProgramPermission_ShouldRejectManualSubmission(PermissionType grantedPermission)
    {
        await using var context = CreateContext();
        var graph = CreateAttemptGraph();
        context.AddRange(graph.Program, graph.Content, graph.Enrollment);
        await context.SaveChangesAsync();
        var managerId = Guid.NewGuid();
        var permissions = CreatePermissions(grantedPermission);
        var service = CreateSubmissionService(context, managerId, Guid.NewGuid(), permissions);

        Func<Task> action = () => service.SubmitUserContentAsync(
            graph.Program.Id, graph.Enrollment.UserId, graph.Content.Id, "manual submission");

        await action.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public async Task UpdateUserProgressAsync_ShouldUpdateTheCurrentActiveAttempt()
    {
        await using var context = CreateContext();
        var graph = CreateAttemptGraph();
        graph.OldAttempt.SubmittedAt = graph.OldAttempt.CreatedAt.AddMinutes(1);
        graph.OldAttempt.Status = ProgressStatus.Completed;
        graph.OldAttempt.IsCompleted = true;
        context.AddRange(
            graph.Program,
            graph.Content,
            graph.Enrollment,
            graph.OldAttempt,
            graph.CurrentAttempt);
        await context.SaveChangesAsync();
        var service = new ProgramWriteService(context);

        await service.UpdateUserProgressAsync(
            graph.Program.Id,
            graph.Enrollment.UserId,
            graph.Content.Id,
            ProgressStatus.Completed);

        graph.CurrentAttempt.Status.Should().Be(ProgressStatus.Completed);
        graph.CurrentAttempt.IsCompleted.Should().BeTrue();
        graph.CurrentAttempt.CompletionPercentage.Should().Be(100);
    }

    [Fact]
    public async Task MarkContentCompletedAsync_ShouldCountDistinctRequiredContent()
    {
        await using var context = CreateContext();
        var graph = CreateAttemptGraph();
        graph.Content.IsRequired = true;
        var remainingContent = new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = graph.Program.Id,
            Title = "Remaining lesson",
            Type = ProgramContentType.Lesson,
            IsRequired = true,
        };
        graph.OldAttempt.SubmittedAt = graph.OldAttempt.CreatedAt.AddMinutes(1);
        graph.OldAttempt.Status = ProgressStatus.Completed;
        graph.OldAttempt.IsCompleted = true;
        graph.OldAttempt.ProgressPercentage = 100;
        context.AddRange(
            graph.Program,
            graph.Content,
            remainingContent,
            graph.Enrollment,
            graph.OldAttempt,
            graph.CurrentAttempt);
        await context.SaveChangesAsync();
        var service = new ProgramWriteService(context);

        var completed = await service.MarkContentCompletedAsync(
            graph.Program.Id,
            graph.Enrollment.UserId,
            graph.Content.Id);

        completed.Should().BeTrue();
        graph.CurrentAttempt.IsCompleted.Should().BeTrue();
        graph.Enrollment.CompletionPercentage.Should().Be(50);
    }

    [Fact]
    public async Task GetCompletionRatesAsync_ShouldCountEachLearnerOncePerContent()
    {
        await using var context = CreateContext();
        var graph = CreateAttemptGraph();
        graph.OldAttempt.IsCompleted = true;
        graph.OldAttempt.Status = ProgressStatus.Completed;
        graph.CurrentAttempt.IsCompleted = true;
        graph.CurrentAttempt.Status = ProgressStatus.Completed;
        context.AddRange(
            graph.Program,
            graph.Content,
            graph.Enrollment,
            graph.OldAttempt,
            graph.CurrentAttempt);
        await context.SaveChangesAsync();
        var service = new ProgramReadService(context);

        var rates = await service.GetCompletionRatesAsync(graph.Program.Id);

        rates.Should().NotBeNull();
        rates!.ContentCompletionRates[graph.Content.Id].Should().Be(100);
    }

    [Fact]
    public async Task GetUserProgressDtoAsync_ShouldReturnOnlyTheCurrentAttemptPerContent()
    {
        await using var context = CreateContext();
        var graph = CreateAttemptGraph();
        graph.OldAttempt.SubmittedAt = graph.OldAttempt.CreatedAt.AddMinutes(1);
        graph.OldAttempt.Status = ProgressStatus.Completed;
        graph.OldAttempt.IsCompleted = true;
        graph.OldAttempt.ProgressPercentage = 100;
        context.AddRange(
            graph.Program,
            graph.Content,
            graph.Enrollment,
            graph.OldAttempt,
            graph.CurrentAttempt);
        await context.SaveChangesAsync();
        var service = new ProgramReadService(context);

        var progress = await service.GetUserProgressDtoAsync(
            graph.Program.Id,
            graph.Enrollment.UserId);

        var item = progress!.ContentProgress.Should().ContainSingle().Subject;
        item.ContentId.Should().Be(graph.Content.Id);
        item.Status.Should().Be(ProgressStatus.InProgress);
    }

    [Fact]
    public async Task UpdateUserProgressAsync_ShouldReturnOnlyTheCurrentAttemptPerContent()
    {
        await using var context = CreateContext();
        var graph = CreateAttemptGraph();
        graph.OldAttempt.SubmittedAt = graph.OldAttempt.CreatedAt.AddMinutes(1);
        graph.OldAttempt.Status = ProgressStatus.Completed;
        graph.OldAttempt.IsCompleted = true;
        graph.OldAttempt.ProgressPercentage = 100;
        context.AddRange(
            graph.Program,
            graph.Content,
            graph.Enrollment,
            graph.OldAttempt,
            graph.CurrentAttempt);
        await context.SaveChangesAsync();
        var service = new ProgramWriteService(context);

        var progress = await service.UpdateUserProgressAsync(
            graph.Program.Id,
            graph.Enrollment.UserId,
            new UpdateProgressDto(LastAccessedAt: SystemClock.UtcNow));

        var item = progress!.ContentProgress.Should().ContainSingle().Subject;
        item.ContentId.Should().Be(graph.Content.Id);
        item.Status.Should().Be(ProgressStatus.InProgress);
    }

    [Fact]
    public async Task UpdateUserProgressAsync_WhenConcurrentAttemptWins_ShouldCompleteTheWinner()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using var context = CreateContext(databaseName, databaseRoot);
        var graph = CreateAttemptGraph();
        context.AddRange(graph.Program, graph.Content, graph.Enrollment);
        await context.SaveChangesAsync();
        var winner = new ContentInteraction
        {
            Id = Guid.NewGuid(),
            ProgramUserId = graph.Enrollment.Id,
            UserId = graph.Enrollment.UserId,
            ContentId = graph.Content.Id,
            Status = ProgressStatus.InProgress,
        };
        context.BeforeInteractionSaveAsync = async cancellationToken =>
        {
            await using var winningContext = CreateContext(databaseName, databaseRoot);
            winningContext.Set<ContentInteraction>().Add(winner);
            await winningContext.SaveChangesAsync(cancellationToken);
        };
        var service = new ProgramWriteService(context);

        await service.UpdateUserProgressAsync(
            graph.Program.Id,
            graph.Enrollment.UserId,
            graph.Content.Id,
            ProgressStatus.Completed);

        await using var verificationContext = CreateContext(databaseName, databaseRoot);
        var persisted = await verificationContext.Set<ContentInteraction>().SingleAsync();
        persisted.Id.Should().Be(winner.Id);
        persisted.IsCompleted.Should().BeTrue();
        persisted.Status.Should().Be(ProgressStatus.Completed);
    }

    [Fact]
    public async Task MarkContentCompletedAsync_WhenConcurrentAttemptWins_ShouldCompleteTheWinner()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using var context = CreateContext(databaseName, databaseRoot);
        var graph = CreateAttemptGraph();
        context.AddRange(graph.Program, graph.Content, graph.Enrollment);
        await context.SaveChangesAsync();
        var winner = new ContentInteraction
        {
            Id = Guid.NewGuid(),
            ProgramUserId = graph.Enrollment.Id,
            UserId = graph.Enrollment.UserId,
            ContentId = graph.Content.Id,
            Status = ProgressStatus.InProgress,
        };
        context.BeforeInteractionSaveAsync = async cancellationToken =>
        {
            await using var winningContext = CreateContext(databaseName, databaseRoot);
            winningContext.Set<ContentInteraction>().Add(winner);
            await winningContext.SaveChangesAsync(cancellationToken);
        };
        var service = new ProgramWriteService(context);

        var completed = await service.MarkContentCompletedAsync(
            graph.Program.Id,
            graph.Enrollment.UserId,
            graph.Content.Id);

        completed.Should().BeTrue();
        await using var verificationContext = CreateContext(databaseName, databaseRoot);
        var persisted = await verificationContext.Set<ContentInteraction>().SingleAsync();
        persisted.Id.Should().Be(winner.Id);
        persisted.IsCompleted.Should().BeTrue();
        persisted.Status.Should().Be(ProgressStatus.Completed);
    }

    [Fact]
    public async Task SubmitUserContentAsync_WhenRequestsRace_ShouldKeepOneCanonicalSubmission()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using var context = CreateContext(databaseName, databaseRoot);
        var graph = CreateAttemptGraph();
        context.AddRange(graph.Program, graph.Content, graph.Enrollment);
        await context.SaveChangesAsync();
        context.SimulateInteractionConflictAfterHook = false;
        context.BeforeInteractionSaveAsync = async _ =>
        {
            await using var winningContext = CreateContext(databaseName, databaseRoot);
            var winningService = CreateSubmissionService(winningContext, graph.Enrollment.UserId);
            await winningService.SubmitUserContentAsync(
                graph.Program.Id,
                graph.Enrollment.UserId,
                graph.Content.Id,
                "winner submission");
        };
        var service = CreateSubmissionService(context, graph.Enrollment.UserId);

        var result = await service.SubmitUserContentAsync(
            graph.Program.Id,
            graph.Enrollment.UserId,
            graph.Content.Id,
            "losing submission");

        result.Should().NotBeNull();
        result!.SubmissionData.Should().Be("winner submission");
        await using var verificationContext = CreateContext(databaseName, databaseRoot);
        var submissions = await verificationContext.Set<ContentInteraction>().ToListAsync();
        submissions.Should().ContainSingle();
        submissions[0].Id.Should().Be(result.Id);
    }

    [Fact]
    public async Task SubmitUserContentAsync_WhenCanonicalSubmissionWasDeleted_ShouldRestoreIt()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using var context = CreateContext(databaseName, databaseRoot);
        var graph = CreateAttemptGraph();
        context.AddRange(graph.Program, graph.Content, graph.Enrollment);
        await context.SaveChangesAsync();
        var service = CreateSubmissionService(context, graph.Enrollment.UserId);
        var original = await service.SubmitUserContentAsync(
            graph.Program.Id,
            graph.Enrollment.UserId,
            graph.Content.Id,
            "original submission");
        original!.Version = 1;
        original!.SoftDelete();
        await context.SaveChangesAsync();
        await using var retryContext = CreateContext(databaseName, databaseRoot);
        var retryService = CreateSubmissionService(retryContext, graph.Enrollment.UserId);

        var restored = await retryService.SubmitUserContentAsync(
            graph.Program.Id,
            graph.Enrollment.UserId,
            graph.Content.Id,
            "restored submission");

        restored.Should().NotBeNull();
        restored!.Id.Should().Be(original.Id);
        restored.DeletedAt.Should().BeNull();
        restored.SubmissionData.Should().Be("restored submission");
    }

    private static Program CreateProgram() =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Course",
            Slug = $"course-{Guid.NewGuid():N}",
        };

    private static ContentInteraction ReflectionResponse(ProgramUser enrollment, ProgramContent reflection) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProgramUserId = enrollment.Id,
            UserId = enrollment.UserId,
            ContentId = reflection.Id,
            SubmittedAt = SystemClock.UtcNow,
            SubmissionData = """{"kind":"reflection","body":"response"}""",
        };

    private static Guid? GetRespondentUserId(object result) =>
        result.GetType().GetProperty("RespondentUserId")?.GetValue(result) is Guid respondentId
            ? respondentId
            : null;

    private static AttemptGraph CreateAttemptGraph()
    {
        var program = CreateProgram();
        var content = new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Title = "Assignment",
            Type = ProgramContentType.Assignment,
        };
        var enrollment = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            UserId = Guid.NewGuid(),
            JoinedAt = SystemClock.UtcNow.AddDays(-1),
        };
        var oldAttempt = new ContentInteraction
        {
            Id = Guid.NewGuid(),
            ProgramUserId = enrollment.Id,
            UserId = enrollment.UserId,
            ContentId = content.Id,
            CreatedAt = SystemClock.UtcNow.AddHours(-1),
        };
        var currentAttempt = new ContentInteraction
        {
            Id = Guid.NewGuid(),
            ProgramUserId = enrollment.Id,
            UserId = enrollment.UserId,
            ContentId = content.Id,
            CreatedAt = SystemClock.UtcNow,
            Status = ProgressStatus.InProgress,
        };

        return new AttemptGraph(program, content, enrollment, oldAttempt, currentAttempt);
    }

    private static LearningCoursesTestContext CreateContext(
        string? databaseName = null,
        InMemoryDatabaseRoot? databaseRoot = null)
    {
        var builder = new DbContextOptionsBuilder<LearningCoursesTestContext>();
        var resolvedName = databaseName ?? Guid.NewGuid().ToString();
        if (databaseRoot is null)
        {
            builder.UseInMemoryDatabase(resolvedName);
        }
        else
        {
            builder.UseInMemoryDatabase(resolvedName, databaseRoot);
        }

        var options = builder.Options;
        return new LearningCoursesTestContext(options);
    }

    private static CloneTestContext CreateCloneContext()
    {
        var options = new DbContextOptionsBuilder<CloneTestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CloneTestContext(options);
    }

    private static ProgramWriteService CreateSubmissionService(
        IApplicationDbContext context,
        Guid userId,
        Guid? tenantId = null,
        IPermissionQueryService? permissions = null) =>
        new(context, requestContextAccessor: new TestRequestContextAccessor(userId, tenantId), permissionQueryService: permissions);

    private static IPermissionQueryService CreatePermissions(PermissionType grantedPermission)
    {
        var permissions = new Mock<IPermissionQueryService>();
        permissions.Setup(service => service.HasTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? _, Guid? _, string permission, CancellationToken _) =>
                permission.EndsWith($".{grantedPermission}", StringComparison.Ordinal));
        return permissions.Object;
    }

    private sealed class TestRequestContextAccessor(Guid userId, Guid? tenantId = null) : IRequestContextAccessor
    {
        public Guid? CurrentUserId => userId;
        public Guid? CurrentTenantId => tenantId;
        public bool IsAuthenticated => true;
        public bool HasTenantContext => tenantId.HasValue;
        public Task<UserInfo?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<UserInfo?>(new UserInfo(userId, "learner@example.com", "Learner", true));
        public Task<TenantInfo?> GetCurrentTenantAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantInfo?>(null);
    }

    private sealed class LearningCoursesTestContext(DbContextOptions<LearningCoursesTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Func<CancellationToken, Task>? BeforeInteractionSaveAsync { get; set; }

        public bool SimulateInteractionConflictAfterHook { get; set; } = true;

        public DbSet<Program> Programs => Set<Program>();

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is EntityBase<Guid> entity &&
                    (entry.State == EntityState.Added || entry.State == EntityState.Modified))
                {
                    entry.Property(nameof(EntityBase<Guid>.Version)).CurrentValue = entity.Version + 1;
                }
            }

            var beforeInteractionSave = BeforeInteractionSaveAsync;
            var hasAddedInteraction = ChangeTracker.Entries<ContentInteraction>()
                .Any(entry => entry.State == EntityState.Added);
            if (beforeInteractionSave is not null && hasAddedInteraction)
            {
                BeforeInteractionSaveAsync = null;
                await beforeInteractionSave(cancellationToken);
                if (SimulateInteractionConflictAfterHook)
                    throw new DbUpdateException("Simulated concurrent active-attempt conflict.");
            }

            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (ArgumentException exception) when (hasAddedInteraction)
            {
                throw new DbUpdateException("Simulated relational duplicate-key conflict.", exception);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Program>(entity =>
            {
                entity.Ignore(program => program.ProgramContents);
                entity.Ignore(program => program.ProgramUsers);
                entity.Ignore(program => program.ProgramRatings);
                entity.Ignore(program => program.ProgramWishlists);
            });
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.Ignore(content => content.Program);
                entity.Ignore(content => content.Parent);
                entity.Ignore(content => content.Children);
                entity.Ignore(content => content.ContentInteractions);
            });
            modelBuilder.Entity<ProgramUser>(entity =>
            {
                entity.Ignore(enrollment => enrollment.User);
                entity.Ignore(enrollment => enrollment.Program);
                entity.Ignore(enrollment => enrollment.ContentInteractions);
                entity.Ignore(enrollment => enrollment.ReceivedGrades);
                entity.Ignore(enrollment => enrollment.GivenGrades);
                entity.Ignore(enrollment => enrollment.ProgramRatings);
            });
            modelBuilder.Entity<ProgramEnrollment>(entity =>
            {
                entity.Ignore(enrollment => enrollment.Program);
                entity.Ignore(enrollment => enrollment.User);
            });
            modelBuilder.Entity<ContentInteraction>(entity =>
            {
                entity.Ignore(interaction => interaction.User);
                entity.Ignore(interaction => interaction.ProgramUser);
                entity.Ignore(interaction => interaction.ActivityGrades);
                entity.Ignore(interaction => interaction.Events);
                entity.HasOne(interaction => interaction.Content)
                    .WithMany()
                    .HasForeignKey(interaction => interaction.ContentId);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not needed for these service tests.");
        }
    }

    private sealed class CloneTestContext(DbContextOptions<CloneTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Program>(entity =>
            {
                entity.Ignore(program => program.ProgramUsers);
                entity.Ignore(program => program.ProgramRatings);
                entity.Ignore(program => program.ProgramWishlists);
            });
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.Ignore(content => content.Parent);
                entity.Ignore(content => content.Children);
                entity.Ignore(content => content.ContentInteractions);
                entity.HasOne(content => content.Program)
                    .WithMany(program => program.ProgramContents)
                    .HasForeignKey(content => content.ProgramId);
            });
            modelBuilder.Entity<ProgramUser>(entity =>
            {
                entity.Ignore(enrollment => enrollment.User);
                entity.Ignore(enrollment => enrollment.ContentInteractions);
                entity.Ignore(enrollment => enrollment.ReceivedGrades);
                entity.Ignore(enrollment => enrollment.GivenGrades);
                entity.Ignore(enrollment => enrollment.ProgramRatings);
                entity.HasOne(enrollment => enrollment.Program)
                    .WithMany(program => program.ProgramUsers)
                    .HasForeignKey(enrollment => enrollment.ProgramId);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Transactions are not needed for these service tests.");
    }

    private sealed record AttemptGraph(
        Program Program,
        ProgramContent Content,
        ProgramUser Enrollment,
        ContentInteraction OldAttempt,
        ContentInteraction CurrentAttempt);

    public enum ThreadRootKind
    {
        Valid,
        Arbitrary,
        NestedDiscussion,
        OtherContent,
        OtherCourse,
        Deleted,
        NonDiscussion,
    }
}

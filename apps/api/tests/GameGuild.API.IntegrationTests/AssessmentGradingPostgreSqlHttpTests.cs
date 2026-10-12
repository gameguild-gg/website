using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Code;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CourseProgram = GameGuild.Learning.Courses.Program;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AssessmentGradingPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string AssessmentsRoute = "/v1/assessments";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanonicalProgramEnrollment_GradingRuntimeAcceptsPersistedIndividualAndCollectiveMembership(
        bool collective)
    {
        var scenario = await CreateScenarioAsync(collective, automatedReview: false);
        var firstEnrollmentId = Guid.NewGuid();
        Guid courseId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var assessment = await context.Set<Assessment>().SingleAsync(value => value.Id == scenario.AssessmentId);
            courseId = assessment.CourseId;
            var prior = await context.Set<Enrollment>()
                .Where(value => value.CourseId == assessment.CourseId)
                .ToListAsync();
            context.RemoveRange(prior);
            context.AddRange(new[] { scenario.Learner1Id, scenario.Learner2Id }.Select(userId => new ProgramEnrollment
            {
                Id = userId == scenario.Learner1Id ? firstEnrollmentId : Guid.NewGuid(),
                ProgramId = assessment.CourseId,
                UserId = userId,
                TenantId = scenario.TenantId,
                EnrollmentStatus = GameGuild.Learning.Courses.EnrollmentStatus.Active,
            }));
            await context.SaveChangesAsync();
        }

        using var learner = fixture.CreateAuthenticatedClient(scenario.Learner1Id, scenario.TenantId);
        var started = collective
            ? await PostAsync<AssessmentSubmissionViewV1>(learner,
                $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/collective",
                new StartCollectiveRuntimeSubmissionRequest(scenario.CourseGroupId!.Value, $"canonical-{Guid.NewGuid():N}"))
            : await PostAsync<AssessmentSubmissionViewV1>(learner,
                $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/individual",
                new StartIndividualRuntimeSubmissionRequest(firstEnrollmentId, $"canonical-{Guid.NewGuid():N}"));
        started.SubmissionId.Should().NotBe(Guid.Empty);

        await using var verification = fixture.Factory.Services.CreateAsyncScope();
        var verifiedContext = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var submission = await verifiedContext.Set<AssessmentSubmission>().SingleAsync(value => value.Id == started.SubmissionId);
        if (collective)
        {
            submission.EnrollmentId.Should().BeNull("a collective attempt belongs to its course group");
            submission.UserId.Should().BeNull("participants own the collective attempt through the group snapshot");
            submission.CourseGroupId.Should().Be(scenario.CourseGroupId);
            var participants = await verifiedContext.Set<AssessmentSubmissionParticipant>()
                .Where(value => value.SubmissionId == started.SubmissionId)
                .ToListAsync();
            participants.Should().HaveCount(2);
            participants.Should().ContainSingle(value => value.UserId == scenario.Learner1Id && value.EnrollmentId == firstEnrollmentId);
            foreach (var participant in participants)
            {
                var membership = await verifiedContext.Set<ProgramEnrollment>()
                    .SingleAsync(value => value.Id == participant.EnrollmentId);
                membership.UserId.Should().Be(participant.UserId);
                membership.TenantId.Should().Be(scenario.TenantId);
                membership.ProgramId.Should().Be(courseId);
            }
        }
        else
        {
            submission.EnrollmentId.Should().Be(firstEnrollmentId);
            submission.UserId.Should().Be(scenario.Learner1Id);
        }
        submission.StartedByUserId.Should().Be(scenario.Learner1Id);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OfficialGradingFlow_IsDurableAuthorizedAndIdempotent(
        bool collective,
        bool automatedReview)
    {
        var scenario = await CreateScenarioAsync(collective, automatedReview);
        using var learner1 = fixture.CreateAuthenticatedClient(scenario.Learner1Id, scenario.TenantId);
        using var learner2 = fixture.CreateAuthenticatedClient(scenario.Learner2Id, scenario.TenantId);
        using var outsider = fixture.CreateAuthenticatedClient(scenario.OutsiderId, scenario.TenantId);
        using var instructor = fixture.CreateAuthenticatedClient(scenario.InstructorId, scenario.TenantId);

        var genericStart = await learner1.PostAsJsonAsync(
            $"{AssessmentsRoute}/{scenario.AssessmentId}/submissions/start",
            new StartSubmissionRequest(scenario.Enrollment1Id),
            JsonOptions);
        genericStart.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "content-backed graded quizzes must fail closed on the superseded generic route");

        var startKey = $"start-{Guid.NewGuid():N}";
        AssessmentSubmissionViewV1 started;
        if (collective)
        {
            var request = new StartCollectiveRuntimeSubmissionRequest(scenario.CourseGroupId!.Value, startKey);
            started = await PostAsync<AssessmentSubmissionViewV1>(
                learner1,
                $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/collective",
                request);
            var replay = await PostAsync<AssessmentSubmissionViewV1>(
                learner1,
                $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/collective",
                request);
            replay.SubmissionId.Should().Be(started.SubmissionId);
            replay.Execution.ExecutionId.Should().Be(started.Execution.ExecutionId);
            replay.Execution.DeliveryHash.Should().Be(started.Execution.DeliveryHash);
            replay.Execution.DefinitionRevisionId.Should().Be(started.Execution.DefinitionRevisionId);
            AssertSameDelivery(replay, started);

            var resumedBySecondMember = await PostAsync<AssessmentSubmissionViewV1>(
                learner2,
                $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/collective",
                new StartCollectiveRuntimeSubmissionRequest(
                    scenario.CourseGroupId.Value,
                    $"resume-{Guid.NewGuid():N}"));
            resumedBySecondMember.SubmissionId.Should().Be(started.SubmissionId);
        }
        else
        {
            var request = new StartIndividualRuntimeSubmissionRequest(scenario.Enrollment1Id, startKey);
            started = await PostAsync<AssessmentSubmissionViewV1>(
                learner1,
                $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/individual",
                request);
            var replay = await PostAsync<AssessmentSubmissionViewV1>(
                learner1,
                $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/individual",
                request);
            replay.SubmissionId.Should().Be(started.SubmissionId);
            replay.Execution.ExecutionId.Should().Be(started.Execution.ExecutionId);
            replay.Execution.DeliveryHash.Should().Be(started.Execution.DeliveryHash);
            replay.Execution.DefinitionRevisionId.Should().Be(started.Execution.DefinitionRevisionId);
            AssertSameDelivery(replay, started);
        }

        var outsiderRead = await outsider.GetAsync(
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        outsiderRead.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var submittingClient = collective ? learner2 : learner1;
        var submitKey = $"submit-{Guid.NewGuid():N}";
        var submitRequest = new SubmitAssessmentRuntimeRequest(
            TrueFalseAnswer(true),
            submitKey,
            collective ? 0 : null);
        var submitted = await PostAsync<AssessmentSubmissionViewV1>(
            submittingClient,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/submit",
            submitRequest);
        var submitReplay = await PostAsync<AssessmentSubmissionViewV1>(
            submittingClient,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/submit",
            submitRequest);
        submitReplay.Execution.ActiveRoundId.Should().Be(submitted.Execution.ActiveRoundId);
        submitted.Execution.ActiveRoundId.Should().NotBeNull();

        var divergentSubmit = await submittingClient.PostAsJsonAsync(
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/submit",
            new SubmitAssessmentRuntimeRequest(
                TrueFalseAnswer(false),
                submitKey,
                collective ? 0 : null),
            JsonOptions);
        divergentSubmit.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var learnerBeforeRelease = await GetAsync<AssessmentSubmissionViewV1>(
            learner1,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        learnerBeforeRelease.Execution.LearnerVisibleResult.Should().BeNull();
        learnerBeforeRelease.Execution.InstructorVisibleResult.Should().BeNull();
        learnerBeforeRelease.Execution.Released.Should().BeFalse();

        var unauthorizedReview = await outsider.PostAsJsonAsync(
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/instructor-review",
            new ResolveInstructorReviewRequest(
                Resolution(150),
                $"unauthorized-review-{Guid.NewGuid():N}"),
            JsonOptions);
        unauthorizedReview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var unauthorizedRelease = await outsider.PostAsJsonAsync(
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/release",
            new ReleaseGradeResultCommand(
                submitted.Execution.ActiveRoundId!.Value,
                submitted.Version,
                $"unauthorized-release-{Guid.NewGuid():N}"),
            JsonOptions);
        unauthorizedRelease.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        AssessmentSubmissionViewV1 finalized;
        var expectedScore = automatedReview ? 200 : 175;
        if (automatedReview)
        {
            finalized = await GetAsync<AssessmentSubmissionViewV1>(
                instructor,
                $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
            finalized.Execution.RequiresInstructorReview.Should().BeFalse();
        }
        else
        {
            finalized = await PostAsync<AssessmentSubmissionViewV1>(
                instructor,
                $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/instructor-review",
                new ResolveInstructorReviewRequest(
                    Resolution(expectedScore),
                    $"review-{Guid.NewGuid():N}"));
            finalized.Execution.RequiresInstructorReview.Should().BeFalse();
        }

        finalized.Execution.ActiveRoundId.Should().NotBeNull();
        finalized.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(expectedScore));

        var releaseKey = $"release-{Guid.NewGuid():N}";
        var releaseRequest = new ReleaseGradeResultCommand(
            finalized.Execution.ActiveRoundId!.Value,
            finalized.Version,
            releaseKey,
            "Approved by the integration-test instructor");
        var released = await PostAsync<GradeResultReleaseResponse>(
            instructor,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/release",
            releaseRequest);
        var releaseReplay = await PostAsync<GradeResultReleaseResponse>(
            instructor,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/release",
            releaseRequest);
        releaseReplay.ReleaseId.Should().Be(released.ReleaseId);

        var learnerAfterRelease = await GetAsync<AssessmentSubmissionViewV1>(
            learner1,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        learnerAfterRelease.Execution.Released.Should().BeTrue();
        learnerAfterRelease.Execution.LearnerVisibleResult!.Score.Should()
            .Be(ScoreValue.FromUnits(expectedScore));
        learnerAfterRelease.ContentCompleted.Should().BeTrue();

        if (collective)
        {
            var secondParticipant = await GetAsync<AssessmentSubmissionViewV1>(
                learner2,
                $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
            secondParticipant.Execution.LearnerVisibleResult!.Score.Should()
                .Be(ScoreValue.FromUnits(expectedScore));
            secondParticipant.ContentCompleted.Should().BeTrue();
        }

        await AssertPersistedStateAsync(
            scenario,
            started,
            collective ? 2 : 0,
            collective ? 2 : 1);
    }

    [Theory]
    [InlineData("Accepted frozen solution")]
    [InlineData(null)]
    [InlineData(" \t")]
    public async Task CodeOfficialFlow_FreezesPrivateTestsAndRubricAndPersistsAuthorizedReviewAndRelease(string? feedback)
    {
        var scenario = await CreateScenarioAsync(false, false, code: true);
        using var learner = fixture.CreateAuthenticatedClient(scenario.Learner1Id, scenario.TenantId);
        using var outsider = fixture.CreateAuthenticatedClient(scenario.OutsiderId, scenario.TenantId);
        using var instructor = fixture.CreateAuthenticatedClient(scenario.InstructorId, scenario.TenantId);
        var generic = await learner.PostAsJsonAsync($"{AssessmentsRoute}/{scenario.AssessmentId}/submissions/start",
            new StartSubmissionRequest(scenario.Enrollment1Id), JsonOptions);
        generic.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var started = await PostAsync<AssessmentSubmissionViewV1>(learner,
            $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/individual",
            new StartIndividualRuntimeSubmissionRequest(scenario.Enrollment1Id, $"code-start-{Guid.NewGuid():N}"));
        started.Execution.InstructorVisibleContent.Should().BeNull();
        started.Execution.InstructorVisibleRubric.Should().BeNull();
        JsonSerializer.Serialize(started, JsonOptions).Should().NotContain("private-code-secret");
        var unauthorized = await outsider.GetAsync($"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Guid criterionId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var assessment = await context.Set<Assessment>().SingleAsync(value => value.Id == scenario.AssessmentId);
            var content = await context.Set<ProgramContent>().SingleAsync(value => value.Id == assessment.ContentId);
            content.JsonBody = CodeDefinition("unpublished draft").GetRawText();
            var rubric = await context.Set<AssessmentRubric>().SingleAsync(value => value.Id == assessment.RubricId);
            criterionId = await context.Set<RubricCriterion>().Where(value => value.RubricId == rubric.Id).Select(value => value.Id).SingleAsync();
            rubric.Replace("Unpublished rubric");
            await context.SaveChangesAsync();
        }
        var resumed = await GetAsync<AssessmentSubmissionViewV1>(learner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        AssertSameDelivery(resumed, started);
        var instructorView = await GetAsync<AssessmentSubmissionViewV1>(instructor,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        instructorView.Execution.InstructorVisibleContent!.Value.GetRawText().Should().Contain("private-code-secret")
            .And.NotContain("unpublished draft");
        instructorView.Execution.InstructorVisibleRubric!.Value.GetProperty("title").GetString().Should().Be("Frozen Code rubric");

        var response = CodeResponse("main.cpp");
        var submitted = await PostAsync<AssessmentSubmissionViewV1>(learner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/submit",
            new SubmitAssessmentRuntimeRequest(response, $"code-submit-{Guid.NewGuid():N}"));
        submitted.Execution.RequiresInstructorReview.Should().BeTrue();
        var resolution = new InstructorReviewResolutionV1(1, [new("code", ScoreValue.FromUnits(150))],
            feedback, RubricScores: new Dictionary<Guid, ScoreValue> { [criterionId] = ScoreValue.FromUnits(150) });
        var learnerReview = await learner.PostAsJsonAsync(
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/instructor-review",
            new ResolveInstructorReviewRequest(resolution, "forged-code-review"), JsonOptions);
        learnerReview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var resolved = await PostAsync<AssessmentSubmissionViewV1>(instructor,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/instructor-review",
            new ResolveInstructorReviewRequest(resolution, "code-review"));
        var replay = await PostAsync<AssessmentSubmissionViewV1>(instructor,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/instructor-review",
            new ResolveInstructorReviewRequest(resolution, "code-review"));
        resolved.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(150));
        resolved.Execution.InstructorVisibleResult.Feedback.Should().Be(feedback);
        resolved.Execution.InstructorVisibleRubricScores![criterionId].Should().Be(ScoreValue.FromUnits(150));
        replay.Execution.ActiveRoundId.Should().Be(resolved.Execution.ActiveRoundId);
        var beforeRelease = await GetAsync<AssessmentSubmissionViewV1>(learner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        beforeRelease.Execution.LearnerVisibleResult.Should().BeNull();
        var released = await instructor.PostAsJsonAsync(
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/release",
            new ReleaseGradeResultCommand(resolved.Execution.ActiveRoundId!.Value, resolved.Version, "code-release"), JsonOptions);
        released.IsSuccessStatusCode.Should().BeTrue(await released.Content.ReadAsStringAsync());
        var learnerResult = await GetAsync<AssessmentSubmissionViewV1>(learner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        learnerResult.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(150));
        learnerResult.Execution.LearnerVisibleResult.Feedback.Should().Be(feedback);
        learnerResult.Execution.InstructorVisibleRubricScores.Should().BeNull();
        JsonSerializer.Serialize(learnerResult, JsonOptions).Should().NotContain("private-code-secret");
        await using var verification = fixture.Factory.Services.CreateAsyncScope();
        var database = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await database.Set<AssessmentSubmission>().SingleAsync(value => value.Id == started.SubmissionId);
        persisted.CodePayload.Should().Contain("submitted-code");
        persisted.Feedback.Should().Be(feedback);
        persisted.RubricScoresPayload.Should().Contain(criterionId.ToString()).And.Contain("150");
    }

    [Theory]
    [InlineData("private.h")]
    [InlineData("readonly.h")]
    [InlineData("../main.cpp")]
    [InlineData("forged-score")]
    public async Task CodeOfficialFlow_RejectsPrivateFileOverridesUnsafePathsAndClientScores(string mutation)
    {
        var scenario = await CreateScenarioAsync(false, false, code: true);
        using var learner = fixture.CreateAuthenticatedClient(scenario.Learner1Id, scenario.TenantId);
        var started = await PostAsync<AssessmentSubmissionViewV1>(learner,
            $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/individual",
            new StartIndividualRuntimeSubmissionRequest(scenario.Enrollment1Id, "code-start"));
        var envelope = mutation == "forged-score"
            ? new AssessmentResponseEnvelopeV1(1, CodeAssessmentContracts.ContentType, CodeAssessmentContracts.PayloadSchema,
                JsonSerializer.SerializeToElement(new { files = new { }, score = 200 })) : CodeResponse(mutation);
        var rejected = await learner.PostAsJsonAsync(
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/submit",
            new SubmitAssessmentRuntimeRequest(envelope, "invalid-code-response"), JsonOptions);
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var persisted = await GetAsync<AssessmentSubmissionViewV1>(learner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        persisted.Status.Should().Be(started.Status);
        persisted.Execution.SubmittedResponse.Should().BeNull();
        persisted.Execution.ActiveRoundId.Should().BeNull();
    }

    [Fact]
    public async Task CodeOfficialFlow_UnavailableWorkerDoesNotCommitResponseGradeOrSubmissionReceipt()
    {
        var scenario = await CreateScenarioAsync(false, true, code: true);
        using var learner = fixture.CreateAuthenticatedClient(scenario.Learner1Id, scenario.TenantId);
        var started = await PostAsync<AssessmentSubmissionViewV1>(learner,
            $"{AssessmentsRoute}/{scenario.AssessmentId}/runtime-submissions/individual",
            new StartIndividualRuntimeSubmissionRequest(scenario.Enrollment1Id, "code-start"));
        // This HTTP fixture has no external grading worker configuration. The
        // real worker must fail closed; the native browser cycle covers WASM.
        var unavailable = await learner.PostAsJsonAsync(
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/submit",
            new SubmitAssessmentRuntimeRequest(CodeResponse("main.cpp"), "unavailable-worker"), JsonOptions);
        unavailable.IsSuccessStatusCode.Should().BeFalse();
        var resumed = await GetAsync<AssessmentSubmissionViewV1>(learner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        resumed.Status.Should().Be(started.Status);
        resumed.SubmittedAt.Should().BeNull();
        resumed.Execution.SubmittedResponse.Should().BeNull();
        resumed.Execution.ActiveRoundId.Should().BeNull();
        await using var verification = fixture.Factory.Services.CreateAsyncScope();
        var database = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var submission = await database.Set<AssessmentSubmission>().SingleAsync(value => value.Id == started.SubmissionId);
        submission.CodePayload.Should().BeNull();
    }

    private async Task<Scenario> CreateScenarioAsync(bool collective, bool automatedReview, bool code = false)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var authoring = scope.ServiceProvider.GetRequiredService<IAssessmentAuthoringService>();
        var suffix = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var learner1Id = Guid.NewGuid();
        var learner2Id = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var contentId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = $"Grading E2E {suffix}",
            Slug = $"grading-e2e-{suffix}",
            AdminEmail = $"admin-{suffix}@grading.test",
            IsActive = true,
        };
        var users = new[]
        {
            User(instructorId, "Instructor", suffix),
            User(learner1Id, "Learner One", suffix),
            User(learner2Id, "Learner Two", suffix),
            User(outsiderId, "Outsider", suffix),
        };
        var memberships = users.Select(user => new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Role = "Member",
            IsActive = true,
        });
        var course = new CourseProgram
        {
            Id = courseId,
            TenantId = tenantId,
            CreatorId = instructorId,
            Title = $"Grading course {suffix}",
            Slug = $"grading-course-{suffix}",
        };
        var content = new ProgramContent
        {
            Id = contentId,
            TenantId = tenantId,
            ProgramId = courseId,
            Title = "Quiz",
            Slug = $"quiz-{suffix}",
            Type = code ? ProgramContentType.Code : ProgramContentType.Questionnaire,
            JsonBody = "{\"schemaVersion\":1,\"order\":[],\"blocks\":{}}",
            LessonFormat = null,
        };

        context.Add(tenant);
        context.AddRange(users);
        context.AddRange(memberships);
        context.Add(course);
        context.Add(content);
        await context.SaveChangesAsync();

        var enrollment1 = Enrollment.Create(courseId, learner1Id);
        enrollment1.TenantId = tenantId;
        var enrollment2 = Enrollment.Create(courseId, learner2Id);
        enrollment2.TenantId = tenantId;
        context.Set<Enrollment>().AddRange(enrollment1, enrollment2);
        await context.SaveChangesAsync();

        var saved = await authoring.SaveDraftAsync(
            courseId,
            contentId,
            instructorId,
            new SaveAssessmentDraftRequest(
                content.Version,
                null,
                "Quiz",
                content.Slug,
                null,
                code ? CodeDefinition("int main(){return 0;}") : TrueFalseQuiz(),
                Visibility.Public,
                true,
                null,
                EstimatedMinutesSource.Auto,
                automatedReview ? ReviewMethods.AutomatedReview : ReviewMethods.InstructorReview,
                ScoreValue.FromUnits(100),
                MaxAttempts: 1,
                ContentCompletionMode: ContentCompletionMode.OnReleaseAndPass,
                ResultReleaseMode: ResultReleaseMode.Manual,
                ReviewConfigurationCanonicalJson: code && !automatedReview
                    ? "{\"schemaVersion\":1,\"instructor\":{\"requireOverrideReason\":false}}" : null));
        saved.IsSuccess.Should().BeTrue(saved.IsSuccess ? string.Empty : saved.Error.Description);

        var assessment = await context.Set<Assessment>()
            .SingleAsync(value => value.Id == saved.Value.AssessmentId!.Value);
        var gradebookGroup = AssessmentGroup.Create(
            courseId,
            $"Course grade {suffix}",
            PercentValue.FromUnits(10000));
        gradebookGroup.TenantId = tenantId;
        context.Set<AssessmentGroup>().Add(gradebookGroup);
        assessment.AssignToGroup(gradebookGroup.Id);
        if (code)
        {
            var rubric = AssessmentRubric.Create("Frozen Code rubric");
            rubric.TenantId = tenantId;
            var criterion = RubricCriterion.Create(rubric.Id, "Correctness", ScoreValue.FromUnits(200), 0);
            criterion.TenantId = tenantId;
            context.AddRange(rubric, criterion);
            assessment.AssignRubric(rubric.Id);
        }

        Guid? courseGroupId = null;
        if (collective)
        {
            var groupSet = CourseGroupSet.Create(courseId, $"Teams {suffix}");
            groupSet.TenantId = tenantId;
            var group = CourseGroup.Create(groupSet.Id, "Team One", 4);
            group.TenantId = tenantId;
            var firstMember = CourseGroupMember.Create(group.Id, learner1Id);
            firstMember.TenantId = tenantId;
            var secondMember = CourseGroupMember.Create(group.Id, learner2Id);
            secondMember.TenantId = tenantId;
            context.Set<CourseGroupSet>().Add(groupSet);
            context.Set<CourseGroup>().Add(group);
            context.Set<CourseGroupMember>().AddRange(firstMember, secondMember);
            assessment.AssignToGroupSet(groupSet.Id);
            courseGroupId = group.Id;
        }

        await context.SaveChangesAsync();
        await context.Entry(assessment).ReloadAsync();
        var prepared = await authoring.PrepareAsync(
            assessment.Id,
            instructorId,
            new PrepareAssessmentRevisionRequest(assessment.Version));
        prepared.IsSuccess.Should().BeTrue(prepared.IsSuccess ? string.Empty : prepared.Error.Description);
        await context.Entry(assessment).ReloadAsync();
        var published = await authoring.PublishAsync(
            assessment.Id,
            instructorId,
            new PublishAssessmentRevisionRequest(prepared.Value.RevisionId, assessment.Version));
        published.IsSuccess.Should().BeTrue(published.IsSuccess ? string.Empty : published.Error.Description);

        return new Scenario(
            tenantId,
            instructorId,
            learner1Id,
            learner2Id,
            outsiderId,
            assessment.Id,
            enrollment1.Id,
            courseGroupId);
    }

    private async Task AssertPersistedStateAsync(
        Scenario scenario,
        AssessmentSubmissionViewV1 started,
        int participantCount,
        int projectionCount)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await context.Set<AssessmentSubmission>()
                .AsNoTracking()
                .CountAsync(value => value.AssessmentId == scenario.AssessmentId))
            .Should().Be(1);
        (await context.Set<GradingExecution>()
                .AsNoTracking()
                .CountAsync(value =>
                    value.AssessmentSubmissionId == started.SubmissionId &&
                    value.ExecutionContext == ReviewExecutionContext.OfficialSubmission))
            .Should().Be(1);
        (await context.Set<GradeRound>()
                .AsNoTracking()
                .CountAsync(value => value.GradingExecutionId == started.Execution.ExecutionId))
            .Should().Be(1);
        (await context.Set<GradeResultRelease>()
                .AsNoTracking()
                .CountAsync(value => value.GradingExecutionId == started.Execution.ExecutionId))
            .Should().Be(1);
        (await context.Set<AssessmentSubmissionParticipant>()
                .AsNoTracking()
                .CountAsync(value => value.SubmissionId == started.SubmissionId))
            .Should().Be(participantCount);
        (await context.Set<AssessmentGradebookEntry>()
                .AsNoTracking()
                .CountAsync(value => value.SubmissionId == started.SubmissionId))
            .Should().Be(projectionCount);
        (await context.Set<AssessmentContentCompletionProjection>()
                .AsNoTracking()
                .CountAsync(value => value.SubmissionId == started.SubmissionId))
            .Should().Be(projectionCount);
        (await context.Set<AcademicOutboxMessage>()
                .AsNoTracking()
                .CountAsync(value =>
                    (value.EventType == "grade-result-finalized" ||
                     value.EventType == "grade-result-released") &&
                    value.PayloadCanonicalJson.Contains(started.SubmissionId.ToString())))
            .Should().Be(2);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string uri, object request)
    {
        using var response = await client.PostAsJsonAsync(uri, request, JsonOptions);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string uri)
    {
        using var response = await client.GetAsync(uri);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"Expected a successful HTTP response, received {(int)response.StatusCode}: {body}");
    }

    private static User User(Guid id, string name, string suffix) => new()
    {
        Id = id,
        Name = name,
        Email = $"{name.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant()}-{id:N}@{suffix}.test",
        Username = $"{name.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant()}-{id:N}",
        IsActive = true,
        IsEmailVerified = true,
    };

    private static InstructorReviewResolutionV1 Resolution(int scoreUnits) => new(
        1,
        [new InstructorItemResolutionV1("q1", ScoreValue.FromUnits(scoreUnits))],
        "Reviewed by instructor");

    private static void AssertSameDelivery(
        AssessmentSubmissionViewV1 actual,
        AssessmentSubmissionViewV1 expected)
    {
        var actualCanonical = CanonicalJson.Serialize(
            JsonSerializer.SerializeToElement(actual.Execution.Delivery, GradingJson.Options));
        var expectedCanonical = CanonicalJson.Serialize(
            JsonSerializer.SerializeToElement(expected.Execution.Delivery, GradingJson.Options));
        actualCanonical.Should().Be(expectedCanonical);
    }

    private static AssessmentResponseEnvelopeV1 TrueFalseAnswer(bool value) => new(
        1,
        "quiz",
        "quiz-answer/v1",
        JsonSerializer.SerializeToElement(new
        {
            answers = new Dictionary<string, object>
            {
                ["q1"] = new { type = "TRUE_FALSE", value },
            },
        }));

    private static AssessmentResponseEnvelopeV1 CodeResponse(string path) => new(1,
        CodeAssessmentContracts.ContentType, CodeAssessmentContracts.PayloadSchema,
        JsonSerializer.SerializeToElement(new { files = new Dictionary<string, object>
        {
            [path] = new { content = "// submitted-code\nint main(){return 0;}", encoding = "text" },
        } }));

    private static JsonElement CodeDefinition(string source) => JsonSerializer.SerializeToElement(new CodingAssignmentContent
    {
        Environment = new CodingEnvironment { Language = "cpp", Tools = "clang" },
        Data = new WorkspaceData { Files = new Dictionary<string, BundleFileMeta>
        {
            ["main.cpp"] = new() { Content = source },
            ["private.h"] = new() { Content = "private-code-secret", Visibility = "Private", Modifiable = false },
            ["readonly.h"] = new() { Content = "immutable-header", Modifiable = false },
        } },
        Tests = new TestSuite
        {
            Public = [new StandardTest { Stdout = "", Weight = 1 }],
            Private = [new StandardTest { Stdout = "private-code-secret", Weight = 1 }],
        },
        Grading = new GradingConfig { MaxScore = 2 },
    }, CodeAssessmentContracts.ContentJson);

    private static JsonElement TrueFalseQuiz() => Json("""
        {
          "schemaVersion": 1,
          "order": [["q1", "quiz"]],
          "blocks": {
            "q1": {
              "type": "TRUE_FALSE",
              "stem": "The statement is true.",
              "points": 200,
              "correctAnswer": true,
              "settings": { "allowRetry": false }
            }
          },
          "grading": { "schemaVersion": 2, "items": { "q1": {} } }
        }
        """);

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private sealed record Scenario(
        Guid TenantId,
        Guid InstructorId,
        Guid Learner1Id,
        Guid Learner2Id,
        Guid OutsiderId,
        Guid AssessmentId,
        Guid Enrollment1Id,
        Guid? CourseGroupId);
}

using System.Text.Json;
using FluentAssertions;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Capabilities;
using GameGuild.Learning.Assessments.Grading.Code;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Assessments.QuizAdapter;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class AssessmentGradingRuntimeTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" \t", " \t")]
    [InlineData("Instructor summary", "Instructor item feedback")]
    [InlineData("Instructor summary", null)]
    [InlineData(null, "Instructor item feedback")]
    public async Task OfficialCode_InstructorReviewPreservesTrustedFeedbackUnlessInstructorSuppliesIt(
        string? feedback, string? itemFeedback)
    {
        const string trustedFeedback = "Code tests completed by the trusted grading worker.";
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview | ReviewMethods.InstructorReview,
            CodeDefinition(), ContentType: ProgramContentType.Code));
        var started = await harness.StartIndividualAsync("code-feedback-start");
        var submitted = await harness.Runtime.SubmitOfficialAsync(started.SubmissionId, harness.Learner1Id,
            new SubmitAssessmentResponseCommand(CodeAnswer(), "code-feedback-submit"));
        submitted.Execution.RequiresInstructorReview.Should().BeTrue();
        var resolution = new InstructorReviewResolutionV1(1,
            [new(CodeAssessmentContracts.ItemId, ScoreValue.FromUnits(200), itemFeedback)], feedback);
        var reviewed = await harness.Runtime.ResolveOfficialInstructorReviewAsync(
            started.SubmissionId, harness.InstructorId, resolution, "code-feedback-review");
        var replay = await harness.Runtime.ResolveOfficialInstructorReviewAsync(
            started.SubmissionId, harness.InstructorId, resolution, "code-feedback-review");

        var expectedFeedback = string.IsNullOrWhiteSpace(feedback) ? trustedFeedback : feedback;
        var expectedItemFeedback = string.IsNullOrWhiteSpace(itemFeedback) ? trustedFeedback : itemFeedback;
        reviewed.Status.Should().Be(SubmissionStatus.Graded);
        reviewed.Execution.InstructorVisibleResult!.Feedback.Should().Be(expectedFeedback);
        reviewed.Execution.InstructorVisibleResult.Items.Single().Feedback.Should().Be(expectedItemFeedback);
        replay.Execution.InstructorVisibleResult.Should().BeEquivalentTo(reviewed.Execution.InstructorVisibleResult);
        replay.Execution.ActiveRoundId.Should().Be(reviewed.Execution.ActiveRoundId);
        var persisted = await harness.Context.Set<AssessmentSubmission>().AsNoTracking()
            .SingleAsync(value => value.Id == started.SubmissionId);
        persisted.Feedback.Should().Be(expectedFeedback);
        persisted.Score.Should().Be(ScoreValue.FromUnits(200));
        var evidence = await harness.Context.Set<ReviewEvidence>()
            .SingleAsync(value => value.ProducedByActorId == harness.InstructorId);
        using var payload = JsonDocument.Parse(evidence.CanonicalJson);
        (payload.RootElement.TryGetProperty("feedback", out var originalFeedback) ? originalFeedback.GetString() : null)
            .Should().Be(feedback);
        (payload.RootElement.GetProperty("items")[0].TryGetProperty("feedback", out var originalItemFeedback)
            ? originalItemFeedback.GetString() : null).Should().Be(itemFeedback);
        payload.RootElement.GetProperty("previousResult").GetProperty("items")[0]
            .GetProperty("feedback").GetString().Should().Be(trustedFeedback);

        await harness.Release.ReleaseByActorAsync(started.SubmissionId, reviewed.Execution.ActiveRoundId!.Value,
            reviewed.Version, harness.InstructorId, "code-feedback-release", "Release reviewed Code feedback");
        var learner = await harness.Runtime.GetSubmissionAsync(started.SubmissionId, harness.Learner1Id, false);
        learner.Execution.LearnerVisibleResult!.Feedback.Should().Be(expectedFeedback);
        JsonSerializer.Serialize(learner.Execution.LearnerVisibleResult, GradingJson.Options)
            .Should().NotContain("private-code-secret");
    }

    [Theory]
    [InlineData(ProgramContentType.Code, ReviewMethods.InstructorReview)]
    [InlineData(ProgramContentType.Questionnaire, ReviewMethods.AutomatedReview | ReviewMethods.InstructorReview)]
    public async Task OfficialInstructorReview_DoesNotInventCodeFeedbackWithoutATrustedCodeResult(
        ProgramContentType contentType, ReviewMethods methods)
    {
        var code = contentType == ProgramContentType.Code;
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(methods,
            code ? CodeDefinition() : TrueFalseQuiz(), ContentType: contentType));
        var started = await harness.StartIndividualAsync("no-code-feedback-start");
        await harness.Runtime.SubmitOfficialAsync(started.SubmissionId, harness.Learner1Id,
            new SubmitAssessmentResponseCommand(code ? CodeAnswer() : TrueFalseAnswer(true), "no-code-feedback-submit"));
        var reviewed = await harness.Runtime.ResolveOfficialInstructorReviewAsync(started.SubmissionId,
            harness.InstructorId, new InstructorReviewResolutionV1(1,
                [new(code ? CodeAssessmentContracts.ItemId : "q1", ScoreValue.FromUnits(200))]), "no-code-feedback-review");
        reviewed.Execution.InstructorVisibleResult!.Feedback.Should().BeNull();
        reviewed.Execution.InstructorVisibleResult.Items.Single().Feedback.Should().BeNull();
        (await harness.Context.Set<AssessmentSubmission>().SingleAsync(value => value.Id == started.SubmissionId))
            .Feedback.Should().BeNull();
    }

    [Fact]
    public async Task AuthorTest_InstructorReviewIsIdempotentIsolatedAndRestartedAsANewExecution()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.InstructorReview,
            TrueFalseQuiz(),
            Publish: false));

        var start = new StartAssessmentTestRunCommand(
            harness.RevisionId,
            "instructor-preview",
            "Instructor preview",
            "start-test-1");
        var run = await harness.Runtime.StartTestRunAsync(harness.AssessmentId, harness.InstructorId, start);
        var replay = await harness.Runtime.StartTestRunAsync(harness.AssessmentId, harness.InstructorId, start);

        replay.TestRunId.Should().Be(run.TestRunId);
        replay.Execution.ExecutionId.Should().Be(run.Execution.ExecutionId);
        replay.Execution.DeliveryHash.Should().Be(run.Execution.DeliveryHash);
        CanonicalJson.Serialize(JsonSerializer.SerializeToElement(replay.Execution.Delivery, GradingJson.Options))
            .Should().Be(CanonicalJson.Serialize(JsonSerializer.SerializeToElement(run.Execution.Delivery, GradingJson.Options)));

        var submit = await harness.Runtime.SubmitTestRunAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "submit-test-1"));
        submit.Execution.RequiresInstructorReview.Should().BeTrue();

        var completed = await harness.Runtime.ResolveTestInstructorReviewAsync(
            run.TestRunId,
            harness.InstructorId,
            Resolution(("q1", 175)),
            "resolve-test-1");
        var resolveReplay = await harness.Runtime.ResolveTestInstructorReviewAsync(
            run.TestRunId,
            harness.InstructorId,
            Resolution(("q1", 175)),
            "resolve-test-1");

        completed.Status.Should().Be(AssessmentTestRunStatus.Completed);
        completed.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(175));
        resolveReplay.Execution.ActiveRoundId.Should().Be(completed.Execution.ActiveRoundId);
        (await harness.Context.Set<GradeRound>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<ReviewEvidence>().SingleAsync()).ProducedByActorId.Should().Be(harness.InstructorId);
        (await harness.Context.Set<AssessmentSubmission>().CountAsync()).Should().Be(0);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(0);
        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(0);
        (await harness.AcademicEventTypes()).Should().BeEmpty();

        var restarted = await harness.Runtime.RestartTestRunAsync(
            run.TestRunId,
            harness.InstructorId,
            "restart-test-1");
        restarted.TestRunId.Should().NotBe(run.TestRunId);
        restarted.Execution.ExecutionId.Should().NotBe(run.Execution.ExecutionId);
        restarted.DefinitionRevisionId.Should().Be(run.DefinitionRevisionId);
        restarted.Execution.ExecutionSnapshotHash.Should().Be(run.Execution.ExecutionSnapshotHash);
    }

    [Fact]
    public async Task AuthorTest_AutomatedReviewCompletesKnownItemsAndHandsPartialCoverageToInstructor()
    {
        await using var automatic = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview,
            TrueFalseQuiz(),
            Publish: false));
        var automaticRun = await automatic.StartTestRunAsync();

        var completed = await automatic.Runtime.SubmitTestRunAsync(
            automaticRun.TestRunId,
            automatic.InstructorId,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "automatic-submit"));

        completed.Status.Should().Be(AssessmentTestRunStatus.Completed);
        completed.Execution.InstructorVisibleResult!.State.Should().Be("final");
        completed.Execution.InstructorVisibleResult.Score.Should().Be(ScoreValue.FromUnits(200));
        completed.ReadyForPublication.Should().BeTrue();
        (await automatic.AcademicEventTypes()).Should().BeEmpty();

        await using var combined = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview | ReviewMethods.InstructorReview,
            MixedQuiz(),
            Publish: false));
        var combinedRun = await combined.StartTestRunAsync();
        var partial = await combined.Runtime.SubmitTestRunAsync(
            combinedRun.TestRunId,
            combined.InstructorId,
            new SubmitAssessmentResponseCommand(MixedAnswer(true, "Because it is."), "combined-submit"));

        partial.Execution.RequiresInstructorReview.Should().BeTrue();
        partial.Execution.InstructorVisibleResult!.Items.Should().ContainEquivalentOf(
            new
            {
                ItemId = "q1",
                State = GradeItemState.Graded,
                Score = (ScoreValue?)ScoreValue.FromUnits(200),
            });
        partial.Execution.InstructorVisibleResult.Items.Should().ContainEquivalentOf(
            new
            {
                ItemId = "q2",
                State = GradeItemState.Pending,
                Score = (ScoreValue?)null,
            });

        var reviewed = await combined.Runtime.ResolveTestInstructorReviewAsync(
            combinedRun.TestRunId,
            combined.InstructorId,
            Resolution(("q1", 200), ("q2", 325)),
            "combined-resolve");
        reviewed.Status.Should().Be(AssessmentTestRunStatus.Completed);
        reviewed.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(525));
        reviewed.Execution.History.Should().ContainSingle();
        (await combined.AcademicEventTypes()).Should().BeEmpty();
    }

    [Fact]
    public async Task OfficialIndividual_RetainsThenReleasesOneCanonicalResultAndSurvivesUnpublish()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            ContentCompletionMode.OnReleaseAndPass,
            PassingScoreUnits: 150,
            GradebookWeightUnits: 5000));

        var started = await harness.StartIndividualAsync("individual-start");
        var replay = await harness.StartIndividualAsync("individual-start");
        replay.SubmissionId.Should().Be(started.SubmissionId);
        replay.Execution.ExecutionId.Should().Be(started.Execution.ExecutionId);
        replay.Execution.DeliveryHash.Should().Be(started.Execution.DeliveryHash);

        var assessment = await harness.Context.Set<Assessment>().SingleAsync();
        var unpublish = await harness.Authoring.UnpublishAsync(
            assessment.Id,
            harness.InstructorId,
            new UnpublishAssessmentRevisionRequest(
                harness.RevisionId,
                assessment.Version,
                "unpublish-after-start"));
        unpublish.IsSuccess.Should().BeTrue();
        var resumedAfterUnpublish = await harness.StartIndividualAsync("resume-after-unpublish");
        resumedAfterUnpublish.SubmissionId.Should().Be(started.SubmissionId);
        var newStartAfterUnpublish = () => harness.StartIndividualAsync(
            "new-start-after-unpublish",
            harness.Enrollment2Id,
            harness.Learner2Id);
        await newStartAfterUnpublish.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*published executable revision*");

        var submitted = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "individual-submit"));
        var submitReplay = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "individual-submit"));
        submitted.Status.Should().Be(SubmissionStatus.Graded);
        submitReplay.Execution.ActiveRoundId.Should().Be(submitted.Execution.ActiveRoundId);
        submitted.ContentCompleted.Should().BeFalse();
        submitted.Execution.LearnerVisibleResult.Should().BeNull();
        submitted.Execution.InstructorVisibleResult.Should().BeNull();
        submitted.Execution.History.Should().BeEmpty();
        (await harness.Context.Set<GradeRound>().CountAsync()).Should().Be(1);
        var divergentSubmit = () => harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(false), "individual-submit"));
        await divergentSubmit.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different request*");

        var persisted = await harness.Context.Set<AssessmentSubmission>()
            .AsNoTracking()
            .SingleAsync(value => value.Id == started.SubmissionId);
        persisted.Score.Should().Be(ScoreValue.FromUnits(200));
        persisted.Passed.Should().BeTrue();
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AssessmentContentCompletionProjection>().CountAsync()).Should().Be(0);

        var privateGradebook = await harness.Gradebook.GetCourseProjectionAsync(
            harness.CourseId,
            harness.Enrollment1Id,
            false);
        privateGradebook.CoursePercentUnits.Should().Be(5000);
        var learnerGradebook = await harness.Gradebook.GetCourseProjectionAsync(
            harness.CourseId,
            harness.Enrollment1Id,
            true);
        learnerGradebook.LearnerVisible.Should().BeFalse();
        learnerGradebook.CoursePercentUnits.Should().BeNull();

        var roundId = submitted.Execution.ActiveRoundId!.Value;
        var release = await harness.Release.ReleaseByActorAsync(
            submitted.SubmissionId,
            roundId,
            submitted.Version,
            harness.InstructorId,
            "release-individual",
            "Reviewed");
        var releaseReplay = await harness.Release.ReleaseByActorAsync(
            submitted.SubmissionId,
            roundId,
            submitted.Version,
            harness.InstructorId,
            "release-individual",
            "Reviewed");

        releaseReplay.Id.Should().Be(release.Id);
        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AssessmentContentCompletionProjection>().CountAsync()).Should().Be(1);
        var visible = await harness.Runtime.GetSubmissionAsync(
            submitted.SubmissionId,
            harness.Learner1Id,
            false);
        visible.ContentCompleted.Should().BeTrue();
        visible.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(200));
        visible.Execution.History.Should().ContainSingle(value => value.Released);

        learnerGradebook = await harness.Gradebook.GetCourseProjectionAsync(
            harness.CourseId,
            harness.Enrollment1Id,
            true);
        learnerGradebook.LearnerVisible.Should().BeTrue();
        learnerGradebook.CoursePercentUnits.Should().Be(5000);

        (await harness.AcademicEventTypes()).Should().BeEquivalentTo(
            ["grade-result-finalized", "grade-result-released"]);
        var divergentReplay = () => harness.Release.ReleaseByActorAsync(
            submitted.SubmissionId,
            roundId,
            submitted.Version,
            harness.InstructorId,
            "release-individual",
            "Different reason");
        await divergentReplay.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different release request*");
    }

    [Fact]
    public async Task OfficialImmediateRelease_IsDurableAndWorkerRetryIsIdempotent()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Immediate,
            ContentCompletionMode.OnReleaseAndPass,
            PassingScoreUnits: 150,
            GradebookWeightUnits: 5000));
        var started = await harness.StartIndividualAsync("immediate-start");

        var submitted = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "immediate-submit"));

        submitted.ContentCompleted.Should().BeFalse();
        submitted.Execution.LearnerVisibleResult.Should().BeNull();
        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(0);
        (await harness.Context.Set<AssessmentContentCompletionProjection>().CountAsync()).Should().Be(0);
        var releaseRequest = await harness.Context.Set<AcademicOutboxMessage>()
            .SingleAsync(value => value.EventType == "grade-result-release-requested");
        var delivery = await harness.Context.Set<AcademicOutboxDelivery>()
            .SingleAsync(value => value.OutboxMessageId == releaseRequest.Id);
        delivery.ConsumerKey.Should().Be("grading.immediate-release.v1");
        delivery.Status.Should().Be(AcademicOutboxDeliveryStatus.Pending);

        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(harness.Context);
        services.AddSingleton<IGradeReleaseService>(harness.Release);
        services.AddScoped<IAcademicOutboxConsumer, ImmediateGradeReleaseConsumer>();
        using var provider = services.BuildServiceProvider();
        var dispatcher = new AcademicOutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AcademicOutboxDispatcher>.Instance);

        (await dispatcher.DispatchBatchAsync()).Should().Be(1);
        (await dispatcher.DispatchBatchAsync()).Should().Be(0);

        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AssessmentContentCompletionProjection>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<GradingCommandReceipt>()
            .CountAsync(value => value.CommandType == "release-grade-result")).Should().Be(1);
        delivery = await harness.Context.Set<AcademicOutboxDelivery>()
            .SingleAsync(value => value.OutboxMessageId == releaseRequest.Id);
        delivery.Status.Should().Be(AcademicOutboxDeliveryStatus.Confirmed);

        var visible = await harness.Runtime.GetSubmissionAsync(
            submitted.SubmissionId,
            harness.Learner1Id,
            false);
        visible.ContentCompleted.Should().BeTrue();
        visible.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(200));
        var learnerGradebook = await harness.Gradebook.GetCourseProjectionAsync(
            harness.CourseId,
            harness.Enrollment1Id,
            true);
        learnerGradebook.LearnerVisible.Should().BeTrue();
        learnerGradebook.CoursePercentUnits.Should().Be(5000);
        (await harness.AcademicEventTypes()).Should().BeEquivalentTo(
            ["grade-result-finalized", "grade-result-release-requested", "grade-result-released"]);
    }

    [Fact]
    public async Task OfficialOnSubmit_CompletesBeforeInstructorReviewWithoutCreatingGradebookPlacement()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.InstructorReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            ContentCompletionMode.OnSubmit));
        var started = await harness.StartIndividualAsync("on-submit-start");

        var submitted = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "on-submit-answer"));

        submitted.Execution.RequiresInstructorReview.Should().BeTrue();
        submitted.ContentCompleted.Should().BeTrue();
        (await harness.Context.Set<AssessmentContentCompletionProjection>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(0,
            "an assessment without a grading group has a result but no gradebook placement");
    }

    [Fact]
    public async Task OfficialOnFinalize_CompletesAtDeterministicFinalizationWithoutCreatingGradebookPlacement()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            ContentCompletionMode.OnFinalize));
        var started = await harness.StartIndividualAsync("on-finalize-start");

        var submitted = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "on-finalize-answer"));

        submitted.Status.Should().Be(SubmissionStatus.Graded);
        submitted.ContentCompleted.Should().BeTrue();
        (await harness.Context.Set<AssessmentContentCompletionProjection>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(0);
        var gradebook = await harness.Gradebook.GetCourseProjectionAsync(
            harness.CourseId,
            harness.Enrollment1Id,
            learnerView: false);
        gradebook.CoursePercentUnits.Should().Be(0);
        gradebook.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task OfficialOnReleaseAndPass_DoesNotCompleteAReleasedFailingResult()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            ContentCompletionMode.OnReleaseAndPass,
            PassingScoreUnits: 150));
        var started = await harness.StartIndividualAsync("failed-release-start");
        var submitted = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(false), "failed-release-answer"));

        await harness.Release.ReleaseByActorAsync(
            submitted.SubmissionId,
            submitted.Execution.ActiveRoundId!.Value,
            submitted.Version,
            harness.InstructorId,
            "failed-release",
            null);

        var visible = await harness.Runtime.GetSubmissionAsync(
            submitted.SubmissionId,
            harness.Learner1Id,
            false);
        visible.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.Zero);
        visible.ContentCompleted.Should().BeFalse();
        (await harness.Context.Set<AssessmentContentCompletionProjection>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OfficialRegrade_PreservesReleasedLearnerResultUntilTheNewRoundIsReleased()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.InstructorReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            GradebookWeightUnits: 4000));
        var started = await harness.StartIndividualAsync("regrade-start");
        var awaiting = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "regrade-submit"));
        var first = await harness.Runtime.ResolveOfficialInstructorReviewAsync(
            started.SubmissionId,
            harness.InstructorId,
            Resolution(("q1", 100)),
            "first-review");
        await harness.Release.ReleaseByActorAsync(
            first.SubmissionId,
            first.Execution.ActiveRoundId!.Value,
            first.Version,
            harness.InstructorId,
            "release-first",
            null);

        var opened = await harness.Runtime.RegradeOfficialAsync(
            started.SubmissionId,
            harness.InstructorId,
            new RegradeExecutionCommand("open-regrade", "Correction requested"));
        opened.Execution.RequiresInstructorReview.Should().BeTrue();
        var duringRegrade = await harness.Runtime.GetSubmissionAsync(
            started.SubmissionId,
            harness.Learner1Id,
            false);
        duringRegrade.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(100));

        var second = await harness.Runtime.ResolveOfficialInstructorReviewAsync(
            started.SubmissionId,
            harness.InstructorId,
            Resolution([("q1", 175)], "Accepted alternative reasoning"),
            "second-review");
        second.Execution.History.Should().HaveCount(2);
        var beforeSecondRelease = await harness.Runtime.GetSubmissionAsync(
            started.SubmissionId,
            harness.Learner1Id,
            false);
        beforeSecondRelease.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(100));
        beforeSecondRelease.Execution.History.Should().ContainSingle();
        var learnerResults = await new AssessmentLearnerResultProjectionService(harness.Context)
            .GetLatestReleasedAsync([started.SubmissionId]);
        learnerResults[started.SubmissionId].Score.Should().Be(ScoreValue.FromUnits(100));

        var learnerGradebook = await harness.Gradebook.GetCourseProjectionAsync(
            harness.CourseId,
            harness.Enrollment1Id,
            true);
        learnerGradebook.CoursePercentUnits.Should().Be(2000);

        await harness.Release.ReleaseByActorAsync(
            second.SubmissionId,
            second.Execution.ActiveRoundId!.Value,
            second.Version,
            harness.InstructorId,
            "release-second",
            null);
        var afterSecondRelease = await harness.Runtime.GetSubmissionAsync(
            started.SubmissionId,
            harness.Learner1Id,
            false);
        afterSecondRelease.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(175));
        afterSecondRelease.Execution.History.Should().HaveCount(2);
        learnerResults = await new AssessmentLearnerResultProjectionService(harness.Context)
            .GetLatestReleasedAsync([started.SubmissionId]);
        learnerResults[started.SubmissionId].Score.Should().Be(ScoreValue.FromUnits(175));
    }

    [Fact]
    public async Task CollectiveSubmission_UsesOneExecutionVersionedDraftAndFrozenParticipants()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            ContentCompletionMode.OnRelease,
            GradebookWeightUnits: 2500,
            Collective: true));

        var first = await harness.Runtime.StartCollectiveSubmissionAsync(
            harness.AssessmentId,
            new StartCollectiveSubmissionCommand(harness.CourseGroupId!.Value, harness.Learner1Id, "group-start-1"));
        var secondMemberResume = await harness.Runtime.StartCollectiveSubmissionAsync(
            harness.AssessmentId,
            new StartCollectiveSubmissionCommand(harness.CourseGroupId.Value, harness.Learner2Id, "group-start-2"));
        secondMemberResume.SubmissionId.Should().Be(first.SubmissionId);
        secondMemberResume.Execution.ExecutionId.Should().Be(first.Execution.ExecutionId);
        secondMemberResume.Execution.DeliveryHash.Should().Be(first.Execution.DeliveryHash);

        var draft1 = await harness.Runtime.SaveCollectiveDraftAsync(
            first.SubmissionId,
            harness.Learner1Id,
            new SaveCollectiveAssessmentDraftCommand(TrueFalseAnswer(false), 0, "draft-1"));
        draft1.DraftVersion.Should().Be(1);
        var draftReplay = await harness.Runtime.SaveCollectiveDraftAsync(
            first.SubmissionId,
            harness.Learner1Id,
            new SaveCollectiveAssessmentDraftCommand(TrueFalseAnswer(false), 0, "draft-1"));
        draftReplay.DraftVersion.Should().Be(1);
        (await harness.Context.Set<CollectiveAttemptDraftChange>().CountAsync()).Should().Be(1);
        var divergentDraftReplay = () => harness.Runtime.SaveCollectiveDraftAsync(
            first.SubmissionId,
            harness.Learner1Id,
            new SaveCollectiveAssessmentDraftCommand(TrueFalseAnswer(true), 0, "draft-1"));
        await divergentDraftReplay.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different request*");

        var stale = () => harness.Runtime.SaveCollectiveDraftAsync(
            first.SubmissionId,
            harness.Learner2Id,
            new SaveCollectiveAssessmentDraftCommand(TrueFalseAnswer(true), 0, "draft-stale"));
        await stale.Should().ThrowAsync<InvalidOperationException>().WithMessage("*stale*");

        var draft2 = await harness.Runtime.SaveCollectiveDraftAsync(
            first.SubmissionId,
            harness.Learner2Id,
            new SaveCollectiveAssessmentDraftCommand(TrueFalseAnswer(true), 1, "draft-2"));
        var finalized = await harness.Runtime.SubmitOfficialAsync(
            first.SubmissionId,
            harness.Learner2Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "group-submit", draft2.DraftVersion));
        var submitReplay = await harness.Runtime.SubmitOfficialAsync(
            first.SubmissionId,
            harness.Learner2Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "group-submit", draft2.DraftVersion));
        finalized.Status.Should().Be(SubmissionStatus.Graded);
        submitReplay.Execution.ActiveRoundId.Should().Be(finalized.Execution.ActiveRoundId);
        var divergentSubmitReplay = () => harness.Runtime.SubmitOfficialAsync(
            first.SubmissionId,
            harness.Learner2Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(false), "group-submit", draft2.DraftVersion));
        await divergentSubmitReplay.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different request*");

        (await harness.Context.Set<AssessmentSubmission>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<GradingExecution>().CountAsync(value =>
            value.ExecutionContext == ReviewExecutionContext.OfficialSubmission)).Should().Be(1);
        (await harness.Context.Set<GradeRound>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AssessmentSubmissionParticipant>().CountAsync()).Should().Be(2);
        (await harness.Context.Set<CollectiveAttemptDraftChange>().CountAsync()).Should().Be(2);

        var outsider = Guid.NewGuid();
        harness.Context.Set<CourseGroupMember>().Add(CourseGroupMember.Create(harness.CourseGroupId.Value, outsider));
        await harness.Context.SaveChangesAsync();
        var outsiderRead = () => harness.Runtime.GetSubmissionAsync(first.SubmissionId, outsider, false);
        await outsiderRead.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*frozen group participant snapshot*");

        await harness.Release.ReleaseByActorAsync(
            finalized.SubmissionId,
            finalized.Execution.ActiveRoundId!.Value,
            finalized.Version,
            harness.InstructorId,
            "release-group",
            null);
        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(2);
        (await harness.Context.Set<AssessmentContentCompletionProjection>().CountAsync()).Should().Be(2);
        (await harness.AcademicEventTypes()).Count(value => value == "grade-result-finalized").Should().Be(1);
        (await harness.AcademicEventTypes()).Count(value => value == "grade-result-released").Should().Be(1);
    }

    [Fact]
    public async Task CollectiveInstructorReview_GradesOnceAndProjectsTheSharedResultToEveryFrozenParticipant()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.InstructorReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            ContentCompletionMode.OnRelease,
            GradebookWeightUnits: 3000,
            Collective: true));
        var started = await harness.Runtime.StartCollectiveSubmissionAsync(
            harness.AssessmentId,
            new StartCollectiveSubmissionCommand(harness.CourseGroupId!.Value, harness.Learner1Id, "review-group-start"));
        var awaitingReview = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner2Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "review-group-submit", 0));

        awaitingReview.Execution.RequiresInstructorReview.Should().BeTrue();
        var finalized = await harness.Runtime.ResolveOfficialInstructorReviewAsync(
            started.SubmissionId,
            harness.InstructorId,
            Resolution(("q1", 175)),
            "review-group-resolve");
        finalized.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(175));
        (await harness.Context.Set<GradingExecution>().CountAsync(value =>
            value.ExecutionContext == ReviewExecutionContext.OfficialSubmission)).Should().Be(1);
        (await harness.Context.Set<GradeRound>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<ReviewEvidence>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(2);

        await harness.Release.ReleaseByActorAsync(
            finalized.SubmissionId,
            finalized.Execution.ActiveRoundId!.Value,
            finalized.Version,
            harness.InstructorId,
            "review-group-release",
            null);

        var firstParticipant = await harness.Runtime.GetSubmissionAsync(
            finalized.SubmissionId,
            harness.Learner1Id,
            false);
        var secondParticipant = await harness.Runtime.GetSubmissionAsync(
            finalized.SubmissionId,
            harness.Learner2Id,
            false);
        firstParticipant.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(175));
        secondParticipant.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(175));
        firstParticipant.ContentCompleted.Should().BeTrue();
        secondParticipant.ContentCompleted.Should().BeTrue();
        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AssessmentContentCompletionProjection>().CountAsync()).Should().Be(2);
        (await harness.AcademicEventTypes()).Count(value => value == "grade-result-finalized").Should().Be(1);
        (await harness.AcademicEventTypes()).Count(value => value == "grade-result-released").Should().Be(1);
    }

    [Fact]
    public void ReleaseModel_BindsTheRoundToTheSameOfficialExecutionWithoutDuplicatingSubmissionId()
    {
        using var context = RuntimeTestContext.Create();
        var release = context.Model.FindEntityType(typeof(GradeResultRelease))!;

        release.FindProperty("AssessmentSubmissionId").Should().BeNull();
        release.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(GradeRound) &&
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(GradeResultRelease.GradeRoundId), nameof(GradeResultRelease.GradingExecutionId) }));
        release.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(GradingExecution) &&
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(GradeResultRelease.GradingExecutionId), nameof(GradeResultRelease.ExecutionContext) }));
    }

    private static InstructorReviewResolutionV1 Resolution(
        params (string ItemId, int ScoreUnits)[] items) =>
        Resolution(items, null);

    private static InstructorReviewResolutionV1 Resolution(
        (string ItemId, int ScoreUnits)[] items,
        string? overrideReason) =>
        new(
            1,
            items.Select(item => new InstructorItemResolutionV1(
                item.ItemId,
                ScoreValue.FromUnits(item.ScoreUnits))).ToArray(),
            "Instructor feedback",
            overrideReason);

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

    private static JsonElement MixedQuiz() => Json("""
        {
          "schemaVersion": 1,
          "order": [["q1", "quiz"], ["q2", "quiz"]],
          "blocks": {
            "q1": {
              "type": "TRUE_FALSE",
              "stem": "The statement is true.",
              "points": 200,
              "correctAnswer": true,
              "settings": { "allowRetry": false }
            },
            "q2": {
              "type": "ESSAY",
              "stem": "Explain the result.",
              "points": 400,
              "settings": { "allowRetry": false }
            }
          },
          "grading": { "schemaVersion": 2, "items": { "q1": {}, "q2": {} } }
        }
        """);

    private static AssessmentResponseEnvelopeV1 TrueFalseAnswer(bool value) =>
        new(
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

    private static AssessmentResponseEnvelopeV1 MixedAnswer(bool value, string essay) =>
        new(
            1,
            "quiz",
            "quiz-answer/v1",
            JsonSerializer.SerializeToElement(new
            {
                answers = new Dictionary<string, object>
                {
                    ["q1"] = new { type = "TRUE_FALSE", value },
                    ["q2"] = new { type = "ESSAY", richText = (object?)null, plainText = essay },
                },
            }));

    private static AssessmentResponseEnvelopeV1 Envelope(string payload) =>
        new(1, "quiz", "quiz-answer/v1", Json(payload));

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static JsonElement CodeDefinition() => JsonSerializer.SerializeToElement(new CodingAssignmentContent
    {
        Environment = new CodingEnvironment { Language = "cpp", Tools = "clang" },
        Data = new WorkspaceData { Files = new Dictionary<string, BundleFileMeta>
        {
            ["main.cpp"] = new() { Content = "int main(){return 0;}" },
            ["hidden.h"] = new() { Content = "private-code-secret", Visibility = "Private", Modifiable = false },
        } },
        Tests = new TestSuite
        {
            Public = [new StandardTest { Stdout = "", Weight = 1 }],
            Private = [new StandardTest { Stdout = "private-code-secret", Weight = 1 }],
        },
        Grading = new GradingConfig { MaxScore = 2 },
    }, CodeAssessmentContracts.ContentJson);

    private static AssessmentResponseEnvelopeV1 CodeAnswer() => new(1,
        CodeAssessmentContracts.ContentType, CodeAssessmentContracts.PayloadSchema,
        JsonSerializer.SerializeToElement(new { files = new Dictionary<string, object>
        {
            ["main.cpp"] = new { content = "// submitted-code\nint main(){return 0;}", encoding = "text" },
        } }));

    private sealed class PassingCodeExecutor : ICodeAssessmentExecutor
    {
        public Task<CodeExecutionReceipt> ExecuteAsync(CodingAssignmentContent definition, JsonElement files,
            CodeToolchainIdentity toolchain, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            toolchain.Should().Be(CodeToolchainIdentity.Version1);
            definition.Tests.Private.Should().ContainSingle().Which.Should().BeOfType<StandardTest>()
                .Which.Stdout.Should().Be("private-code-secret");
            files.GetProperty("main.cpp").GetProperty("content").GetString().Should().Contain("submitted-code");
            return Task.FromResult(new CodeExecutionReceipt(
                definition.Tests.Public.Concat(definition.Tests.Private).Select(_ => true).ToArray(), new string('a', 64)));
        }
    }

    private sealed record RuntimeOptions(
        ReviewMethods ReviewMethods,
        JsonElement Document,
        ResultReleaseMode ReleaseMode = ResultReleaseMode.Manual,
        ContentCompletionMode CompletionMode = ContentCompletionMode.OnReleaseAndPass,
        int PassingScoreUnits = 100,
        int? GradebookWeightUnits = null,
        bool Publish = true,
        bool Collective = false,
        ProgramContentType ContentType = ProgramContentType.Questionnaire);

    private sealed class RuntimeHarness : IAsyncDisposable
    {
        private RuntimeHarness(
            RuntimeTestContext context,
            AssessmentAuthoringService authoring,
            AssessmentGradingRuntimeService runtime,
            GradeReleaseService release,
            AssessmentGradebookProjectionService gradebook,
            Guid tenantId,
            Guid courseId,
            Guid assessmentId,
            Guid revisionId,
            Guid instructorId,
            Guid learner1Id,
            Guid learner2Id,
            Guid enrollment1Id,
            Guid enrollment2Id,
            Guid? courseGroupId)
        {
            Context = context;
            Authoring = authoring;
            Runtime = runtime;
            Release = release;
            Gradebook = gradebook;
            TenantId = tenantId;
            CourseId = courseId;
            AssessmentId = assessmentId;
            RevisionId = revisionId;
            InstructorId = instructorId;
            Learner1Id = learner1Id;
            Learner2Id = learner2Id;
            Enrollment1Id = enrollment1Id;
            Enrollment2Id = enrollment2Id;
            CourseGroupId = courseGroupId;
        }

        public RuntimeTestContext Context { get; }
        public AssessmentAuthoringService Authoring { get; }
        public AssessmentGradingRuntimeService Runtime { get; }
        public GradeReleaseService Release { get; }
        public AssessmentGradebookProjectionService Gradebook { get; }
        public Guid TenantId { get; }
        public Guid CourseId { get; }
        public Guid AssessmentId { get; }
        public Guid RevisionId { get; }
        public Guid InstructorId { get; }
        public Guid Learner1Id { get; }
        public Guid Learner2Id { get; }
        public Guid Enrollment1Id { get; }
        public Guid Enrollment2Id { get; }
        public Guid? CourseGroupId { get; }

        public static async Task<RuntimeHarness> CreateAsync(RuntimeOptions options)
        {
            var context = RuntimeTestContext.Create();
            var tenantId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var contentId = Guid.NewGuid();
            var instructorId = Guid.NewGuid();
            var learner1Id = Guid.NewGuid();
            var learner2Id = Guid.NewGuid();
            var content = new ProgramContent
            {
                Id = contentId,
                TenantId = tenantId,
                ProgramId = courseId,
                Title = "Quiz",
                Slug = "quiz",
                Type = options.ContentType,
                JsonBody = "{\"schemaVersion\":1,\"order\":[],\"blocks\":{}}",
                LessonFormat = null,
            };
            context.Set<ProgramContent>().Add(content);
            await context.SaveChangesAsync();

            var registry = new ReviewCapabilityRegistry();
            new CoreGradingCapabilityRegistration().Register(registry);
            new QuizCapabilityRegistration().Register(registry);
            new CodeCapabilityRegistration().Register(registry);
            var quizAdapter = new QuizAssessmentTypeAdapter(
                new QuizAuthoringAdapter(new QuizItemProjector()),
                new QuizDeliveryGenerator(),
                new QuizAnswerDecoder(),
                new QuizDeterministicReviewAlgorithm());
            var instructorHandler = new InstructorReviewStageHandler();
            var automatedHandler = new QuizAutomatedReviewStageHandler(quizAdapter);
            var codeAdapter = new CodeAssessmentTypeAdapter(new PassingCodeExecutor());
            IReviewStageHandler[] handlers = [instructorHandler, automatedHandler, new CodeAutomatedReviewStageHandler(codeAdapter)];
            var adapters = new AssessmentTypeAdapterResolver(registry, [quizAdapter, codeAdapter]);
            var policies = new AssessmentExecutionPolicyResolver(registry);
            var stages = new ReviewStageHandlerResolver(registry, handlers);
            var outbox = new AcademicOutboxWriter(context, [new RouteOnlyReleaseConsumer()]);
            var authoring = new AssessmentAuthoringService(
                context,
                adapters,
                policies,
                stages,
                handlers,
                outbox,
                NullLogger<AssessmentAuthoringService>.Instance);
            var saved = await authoring.SaveDraftAsync(
                courseId,
                contentId,
                instructorId,
                new SaveAssessmentDraftRequest(
                    content.Version,
                    null,
                    "Quiz",
                    "quiz",
                    null,
                    options.Document,
                    Visibility.Public,
                    true,
                    null,
                    EstimatedMinutesSource.Auto,
                    options.ReviewMethods,
                    ScoreValue.FromUnits(options.PassingScoreUnits),
                    MaxAttempts: 1,
                    ContentCompletionMode: options.CompletionMode,
                    ResultReleaseMode: options.ReleaseMode,
                    ReviewConfigurationCanonicalJson: options.ContentType == ProgramContentType.Code &&
                        options.ReviewMethods.HasFlag(ReviewMethods.InstructorReview)
                        ? "{\"schemaVersion\":1,\"instructor\":{\"requireOverrideReason\":false}}" : null));
            saved.IsSuccess.Should().BeTrue();
            var assessment = await context.Set<Assessment>().SingleAsync();

            if (options.GradebookWeightUnits.HasValue)
            {
                var gradebookGroup = AssessmentGroup.Create(
                    courseId,
                    "Course grade",
                    PercentValue.FromUnits(options.GradebookWeightUnits.Value));
                gradebookGroup.TenantId = tenantId;
                context.Set<AssessmentGroup>().Add(gradebookGroup);
                assessment.AssignToGroup(gradebookGroup.Id);
            }

            Guid? courseGroupId = null;
            if (options.Collective)
            {
                var groupSet = CourseGroupSet.Create(courseId, "Project teams");
                var group = CourseGroup.Create(groupSet.Id, "Team One", 4);
                context.Set<CourseGroupSet>().Add(groupSet);
                context.Set<CourseGroup>().Add(group);
                context.Set<CourseGroupMember>().AddRange(
                    CourseGroupMember.Create(group.Id, learner1Id),
                    CourseGroupMember.Create(group.Id, learner2Id));
                assessment.AssignToGroupSet(groupSet.Id);
                courseGroupId = group.Id;
            }

            var enrollment1 = Enrollment.Create(courseId, learner1Id);
            var enrollment2 = Enrollment.Create(courseId, learner2Id);
            enrollment1.TenantId = tenantId;
            enrollment2.TenantId = tenantId;
            context.Set<Enrollment>().AddRange(enrollment1, enrollment2);
            await context.SaveChangesAsync();

            var prepared = await authoring.PrepareAsync(
                assessment.Id,
                instructorId,
                new PrepareAssessmentRevisionRequest(assessment.Version));
            prepared.IsSuccess.Should().BeTrue();
            if (options.Publish)
            {
                var published = await authoring.PublishAsync(
                    assessment.Id,
                    instructorId,
                    new PublishAssessmentRevisionRequest(prepared.Value.RevisionId, assessment.Version));
                published.IsSuccess.Should().BeTrue();
            }

            var gradebook = new AssessmentGradebookProjectionService(context, outbox);
            var sink = new OfficialGradingFinalizationSink(context, outbox, gradebook);
            var orchestrator = new GradingExecutionOrchestrator(context, adapters, stages, sink);
            var runtime = new AssessmentGradingRuntimeService(context, orchestrator, authoring, sink);
            var release = new GradeReleaseService(context, outbox, sink);
            return new RuntimeHarness(
                context,
                authoring,
                runtime,
                release,
                gradebook,
                tenantId,
                courseId,
                assessment.Id,
                prepared.Value.RevisionId,
                instructorId,
                learner1Id,
                learner2Id,
                enrollment1.Id,
                enrollment2.Id,
                courseGroupId);
        }

        public Task<AssessmentTestRunViewV1> StartTestRunAsync() =>
            Runtime.StartTestRunAsync(
                AssessmentId,
                InstructorId,
                new StartAssessmentTestRunCommand(
                    RevisionId,
                    "instructor-preview",
                    "Instructor preview",
                    $"start-{Guid.NewGuid():N}"));

        public Task<AssessmentSubmissionViewV1> StartIndividualAsync(
            string idempotencyKey,
            Guid? enrollmentId = null,
            Guid? userId = null) =>
            Runtime.StartIndividualSubmissionAsync(
                AssessmentId,
                new StartIndividualSubmissionCommand(
                    enrollmentId ?? Enrollment1Id,
                    userId ?? Learner1Id,
                    userId ?? Learner1Id,
                    idempotencyKey));

        public async Task<string[]> AcademicEventTypes() =>
            await Context.Set<AcademicOutboxMessage>()
                .AsNoTracking()
                .Where(message => message.EventType.StartsWith("grade-result"))
                .OrderBy(message => message.EventType)
                .Select(message => message.EventType)
                .ToArrayAsync();

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class RouteOnlyReleaseConsumer : IAcademicOutboxConsumer
    {
        public string Key => "grading.immediate-release.v1";
        public IReadOnlySet<string> EventTypes { get; } =
            new HashSet<string>(StringComparer.Ordinal) { "grade-result-release-requested" };

        public Task ConsumeAsync(AcademicOutboxEvent message, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The runtime tests invoke the release command explicitly.");
    }

    private sealed class RuntimeTestContext(DbContextOptions<RuntimeTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public static RuntimeTestContext Create() => new(
            new DbContextOptionsBuilder<RuntimeTestContext>()
                .UseInMemoryDatabase($"AssessmentGradingRuntime_{Guid.NewGuid():N}")
                .Options);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<EntityBase<Guid>>()
                         .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            {
                entry.Entity.Version++;
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            new GradingPersistenceModelConfiguration().Configure(modelBuilder);
            new EnrollmentConfiguration().Configure(modelBuilder.Entity<Enrollment>());
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.Ignore(value => value.Program);
                entity.Ignore(value => value.Parent);
                entity.Ignore(value => value.Children);
                entity.Ignore(value => value.ContentInteractions);
            });
        }
    }
}

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
using GameGuild.Notifications;
using GameGuild.Notifications.Configuration;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    public async Task AuthorTest_SelfReviewSupportsMultipleSyntheticSubjectsWithoutAcademicEffects()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.SelfReview,
            TrueFalseQuiz(),
            Publish: false));
        var run = await harness.Runtime.StartTestRunAsync(
            harness.AssessmentId,
            harness.InstructorId,
            new StartAssessmentTestRunCommand(
                harness.RevisionId,
                "learner-a",
                "Learner A",
                "self-multipersona-start",
                [
                    new AssessmentTestRunSubjectCommandV1("learner-a", "Learner A"),
                    new AssessmentTestRunSubjectCommandV1("learner-b", "Learner B"),
                ]));

        run.Subjects.Should().HaveCount(2);
        var subjects = run.Subjects!;
        var learnerA = subjects.Single(subject => subject.PersonaKey == "learner-a");
        var learnerB = subjects.Single(subject => subject.PersonaKey == "learner-b");
        await harness.Runtime.SubmitTestRunAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "self-a-answer", TestRunSubjectId: learnerA.SubjectId));
        var first = await harness.Runtime.SubmitTestSelfReviewAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitSelfReviewCommand(SelfEvidence(150), "self-a-review", TestRunSubjectId: learnerA.SubjectId));

        first.Status.Should().Be(AssessmentTestRunStatus.Running);
        first.Subjects!.Single(subject => subject.SubjectId == learnerA.SubjectId)
            .Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(150));
        await harness.Runtime.SubmitTestRunAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(false), "self-b-answer", TestRunSubjectId: learnerB.SubjectId));
        var completed = await harness.Runtime.SubmitTestSelfReviewAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitSelfReviewCommand(SelfEvidence(100), "self-b-review", TestRunSubjectId: learnerB.SubjectId));

        completed.Status.Should().Be(AssessmentTestRunStatus.Completed);
        completed.Subjects!.Single(subject => subject.SubjectId == learnerB.SubjectId)
            .Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(100));
        (await harness.Context.Set<AssessmentSubmission>().CountAsync()).Should().Be(0);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(0);
        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(0);
        (await harness.AcademicEventTypes()).Should().BeEmpty();
    }

    [Fact]
    public async Task AuthorTest_SelfReviewCanHandOffToInstructorReview()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.SelfReview | ReviewMethods.InstructorReview,
            TrueFalseQuiz(),
            Publish: false));
        var run = await harness.StartTestRunAsync();
        await harness.Runtime.SubmitTestRunAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "self-instructor-answer"));

        var afterSelf = await harness.Runtime.SubmitTestSelfReviewAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitSelfReviewCommand(SelfEvidence(125), "self-instructor-self-review"));

        afterSelf.Status.Should().Be(AssessmentTestRunStatus.Running);
        afterSelf.Execution.RequiresInstructorReview.Should().BeTrue();

        var completed = await harness.Runtime.ResolveTestInstructorReviewAsync(
            run.TestRunId,
            harness.InstructorId,
            Resolution(("q1", 175)),
            "self-instructor-final-review");

        completed.Status.Should().Be(AssessmentTestRunStatus.Completed);
        completed.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(175));
        (await harness.Context.Set<AssessmentSubmission>().CountAsync()).Should().Be(0);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(0);
        (await harness.AcademicEventTypes()).Should().BeEmpty();
    }

    [Fact]
    public async Task OfficialSelfReview_FinalizesOneResultAndRequiresTheFrozenSubject()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.SelfReview,
            TrueFalseQuiz(),
            GradebookWeightUnits: 5000));
        var started = await harness.StartIndividualAsync("self-official-start");
        var awaiting = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "self-official-answer"));

        awaiting.Status.Should().Be(SubmissionStatus.Submitted);
        var outsiderSubmit = () => harness.Runtime.SubmitOfficialSelfReviewAsync(
            started.SubmissionId,
            harness.Learner2Id,
            new SubmitSelfReviewCommand(SelfEvidence(125), "self-outsider"));
        await outsiderSubmit.Should().ThrowAsync<UnauthorizedAccessException>();

        var completed = await harness.Runtime.SubmitOfficialSelfReviewAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitSelfReviewCommand(SelfEvidence(125), "self-official-review"));
        var replay = await harness.Runtime.SubmitOfficialSelfReviewAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitSelfReviewCommand(SelfEvidence(125), "self-official-review"));
        var instructorView = await harness.Runtime.GetSubmissionAsync(
            started.SubmissionId,
            harness.InstructorId,
            true);

        completed.Status.Should().Be(SubmissionStatus.Graded);
        instructorView.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(125));
        replay.Execution.ActiveRoundId.Should().Be(completed.Execution.ActiveRoundId);
        (await harness.Context.Set<ReviewEvidence>().CountAsync(value =>
            value.EvidenceType == "self-review-evidence")).Should().Be(1);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task OfficialSelfReviewCanHandOffToInstructorReview()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.SelfReview | ReviewMethods.InstructorReview,
            TrueFalseQuiz(),
            GradebookWeightUnits: 5000));
        var started = await harness.StartIndividualAsync("self-instructor-official-start");
        await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "self-instructor-official-answer"));

        var afterSelf = await harness.Runtime.SubmitOfficialSelfReviewAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitSelfReviewCommand(SelfEvidence(125), "self-instructor-official-self-review"));

        afterSelf.Status.Should().Be(SubmissionStatus.Submitted);
        afterSelf.Execution.RequiresInstructorReview.Should().BeTrue();
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(0);

        var reviewed = await harness.Runtime.ResolveOfficialInstructorReviewAsync(
            started.SubmissionId,
            harness.InstructorId,
            Resolution(("q1", 175)),
            "self-instructor-official-final-review");

        reviewed.Status.Should().Be(SubmissionStatus.Graded);
        reviewed.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(175));
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task OfficialSelfReview_RegradeReusesFinalEvidenceByReference()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.SelfReview,
            TrueFalseQuiz(),
            GradebookWeightUnits: 5000));
        var started = await harness.StartIndividualAsync("self-regrade-start");
        await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "self-regrade-answer"));
        var first = await harness.Runtime.SubmitOfficialSelfReviewAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitSelfReviewCommand(SelfEvidence(125), "self-regrade-review"));

        var regraded = await harness.Runtime.RegradeOfficialAsync(
            started.SubmissionId,
            harness.InstructorId,
            new RegradeExecutionCommand("self-regrade", "Recalculate from frozen evidence"));

        regraded.Status.Should().Be(SubmissionStatus.Graded);
        regraded.Execution.ActiveRoundId!.Value.Should().NotBe(first.Execution.ActiveRoundId!.Value);
        regraded.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(125));
        var submittedEvidence = await harness.Context.Set<ReviewEvidence>()
            .SingleAsync(value => value.EvidenceType == "self-review-evidence");
        submittedEvidence.ProducedByActorId.Should().Be(harness.Learner1Id);
        var reference = await harness.Context.Set<ReviewEvidence>()
            .SingleAsync(value => value.EvidenceType == "review-evidence-reference");
        reference.SourceReviewEvidenceId.Should().Be(submittedEvidence.Id);
        reference.ProducedByService.Should().Be("grading-regrade");
    }

    [Fact]
    public async Task CollectiveSelfReview_UsesOneVersionedDraftAndOneFinalEvidence()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.SelfReview,
            TrueFalseQuiz(),
            GradebookWeightUnits: 5000,
            Collective: true));
        var started = await harness.Runtime.StartCollectiveSubmissionAsync(
            harness.AssessmentId,
            new StartCollectiveSubmissionCommand(harness.CourseGroupId!.Value, harness.Learner1Id, "self-group-start"));
        await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "self-group-answer", 0));

        await harness.Runtime.SaveCollectiveSelfReviewDraftAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SaveCollectiveSelfReviewDraftCommand(SelfEvidence(100), 0, "self-group-draft-1"));
        await harness.Runtime.SaveCollectiveSelfReviewDraftAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SaveCollectiveSelfReviewDraftCommand(SelfEvidence(100), 0, "self-group-draft-1"));
        var stale = () => harness.Runtime.SaveCollectiveSelfReviewDraftAsync(
            started.SubmissionId,
            harness.Learner2Id,
            new SaveCollectiveSelfReviewDraftCommand(SelfEvidence(125), 0, "self-group-stale"));
        await stale.Should().ThrowAsync<InvalidOperationException>().WithMessage("*stale*");
        await harness.Runtime.SaveCollectiveSelfReviewDraftAsync(
            started.SubmissionId,
            harness.Learner2Id,
            new SaveCollectiveSelfReviewDraftCommand(SelfEvidence(150), 1, "self-group-draft-2"));

        var completed = await harness.Runtime.SubmitOfficialSelfReviewAsync(
            started.SubmissionId,
            harness.Learner2Id,
            new SubmitSelfReviewCommand(SelfEvidence(150), "self-group-final", 2));
        var instructorView = await harness.Runtime.GetSubmissionAsync(
            started.SubmissionId,
            harness.InstructorId,
            true);
        completed.Status.Should().Be(SubmissionStatus.Graded);
        instructorView.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(150));
        (await harness.Context.Set<ReviewEvidence>().CountAsync(value =>
            value.EvidenceType == "self-review-draft-change")).Should().Be(2);
        (await harness.Context.Set<ReviewEvidence>().CountAsync(value =>
            value.EvidenceType == "self-review-evidence")).Should().Be(1);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorTest_PeerReviewUsesSyntheticSubjectsAndOptionallyHandsOffToInstructor(
        bool requireInstructorReview)
    {
        var methods = ReviewMethods.PeerReview |
                      (requireInstructorReview ? ReviewMethods.InstructorReview : ReviewMethods.None);
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            methods,
            TrueFalseQuiz(),
            Publish: false));
        var run = await harness.Runtime.StartTestRunAsync(
            harness.AssessmentId,
            harness.InstructorId,
            new StartAssessmentTestRunCommand(
                harness.RevisionId,
                "learner-a",
                "Learner A",
                "peer-test-start",
                [
                    new AssessmentTestRunSubjectCommandV1("learner-a", "Learner A"),
                    new AssessmentTestRunSubjectCommandV1("learner-b", "Learner B"),
                ]));
        var subjects = run.Subjects!;
        var learnerA = subjects.Single(subject => subject.PersonaKey == "learner-a");
        var learnerB = subjects.Single(subject => subject.PersonaKey == "learner-b");

        await harness.Runtime.SubmitTestRunAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "peer-a-answer", TestRunSubjectId: learnerA.SubjectId));
        await harness.Runtime.SubmitTestRunAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(false), "peer-b-answer", TestRunSubjectId: learnerB.SubjectId));

        var claimA = await harness.PeerReviews.ClaimAuthorTestAsync(
            run.TestRunId,
            learnerA.SubjectId,
            harness.InstructorId);
        var claimB = await harness.PeerReviews.ClaimAuthorTestAsync(
            run.TestRunId,
            learnerB.SubjectId,
            harness.InstructorId);
        claimA.IsSuccess.Should().BeTrue();
        claimB.IsSuccess.Should().BeTrue();

        (await harness.PeerReviews.SubmitReviewAsync(
            claimA.Value.ReviewId,
            harness.InstructorId,
            ScoreValue.FromUnits(125),
            "Synthetic learner A review",
            null)).IsSuccess.Should().BeTrue();
        (await harness.PeerReviews.SubmitReviewAsync(
            claimB.Value.ReviewId,
            harness.InstructorId,
            ScoreValue.FromUnits(175),
            "Synthetic learner B review",
            null)).IsSuccess.Should().BeTrue();

        var afterPeer = await harness.Runtime.GetTestRunAsync(run.TestRunId, harness.InstructorId);
        if (requireInstructorReview)
        {
            afterPeer.Status.Should().Be(AssessmentTestRunStatus.Running);
            afterPeer.Subjects.Should().OnlyContain(subject => subject.Execution.RequiresInstructorReview);
            await harness.Runtime.ResolveTestInstructorReviewAsync(
                run.TestRunId,
                harness.InstructorId,
                Resolution(("q1", 150)),
                "peer-test-instructor-a",
                learnerA.SubjectId);
            afterPeer = await harness.Runtime.ResolveTestInstructorReviewAsync(
                run.TestRunId,
                harness.InstructorId,
                Resolution(("q1", 160)),
                "peer-test-instructor-b",
                learnerB.SubjectId);
        }

        afterPeer.Status.Should().Be(AssessmentTestRunStatus.Completed);
        afterPeer.Subjects.Should().OnlyContain(subject =>
            subject.Execution.InstructorVisibleResult != null &&
            subject.Execution.InstructorVisibleResult.State == "final");
        (await harness.Context.Set<AssessmentSubmission>().CountAsync()).Should().Be(0);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(0);
        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfficialPeerReview_FinalizesIndividualSubmissionsAndReusesEvidenceOnRegrade(
        bool requireInstructorReview)
    {
        var methods = ReviewMethods.PeerReview |
                      (requireInstructorReview ? ReviewMethods.InstructorReview : ReviewMethods.None);
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            methods,
            TrueFalseQuiz(),
            GradebookWeightUnits: 5000));
        var first = await harness.StartIndividualAsync("peer-first-start");
        var second = await harness.StartIndividualAsync(
            "peer-second-start",
            harness.Enrollment2Id,
            harness.Learner2Id);
        await harness.Runtime.SubmitOfficialAsync(
            first.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "peer-first-answer"));
        await harness.Runtime.SubmitOfficialAsync(
            second.SubmissionId,
            harness.Learner2Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(false), "peer-second-answer"));

        var firstReviewerClaim = await harness.PeerReviews.ClaimAsync(harness.AssessmentId, harness.Learner1Id);
        var secondReviewerClaim = await harness.PeerReviews.ClaimAsync(harness.AssessmentId, harness.Learner2Id);
        firstReviewerClaim.IsSuccess.Should().BeTrue();
        secondReviewerClaim.IsSuccess.Should().BeTrue();
        (await harness.PeerReviews.GetReviewAsync(firstReviewerClaim.Value.ReviewId))!.SubmissionId
            .Should().Be(second.SubmissionId);
        (await harness.PeerReviews.GetReviewAsync(secondReviewerClaim.Value.ReviewId))!.SubmissionId
            .Should().Be(first.SubmissionId);

        (await harness.PeerReviews.SubmitReviewAsync(
            firstReviewerClaim.Value.ReviewId,
            harness.Learner1Id,
            ScoreValue.FromUnits(125),
            "Review for the second learner",
            null)).IsSuccess.Should().BeTrue();
        (await harness.PeerReviews.SubmitReviewAsync(
            secondReviewerClaim.Value.ReviewId,
            harness.Learner2Id,
            ScoreValue.FromUnits(175),
            "Review for the first learner",
            null)).IsSuccess.Should().BeTrue();

        if (requireInstructorReview)
        {
            await harness.Runtime.ResolveOfficialInstructorReviewAsync(
                first.SubmissionId,
                harness.InstructorId,
                Resolution(("q1", 180)),
                "peer-first-instructor");
            await harness.Runtime.ResolveOfficialInstructorReviewAsync(
                second.SubmissionId,
                harness.InstructorId,
                Resolution(("q1", 140)),
                "peer-second-instructor");
        }

        var firstResult = await harness.Runtime.GetSubmissionAsync(
            first.SubmissionId,
            harness.InstructorId,
            true);
        var secondResult = await harness.Runtime.GetSubmissionAsync(
            second.SubmissionId,
            harness.InstructorId,
            true);
        firstResult.Status.Should().Be(SubmissionStatus.Graded);
        secondResult.Status.Should().Be(SubmissionStatus.Graded);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(2);

        if (!requireInstructorReview)
        {
            var regraded = await harness.Runtime.RegradeOfficialAsync(
                first.SubmissionId,
                harness.InstructorId,
                new RegradeExecutionCommand("peer-regrade", "Recalculate frozen peer evidence"));
            regraded.Status.Should().Be(SubmissionStatus.Graded);
            regraded.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(175));
            var original = await harness.Context.Set<ReviewEvidence>()
                .SingleAsync(value => value.EvidenceType == "peer-review-evidence" &&
                                      value.ProducedByActorId == harness.Learner2Id);
            var reused = await harness.Context.Set<ReviewEvidence>()
                .SingleAsync(value => value.SourceReviewEvidenceId == original.Id);
            reused.EvidenceType.Should().Be("review-evidence-reference");
        }
    }

    [Fact]
    public async Task CollectivePeerReview_ExcludesEveryTargetParticipantAndGradesOncePerGroup()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.PeerReview,
            TrueFalseQuiz(),
            GradebookWeightUnits: 5000,
            Collective: true));
        var groupSetId = await harness.Context.Set<CourseGroupSet>().Select(value => value.Id).SingleAsync();
        var learner3Id = Guid.NewGuid();
        var learner4Id = Guid.NewGuid();
        var secondGroup = CourseGroup.Create(groupSetId, "Team Two", 4);
        var enrollment3 = Enrollment.Create(harness.CourseId, learner3Id);
        var enrollment4 = Enrollment.Create(harness.CourseId, learner4Id);
        enrollment3.TenantId = harness.TenantId;
        enrollment4.TenantId = harness.TenantId;
        harness.Context.Set<CourseGroup>().Add(secondGroup);
        harness.Context.Set<CourseGroupMember>().AddRange(
            CourseGroupMember.Create(secondGroup.Id, learner3Id),
            CourseGroupMember.Create(secondGroup.Id, learner4Id));
        harness.Context.Set<Enrollment>().AddRange(enrollment3, enrollment4);
        await harness.Context.SaveChangesAsync();

        var first = await harness.Runtime.StartCollectiveSubmissionAsync(
            harness.AssessmentId,
            new StartCollectiveSubmissionCommand(harness.CourseGroupId!.Value, harness.Learner1Id, "peer-group-one"));
        var second = await harness.Runtime.StartCollectiveSubmissionAsync(
            harness.AssessmentId,
            new StartCollectiveSubmissionCommand(secondGroup.Id, learner3Id, "peer-group-two"));
        await harness.Runtime.SubmitOfficialAsync(
            first.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "peer-group-one-answer", 0));
        await harness.Runtime.SubmitOfficialAsync(
            second.SubmissionId,
            learner3Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(false), "peer-group-two-answer", 0));

        var learner1Claim = await harness.PeerReviews.ClaimAsync(harness.AssessmentId, harness.Learner1Id);
        var learner3Claim = await harness.PeerReviews.ClaimAsync(harness.AssessmentId, learner3Id);
        learner1Claim.IsSuccess.Should().BeTrue();
        learner3Claim.IsSuccess.Should().BeTrue();
        var learner1Target = await harness.PeerReviews.GetReviewAsync(learner1Claim.Value.ReviewId);
        var learner3Target = await harness.PeerReviews.GetReviewAsync(learner3Claim.Value.ReviewId);
        learner1Target!.SubmissionId.Should().Be(second.SubmissionId);
        learner3Target!.SubmissionId.Should().Be(first.SubmissionId);
        (await harness.Context.Set<AssessmentSubmissionParticipant>().AnyAsync(value =>
            value.SubmissionId == learner1Target.SubmissionId && value.UserId == harness.Learner1Id)).Should().BeFalse();
        (await harness.Context.Set<AssessmentSubmissionParticipant>().AnyAsync(value =>
            value.SubmissionId == learner3Target.SubmissionId && value.UserId == learner3Id)).Should().BeFalse();

        (await harness.PeerReviews.SubmitReviewAsync(
            learner1Claim.Value.ReviewId,
            harness.Learner1Id,
            ScoreValue.FromUnits(110),
            "Team Two review",
            null)).IsSuccess.Should().BeTrue();
        (await harness.PeerReviews.SubmitReviewAsync(
            learner3Claim.Value.ReviewId,
            learner3Id,
            ScoreValue.FromUnits(190),
            "Team One review",
            null)).IsSuccess.Should().BeTrue();

        (await harness.Context.Set<AssessmentSubmission>().CountAsync()).Should().Be(2);
        (await harness.Context.Set<GradeRound>().CountAsync()).Should().Be(2);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task OfficialPeerReview_DeadlineOperationsPreserveEvidenceAndRequireExplicitResolution()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.PeerReview,
            TrueFalseQuiz(),
            GradebookWeightUnits: 5000));
        var target = await harness.StartIndividualAsync("peer-operations-target");
        var reviewer = await harness.StartIndividualAsync(
            "peer-operations-reviewer",
            harness.Enrollment2Id,
            harness.Learner2Id);
        await harness.Runtime.SubmitOfficialAsync(
            target.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "peer-operations-target-answer"));
        await harness.Runtime.SubmitOfficialAsync(
            reviewer.SubmissionId,
            harness.Learner2Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(false), "peer-operations-reviewer-answer"));

        var claimed = await harness.PeerReviews.ClaimAsync(harness.AssessmentId, harness.Learner2Id);
        claimed.IsSuccess.Should().BeTrue();
        var claim = await harness.PeerReviews.GetReviewAsync(claimed.Value.ReviewId);
        claim!.SubmissionId.Should().Be(target.SubmissionId);
        await harness.PeerReviews.ReassignClaimAsync(
            claim.Id,
            harness.InstructorId,
            claim.Version,
            "Reviewer unavailable",
            "peer-reassign");
        await harness.PeerReviews.ReassignClaimAsync(
            claim.Id,
            harness.InstructorId,
            claim.Version,
            "Reviewer unavailable",
            "peer-reassign");

        var execution = await harness.Context.Set<GradingExecution>()
            .SingleAsync(value => value.AssessmentSubmissionId == target.SubmissionId);
        var stage = await harness.Context.Set<ReviewStage>()
            .SingleAsync(value => value.GradeRoundId == execution.ActiveGradeRoundId &&
                                  value.ReviewMethod == ReviewMethod.PeerReview);
        harness.Context.Entry(stage).Property(value => value.EvidenceDeadlineAt).CurrentValue =
            SystemClock.UtcNow.AddSeconds(-1);
        await harness.Context.SaveChangesAsync();

        var deadlines = new ReviewStageDeadlineProcessor(
            harness.Context,
            harness.Orchestrator,
            NullLogger<ReviewStageDeadlineProcessor>.Instance);
        (await deadlines.ProcessDueAsync()).Should().Be(1);
        stage = await harness.Context.Set<ReviewStage>().SingleAsync(value => value.Id == stage.Id);
        stage.Status.Should().Be(PersistedReviewStageStatus.AwaitingInstructorResolution);
        (await harness.Context.Set<AssessmentGradebookEntry>()
            .CountAsync(value => value.SubmissionId == target.SubmissionId)).Should().Be(0);

        var expectedStageVersion = stage.Version;
        var extendedDeadline = SystemClock.UtcNow.AddMinutes(30);
        await harness.PeerReviews.ExtendEvidenceWindowAsync(
            stage.Id,
            harness.InstructorId,
            expectedStageVersion,
            extendedDeadline,
            "Allow another reviewer",
            "peer-extend");
        await harness.PeerReviews.ExtendEvidenceWindowAsync(
            stage.Id,
            harness.InstructorId,
            expectedStageVersion,
            extendedDeadline,
            "Allow another reviewer",
            "peer-extend");
        stage = await harness.Context.Set<ReviewStage>().SingleAsync(value => value.Id == stage.Id);
        stage.Status.Should().Be(PersistedReviewStageStatus.AwaitingEvidence);

        harness.Context.Entry(stage).Property(value => value.EvidenceDeadlineAt).CurrentValue =
            SystemClock.UtcNow.AddSeconds(-1);
        await harness.Context.SaveChangesAsync();
        (await deadlines.ProcessDueAsync()).Should().Be(1);
        stage = await harness.Context.Set<ReviewStage>().SingleAsync(value => value.Id == stage.Id);
        stage.Status.Should().Be(PersistedReviewStageStatus.AwaitingInstructorResolution);

        var resolution = new PeerReviewInsufficiencyResolutionV1(
            1,
            [new InstructorItemResolutionV1("q1", ScoreValue.FromUnits(125), "Resolved by instructor")],
            "Minimum peer evidence was not reached",
            "Instructor resolved the incomplete peer stage");
        await harness.PeerReviews.ResolveInsufficientEvidenceAsync(
            stage.Id,
            harness.InstructorId,
            resolution,
            "peer-resolve-insufficient");
        await FluentActions.Invoking(() => harness.PeerReviews.ResolveInsufficientEvidenceAsync(
                stage.Id,
                harness.InstructorId,
                resolution with { Feedback = "Different resolution" },
                "peer-resolve-insufficient"))
            .Should().ThrowAsync<InvalidOperationException>();
        await harness.PeerReviews.ResolveInsufficientEvidenceAsync(
            stage.Id,
            harness.InstructorId,
            resolution,
            "peer-resolve-insufficient");

        var completed = await harness.Runtime.GetSubmissionAsync(
            target.SubmissionId,
            harness.InstructorId,
            true);
        completed.Status.Should().Be(SubmissionStatus.Graded);
        completed.Execution.InstructorVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(125));
        (await harness.Context.Set<ReviewEvidence>().CountAsync(value =>
            value.ReviewStageId == stage.Id &&
            value.EvidenceType == "peer-review-insufficiency-resolution")).Should().Be(1);
        (await harness.Context.Set<GradingCommandReceipt>().CountAsync(value =>
            value.ResourceId == stage.Id &&
            value.CommandType == "resolve-peer-review-insufficiency")).Should().Be(1);
        (await harness.Context.Set<PeerReviewClaim>().SingleAsync(value => value.Id == claim.Id))
            .Status.Should().Be(PeerReviewClaimStatus.Cancelled);
        (await harness.Context.Set<AssessmentGradebookEntry>()
            .CountAsync(value => value.SubmissionId == target.SubmissionId)).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorTest_AIReviewUsesDurableIdempotentEvidenceAndOptionallyHandsOffToInstructor(
        bool requireInstructorReview)
    {
        var methods = ReviewMethods.AIReview |
                      (requireInstructorReview ? ReviewMethods.InstructorReview : ReviewMethods.None);
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            methods,
            TrueFalseQuiz(),
            Publish: false));
        var run = await harness.StartTestRunAsync();
        var awaitingAI = await harness.Runtime.SubmitTestRunAsync(
            run.TestRunId,
            harness.InstructorId,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "ai-test-answer"));
        awaitingAI.Status.Should().Be(AssessmentTestRunStatus.Running);
        var request = await harness.Context.Set<AcademicOutboxMessage>()
            .SingleAsync(value => value.EventType == "ai-review-requested");

        if (requireInstructorReview)
        {
            harness.AIProvider.FailNext = true;
            var transientFailure = () => harness.DispatchAIReviewAsync(request.Id);
            await transientFailure.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*temporarily unavailable*");
            (await harness.Context.Set<ReviewEvidence>().CountAsync(value =>
                value.EvidenceType == "ai-review-response")).Should().Be(0);
        }

        await harness.DispatchAIReviewAsync(request.Id);
        await harness.DispatchAIReviewAsync(request.Id);
        (await harness.Context.Set<ReviewEvidence>().CountAsync(value =>
            value.EvidenceType == "ai-review-response")).Should().Be(1);
        var afterAI = await harness.Runtime.GetTestRunAsync(run.TestRunId, harness.InstructorId);
        if (requireInstructorReview)
        {
            afterAI.Execution.RequiresInstructorReview.Should().BeTrue();
            afterAI = await harness.Runtime.ResolveTestInstructorReviewAsync(
                run.TestRunId,
                harness.InstructorId,
                Resolution(("q1", 175)),
                "ai-test-instructor");
        }

        afterAI.Status.Should().Be(AssessmentTestRunStatus.Completed);
        afterAI.Execution.InstructorVisibleResult!.State.Should().Be("final");
        (await harness.Context.Set<AssessmentSubmission>().CountAsync()).Should().Be(0);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OfficialCollectiveAIReviewCreatesANewDurableRequestForRegrade()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AIReview,
            TrueFalseQuiz(),
            GradebookWeightUnits: 5000,
            Collective: true));
        var started = await harness.Runtime.StartCollectiveSubmissionAsync(
            harness.AssessmentId,
            new StartCollectiveSubmissionCommand(harness.CourseGroupId!.Value, harness.Learner1Id, "ai-group-start"));
        await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner2Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "ai-group-answer", 0));
        var firstRequest = await harness.Context.Set<AcademicOutboxMessage>()
            .SingleAsync(value => value.EventType == "ai-review-requested");
        await harness.DispatchAIReviewAsync(firstRequest.Id);
        var firstResult = await harness.Runtime.GetSubmissionAsync(
            started.SubmissionId,
            harness.InstructorId,
            true);
        firstResult.Status.Should().Be(SubmissionStatus.Graded);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(2);

        var regrading = await harness.Runtime.RegradeOfficialAsync(
            started.SubmissionId,
            harness.InstructorId,
            new RegradeExecutionCommand("ai-group-regrade", "Run the frozen provider policy again"));
        regrading.Status.Should().Be(SubmissionStatus.Graded);
        regrading.Execution.LearnerVisibleResult.Should().BeNull();
        var secondRequest = await harness.Context.Set<AcademicOutboxMessage>()
            .SingleAsync(value => value.EventType == "ai-review-requested" && value.Id != firstRequest.Id);
        await harness.DispatchAIReviewAsync(secondRequest.Id);

        var regraded = await harness.Runtime.GetSubmissionAsync(
            started.SubmissionId,
            harness.InstructorId,
            true);
        regraded.Status.Should().Be(SubmissionStatus.Graded);
        harness.AIProvider.RequestIds.Distinct().Should().HaveCount(2);
        regraded.Execution.History.Should().HaveCount(2);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public void AIReviewWithoutACompatibleProviderFailsClosed()
    {
        var registry = new AIReviewProviderRegistry([]);
        var resolve = () => registry.Resolve("missing", "1", ReviewExecutionContext.OfficialSubmission);

        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage("*unavailable for OfficialSubmission*");
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
    public async Task ReleasedResult_FansOutToNotificationAndPassbackWithoutRecalculatingGrade()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            ContentCompletionMode.OnReleaseAndPass,
            PassingScoreUnits: 150,
            GradebookWeightUnits: 5000));
        var started = await harness.StartIndividualAsync("consumer-start");
        var submitted = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "consumer-submit"));
        await harness.Release.ReleaseByActorAsync(
            submitted.SubmissionId,
            submitted.Execution.ActiveRoundId!.Value,
            submitted.Version,
            harness.InstructorId,
            "consumer-release",
            "Reviewed");
        var message = await harness.Context.Set<AcademicOutboxMessage>()
            .SingleAsync(value => value.EventType == "grade-result-released");
        var outboxEvent = new AcademicOutboxEvent(
            message.Id,
            message.TenantId,
            message.EventType,
            message.EventSchemaVersion,
            message.PayloadCanonicalJson,
            message.PayloadHash,
            message.OccurredAt);

        var notifications = new Mock<INotificationService>(MockBehavior.Strict);
        notifications.Setup(service => service.SendAsync(
                harness.Learner1Id,
                NotificationType.AssessmentGraded,
                "Assessment result available",
                "Your assessment score and feedback are now available.",
                NotificationChannel.InApp,
                harness.TenantId,
                "/learn",
                NotificationPriority.Normal,
                message.Id,
                "GradeResultReleased",
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Notification.Create(
                harness.Learner1Id,
                NotificationType.AssessmentGraded,
                NotificationChannel.InApp,
                "Assessment result available",
                "Your assessment score and feedback are now available.",
                harness.TenantId,
                "/learn",
                referenceEntityId: message.Id,
                referenceEntityType: "GradeResultReleased")));
        var passback = new Mock<ILtiScorePassback>(MockBehavior.Strict);
        passback.Setup(service => service.PostScoreIfMappedAsync(
                harness.AssessmentId,
                harness.Learner1Id,
                ScoreValue.FromUnits(200),
                ScoreValue.FromUnits(200),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LtiScorePassbackOutcome.Delivered);
        using var services = new ServiceCollection()
            .AddSingleton(notifications.Object)
            .AddSingleton(passback.Object)
            .BuildServiceProvider();

        await new GradeReleaseNotificationConsumer(harness.Context, services)
            .ConsumeAsync(outboxEvent, default);
        await new GradeReleasePassbackConsumer(harness.Context, services)
            .ConsumeAsync(outboxEvent, default);

        notifications.VerifyAll();
        passback.VerifyAll();
        var persistedNotification = Notification.Create(
            harness.Learner1Id,
            NotificationType.AssessmentGraded,
            NotificationChannel.InApp,
            "Assessment result available",
            "Your assessment score and feedback are now available.",
            harness.TenantId,
            "/learn",
            referenceEntityId: message.Id,
            referenceEntityType: "GradeResultReleased");
        harness.Context.Set<Notification>().Add(persistedNotification);
        await harness.Context.SaveChangesAsync();
        await new GradeReleaseNotificationConsumer(harness.Context, services)
            .ConsumeAsync(outboxEvent, default);
        notifications.Verify(service => service.SendAsync(
            It.IsAny<Guid?>(),
            It.IsAny<NotificationType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<NotificationChannel>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        (await harness.Context.Set<AssessmentGradebookEntry>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task OfficialScheduledRelease_IsVersionedCancellableAndReleasedExactlyOnceWhenDue()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.AutomatedReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            GradebookWeightUnits: 5000));
        var started = await harness.StartIndividualAsync("scheduled-release-start");
        var submitted = await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "scheduled-release-submit"));
        var roundId = submitted.Execution.ActiveRoundId!.Value;
        var firstDueAt = harness.Clock.GetUtcNow().AddMinutes(30).UtcDateTime;

        var scheduled = await harness.ReleaseSchedules.ScheduleByActorAsync(
            submitted.SubmissionId,
            roundId,
            submitted.Version,
            0,
            firstDueAt,
            harness.InstructorId,
            "schedule-release",
            "Publish after review window");
        var replay = await harness.ReleaseSchedules.ScheduleByActorAsync(
            submitted.SubmissionId,
            roundId,
            submitted.Version,
            0,
            firstDueAt,
            harness.InstructorId,
            "schedule-release",
            "Publish after review window");

        replay.Should().BeEquivalentTo(scheduled);
        var staleReschedule = () => harness.ReleaseSchedules.ScheduleByActorAsync(
            submitted.SubmissionId,
            roundId,
            submitted.Version,
            0,
            firstDueAt.AddMinutes(15),
            harness.InstructorId,
            "stale-reschedule",
            null);
        await staleReschedule.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*schedule version is stale*");

        var rescheduled = await harness.ReleaseSchedules.ScheduleByActorAsync(
            submitted.SubmissionId,
            roundId,
            submitted.Version,
            scheduled.Version,
            firstDueAt.AddMinutes(15),
            harness.InstructorId,
            "reschedule-release",
            null);
        var cancelled = await harness.ReleaseSchedules.CancelByActorAsync(
            submitted.SubmissionId,
            roundId,
            submitted.Version,
            rescheduled.Version,
            harness.InstructorId,
            "cancel-release",
            "Additional review required");
        cancelled.Status.Should().Be(GradeResultReleaseScheduleStatus.Cancelled);
        var restored = await harness.ReleaseSchedules.ScheduleByActorAsync(
            submitted.SubmissionId,
            roundId,
            submitted.Version,
            cancelled.Version,
            firstDueAt,
            harness.InstructorId,
            "restore-release",
            "Review completed");

        (await harness.ReleaseSchedules.ReleaseDueBatchAsync()).Should().Be(0);
        harness.Clock.Advance(TimeSpan.FromMinutes(31));
        (await harness.ReleaseSchedules.ReleaseDueBatchAsync()).Should().Be(1);
        (await harness.ReleaseSchedules.ReleaseDueBatchAsync()).Should().Be(0);

        var persisted = await harness.Context.Set<GradeResultReleaseSchedule>()
            .SingleAsync(value => value.Id == restored.ScheduleId);
        persisted.Status.Should().Be(GradeResultReleaseScheduleStatus.Released);
        (await harness.Context.Set<GradeResultRelease>()
            .CountAsync(value => value.GradeRoundId == roundId)).Should().Be(1);
        (await harness.Context.Set<AcademicOutboxMessage>()
            .CountAsync(value => value.EventType == "grade-result-released")).Should().Be(1);
    }

    [Fact]
    public async Task OfficialScheduledRelease_RegradeKeepsEachRoundIndependentAndLearnerSeesLatestReleasedRound()
    {
        await using var harness = await RuntimeHarness.CreateAsync(new RuntimeOptions(
            ReviewMethods.InstructorReview,
            TrueFalseQuiz(),
            ResultReleaseMode.Manual,
            GradebookWeightUnits: 5000));
        var started = await harness.StartIndividualAsync("scheduled-regrade-start");
        await harness.Runtime.SubmitOfficialAsync(
            started.SubmissionId,
            harness.Learner1Id,
            new SubmitAssessmentResponseCommand(TrueFalseAnswer(true), "scheduled-regrade-submit"));
        var first = await harness.Runtime.ResolveOfficialInstructorReviewAsync(
            started.SubmissionId,
            harness.InstructorId,
            Resolution(("q1", 100)),
            "scheduled-first-review");
        var firstRoundId = first.Execution.ActiveRoundId!.Value;
        var firstDueAt = harness.Clock.GetUtcNow().AddMinutes(20).UtcDateTime;

        await harness.Runtime.RegradeOfficialAsync(
            started.SubmissionId,
            harness.InstructorId,
            new RegradeExecutionCommand("scheduled-open-regrade", "Correct the score"));
        var second = await harness.Runtime.ResolveOfficialInstructorReviewAsync(
            started.SubmissionId,
            harness.InstructorId,
            Resolution(("q1", 175)),
            "scheduled-second-review");
        var secondRoundId = second.Execution.ActiveRoundId!.Value;
        secondRoundId.Should().NotBe(firstRoundId);

        await harness.ReleaseSchedules.ScheduleByServiceAsync(
            started.SubmissionId,
            firstRoundId,
            first.Version,
            firstDueAt,
            "grading.scheduled-release-request.v1",
            "old-round-schedule-event",
            "Frozen policy for the first round");
        await harness.ReleaseSchedules.ScheduleByActorAsync(
            started.SubmissionId,
            secondRoundId,
            second.Version,
            0,
            firstDueAt.AddMinutes(20),
            harness.InstructorId,
            "schedule-second-round",
            "Publish corrected result later");

        harness.Clock.Advance(TimeSpan.FromMinutes(21));
        (await harness.ReleaseSchedules.ReleaseDueBatchAsync()).Should().Be(1);
        var afterFirstRelease = await harness.Runtime.GetSubmissionAsync(
            started.SubmissionId,
            harness.Learner1Id,
            false);
        afterFirstRelease.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(100));
        afterFirstRelease.Execution.History.Should().ContainSingle();

        harness.Clock.Advance(TimeSpan.FromMinutes(20));
        (await harness.ReleaseSchedules.ReleaseDueBatchAsync()).Should().Be(1);
        (await harness.ReleaseSchedules.ReleaseDueBatchAsync()).Should().Be(0);
        var afterSecondRelease = await harness.Runtime.GetSubmissionAsync(
            started.SubmissionId,
            harness.Learner1Id,
            false);
        afterSecondRelease.Execution.LearnerVisibleResult!.Score.Should().Be(ScoreValue.FromUnits(175));
        afterSecondRelease.Execution.History.Should().HaveCount(2);
        (await harness.Context.Set<GradeResultRelease>().CountAsync()).Should().Be(2);
        (await harness.Context.Set<AcademicOutboxMessage>()
            .CountAsync(value => value.EventType == "grade-result-released")).Should().Be(2);
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

    private static SelfReviewEvidenceV1 SelfEvidence(int scoreUnits) =>
        new(
            1,
            [new SelfReviewItemV1("q1", ScoreValue.FromUnits(scoreUnits), "Self assessment")],
            "Self-review complete");

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

    private sealed class ControlledAIReviewProvider : IAIReviewProvider
    {
        public const string ProviderKey = "controlled-ai";

        public string Key => ProviderKey;
        public IReadOnlySet<string> PolicyVersions { get; } = new HashSet<string>(StringComparer.Ordinal) { "1" };
        public IReadOnlySet<ReviewExecutionContext> Contexts { get; } = new HashSet<ReviewExecutionContext>
        {
            ReviewExecutionContext.AuthorTest,
            ReviewExecutionContext.OfficialSubmission,
        };
        public bool FailNext { get; set; }
        public List<Guid> RequestIds { get; } = [];

        public ValueTask<AIReviewProviderResponseV1> ReviewAsync(
            AIReviewProviderRequestV1 request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestIds.Add(request.RequestId);
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("Controlled provider is temporarily unavailable.");
            }

            var items = request.Snapshot.Manifest.Items.Select(item =>
            {
                var maximum = ScoreValue.FromUnits(request.Snapshot.ItemProjections[item.ItemId]
                    .GetProperty("maxScore")
                    .GetInt32());
                return new GradeItemResultV1(
                    item.ItemId,
                    GradeItemState.Graded,
                    maximum,
                    maximum,
                    [$"ai-request:{request.RequestId:N}"],
                    ReviewMethod.AIReview,
                    "ai-review",
                    "1",
                    "Controlled AI review",
                    ProviderKey);
            }).ToArray();
            var result = new GradeResultV1(
                GradingContractVersions.GradeResult,
                "final",
                ScoreValue.Sum(items.Select(item => item.Score!.Value)),
                ScoreValue.Sum(items.Select(item => item.MaxScore)),
                items,
                [$"ai-request:{request.RequestId:N}"],
                "Controlled AI review");
            return ValueTask.FromResult(new AIReviewProviderResponseV1(
                1,
                request.RequestId,
                request.GradingExecutionId,
                request.GradeRoundId,
                request.ReviewStageId,
                request.ExecutionSnapshotHash,
                request.ProviderKey,
                request.ProviderPolicyVersion,
                "controlled-model",
                "1",
                result));
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
            GradeResultReleaseScheduleService releaseSchedules,
            AssessmentGradebookProjectionService gradebook,
            PeerReviewAssignmentService peerReviews,
            IGradingExecutionOrchestrator orchestrator,
            AIReviewProviderRegistry aiProviders,
            ControlledAIReviewProvider aiProvider,
            MutableTimeProvider clock,
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
            ReleaseSchedules = releaseSchedules;
            Gradebook = gradebook;
            PeerReviews = peerReviews;
            Orchestrator = orchestrator;
            AIProviders = aiProviders;
            AIProvider = aiProvider;
            Clock = clock;
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
        public GradeResultReleaseScheduleService ReleaseSchedules { get; }
        public AssessmentGradebookProjectionService Gradebook { get; }
        public PeerReviewAssignmentService PeerReviews { get; }
        public IGradingExecutionOrchestrator Orchestrator { get; }
        public AIReviewProviderRegistry AIProviders { get; }
        public ControlledAIReviewProvider AIProvider { get; }
        public MutableTimeProvider Clock { get; }
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
            var aiProvider = new ControlledAIReviewProvider();
            var aiProviders = new AIReviewProviderRegistry([aiProvider]);
            IReviewStageHandler[] handlers =
            [
                instructorHandler,
                new SelfReviewStageHandler(),
                new PeerReviewStageHandler(),
                new AIReviewStageHandler(),
                automatedHandler,
                new CodeAutomatedReviewStageHandler(codeAdapter),
            ];
            var adapters = new AssessmentTypeAdapterResolver(registry, [quizAdapter, codeAdapter]);
            var policies = new AssessmentExecutionPolicyResolver(registry);
            var stages = new ReviewStageHandlerResolver(registry, handlers);
            var outbox = new AcademicOutboxWriter(
                context,
                [new RouteOnlyReleaseConsumer(), new RouteOnlyAIReviewConsumer()]);
            var authoring = new AssessmentAuthoringService(
                context,
                adapters,
                policies,
                stages,
                aiProviders,
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
                    ReviewConfigurationCanonicalJson: ReviewConfiguration(options)));
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
            var orchestrator = new GradingExecutionOrchestrator(context, adapters, stages, sink, outbox);
            var runtime = new AssessmentGradingRuntimeService(context, orchestrator, authoring, sink);
            var release = new GradeReleaseService(context, outbox, sink);
            var clock = new MutableTimeProvider(DateTimeOffset.UtcNow.AddHours(-1));
            var releaseSchedules = new GradeResultReleaseScheduleService(
                context,
                outbox,
                release,
                clock,
                NullLogger<GradeResultReleaseScheduleService>.Instance);
            var peerReviews = new PeerReviewAssignmentService(
                context,
                orchestrator,
                outbox,
                NullLogger<PeerReviewAssignmentService>.Instance);
            return new RuntimeHarness(
                context,
                authoring,
                runtime,
                release,
                releaseSchedules,
                gradebook,
                peerReviews,
                orchestrator,
                aiProviders,
                aiProvider,
                clock,
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

        public async Task DispatchAIReviewAsync(Guid messageId)
        {
            var message = await Context.Set<AcademicOutboxMessage>()
                .SingleAsync(value => value.Id == messageId && value.EventType == "ai-review-requested");
            using var services = new ServiceCollection()
                .AddSingleton<IGradingExecutionOrchestrator>(Orchestrator)
                .BuildServiceProvider();
            var consumer = new AIReviewRequestConsumer(AIProviders, services);
            await consumer.ConsumeAsync(new AcademicOutboxEvent(
                message.Id,
                message.TenantId,
                message.EventType,
                message.EventSchemaVersion,
                message.PayloadCanonicalJson,
                message.PayloadHash,
                message.OccurredAt), default);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();

        private static string? ReviewConfiguration(RuntimeOptions options)
        {
            if (!options.ReviewMethods.HasFlag(ReviewMethods.SelfReview) &&
                !options.ReviewMethods.HasFlag(ReviewMethods.PeerReview) &&
                !options.ReviewMethods.HasFlag(ReviewMethods.AIReview) &&
                !options.ReviewMethods.HasFlag(ReviewMethods.InstructorReview))
            {
                return null;
            }

            return JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                self = options.ReviewMethods.HasFlag(ReviewMethods.SelfReview)
                    ? new { instructions = "Assess your response.", requireFeedback = true }
                    : null,
                peer = options.ReviewMethods.HasFlag(ReviewMethods.PeerReview)
                    ? new
                    {
                        reviewsPerReviewer = 1,
                        reviewsRequiredPerSubmission = 1,
                        minimumReviewsToFinalize = 1,
                        aggregation = "mean",
                        claimLeaseMinutes = 30,
                        evidenceWindowMinutes = 60,
                        onInsufficientEvidence = "await-instructor-resolution",
                    }
                    : null,
                ai = options.ReviewMethods.HasFlag(ReviewMethods.AIReview)
                    ? new { providerKey = ControlledAIReviewProvider.ProviderKey, policyVersion = "1" }
                    : null,
                instructor = options.ReviewMethods.HasFlag(ReviewMethods.InstructorReview)
                    ? new { requireOverrideReason = false }
                    : null,
            }, GradingJson.Options);
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class RouteOnlyReleaseConsumer : IAcademicOutboxConsumer
    {
        public string Key => "grading.immediate-release.v1";
        public IReadOnlySet<string> EventTypes { get; } =
            new HashSet<string>(StringComparer.Ordinal) { "grade-result-release-requested" };

        public Task ConsumeAsync(AcademicOutboxEvent message, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The runtime tests invoke the release command explicitly.");
    }

    private sealed class RouteOnlyAIReviewConsumer : IAcademicOutboxConsumer
    {
        public string Key => "grading.ai-review-request.v1";
        public IReadOnlySet<string> EventTypes { get; } =
            new HashSet<string>(StringComparer.Ordinal) { "ai-review-requested" };

        public Task ConsumeAsync(AcademicOutboxEvent message, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The runtime tests dispatch AI review requests explicitly.");
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
            new NotificationConfiguration().Configure(modelBuilder.Entity<Notification>());
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

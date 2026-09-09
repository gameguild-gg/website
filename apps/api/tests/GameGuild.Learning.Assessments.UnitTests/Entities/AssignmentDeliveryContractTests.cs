using FluentAssertions;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class AssignmentDeliveryContractTests
{
    [Fact]
    public void Assessment_ShouldExposeExplicitSubmissionAndPresentationContracts()
    {
        var assembly = typeof(Assessment).Assembly;
        var modality = assembly.GetType("GameGuild.Learning.Assessments.SubmissionModality");
        var presentationMode = assembly.GetType("GameGuild.Learning.Assessments.AssessmentPresentationMode");

        modality.Should().NotBeNull();
        presentationMode.Should().NotBeNull();
        typeof(Assessment).GetProperty("SubmissionModalities").Should().NotBeNull();
        typeof(Assessment).GetProperty("PresentationMode").Should().NotBeNull();
    }

    [Fact]
    public void DeliveryEnums_ShouldKeepExplicitPersistedValues()
    {
        ((int)SubmissionModality.Text).Should().Be(1);
        ((int)SubmissionModality.File).Should().Be(2);
        ((int)SubmissionModality.Url).Should().Be(4);
        ((int)SubmissionModality.Code).Should().Be(8);
        ((int)SubmissionModality.Media).Should().Be(16);
        ((int)SubmissionModality.Project).Should().Be(32);
        ((int)SubmissionModality.StructuredAnswer).Should().Be(64);
        ((int)AssessmentPresentationMode.SingleStep).Should().Be(0);
        ((int)AssessmentPresentationMode.Continuous).Should().Be(1);
    }

    [Fact]
    public void SetDeliverySchedule_WhenAvailabilityEndsBeforeItStarts_ShouldRejectIt()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Assignment", AssessmentType.Assignment, Score("100"));

        var action = () => assessment.SetAvailability(DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(1));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*availability end must be on or after availability start*");
    }

    [Fact]
    public void AssessmentSubmission_ShouldExposeOnlyGenericPersistedPayloads()
    {
        var properties = typeof(AssessmentSubmission).GetProperties().Select(property => property.Name);

        properties.Should().Contain(new[]
        {
            "TextPayload",
            "FilePayload",
            "UrlPayload",
            "CodePayload",
            "MediaPayload",
            "ProjectPayload"
        });
        properties.Should().NotContain("StructuredAnswerPayload");
        typeof(SubmitAssessmentRequest).GetProperty("StructuredAnswerPayload").Should().BeNull();
    }

    [Fact]
    public void AssessmentSubmission_ShouldExposeLateSubmissionMetadata()
    {
        typeof(AssessmentSubmission).GetProperty("IsLate").Should().NotBeNull();
    }

    [Fact]
    public void SetDeliverySchedule_WhenLateDeadlineIsOutsideAvailability_ShouldRejectIt()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Assignment", AssessmentType.Assignment, Score("100"));
        var dueAt = DateTime.UtcNow.AddDays(1);

        var action = () => assessment.SetDeliverySchedule(
            DateTime.UtcNow,
            dueAt.AddDays(1),
            dueAt,
            allowLateSubmissions: true,
            lateSubmissionDeadline: dueAt.AddDays(2));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Late submission deadline must be on or before availability end*");
    }

    [Fact]
    public void SetDeliverySchedule_WhenLateSubmissionsHaveNoDeadline_ShouldRejectIt()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Assignment", AssessmentType.Assignment, Score("100"));
        var dueAt = DateTime.UtcNow.AddDays(1);

        var action = () => assessment.SetDeliverySchedule(
            DateTime.UtcNow,
            dueAt.AddDays(2),
            dueAt,
            allowLateSubmissions: true,
            lateSubmissionDeadline: null);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Late submission deadline is required*");
    }

    [Fact]
    public void TryGetSubmissionTiming_ShouldHonorDueAndLateDeadlineBoundaries()
    {
        var now = DateTime.UtcNow;
        var dueAt = now.AddDays(1);
        var deadline = dueAt.AddDays(1);
        var assessment = Assessment.Create(Guid.NewGuid(), "Assignment", AssessmentType.Assignment, Score("100"));
        assessment.SetDeliverySchedule(now, deadline.AddDays(1), dueAt, true, deadline);

        assessment.TryGetSubmissionTiming(dueAt, out var atDueLate).Should().BeTrue();
        atDueLate.Should().BeFalse();
        assessment.TryGetSubmissionTiming(dueAt.AddTicks(1), out var afterDueLate).Should().BeTrue();
        afterDueLate.Should().BeTrue();
        assessment.TryGetSubmissionTiming(deadline, out var atDeadlineLate).Should().BeTrue();
        atDeadlineLate.Should().BeTrue();
        assessment.TryGetSubmissionTiming(deadline.AddTicks(1), out _).Should().BeFalse();

        assessment.SetDeliverySchedule(now, deadline.AddDays(1), dueAt, false, null);
        assessment.TryGetSubmissionTiming(dueAt.AddTicks(1), out _).Should().BeFalse();
    }

    [Fact]
    public void Update_WhenAvailabilityEndsBeforeItStarts_ShouldRejectIt()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Assignment", AssessmentType.Assignment, Score("100"));

        var action = () => assessment.Update(
            title: null,
            description: null,
            clearDescription: false,
            maxScore: null,
            passingScore: null,
            timeLimitMinutes: null,
            clearTimeLimitMinutes: false,
            maxAttempts: null,
            isRequired: null,
            availableFrom: DateTime.UtcNow.AddDays(2),
            clearAvailableFrom: false,
            availableUntil: DateTime.UtcNow.AddDays(1),
            clearAvailableUntil: false);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*availability end must be on or after availability start*");
    }

    [Fact]
    public void TryGetSubmissionTiming_WhenInsideLateWindow_ShouldAcceptAndMarkLate()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Assignment", AssessmentType.Assignment, Score("100"));
        var dueAt = DateTime.UtcNow.AddDays(1);
        assessment.SetDeliverySchedule(
            DateTime.UtcNow,
            dueAt.AddDays(3),
            dueAt,
            allowLateSubmissions: true,
            lateSubmissionDeadline: dueAt.AddDays(2));

        var canSubmit = assessment.TryGetSubmissionTiming(dueAt.AddDays(1), out var isLate);

        canSubmit.Should().BeTrue();
        isLate.Should().BeTrue();
    }

    [Fact]
    public void SetPayload_WhenPayloadUsesAllowedModalities_ShouldPersistEachPayload()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        var payload = new SubmitAssessmentRequest(
            TextPayload: "Reflection notes",
            FilePayload: "asset:file-123",
            UrlPayload: "https://example.test/demo",
            CodePayload: "Console.WriteLine(\"hello\");",
            MediaPayload: "https://example.test/demo.mp4",
            ProjectPayload: "project:abc");

        submission.SetPayload(payload, SubmissionModality.Text |
                                       SubmissionModality.File |
                                       SubmissionModality.Url |
                                       SubmissionModality.Code |
                                       SubmissionModality.Media |
                                       SubmissionModality.Project);

        submission.SubmittedModalities.Should().Be(SubmissionModality.Text |
                                                    SubmissionModality.File |
                                                    SubmissionModality.Url |
                                                    SubmissionModality.Code |
                                                    SubmissionModality.Media |
                                                    SubmissionModality.Project);
        submission.TextPayload.Should().Be(payload.TextPayload);
        submission.FilePayload.Should().Be(payload.FilePayload);
        submission.UrlPayload.Should().Be(payload.UrlPayload);
        submission.CodePayload.Should().Be(payload.CodePayload);
        submission.MediaPayload.Should().Be(payload.MediaPayload);
        submission.ProjectPayload.Should().Be(payload.ProjectPayload);
    }

    [Fact]
    public void SetPayload_WhenOnlyStructuredAnswerModalityIsAllowed_ShouldRejectGenericPayload()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

        var action = () => submission.SetPayload(
            new SubmitAssessmentRequest(TextPayload: "answer owned by grading execution"),
            SubmissionModality.StructuredAnswer);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Submission payload contains a modality that is not accepted by this assessment.*");
    }

    [Fact]
    public void SetPayload_WhenSubmissionIsNotInProgress_ShouldRejectIt()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit();

        var action = () => submission.SetPayload(new SubmitAssessmentRequest(TextPayload: "late edit"), SubmissionModality.Text);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Submission payload can only be changed while in progress.");
    }

    [Fact]
    public void Submit_WhenSubmissionIsNotInProgress_ShouldRejectRepeatedTransition()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit();

        var action = () => submission.Submit();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Only an in-progress submission can be submitted.");
    }

    [Fact]
    public void Grade_WhenSubmissionIsInProgress_ShouldRejectTransition()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

        var action = () => submission.Grade(Score("80"), Score("60"), Score("100"));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Only submitted submissions can be graded.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Grade_WhenSubmissionWasAlreadyGraded_ShouldRejectRepeatedTransition(bool isLate)
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit(isLate);
        submission.Grade(Score("80"), Score("60"), Score("100"));

        var action = () => submission.Grade(Score("90"), Score("60"), Score("100"));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Only submitted submissions can be graded.");
    }

    [Fact]
    public void SetPayload_WhenValidationFails_ShouldNotMutateExistingPayload()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.SetPayload(new SubmitAssessmentRequest(TextPayload: "original"), SubmissionModality.Text);

        var action = () => submission.SetPayload(
            new SubmitAssessmentRequest(TextPayload: "replacement", UrlPayload: "not-a-url"),
            SubmissionModality.Text | SubmissionModality.Url);

        action.Should().Throw<ArgumentException>();
        submission.TextPayload.Should().Be("original");
        submission.UrlPayload.Should().BeNull();
        submission.SubmittedModalities.Should().Be(SubmissionModality.Text);
    }

    [Theory]
    [InlineData(SubmissionModality.File)]
    [InlineData(SubmissionModality.Url)]
    [InlineData(SubmissionModality.Media)]
    [InlineData(SubmissionModality.Project)]
    public void SetPayload_WhenBoundedPayloadExceeds2048Characters_ShouldRejectIt(SubmissionModality modality)
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        var oversized = new string('x', 2049);
        var request = modality switch
        {
            SubmissionModality.File => new SubmitAssessmentRequest(FilePayload: oversized),
            SubmissionModality.Url => new SubmitAssessmentRequest(UrlPayload: $"https://example.test/{oversized}"),
            SubmissionModality.Media => new SubmitAssessmentRequest(MediaPayload: $"https://example.test/{oversized}"),
            SubmissionModality.Project => new SubmitAssessmentRequest(ProjectPayload: oversized),
            _ => throw new InvalidOperationException()
        };

        var action = () => submission.SetPayload(request, modality);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*cannot exceed 2048 characters*");
    }

    [Fact]
    public void Assessments_ShouldPersistInteractiveVideoCueLinks()
    {
        var cueType = typeof(Assessment).Assembly.GetType("GameGuild.Learning.Assessments.InteractiveVideoAssessmentCue");

        cueType.Should().NotBeNull();
        cueType!.GetProperty("AssessmentId").Should().NotBeNull();
        cueType.GetProperty("ContentId").Should().NotBeNull();
        cueType.GetProperty("CueId").Should().NotBeNull();
    }

    [Fact]
    public void DatabaseContract_ShouldRestrictDeliveryEnumsAndSchedule()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());
        new AssessmentsModelConfiguration().Configure(modelBuilder);
        var entity = modelBuilder.Model.FindEntityType(typeof(Assessment))!;
        var constraints = entity.GetCheckConstraints();

        constraints.Should().Contain(constraint => constraint.Name == "CK_Assessments_SubmissionModalities");
        constraints.Should().Contain(constraint => constraint.Name == "CK_Assessments_PresentationMode");
        constraints.Should().Contain(constraint => constraint.Name == "CK_Assessments_DeliverySchedule");
    }

    [Fact]
    public void DatabaseContract_ShouldRestrictSubmittedModalities()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());
        new AssessmentsModelConfiguration().Configure(modelBuilder);
        var entity = modelBuilder.Model.FindEntityType(typeof(AssessmentSubmission))!;

        entity.GetCheckConstraints().Should().Contain(constraint =>
            constraint.Name == "CK_AssessmentSubmissions_SubmittedModalities");
    }

    private static ScoreValue Score(string value) => ScoreValue.FromPoints(value);
}

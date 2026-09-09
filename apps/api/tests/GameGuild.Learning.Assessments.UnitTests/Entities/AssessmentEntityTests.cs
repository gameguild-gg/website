using FluentAssertions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// Unit tests for Assessment entity domain logic.
/// </summary>
public class AssessmentEntityTests
{
    [Fact]
    public void Create_ShouldSetDefaultValues()
    {
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Midterm Exam", AssessmentType.Quiz, Score(100));

        assessment.Id.Should().NotBeEmpty();
        assessment.CourseId.Should().Be(courseId);
        assessment.Title.Should().Be("Midterm Exam");
        assessment.Type.Should().Be(AssessmentType.Quiz);
        assessment.MaxScore.Should().Be(Score(100));
        assessment.IsRequired.Should().BeTrue();
        assessment.Order.Should().Be(0);
        assessment.TimeLimitMinutes.Should().BeNull();
        assessment.MaxAttempts.Should().Be(1);
    }

    [Fact]
    public void Create_WithIsRequiredFalse_ShouldSetFalse()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Quiz", AssessmentType.Quiz, Score(50), isRequired: false);
        assessment.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void Create_WithContentId_ShouldLinkAssessmentToContent()
    {
        var contentId = Guid.NewGuid();

        var assessment = Assessment.Create(
            Guid.NewGuid(),
            "Linked quiz",
            AssessmentType.Quiz,
            Score(100),
            contentId: contentId);

        assessment.ContentId.Should().Be(contentId);
    }

    [Fact]
    public void Create_WithoutContentId_ShouldDefaultToNull()
    {
        var assessment = Assessment.Create(
            Guid.NewGuid(),
            "Quiz",
            AssessmentType.Quiz,
            Score(100));

        assessment.ContentId.Should().BeNull();
    }

    [Fact]
    public void Create_WithReviewMethods_ShouldPersistBitwiseCombination()
    {
        var assessment = Assessment.Create(
            Guid.NewGuid(),
            "Multi-graded quiz",
            AssessmentType.Quiz,
            Score(100),
            reviewMethods: ReviewMethods.AutomatedReview | ReviewMethods.InstructorReview);

        assessment.ReviewMethods.Should().Be(ReviewMethods.AutomatedReview | ReviewMethods.InstructorReview);
        ((int)assessment.ReviewMethods).Should().Be(12);
    }

    [Fact]
    public void Create_WithoutReviewMethods_ShouldDefaultToInstructorReview()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Quiz", AssessmentType.Quiz, Score(100));

        assessment.ReviewMethods.Should().Be(ReviewMethods.InstructorReview);
    }

    [Fact]
    public void Create_WithNoReviewMethods_ShouldBeAcceptedAsDraft()
    {
        var assessment = Assessment.Create(
            Guid.NewGuid(),
            "Survey",
            AssessmentType.Quiz,
            Score(100),
            reviewMethods: ReviewMethods.None);

        assessment.ReviewMethods.Should().Be(ReviewMethods.None);
    }

    [Fact]
    public void Update_WithReviewMethods_ShouldPersistNewFlags()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Quiz", AssessmentType.Quiz, Score(100));

        UpdateAssessment(
            assessment,
            reviewMethods: ReviewMethods.PeerReview | ReviewMethods.InstructorReview,
            reviewConfigurationCanonicalJson: "{\"instructor\":{\"requireOverrideReason\":false},\"peer\":{\"aggregation\":\"mean\",\"claimLeaseMinutes\":30,\"evidenceWindowMinutes\":60,\"minimumReviewsToFinalize\":1,\"onInsufficientEvidence\":\"await-instructor-resolution\",\"reviewsPerReviewer\":1,\"reviewsRequiredPerSubmission\":1},\"schemaVersion\":1}");

        assessment.ReviewMethods.Should().Be(ReviewMethods.PeerReview | ReviewMethods.InstructorReview);
    }

    [Fact]
    public void Update_WithoutReviewMethods_ShouldLeaveExistingFlagsUnchanged()
    {
        var assessment = Assessment.Create(
            Guid.NewGuid(),
            "Quiz",
            AssessmentType.Quiz,
            Score(100),
            reviewMethods: ReviewMethods.PeerReview);

        UpdateAssessment(assessment);

        assessment.ReviewMethods.Should().Be(ReviewMethods.PeerReview);
    }

    [Fact]
    public void Assessment_ShouldNotExposeMutableGenericDefinitionPayload()
    {
        typeof(Assessment).GetProperty("DefinitionPayload").Should().BeNull();
        typeof(Assessment).GetProperty("DefinitionSchemaVersion").Should().BeNull();
        typeof(Assessment).GetMethod("SetDefinition").Should().BeNull();
    }

    [Fact]
    public void Create_WithZeroMaxScore_Throws()
    {
        var action = () => Assessment.Create(Guid.NewGuid(), "Quiz", AssessmentType.Quiz, ScoreValue.Zero);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Assessment_ShouldExposeWeightedGroupAssignment()
    {
        typeof(Assessment).GetProperty("AssessmentGroupId").Should().NotBeNull();
    }

    [Fact]
    public void AssessmentGroup_ShouldExposeCourseWeightAndOrder()
    {
        var type = typeof(Assessment).Assembly.GetType("GameGuild.Learning.Assessments.AssessmentGroup");

        type.Should().NotBeNull();
        type!.GetProperty("CourseId").Should().NotBeNull();
        type.GetProperty("WeightPercent").Should().NotBeNull();
        type.GetProperty("Order").Should().NotBeNull();
    }

    [Fact]
    public void AssessmentType_ShouldKeepExplicitPersistedValues()
    {
        ((int)AssessmentType.Quiz).Should().Be(0);
        ((int)AssessmentType.Assignment).Should().Be(2);
        ((int)AssessmentType.Project).Should().Be(3);
        ((int)AssessmentType.PeerReview).Should().Be(4);
        ((int)AssessmentType.SelfAssessment).Should().Be(5);
        Enum.GetNames<AssessmentType>().Should().NotContain("Exam");
    }

    [Fact]
    public void IsAvailable_WhenNoDateRestrictions_ShouldReturnTrue()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Test", AssessmentType.Quiz, Score(100));
        assessment.IsAvailable().Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WhenBeforeAvailableFrom_ShouldReturnFalse()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Test", AssessmentType.Quiz, Score(100));
        assessment.SetAvailability(DateTime.UtcNow.AddDays(1), null);
        assessment.IsAvailable().Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_WhenAfterAvailableUntil_ShouldReturnFalse()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Test", AssessmentType.Quiz, Score(100));
        assessment.SetAvailability(null, DateTime.UtcNow.AddDays(-1));
        assessment.IsAvailable().Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_WhenWithinWindow_ShouldReturnTrue()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Test", AssessmentType.Quiz, Score(100));
        assessment.SetAvailability(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        assessment.IsAvailable().Should().BeTrue();
    }

    [Fact]
    public void SetDescription_ShouldUpdateDescription()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Test", AssessmentType.Quiz, Score(100));
        assessment.SetDescription("A comprehensive quiz");
        assessment.Description.Should().Be("A comprehensive quiz");
    }

    [Fact]
    public void SetTimeLimit_ShouldUpdateTimeLimit()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Test", AssessmentType.Quiz, Score(100));
        assessment.SetTimeLimit(90);
        assessment.TimeLimitMinutes.Should().Be(90);
    }

    [Fact]
    public void SetMaxAttempts_WithOne_ShouldKeepSupportedPolicy()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Test", AssessmentType.Quiz, Score(100));
        assessment.SetMaxAttempts(1);
        assessment.MaxAttempts.Should().Be(1);
    }

    [Fact]
    public void SetMaxAttempts_AboveOne_ShouldRejectUntilContributionRuntimeExists()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Test", AssessmentType.Quiz, Score(100));

        var action = () => assessment.SetMaxAttempts(2);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_ShouldModifyMultipleFields()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Old Title", AssessmentType.Quiz, Score(100));
        UpdateAssessment(
            assessment,
            title: "New Title",
            description: "New Desc",
            maxScore: Score(200),
            timeLimitMinutes: 60,
            maxAttempts: 1,
            isRequired: false);

        assessment.Title.Should().Be("New Title");
        assessment.Description.Should().Be("New Desc");
        assessment.MaxScore.Should().Be(Score(200));
        assessment.TimeLimitMinutes.Should().Be(60);
        assessment.MaxAttempts.Should().Be(1);
        assessment.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void Update_WithLowerMaxScore_SucceedsWhenNoSubmissionConflicts()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Quiz", AssessmentType.Quiz, Score(100));

        UpdateAssessment(assessment, maxScore: Score(50));

        assessment.MaxScore.Should().Be(Score(50));
    }

    [Fact]
    public void Update_WithContentId_ShouldSetContentId()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Title", AssessmentType.Quiz, Score(100));
        var contentId = Guid.NewGuid();

        UpdateAssessment(assessment, contentId: contentId);

        assessment.ContentId.Should().Be(contentId);
    }

    [Fact]
    public void Update_WithClearContentId_ThrowsWhenContentAlreadyLinked()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Title", AssessmentType.Quiz, Score(100));
        UpdateAssessment(assessment, contentId: Guid.NewGuid());

        var act = () => UpdateAssessment(assessment, clearContentId: true);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be unlinked*");
        assessment.ContentId.Should().NotBeNull();
    }

    [Fact]
    public void Update_WithDifferentContentId_ThrowsWhenContentAlreadyLinked()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Title", AssessmentType.Quiz, Score(100));
        UpdateAssessment(assessment, contentId: Guid.NewGuid());

        var act = () => UpdateAssessment(assessment, contentId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be unlinked*");
    }

    [Fact]
    public void Update_WithSameContentId_IsAllowedNoOp()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Title", AssessmentType.Quiz, Score(100));
        var contentId = Guid.NewGuid();
        UpdateAssessment(assessment, contentId: contentId);

        UpdateAssessment(assessment, contentId: contentId);

        assessment.ContentId.Should().Be(contentId);
    }

    private static void UpdateAssessment(
        Assessment assessment,
        string? title = null,
        string? description = null,
        ScoreValue? maxScore = null,
        int? timeLimitMinutes = null,
        int? maxAttempts = null,
        bool? isRequired = null,
        Guid? contentId = null,
        bool clearContentId = false,
        ReviewMethods? reviewMethods = null,
        string? reviewConfigurationCanonicalJson = null)
    {
        assessment.Update(
            title,
            description,
            clearDescription: false,
            maxScore,
            passingScore: null,
            timeLimitMinutes,
            clearTimeLimitMinutes: false,
            maxAttempts,
            isRequired,
            availableFrom: null,
            clearAvailableFrom: false,
            availableUntil: null,
            clearAvailableUntil: false,
            contentId,
            clearContentId,
            reviewMethods: reviewMethods,
            reviewConfigurationCanonicalJson: reviewConfigurationCanonicalJson);
    }
}

/// <summary>
/// Service-level tests for AssessmentService.RestoreAssessmentAsync.
/// </summary>
public class AssessmentServiceRestoreTests
{
    [Fact]
    public async Task RestoreAssessmentAsync_OnSoftDeletedAssessment_MakesItFetchable()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Quiz", AssessmentType.Quiz, Score(100));
        assessment.SetMaxAttempts(1);
        assessment.Version = 1;
        assessment.SoftDelete();
        db.Set<Assessment>().Add(assessment);
        await db.SaveChangesAsync();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var before = await service.GetAssessmentByIdAsync(assessment.Id);
        before.Should().BeNull();

        var result = await service.RestoreAssessmentAsync(assessment.Id);

        result.IsSuccess.Should().BeTrue();
        var after = await service.GetAssessmentByIdAsync(assessment.Id);
        after.Should().NotBeNull();
        after!.DeletedAt.Should().BeNull();
        after.Title.Should().Be("Quiz");
    }

    [Fact]
    public async Task RestoreAssessmentAsync_OnUnknownId_ReturnsNotFound()
    {
        await using var db = CreateContext();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.RestoreAssessmentAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task RestoreAssessmentAsync_OnActiveAssessment_IsIdempotent()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Quiz", AssessmentType.Quiz, Score(100));
        db.Set<Assessment>().Add(assessment);
        await db.SaveChangesAsync();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.RestoreAssessmentAsync(assessment.Id);

        result.IsSuccess.Should().BeTrue();
        var fetched = await service.GetAssessmentByIdAsync(assessment.Id);
        fetched.Should().NotBeNull();
        fetched!.DeletedAt.Should().BeNull();
    }

    private static TestAssessmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestAssessmentDbContext>()
            .UseInMemoryDatabase($"AssessmentRestore_{Guid.NewGuid()}")
            .Options;
        return new TestAssessmentDbContext(options);
    }

    private sealed class TestAssessmentDbContext(DbContextOptions<TestAssessmentDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for assessment restore tests.");
        }
    }
}

/// <summary>
/// Unit tests for AssessmentSubmission entity domain logic.
/// </summary>
public class AssessmentSubmissionEntityTests
{
    [Fact]
    public void Grade_RequiresAssessmentMaximumScore()
    {
        var gradeMethodSignatures = typeof(AssessmentSubmission)
            .GetMethods()
            .Where(method => method.Name == nameof(AssessmentSubmission.Grade))
            .Select(method => string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.Name)))
            .ToList();

        gradeMethodSignatures.Should().Contain("ScoreValue,ScoreValue,ScoreValue,Nullable`1,String");
        gradeMethodSignatures.Should().Contain("ScoreValue,ScoreValue,ScoreValue,Nullable`1,String,String");
    }

    [Fact]
    public void Start_ShouldSetDefaultValues()
    {
        var assessmentId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var submission = AssessmentSubmission.Start(assessmentId, enrollmentId, userId, 1);

        submission.Id.Should().NotBeEmpty();
        submission.AssessmentId.Should().Be(assessmentId);
        submission.EnrollmentId.Should().Be(enrollmentId);
        submission.UserId.Should().Be(userId);
        submission.AttemptNumber.Should().Be(1);
        submission.Status.Should().Be(SubmissionStatus.InProgress);
        submission.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        submission.Score.Should().BeNull();
        submission.Passed.Should().BeNull();
        submission.SubmittedAt.Should().BeNull();
    }

    [Fact]
    public void Submit_ShouldChangeStatusAndSetSubmittedAt()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit();

        submission.Status.Should().Be(SubmissionStatus.Submitted);
        submission.SubmittedAt.Should().NotBeNull();
    }

    [Fact]
    public void Grade_ShouldSetScoreAndPassStatus_WhenPassing()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit();

        var graderId = Guid.NewGuid();
        submission.Grade(Score(85), Score(70), Score(100), graderId, "Good work!");

        submission.Score.Should().Be(Score(85));
        submission.Passed.Should().BeTrue();
        submission.Status.Should().Be(SubmissionStatus.Graded);
        submission.GradedAt.Should().NotBeNull();
        submission.GradedBy.Should().Be(graderId);
        submission.Feedback.Should().Be("Good work!");
    }

    [Fact]
    public void Grade_ShouldSetPassedFalse_WhenFailing()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit();
        submission.Grade(Score(50), Score(70), Score(100));

        submission.Passed.Should().BeFalse();
    }

    [Fact]
    public void Grade_AtExactPassingScore_ShouldPass()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit();
        submission.Grade(Score(70), Score(70), Score(100));

        submission.Passed.Should().BeTrue();
    }

    [Fact]
    public void Grade_WithScoreOutsideAssessmentMaximum_Throws()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit();
        var gradeWithMaximum = typeof(AssessmentSubmission).GetMethod(
            nameof(AssessmentSubmission.Grade),
            [typeof(ScoreValue), typeof(ScoreValue), typeof(ScoreValue), typeof(Guid?), typeof(string)]);

        gradeWithMaximum.Should().NotBeNull();
        var action = () => gradeWithMaximum!.Invoke(submission, [Score(101), Score(60), Score(100), null, null]);

        action.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task StartSubmissionAsync_RejectsGenericRuntimeForAnyContentBackedGradedAssessment()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(
            Guid.NewGuid(),
            "Assignment",
            AssessmentType.Assignment,
            Score(100),
            contentId: Guid.NewGuid());
        var enrollmentId = Guid.NewGuid();
        db.Add(assessment);
        await db.SaveChangesAsync();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.StartSubmissionAsync(assessment.Id, enrollmentId, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Assessment.OfficialRuntimeUnavailable");
    }

    [Fact]
    public async Task StartSubmissionAsync_AllowsGenericRuntimeWhenContentHasNoReviewWorkflow()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(
            Guid.NewGuid(),
            "Practice activity",
            AssessmentType.Assignment,
            Score(100),
            contentId: Guid.NewGuid(),
            reviewMethods: ReviewMethods.None);
        db.Add(assessment);
        await db.SaveChangesAsync();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.StartSubmissionAsync(assessment.Id, Guid.NewGuid(), Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
    }

    private static TestAssessmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestAssessmentDbContext>()
            .UseInMemoryDatabase($"AssessmentAttempt_{Guid.NewGuid()}")
            .Options;
        return new TestAssessmentDbContext(options);
    }

    private sealed class TestAssessmentDbContext(DbContextOptions<TestAssessmentDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for assessment entity tests.");
        }
    }
}

/// <summary>
/// Tests for AssessmentDto record and mapping.
/// </summary>
public class AssessmentDtoTests
{
    [Fact]
    public void FromEntity_ShouldMapAllProperties()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Final Exam", AssessmentType.Quiz, Score(100));
        assessment.SetDescription("Comprehensive exam");
        assessment.SetTimeLimit(120);

        var dto = AssessmentDto.FromEntity(assessment);

        dto.Id.Should().Be(assessment.Id);
        dto.CourseId.Should().Be(assessment.CourseId);
        dto.Title.Should().Be("Final Exam");
        dto.Description.Should().Be("Comprehensive exam");
        dto.Type.Should().Be(AssessmentType.Quiz);
        dto.MaxScore.Should().Be(Score(100));
        dto.TimeLimitMinutes.Should().Be(120);
        dto.MaxAttempts.Should().Be(1);
        dto.IsRequired.Should().BeTrue();
        dto.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var dto = new AssessmentDto(
            Id: id,
            CourseId: courseId,
            ContentId: null,
            Title: "Quiz",
            Slug: "quiz",
            Description: "Desc",
            Type: AssessmentType.Quiz,
            MaxScore: Score(50),
            PassingScore: Score(25),
            TimeLimitMinutes: 15,
            MaxAttempts: 1,
            IsRequired: false,
            Order: 1,
            AvailableFrom: DateTime.UtcNow,
            AvailableUntil: DateTime.UtcNow.AddDays(7),
            AssessmentGroupId: null,
            AssessmentGroupName: null,
            AssessmentGroupWeightPercent: null,
            AssessmentGroupOrder: null,
            IsAvailable: true);

        dto.Id.Should().Be(id);
        dto.CourseId.Should().Be(courseId);
        dto.ContentId.Should().BeNull();
        dto.Title.Should().Be("Quiz");
        dto.Slug.Should().Be("quiz");
        dto.MaxScore.Should().Be(Score(50));
        dto.TimeLimitMinutes.Should().Be(15);
        dto.MaxAttempts.Should().Be(1);
        dto.IsRequired.Should().BeFalse();
        dto.Order.Should().Be(1);
        dto.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void AssessmentDto_ShouldExposeGroupMetadata()
    {
        typeof(AssessmentDto).GetProperty("AssessmentGroupId").Should().NotBeNull();
        typeof(AssessmentDto).GetProperty("AssessmentGroupName").Should().NotBeNull();
        typeof(AssessmentDto).GetProperty("AssessmentGroupWeightPercent").Should().NotBeNull();
        typeof(AssessmentDto).GetProperty("AssessmentGroupOrder").Should().NotBeNull();
    }

    [Fact]
    public void AssessmentAnalyticsDtos_ShouldExposeScoreDistributionContract()
    {
        typeof(CourseAssessmentAnalyticsDto).GetProperty("CourseId").Should().NotBeNull();
        typeof(CourseAssessmentAnalyticsDto).GetProperty("Distribution").Should().NotBeNull();
        typeof(CourseAssessmentAnalyticsDto).GetProperty("Groups").Should().NotBeNull();
        typeof(AssessmentGroupAnalyticsDto).GetProperty("AveragePercent").Should().NotBeNull();
        typeof(AssessmentScoreBucketDto).GetProperty("Count").Should().NotBeNull();
    }
}

public sealed class AssessmentServiceAnalyticsTests
{
    [Fact]
    public async Task GetCourseAssessmentAnalyticsAsync_ReturnsOverallAndGroupScoreDistribution()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var quizGroup = AssessmentGroup.Create(courseId, "Quizzes", Percent(20), 1);
        var projectGroup = AssessmentGroup.Create(courseId, "Final Project", Percent(30), 2);
        var feedbackGroup = AssessmentGroup.Create(courseId, "Feedback", Percent(0), 3);
        var quiz = Assessment.Create(courseId, "Intro quiz", AssessmentType.Quiz, Score(10), assessmentGroupId: quizGroup.Id);
        var project = Assessment.Create(courseId, "Final build", AssessmentType.Project, Score(100), assessmentGroupId: projectGroup.Id);
        var attendance = Assessment.Create(courseId, "Attendance", AssessmentType.Assignment, Score(10));
        var feedbackOnly = Assessment.Create(
            courseId,
            "Practice quiz",
            AssessmentType.Quiz,
            Score(10),
            assessmentGroupId: feedbackGroup.Id);
        var ignoredOtherCourse = Assessment.Create(Guid.NewGuid(), "Other", AssessmentType.Quiz, Score(10));

        var quizSubmission = AssessmentSubmission.Start(quiz.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        quizSubmission.Submit();
        quizSubmission.Grade(Score(8), Score(6), quiz.MaxScore);
        var projectSubmission = AssessmentSubmission.Start(project.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        projectSubmission.Submit();
        projectSubmission.Grade(Score(50), Score(70), project.MaxScore);
        var ignoredSubmission = AssessmentSubmission.Start(ignoredOtherCourse.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        ignoredSubmission.Submit();
        ignoredSubmission.Grade(Score(10), Score(6), ignoredOtherCourse.MaxScore);

        db.Set<AssessmentGroup>().AddRange(quizGroup, projectGroup, feedbackGroup);
        db.Set<Assessment>().AddRange(quiz, project, attendance, feedbackOnly, ignoredOtherCourse);
        db.Set<AssessmentSubmission>().AddRange(quizSubmission, projectSubmission, ignoredSubmission);
        await db.SaveChangesAsync();

        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var analytics = await service.GetCourseAssessmentAnalyticsAsync(courseId);

        analytics.CourseId.Should().Be(courseId);
        analytics.AssessmentCount.Should().Be(2);
        analytics.GradedCount.Should().Be(2);
        analytics.UngradedCount.Should().Be(0);
        analytics.AveragePercent.Should().Be(Percent(65));
        analytics.PassRate.Should().Be(Percent(50));
        analytics.Distribution.Single(bucket => bucket.Label == "80-89").Count.Should().Be(1);
        analytics.Distribution.Single(bucket => bucket.Label == "0-59").Count.Should().Be(1);
        analytics.Groups.Single(group => group.GroupName == "Quizzes").AveragePercent.Should().Be(Percent(80));
        analytics.Groups.Single(group => group.GroupName == "Final Project").PassRate.Should().Be(Percent(0));
        analytics.Groups.Should().NotContain(group => group.GroupName == "Feedback");
        analytics.Groups.Should().NotContain(group => group.GroupName == "Ungrouped");
    }

    private static TestAssessmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestAssessmentDbContext>()
            .UseInMemoryDatabase($"AssessmentAnalytics_{Guid.NewGuid()}")
            .Options;
        return new TestAssessmentDbContext(options);
    }

    private sealed class TestAssessmentDbContext(DbContextOptions<TestAssessmentDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for assessment analytics tests.");
        }
    }
}

/// <summary>
/// Tests for AssessmentSubmissionDto record and mapping.
/// </summary>
public class AssessmentSubmissionDtoTests
{
    [Fact]
    public void FromEntity_ShouldMapAllProperties()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);
        submission.Submit();
        var graderId = Guid.NewGuid();
        submission.Grade(Score(88), Score(70), Score(100), graderId, "Excellent");

        var dto = AssessmentSubmissionDto.FromEntity(submission);

        dto.Id.Should().Be(submission.Id);
        dto.AssessmentId.Should().Be(submission.AssessmentId);
        dto.EnrollmentId.Should().Be(submission.EnrollmentId);
        dto.UserId.Should().Be(submission.UserId);
        dto.AttemptNumber.Should().Be(2);
        dto.Score.Should().Be(Score(88));
        dto.Passed.Should().BeTrue();
        dto.SubmittedAt.Should().NotBeNull();
        dto.GradedAt.Should().NotBeNull();
        dto.GradedBy.Should().Be(graderId);
        dto.Feedback.Should().Be("Excellent");
        dto.Status.Should().Be(SubmissionStatus.Graded);
    }

    [Fact]
    public void FromEntity_InProgressSubmission_ShouldMapNullableFields()
    {
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

        var dto = AssessmentSubmissionDto.FromEntity(submission);

        dto.Score.Should().BeNull();
        dto.Passed.Should().BeNull();
        dto.SubmittedAt.Should().BeNull();
        dto.GradedAt.Should().BeNull();
        dto.GradedBy.Should().BeNull();
        dto.Feedback.Should().BeNull();
        dto.Status.Should().Be(SubmissionStatus.InProgress);
    }
}

/// <summary>
/// Tests for request records.
/// </summary>
public class AssessmentRequestRecordTests
{
    [Fact]
    public void CreateAssessmentRequest_ShouldSetAllProperties()
    {
        var courseId = Guid.NewGuid();
        var request = new CreateAssessmentRequest(
            CourseId: courseId,
            Title: "Exam",
            Description: "Final",
            Type: AssessmentType.Quiz,
            MaxScore: Score(100),
            PassingScore: Score(60),
            TimeLimitMinutes: 90,
            MaxAttempts: 1,
            IsRequired: true,
            AvailableFrom: DateTime.UtcNow,
            AvailableUntil: DateTime.UtcNow.AddDays(7));

        request.CourseId.Should().Be(courseId);
        request.Title.Should().Be("Exam");
        request.Description.Should().Be("Final");
        request.Type.Should().Be(AssessmentType.Quiz);
        request.MaxScore.Should().Be(Score(100));
        request.PassingScore.Should().Be(Score(60));
        request.TimeLimitMinutes.Should().Be(90);
        request.MaxAttempts.Should().Be(1);
        request.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void CreateAssessmentRequest_Defaults_ShouldBeCorrect()
    {
        var request = new CreateAssessmentRequest(Guid.NewGuid(), "Quiz", null, AssessmentType.Quiz, Score(50));

        request.TimeLimitMinutes.Should().BeNull();
        request.MaxAttempts.Should().Be(1);
        request.IsRequired.Should().BeTrue();
        request.AvailableFrom.Should().BeNull();
        request.AvailableUntil.Should().BeNull();
    }

    [Fact]
    public void CreateAssessmentGroupRequest_ShouldExposeWeightedGroupFields()
    {
        var type = typeof(CreateAssessmentRequest).Assembly.GetType("GameGuild.Learning.Assessments.CreateAssessmentGroupRequest");

        type.Should().NotBeNull();
        type!.GetProperty("CourseId").Should().NotBeNull();
        type.GetProperty("Name").Should().NotBeNull();
        type.GetProperty("WeightPercent").Should().NotBeNull();
    }

    [Fact]
    public void UpdateAssessmentRequest_ShouldSetAllProperties()
    {
        var request = new UpdateAssessmentRequest(
            ExpectedVersion: 7,
            Title: "New Title",
            Description: "New Desc",
            MaxScore: Score(200),
            PassingScore: Score(100),
            TimeLimitMinutes: 90,
            MaxAttempts: 1,
            IsRequired: false,
            AvailableFrom: DateTime.UtcNow,
            AvailableUntil: DateTime.UtcNow.AddDays(14));

        request.Title.Should().Be("New Title");
        request.Description.Should().Be("New Desc");
        request.ExpectedVersion.Should().Be(7);
        request.MaxScore.Should().Be(Score(200));
        request.TimeLimitMinutes.Should().Be(90);
        request.MaxAttempts.Should().Be(1);
        request.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void UpdateAssessmentRequest_AllDefaults_ShouldBeNull()
    {
        var request = new UpdateAssessmentRequest(ExpectedVersion: 0);

        request.Title.Should().BeNull();
        request.Description.Should().BeNull();
        request.MaxScore.Should().BeNull();
        request.TimeLimitMinutes.Should().BeNull();
        request.MaxAttempts.Should().BeNull();
        request.IsRequired.Should().BeNull();
    }

    [Fact]
    public void GradeSubmissionRequest_ShouldSetAllProperties()
    {
        var graderId = Guid.NewGuid();
        var request = new GradeSubmissionRequest(Score(85), graderId, "Well done");

        request.Score.Should().Be(Score(85));
        request.GradedBy.Should().Be(graderId);
        request.Feedback.Should().Be("Well done");
    }

    [Fact]
    public void GradeSubmissionRequest_Defaults_ShouldBeNull()
    {
        var request = new GradeSubmissionRequest(Score(70));

        request.GradedBy.Should().BeNull();
        request.Feedback.Should().BeNull();
    }
}

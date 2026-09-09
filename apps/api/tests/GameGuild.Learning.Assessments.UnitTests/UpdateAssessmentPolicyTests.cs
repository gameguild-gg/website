using FluentAssertions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// Tests for UpdateAssessment wiring of group-set assignment and typed review policy.
/// </summary>
public class UpdateAssessmentPolicyTests
{
    [Fact]
    public async Task UpdateAssessmentAsync_AssignsGroupSet_WhenSetBelongsToCourse()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var set = CourseGroupSet.Create(courseId, "Project teams");
        var assessment = Assessment.Create(courseId, "Group project", AssessmentType.Project, Score("100"));
        db.AddRange(set, assessment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.UpdateAssessmentAsync(
            assessment.Id,
            new UpdateAssessmentRequest(assessment.Version, GroupSetId: set.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.GroupSetId.Should().Be(set.Id);
    }

    [Fact]
    public async Task UpdateAssessmentAsync_RejectsGroupSet_FromAnotherCourse()
    {
        await using var db = CreateContext();
        var set = CourseGroupSet.Create(Guid.NewGuid(), "Other course teams");
        var assessment = Assessment.Create(Guid.NewGuid(), "Group project", AssessmentType.Project, Score("100"));
        db.AddRange(set, assessment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.UpdateAssessmentAsync(
            assessment.Id,
            new UpdateAssessmentRequest(assessment.Version, GroupSetId: set.Id));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        // The tracked entity is mutated before validation fails, but nothing is persisted.
        (await db.Set<Assessment>().AsNoTracking().SingleAsync()).GroupSetId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAssessmentAsync_ClearGroupSetId_RemovesAssignment()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var set = CourseGroupSet.Create(courseId, "Project teams");
        var assessment = Assessment.Create(courseId, "Group project", AssessmentType.Project, Score("100"));
        assessment.AssignToGroupSet(set.Id);
        db.AddRange(set, assessment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.UpdateAssessmentAsync(
            assessment.Id,
            new UpdateAssessmentRequest(assessment.Version, ClearGroupSetId: true));

        result.IsSuccess.Should().BeTrue();
        result.Value.GroupSetId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAssessmentAsync_SetsTypedPeerReviewConfiguration()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Peer task", AssessmentType.Assignment, Score("10"));
        db.Add(assessment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.UpdateAssessmentAsync(
            assessment.Id,
            new UpdateAssessmentRequest(
                assessment.Version,
                ReviewMethods: ReviewMethods.PeerReview,
                ReviewConfigurationCanonicalJson: PeerReviewConfiguration(3)));

        result.IsSuccess.Should().BeTrue();
        result.Value.GetRequiredPeerReviewCount().Should().Be(3);
    }

    [Fact]
    public async Task UpdateAssessmentAsync_RejectsInvalidPeerReviewConfiguration()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Peer task", AssessmentType.Assignment, Score("10"));
        db.Add(assessment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.UpdateAssessmentAsync(
            assessment.Id,
            new UpdateAssessmentRequest(
                assessment.Version,
                ReviewMethods: ReviewMethods.PeerReview,
                ReviewConfigurationCanonicalJson: PeerReviewConfiguration(0)));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void AssessmentDto_ExposesGroupSetAndReviewPolicy()
    {
        var courseId = Guid.NewGuid();
        var set = CourseGroupSet.Create(courseId, "Project teams");
        var assessment = Assessment.Create(courseId, "Group project", AssessmentType.Project, Score("100"));
        assessment.AssignToGroupSet(set.Id);
        assessment.SetReviewPolicy(
            ReviewMethods.PeerReview,
            PeerReviewConfiguration(3),
            null,
            ContentCompletionMode.OnReleaseAndPass,
            ResultReleaseMode.Manual,
            null);

        var dto = AssessmentDto.FromEntity(assessment);

        dto.GroupSetId.Should().Be(set.Id);
        dto.ReviewMethods.Should().Be(ReviewMethods.PeerReview);
        dto.ReviewConfigurationCanonicalJson.Should().Be(PeerReviewConfiguration(3));
    }

    private static AssessmentService CreateService(TestAssessmentDbContext db)
    {
        return new AssessmentService(
            db,
            Mock.Of<IProgramContentService>(),
            new RubricService(db, NullLogger<RubricService>.Instance),
            NullLogger<AssessmentService>.Instance);
    }

    private static TestAssessmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestAssessmentDbContext>()
            .UseInMemoryDatabase($"AssessmentPolicy_{Guid.NewGuid()}")
            .Options;
        return new TestAssessmentDbContext(options);
    }

    private static ScoreValue Score(string value) => ScoreValue.FromPoints(value);

    private static string PeerReviewConfiguration(int requiredCount) =>
        $$"""
        {"peer":{"aggregation":"mean","claimLeaseMinutes":30,"evidenceWindowMinutes":60,"minimumReviewsToFinalize":{{requiredCount}},"onInsufficientEvidence":"await-instructor-resolution","reviewsPerReviewer":{{requiredCount}},"reviewsRequiredPerSubmission":{{requiredCount}}},"schemaVersion":1}
        """;

    private sealed class TestAssessmentDbContext(DbContextOptions<TestAssessmentDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for policy update tests.");
        }
    }
}

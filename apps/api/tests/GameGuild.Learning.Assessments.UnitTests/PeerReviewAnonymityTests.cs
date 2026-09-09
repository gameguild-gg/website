using System.Text.Json;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Assessments.Grading.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// THE anonymity boundary of the speedgrader feature: student-facing peer-review DTOs must not
/// carry reviewer OR reviewee identity (proven by serializing responses and asserting absent
/// JSON keys, not null properties), while the instructor endpoint shows reviewer names.
/// </summary>
public class PeerReviewAnonymityTests
{
    private static readonly string[] ForbiddenSubmissionKeys =
        ["userId", "enrollmentId", "score", "passed", "feedback", "gradedBy", "gradedAt", "courseGroupId"];

    // ===== (a) GET peer-reviews/{reviewId} — reviewer-only anonymous submission =====

    [Fact]
    public async Task GetReview_AsReviewer_ReturnsAnonymousSubmissionWithoutRevieweeFields()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        var controller = CreateController(db, review.ReviewerUserId);

        var result = await controller.GetReview(review.Id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<AnonymousReviewSubmissionDto>().Subject;
        dto.AttemptNumber.Should().Be(1);
        dto.TextPayload.Should().Be("peer work");
        var keys = CollectPropertyNames(JsonSerializer.SerializeToElement(dto));
        keys.Select(k => k.ToLowerInvariant())
            .Should().NotContain(ForbiddenSubmissionKeys, "the reviewer must not see the reviewee's identity, score, or instructor feedback");
    }

    [Fact]
    public async Task GetReview_ByNonReviewer_ReturnsForbidden()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        var controller = CreateController(db, Guid.NewGuid());

        var result = await controller.GetReview(review.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // ===== (b) POST peer-reviews/{reviewId}/submit =====

    [Fact]
    public async Task SubmitReview_WithFeedback_PersistsScoreAndFeedback()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        var controller = CreateController(db, review.ReviewerUserId);

        var result = await controller.SubmitReview(review.Id, new PeerReviewSubmitRequest(Score(80), "Strong thesis", null));

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(200);
        var saved = await db.Set<AssessmentPeerReview>().SingleAsync(r => r.Id == review.Id);
        saved.Status.Should().Be(PeerReviewStatus.Submitted);
        saved.Score.Should().Be(Score(80));
        saved.Feedback.Should().Be("Strong thesis");
    }

    [Fact]
    public async Task SubmitReview_WithoutFeedback_ReturnsBadRequest()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        var controller = CreateController(db, review.ReviewerUserId);

        var result = await controller.SubmitReview(review.Id, new PeerReviewSubmitRequest(Score(80), "   ", null));

        var bad = result.Should().BeAssignableTo<ObjectResult>().Which;
        bad.StatusCode.Should().Be(400);
        bad.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("Feedback comment is required to complete a peer review");
    }

    [Fact]
    public async Task SubmitReview_AfterWindowClosed_ReturnsConflict()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(
            db, dueAt: SystemClock.UtcNow.AddDays(-2), lateDeadline: SystemClock.UtcNow.AddDays(-1));
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        var controller = CreateController(db, review.ReviewerUserId);

        var result = await controller.SubmitReview(review.Id, new PeerReviewSubmitRequest(Score(80), "Late words", null));

        var conflict = result.Should().BeAssignableTo<ObjectResult>().Which;
        conflict.StatusCode.Should().Be(409);
        conflict.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("Review window closed");
    }

    [Fact]
    public async Task SubmitReview_WithRubricButNoRubricScores_ReturnsBadRequest()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        await SeedRubricAsync(db, assessment);
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        var controller = CreateController(db, review.ReviewerUserId);

        var result = await controller.SubmitReview(review.Id, new PeerReviewSubmitRequest(Score(80), "Nice", null));

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(400);
        (await db.Set<AssessmentPeerReview>().SingleAsync(r => r.Id == review.Id))
            .Status.Should().Be(PeerReviewStatus.Assigned, "a rejected submit must not mutate the review");
    }

    [Fact]
    public async Task SubmitReview_WithRubricCriterionOverMax_ReturnsBadRequest()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var (_, criteria) = await SeedRubricAsync(db, assessment);
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        var controller = CreateController(db, review.ReviewerUserId);
        var overMax = RubricScoresJson((criteria[0].Id, 61, null), (criteria[1].Id, 40, null));

        var result = await controller.SubmitReview(
            review.Id, new PeerReviewSubmitRequest(Score(101), "Too generous", overMax));

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SubmitReview_WithValidRubricScores_SubmitsWithPayload()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var (_, criteria) = await SeedRubricAsync(db, assessment);
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        var controller = CreateController(db, review.ReviewerUserId);
        var scores = RubricScoresJson((criteria[0].Id, 60, "clear"), (criteria[1].Id, 40, null));

        var result = await controller.SubmitReview(
            review.Id, new PeerReviewSubmitRequest(Score(100), "Great essay", scores));

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(200);
        var saved = await db.Set<AssessmentPeerReview>().SingleAsync(r => r.Id == review.Id);
        saved.Status.Should().Be(PeerReviewStatus.Submitted);
        saved.RubricScoresPayload.Should().Be(scores);
    }

    [Fact]
    public async Task SubmitReview_AlreadySubmitted_ReturnsConflict()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        review.SubmitReview(Score(70), "first take", null);
        await db.SaveChangesAsync();
        var controller = CreateController(db, review.ReviewerUserId);

        var result = await controller.SubmitReview(review.Id, new PeerReviewSubmitRequest(Score(90), "second take", null));

        var conflict = result.Should().BeAssignableTo<ObjectResult>().Which;
        conflict.StatusCode.Should().Be(409);
        conflict.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("Peer review already submitted");
    }

    // ===== (c) GET submissions/{submissionId}/received-peer-reviews — owner-only anonymized =====

    [Fact]
    public async Task GetReceivedReviews_ByOwner_SerializesWithoutReviewerFields()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var owner = Guid.NewGuid();
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, owner);
        var submitted = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        submitted.SubmitReview(Score(85), "nice work", null);
        var stillAssigned = await SeedReviewAsync(db, assessment.Id, submission.Id, Guid.NewGuid());
        await db.SaveChangesAsync();
        var controller = CreateController(db, owner);

        var result = await controller.GetReceivedPeerReviews(submission.Id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<ReceivedPeerReviewDto>>().Subject.ToList();
        list.Should().ContainSingle("assigned-but-not-submitted reviews carry no feedback yet");
        list[0].ReviewId.Should().Be(submitted.Id);
        list[0].Score.Should().Be(Score(85));
        list[0].Feedback.Should().Be("nice work");
        var keys = CollectPropertyNames(JsonSerializer.SerializeToElement(list));
        keys.Should().NotContain(k => k.ToLowerInvariant().Contains("reviewer"),
            "the reviewee must never learn who reviewed them");
    }

    [Fact]
    public async Task GetReceivedReviews_ByNonOwnerStudent_ReturnsForbidden()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var controller = CreateController(db, Guid.NewGuid());

        var result = await controller.GetReceivedPeerReviews(submission.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetReceivedReviews_GroupSiblingRowReview_VisibleToOwnerAnonymized()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var owner = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var ownRow = await SeedSubmittedRowAsync(db, assessment.Id, owner, groupId: groupId);
        var siblingRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid(), groupId: groupId);
        var review = await SeedReviewAsync(db, assessment.Id, siblingRow.Id, Guid.NewGuid());
        review.SubmitReview(Score(90), "solid team effort", null);
        await db.SaveChangesAsync();
        var controller = CreateController(db, owner);

        var result = await controller.GetReceivedPeerReviews(ownRow.Id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<ReceivedPeerReviewDto>>().Subject.ToList();
        list.Should().ContainSingle("reviews on any row of the same group-attempt are visible to every member, on their own row");
        list[0].ReviewId.Should().Be(review.Id);
        var keys = CollectPropertyNames(JsonSerializer.SerializeToElement(list));
        keys.Should().NotContain(k => k.ToLowerInvariant().Contains("reviewer"));
    }

    // ===== (d) GET submissions/{submissionId}/peer-reviews — instructor-only, named =====

    [Fact]
    public async Task GetPeerReviews_ByInstructor_IncludesReviewerIdentity()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var student = Guid.NewGuid();
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, student);
        var reviewerId = Guid.NewGuid();
        db.Add(new User { Id = reviewerId, Name = "Grace Hopper", Email = "grace@example.com" });
        var review = await SeedReviewAsync(db, assessment.Id, submission.Id, reviewerId);
        review.SubmitReview(Score(75), "good structure", null);
        await db.SaveChangesAsync();
        var instructor = Guid.NewGuid();
        var controller = CreateController(db, instructor, programCreatorId: instructor);

        var result = await controller.GetPeerReviews(submission.Id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<InstructorPeerReviewDto>>().Subject.ToList();
        list.Should().ContainSingle();
        list[0].ReviewerUserId.Should().Be(reviewerId);
        list[0].ReviewerName.Should().Be("Grace Hopper");
        list[0].Score.Should().Be(Score(75));
    }

    [Fact]
    public async Task GetPeerReviews_ByNonInstructor_ReturnsForbidden()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db, dueAt: SystemClock.UtcNow.AddDays(2));
        var submission = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var controller = CreateController(db, Guid.NewGuid());

        var result = await controller.GetPeerReviews(submission.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // ===== CONTROLLER WIRING (real peer-review + rubric services over the seeded db) =====

    private readonly Mock<IAssessmentService> _assessments = new();
    private readonly Mock<IActorContextAccessor> _actor = new();
    private readonly Mock<IProgramCrudService> _programs = new();
    private readonly Mock<IPermissionQueryService> _permissions = new();
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<PeerReviewsController>> _log = new();

    private PeerReviewsController CreateController(
        TestPeerReviewAnonymityDbContext db, Guid userId, Guid? programCreatorId = null)
    {
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = Guid.NewGuid(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        _assessments.Setup(s => s.GetAssessmentByIdAsync(It.IsAny<Guid>()))
            .Returns(async (Guid id) => await db.Set<Assessment>()
                .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null));
        _assessments.Setup(s => s.GetSubmissionByIdAsync(It.IsAny<Guid>()))
            .Returns(async (Guid id) => await db.Set<AssessmentSubmission>()
                .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null));
        _programs.Setup(p => p.GetProgramByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Program { Id = id, CreatorId = programCreatorId ?? Guid.NewGuid(), TenantId = null });
        return new PeerReviewsController(
            new PeerReviewAssignmentService(db, NullLogger<PeerReviewAssignmentService>.Instance),
            _assessments.Object,
            new RubricService(db, NullLogger<RubricService>.Instance),
            _actor.Object,
            _programs.Object,
            _permissions.Object,
            _log.Object);
    }

    /// <summary>Builds a rubric-scores JSON payload keyed by criterion id (same shape the web client sends).</summary>
    private static string RubricScoresJson(params (Guid Id, int Points, string? Comment)[] entries) =>
        "{" + string.Join(",", entries.Select(e =>
            $"\"{e.Id}\":{{\"points\":{Score(e.Points).Units}" + (e.Comment == null ? "" : $",\"comment\":\"{e.Comment}\"") + "}")) + "}";

    /// <summary>Recursively collects every JSON property name (the serialize-and-check-absent-keys proof).</summary>
    private static List<string> CollectPropertyNames(JsonElement element)
    {
        var names = new List<string>();
        CollectPropertyNames(element, names);
        return names;
    }

    private static void CollectPropertyNames(JsonElement element, List<string> sink)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    sink.Add(property.Name);
                    CollectPropertyNames(property.Value, sink);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectPropertyNames(item, sink);
                }

                break;
        }
    }

    // ===== FIXTURE =====

    private static async Task<Assessment> SeedAssessmentAsync(
        TestPeerReviewAnonymityDbContext db, DateTime? dueAt = null, DateTime? lateDeadline = null)
    {
        var assessment = Assessment.Create(
            Guid.NewGuid(), "Peer Essay", AssessmentType.Assignment, Score(100),
            reviewMethods: ReviewMethods.PeerReview);
        if (dueAt.HasValue)
        {
            assessment.SetDeliverySchedule(null, null, dueAt.Value, lateDeadline.HasValue, lateDeadline);
        }

        db.Add(assessment);
        await db.SaveChangesAsync();
        return assessment;
    }

    private static async Task<AssessmentSubmission> SeedSubmittedRowAsync(
        TestPeerReviewAnonymityDbContext db, Guid assessmentId, Guid userId, Guid? groupId = null)
    {
        var row = AssessmentSubmission.Start(assessmentId, Guid.NewGuid(), userId, 1);
        row.SetPayload(new SubmitAssessmentRequest(TextPayload: "peer work"), SubmissionModality.Text);
        row.Submit();
        if (groupId.HasValue)
        {
            row.StampCourseGroup(groupId.Value);
        }

        db.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static async Task<AssessmentPeerReview> SeedReviewAsync(
        TestPeerReviewAnonymityDbContext db, Guid assessmentId, Guid submissionId, Guid reviewerUserId)
    {
        var review = AssessmentPeerReview.Create(assessmentId, submissionId, reviewerUserId);
        db.Add(review);
        await db.SaveChangesAsync();
        return review;
    }

    private static async Task<(AssessmentRubric Rubric, List<RubricCriterion> Criteria)> SeedRubricAsync(
        TestPeerReviewAnonymityDbContext db, Assessment assessment)
    {
        var rubric = AssessmentRubric.Create("Essay rubric");
        var criteria = new List<RubricCriterion>
        {
            RubricCriterion.Create(rubric.Id, "Thesis", Score(60), 1),
            RubricCriterion.Create(rubric.Id, "Mechanics", Score(40), 2)
        };
        db.Add(rubric);
        db.AddRange(criteria);
        assessment.AssignRubric(rubric.Id);
        db.Update(assessment);
        await db.SaveChangesAsync();
        return (rubric, criteria);
    }

    private static TestPeerReviewAnonymityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestPeerReviewAnonymityDbContext>()
            .UseInMemoryDatabase($"PeerReviewAnonymity_{Guid.NewGuid()}")
            .Options;
        return new TestPeerReviewAnonymityDbContext(options);
    }

    private sealed class TestPeerReviewAnonymityDbContext(DbContextOptions<TestPeerReviewAnonymityDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            // ponytail: minimal cross-module mapping for reviewer display names; full mapping lives in ApplicationDbContext.
            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.Ignore(u => u.Profile);
                b.Ignore(u => u.Metadata);
                b.Ignore(u => u.Preferences);
                b.Ignore(u => u.Notifications);
                b.Ignore(u => u.TenantMemberships);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for peer review anonymity tests.");
        }
    }
}

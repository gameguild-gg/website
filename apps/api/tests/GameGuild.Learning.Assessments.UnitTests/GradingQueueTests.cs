using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Authoring;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// SpeedGrader navigation bundle: instructor-only queue with one item per student or
/// collective submission, latest attempt semantics, frozen member names, counts, and sort.
/// </summary>
public class GradingQueueTests
{
    [Fact]
    public async Task Individual_Assessment_OneItemPerStudent_LatestAttemptWithPersistedAssignmentGrade()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var (aliceId, _) = await SeedRowAsync(db, assessment.Id, "Alice", 1, SubmissionStatus.Graded, score: 70);
        var aliceResubmit = await SeedRowAsync(db, assessment.Id, aliceId, "Alice", 2, SubmissionStatus.Submitted);
        var (bobId, bobRow) = await SeedRowAsync(db, assessment.Id, "Bob", 1, SubmissionStatus.Submitted);
        var (daveId, daveRow) = await SeedRowAsync(db, assessment.Id, "Dave", 1, SubmissionStatus.Graded, score: 90);
        await SeedRowAsync(db, assessment.Id, "Carol", 1, SubmissionStatus.InProgress);

        var result = await CreateController(db, instructorId: Guid.NewGuid()).GetGradingQueue(assessment.Id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var queue = ok.Value.Should().BeOfType<GradingQueueDto>().Subject;
        queue.Items.Should().HaveCount(3, "one item per student; the InProgress-only student is absent");
        queue.Items.Select(i => i.DisplayName).Should().ContainInOrder("Alice", "Bob", "Dave");
        var alice = queue.Items[0];
        alice.SubmissionId.Should().Be(aliceResubmit.Id, "nav opens the student's LATEST attempt");
        alice.CanonicalSubmissionId.Should().Be(aliceResubmit.Id);
        alice.UserId.Should().Be(aliceId);
        alice.AttemptNumber.Should().Be(2);
        alice.AttemptCount.Should().Be(2);
        alice.Status.Should().Be(SubmissionStatus.Submitted);
        alice.SubmittedAt.Should().Be(aliceResubmit.SubmittedAt);
        alice.AssignmentScore.Should().Be(Score(70), "graded attempt-1 score persists across the resubmission");
        alice.AssignmentPassed.Should().BeTrue();
        alice.IsLate.Should().BeFalse();
        alice.IsGroup.Should().BeFalse();
        var bob = queue.Items[1];
        bob.SubmissionId.Should().Be(bobRow.Id);
        bob.UserId.Should().Be(bobId);
        bob.Status.Should().Be(SubmissionStatus.Submitted);
        bob.AssignmentScore.Should().BeNull();
        bob.AttemptCount.Should().Be(1);
        bob.IsGroup.Should().BeFalse();
        var dave = queue.Items[2];
        dave.SubmissionId.Should().Be(daveRow.Id);
        dave.UserId.Should().Be(daveId);
        dave.Status.Should().Be(SubmissionStatus.Graded, "latest graded with no newer submission");
        dave.AssignmentScore.Should().Be(Score(90));
        dave.AttemptCount.Should().Be(1);
        queue.Total.Should().Be(3);
        queue.NeedsGrading.Should().Be(2, "Alice (latest attempt submitted) + Bob; Dave's latest attempt is graded");
    }

    [Fact]
    public async Task CollectiveAssessment_ReturnsSingleSubmissionWithFrozenMemberNames()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var group = await SeedGroupAsync(db, assessment, "Alice", "Bob", "Carol");
        var collective = await SeedCollectiveRowAsync(
            db, assessment.Id, group, 1, SubmissionStatus.Submitted);

        var result = await CreateController(db, instructorId: Guid.NewGuid()).GetGradingQueue(assessment.Id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var queue = ok.Value.Should().BeOfType<GradingQueueDto>().Subject;
        queue.Items.Should().ContainSingle("one group attempt is persisted as one collective submission");
        var item = queue.Items[0];
        item.CanonicalSubmissionId.Should().Be(collective.Id);
        item.SubmissionId.Should().Be(collective.Id);
        item.GroupId.Should().Be(group.GroupId);
        item.GroupName.Should().Be(group.GroupName);
        item.MemberNames.Should().BeEquivalentTo(["Alice", "Bob", "Carol"]);
        item.AttemptNumber.Should().Be(1);
        item.AttemptCount.Should().Be(1);
        item.Status.Should().Be(SubmissionStatus.Submitted);
        item.AssignmentScore.Should().BeNull();
        item.IsGroup.Should().BeTrue();
        item.UserId.Should().BeNull();
        queue.Assessment.GroupSetId.Should().Be(group.SetId);
    }

    [Fact]
    public async Task Group_TwoAttempts_CollapseToOneLatestItem_WithPersistedAssignmentGrade()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var group = await SeedGroupAsync(db, assessment, "Alice", "Bob");
        await SeedCollectiveRowAsync(db, assessment.Id, group, 1, SubmissionStatus.Graded, score: 88);
        var attempt2 = await SeedCollectiveRowAsync(db, assessment.Id, group, 2, SubmissionStatus.Submitted);

        var result = await CreateController(db, instructorId: Guid.NewGuid()).GetGradingQueue(assessment.Id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var queue = ok.Value.Should().BeOfType<GradingQueueDto>().Subject;
        queue.Items.Should().ContainSingle("both group attempts collapse into one latest item");
        var item = queue.Items[0];
        item.CanonicalSubmissionId.Should().Be(attempt2.Id);
        item.SubmissionId.Should().Be(attempt2.Id);
        item.AttemptNumber.Should().Be(2);
        item.AttemptCount.Should().Be(2);
        item.Status.Should().Be(SubmissionStatus.Submitted);
        item.AssignmentScore.Should().Be(Score(88), "graded attempt-1 score persists across the resubmission");
        item.AssignmentPassed.Should().BeTrue();
        item.IsGroup.Should().BeTrue();
        queue.NeedsGrading.Should().Be(1);
    }

    [Fact]
    public async Task NonInstructor_GetsForbidden()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var student = Guid.NewGuid();
        await SeedRowAsync(db, assessment.Id, "Alice", 1, SubmissionStatus.Submitted);

        var result = await CreateController(db, instructorId: Guid.NewGuid(), actorId: student).GetGradingQueue(assessment.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Counts_TotalAndNeedsGrading_AcrossMixedStatuses()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        await SeedRowAsync(db, assessment.Id, "Alice", 1, SubmissionStatus.Graded, score: 90);
        await SeedRowAsync(db, assessment.Id, "Bob", 1, SubmissionStatus.Submitted);
        await SeedRowAsync(db, assessment.Id, "Carol", 1, SubmissionStatus.Late, isLate: true);
        await SeedRowAsync(db, assessment.Id, "Dave", 1, SubmissionStatus.InProgress);
        var (eveId, _) = await SeedRowAsync(db, assessment.Id, "Eve", 1, SubmissionStatus.Submitted);
        await SeedRowAsync(db, assessment.Id, eveId, "Eve", 2, SubmissionStatus.Graded, score: 50);

        var result = await CreateController(db, instructorId: Guid.NewGuid()).GetGradingQueue(assessment.Id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var queue = ok.Value.Should().BeOfType<GradingQueueDto>().Subject;
        queue.Total.Should().Be(4);
        queue.NeedsGrading.Should().Be(2, "Bob + Carol; Eve's stale attempt-1 submission is irrelevant — her latest attempt is graded");
        var eve = queue.Items.Single(i => i.DisplayName == "Eve");
        eve.Status.Should().Be(SubmissionStatus.Graded);
        eve.AttemptNumber.Should().Be(2);
        eve.AssignmentScore.Should().Be(Score(50));
        queue.Items.Single(i => i.DisplayName == "Carol").Status.Should().Be(SubmissionStatus.Late);
        queue.Items.Single(i => i.DisplayName == "Carol").IsLate.Should().BeTrue();
    }

    [Fact]
    public async Task Assessment_Summary_IncludesRubricPayloadAndReviewMethods()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db,
            reviewMethods: ReviewMethods.PeerReview | ReviewMethods.InstructorReview);
        ConfigurePeerReview(assessment, 3);
        await SeedRubricAsync(db, assessment);
        await db.SaveChangesAsync();

        var result = await CreateController(db, instructorId: Guid.NewGuid()).GetGradingQueue(assessment.Id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var queue = ok.Value.Should().BeOfType<GradingQueueDto>().Subject;
        var summary = queue.Assessment;
        summary.Id.Should().Be(assessment.Id);
        summary.Title.Should().Be(assessment.Title);
        summary.Type.Should().Be(AssessmentType.Assignment);
        summary.MaxScore.Should().Be(Score(100));
        summary.ReviewMethods.Should().Be(ReviewMethods.PeerReview | ReviewMethods.InstructorReview);
        summary.HasRubric.Should().BeTrue();
        summary.Rubric.Should().NotBeNull();
        summary.Rubric!.Title.Should().Be("Essay rubric");
        summary.Rubric.Criteria.Should().HaveCount(2);
    }

    // ===== CONTROLLER WIRING (real grading-queue + rubric services over the seeded db) =====

    private readonly Mock<IAssessmentService> _assessments = new();
    private readonly Mock<IActorContextAccessor> _actor = new();
    private readonly Mock<IProgramCrudService> _programs = new();
    private readonly Mock<IEnrollmentService> _enrollments = new();
    private readonly Mock<IPermissionQueryService> _permissions = new();
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<AssessmentsController>> _log = new();

    private AssessmentsController CreateController(
        TestGradingQueueDbContext db, Guid instructorId, Guid? actorId = null)
    {
        var userId = actorId ?? instructorId;
        var tenantId = Guid.NewGuid();
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        _assessments.Setup(s => s.GetAssessmentByIdAsync(It.IsAny<Guid>()))
            .Returns(async (Guid id) => await db.Set<Assessment>()
                .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null));
        _programs.Setup(p => p.GetProgramByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Program { Id = id, CreatorId = instructorId, TenantId = tenantId });
        _permissions.Setup(service => service.IsUserInTenantAsync(
                userId,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var programReads = new Mock<IProgramReadService>();
        programReads.Setup(service => service.GetProgramByIdAsync(It.IsAny<Guid>()))
            .Returns<Guid>(courseId => _programs.Object.GetProgramByIdAsync(courseId));
        var courseAccess = new CourseAccessEvaluator(
            programReads.Object,
            Mock.Of<ICourseEnrollmentAccessReader>(),
            _actor.Object,
            _permissions.Object);
        return new AssessmentsController(
            _assessments.Object,
            _actor.Object,
            _programs.Object,
            _enrollments.Object,
            courseAccess,
            new GradingQueueService(
                db,
                 new RubricService(db, NullLogger<RubricService>.Instance),
                 NullLogger<GradingQueueService>.Instance),
             Mock.Of<IAssessmentAuthoringService>(),
             _log.Object,
             new AssessmentEndpointTestSender(assessmentService: _assessments.Object))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };
    }

    // ===== FIXTURE =====

    private static async Task<Assessment> SeedAssessmentAsync(
        TestGradingQueueDbContext db,
        ReviewMethods reviewMethods = ReviewMethods.InstructorReview)
    {
        var assessment = Assessment.Create(
            Guid.NewGuid(), "Essay", AssessmentType.Assignment, Score(100),
            reviewMethods: reviewMethods);
        db.Add(assessment);
        await db.SaveChangesAsync();
        return assessment;
    }

    private static void ConfigurePeerReview(Assessment assessment, int requiredReviews)
    {
        var configuration = $$"""
            {"instructor":{"requireOverrideReason":false},"peer":{"aggregation":"mean","claimLeaseMinutes":30,"evidenceWindowMinutes":60,"minimumReviewsToFinalize":{{requiredReviews}},"onInsufficientEvidence":"await-instructor-resolution","reviewsPerReviewer":{{requiredReviews}},"reviewsRequiredPerSubmission":{{requiredReviews}}},"schemaVersion":1}
            """;
        assessment.SetReviewPolicy(
            assessment.ReviewMethods,
            configuration,
            null,
            ContentCompletionMode.OnReleaseAndPass,
            ResultReleaseMode.Manual,
            null);
    }

    private sealed record QueueGroupFixture(
        Guid SetId,
        Guid GroupId,
        string GroupName,
        (Guid UserId, string Name)[] Members);

    private static async Task<QueueGroupFixture> SeedGroupAsync(
        TestGradingQueueDbContext db, Assessment assessment, params string[] memberNames)
    {
        var set = CourseGroupSet.Create(assessment.CourseId, "Project Groups");
        var group = CourseGroup.Create(set.Id, "Team A", Math.Max(2, memberNames.Length));
        assessment.AssignToGroupSet(set.Id);
        db.AddRange(set, group);
        var members = memberNames.Select(name =>
        {
            var user = Guid.NewGuid();
            db.AddRange(
                new User { Id = user, Name = name, Email = $"{name.ToLowerInvariant()}@example.com" },
                CourseGroupMember.Create(group.Id, user));
            return (user, name);
        }).ToArray();
        await db.SaveChangesAsync();
        return new QueueGroupFixture(set.Id, group.Id, group.Name, members);
    }

    private static async Task<(Guid UserId, AssessmentSubmission Row)> SeedRowAsync(
        TestGradingQueueDbContext db,
        Guid assessmentId,
        string displayName,
        int attempt,
        SubmissionStatus status,
        int? score = null,
        bool isLate = false)
    {
        var userId = Guid.NewGuid();
        db.Add(new User { Id = userId, Name = displayName, Email = $"{displayName.ToLowerInvariant()}@example.com" });
        var row = await BuildRowAsync(db, assessmentId, userId, attempt, status, null, score, isLate);
        return (userId, row);
    }

    private static Task<AssessmentSubmission> SeedRowAsync(
        TestGradingQueueDbContext db,
        Guid assessmentId,
        Guid userId,
        string displayName,
        int attempt,
        SubmissionStatus status,
        int? score = null,
        bool isLate = false) =>
        BuildRowAsync(db, assessmentId, userId, attempt, status, null, score, isLate);

    private static async Task<AssessmentSubmission> SeedCollectiveRowAsync(
        TestGradingQueueDbContext db,
        Guid assessmentId,
        QueueGroupFixture group,
        int attempt,
        SubmissionStatus status,
        int? score = null,
        bool isLate = false)
    {
        var startedBy = group.Members[0].UserId;
        var row = AssessmentSubmission.StartCollective(
            tenantId: null,
            assessmentId,
            Guid.NewGuid(),
            group.GroupId,
            startedBy,
            attempt);
        if (status != SubmissionStatus.InProgress)
        {
            row.SetPayload(new SubmitAssessmentRequest(TextPayload: "work"), SubmissionModality.Text);
            row.Submit(isLate, DateTime.UtcNow, startedBy);
            if (status == SubmissionStatus.Graded)
            {
                row.Grade(Score(score ?? 0), Score(60), Score(100), Guid.NewGuid(), "graded");
            }
        }

        db.Add(row);
        foreach (var member in group.Members)
        {
            db.Add(Grading.Persistence.AssessmentSubmissionParticipant.Create(
                null,
                row.Id,
                Guid.NewGuid(),
                member.UserId));
        }

        await db.SaveChangesAsync();
        return row;
    }

    private static async Task<AssessmentSubmission> BuildRowAsync(
        TestGradingQueueDbContext db,
        Guid assessmentId,
        Guid userId,
        int attempt,
        SubmissionStatus status,
        Guid? groupId,
        int? score,
        bool isLate)
    {
        var row = AssessmentSubmission.Start(assessmentId, Guid.NewGuid(), userId, attempt);
        if (status != SubmissionStatus.InProgress)
        {
            row.SetPayload(new SubmitAssessmentRequest(TextPayload: "work"), SubmissionModality.Text);
            row.Submit(isLate);
            if (status == SubmissionStatus.Graded)
            {
                row.Grade(Score(score ?? 0), Score(60), Score(100), Guid.NewGuid(), "graded");
            }
        }

        if (groupId.HasValue)
        {
            row.StampCourseGroup(groupId.Value);
        }

        db.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static async Task SeedRubricAsync(TestGradingQueueDbContext db, Assessment assessment)
    {
        var rubric = AssessmentRubric.Create("Essay rubric");
        db.Add(rubric);
        db.AddRange(
            RubricCriterion.Create(rubric.Id, "Thesis", Score(60), 1),
            RubricCriterion.Create(rubric.Id, "Mechanics", Score(40), 2));
        assessment.AssignRubric(rubric.Id);
        db.Update(assessment);
    }

    private static TestGradingQueueDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestGradingQueueDbContext>()
            .UseInMemoryDatabase($"GradingQueue_{Guid.NewGuid()}")
            .Options;
        return new TestGradingQueueDbContext(options);
    }

    private sealed class TestGradingQueueDbContext(DbContextOptions<TestGradingQueueDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            new Grading.Persistence.GradingPersistenceModelConfiguration().Configure(modelBuilder);
            // ponytail: minimal cross-module mapping for display names; full mapping lives in ApplicationDbContext.
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
            throw new NotSupportedException("Transactions are not required for grading queue tests.");
        }
    }
}

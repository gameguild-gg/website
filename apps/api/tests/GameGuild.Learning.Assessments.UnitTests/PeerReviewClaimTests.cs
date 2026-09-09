using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Assessments.Grading.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public class PeerReviewClaimTests
{
    // ===== SERVICE TESTS (in-memory DbContext, fixture pattern from GroupSetServiceTests) =====

    [Fact]
    public async Task Claim_LeastReviewedTarget_WinsOverHeavierTarget()
    {
        await using var db = CreateContext();
        var assessment = await SeedPeerReviewAssessmentAsync(db);
        var actorId = Guid.NewGuid();
        await SeedOwnSubmittedRowAsync(db, assessment.Id, actorId);
        var heavyRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var lightRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        await SeedReviewAsync(db, assessment.Id, heavyRow.Id, Guid.NewGuid());
        await SeedReviewAsync(db, assessment.Id, heavyRow.Id, Guid.NewGuid());
        var service = CreateService(db);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeTrue();
        (await db.Set<AssessmentPeerReview>().SingleAsync(r => r.ReviewerUserId == actorId))
            .SubmissionId.Should().Be(lightRow.Id);
        result.Value.MaskedSubmission.Should().Be("Anonymous submission · attempt 1");
    }

    [Fact]
    public async Task Claim_WithOnlyOwnSubmissionAvailable_Fails()
    {
        await using var db = CreateContext();
        var assessment = await SeedPeerReviewAssessmentAsync(db);
        var actorId = Guid.NewGuid();
        await SeedOwnSubmittedRowAsync(db, assessment.Id, actorId);
        var service = CreateService(db);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeFalse();
        (await db.Set<AssessmentPeerReview>().AnyAsync(r => r.ReviewerUserId == actorId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Claim_NeverReturnsActorsOwnRow()
    {
        await using var db = CreateContext();
        var assessment = await SeedPeerReviewAssessmentAsync(db);
        var actorId = Guid.NewGuid();
        await SeedOwnSubmittedRowAsync(db, assessment.Id, actorId);
        var otherRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var service = CreateService(db);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeTrue();
        (await db.Set<AssessmentPeerReview>().SingleAsync(r => r.ReviewerUserId == actorId))
            .SubmissionId.Should().Be(otherRow.Id);
    }

    [Fact]
    public async Task Claim_ExcludesOwnGroupButAllowsOtherGroups()
    {
        await using var db = CreateContext();
        var (assessment, ownGroupId, targetGroupId) = await SeedGroupAssessmentAsync(db);
        var actorId = Guid.NewGuid();
        await SeedGroupMembershipAsync(db, ownGroupId, actorId);
        await SeedSubmittedRowAsync(db, assessment.Id, actorId, groupId: ownGroupId);
        var targetRows = new List<AssessmentSubmission>();
        for (var i = 0; i < 2; i++)
        {
            var memberId = Guid.NewGuid();
            await SeedGroupMembershipAsync(db, targetGroupId, memberId);
            targetRows.Add(await SeedSubmittedRowAsync(db, assessment.Id, memberId, groupId: targetGroupId));
        }
        var service = CreateService(db);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeTrue();
        var claimedSubmissionId = (await db.Set<AssessmentPeerReview>()
            .SingleAsync(r => r.ReviewerUserId == actorId)).SubmissionId;
        claimedSubmissionId.Should().Be(
            targetRows.OrderBy(r => r.Id).First().Id,
            "a group target is represented by its canonical Min(Id) row of the latest attempt");
    }

    [Fact]
    public async Task Claim_WhenQuotaReached_Fails()
    {
        await using var db = CreateContext();
        var assessment = await SeedPeerReviewAssessmentAsync(db, requiredCount: 2);
        var actorId = Guid.NewGuid();
        await SeedOwnSubmittedRowAsync(db, assessment.Id, actorId);
        var targetRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        await SeedReviewAsync(db, assessment.Id, targetRow.Id, actorId);
        await SeedReviewAsync(db, assessment.Id, Guid.NewGuid(), actorId);
        var service = CreateService(db);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Be("Review quota reached");
    }

    [Fact]
    public async Task Claim_WithoutOwnSubmittedWork_Fails()
    {
        await using var db = CreateContext();
        var assessment = await SeedPeerReviewAssessmentAsync(db);
        var actorId = Guid.NewGuid();
        var inProgress = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), actorId, 1);
        db.Add(inProgress);
        await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Be("Submit your own work before reviewing peers");
    }

    [Fact]
    public async Task Claim_WhenPeerReviewFlagOff_Fails()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Essay", AssessmentType.Assignment, ScoreValue.FromPoints("100"));
        db.Add(assessment);
        await db.SaveChangesAsync();
        var actorId = Guid.NewGuid();
        await SeedOwnSubmittedRowAsync(db, assessment.Id, actorId);
        await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var service = CreateService(db);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Be("Peer review is not enabled for this assessment");
    }

    [Fact]
    public async Task Claim_UserWhoseLatestAttemptIsInProgress_NotEligible()
    {
        await using var db = CreateContext();
        var assessment = await SeedPeerReviewAssessmentAsync(db);
        var actorId = Guid.NewGuid();
        await SeedOwnSubmittedRowAsync(db, assessment.Id, actorId);
        var staleUser = Guid.NewGuid();
        await SeedSubmittedRowAsync(db, assessment.Id, staleUser, attempt: 1);
        var inProgress = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), staleUser, 2);
        db.Add(inProgress);
        var freshRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeTrue();
        (await db.Set<AssessmentPeerReview>().SingleAsync(r => r.ReviewerUserId == actorId))
            .SubmissionId.Should().Be(freshRow.Id);
    }

    [Fact]
    public async Task Claim_TwiceBySameActor_NeverLandsOnSameSubmissionTwice()
    {
        await using var db = CreateContext();
        var assessment = await SeedPeerReviewAssessmentAsync(db, requiredCount: 2);
        var actorId = Guid.NewGuid();
        await SeedOwnSubmittedRowAsync(db, assessment.Id, actorId);
        var firstRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var secondRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var service = CreateService(db);

        var first = await service.ClaimAsync(assessment.Id, actorId);
        var second = await service.ClaimAsync(assessment.Id, actorId);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        var claimed = await db.Set<AssessmentPeerReview>()
            .Where(r => r.ReviewerUserId == actorId)
            .ToListAsync();
        claimed.Should().HaveCount(2);
        claimed.Select(r => r.SubmissionId).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Claim_GroupAttemptWithThreeMemberRows_AppearsOnceInEligibility()
    {
        // Lone target group G (3 member rows) + one individual target; two reviewers in their own
        // group H. Dedup means G counts as ONE least-reviewed target: the two consecutive claims can
        // never land on two different rows of the same group attempt (nor both on G).
        await using var db = CreateContext();
        var (assessment, ownGroupId, targetGroupId) = await SeedGroupAssessmentAsync(db);
        var targetRows = new List<AssessmentSubmission>();
        for (var i = 0; i < 3; i++)
        {
            var memberId = Guid.NewGuid();
            await SeedGroupMembershipAsync(db, targetGroupId, memberId);
            targetRows.Add(await SeedSubmittedRowAsync(db, assessment.Id, memberId, groupId: targetGroupId));
        }
        var individualRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var reviewerIds = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            var reviewerId = Guid.NewGuid();
            reviewerIds.Add(reviewerId);
            await SeedGroupMembershipAsync(db, ownGroupId, reviewerId);
            await SeedSubmittedRowAsync(db, assessment.Id, reviewerId, groupId: ownGroupId);
        }
        var canonical = targetRows.OrderBy(r => r.Id).First().Id;
        var service = CreateService(db);

        var first = await service.ClaimAsync(assessment.Id, reviewerIds[0]);
        var second = await service.ClaimAsync(assessment.Id, reviewerIds[1]);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        var firstClaimed = await db.Set<AssessmentPeerReview>().SingleAsync(r => r.ReviewerUserId == reviewerIds[0]);
        var secondClaimed = await db.Set<AssessmentPeerReview>().SingleAsync(r => r.ReviewerUserId == reviewerIds[1]);
        firstClaimed.SubmissionId.Should().NotBe(secondClaimed.SubmissionId);
        foreach (var claimed in new[] { firstClaimed, secondClaimed })
        {
            if (targetRows.Select(r => r.Id).Contains(claimed.SubmissionId))
            {
                claimed.SubmissionId.Should().Be(canonical, "a group target is only ever its canonical row");
            }
        }
    }

    [Fact]
    public async Task Claim_AfterOneSimulatedIndexRace_RetriesAndSucceeds()
    {
        await using var db = CreateContext();
        var assessment = await SeedPeerReviewAssessmentAsync(db);
        var actorId = Guid.NewGuid();
        await SeedOwnSubmittedRowAsync(db, assessment.Id, actorId);
        var targetRow = await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var service = new FlakySaveService(db, failuresBeforeSuccess: 1);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeTrue();
        (await db.Set<AssessmentPeerReview>().SingleAsync(r => r.ReviewerUserId == actorId))
            .SubmissionId.Should().Be(targetRow.Id);
    }

    [Fact]
    public async Task Claim_AfterSecondSimulatedIndexRace_FailsWithFriendlyError()
    {
        await using var db = CreateContext();
        var assessment = await SeedPeerReviewAssessmentAsync(db);
        var actorId = Guid.NewGuid();
        await SeedOwnSubmittedRowAsync(db, assessment.Id, actorId);
        await SeedSubmittedRowAsync(db, assessment.Id, Guid.NewGuid());
        var service = new FlakySaveService(db, failuresBeforeSuccess: 2);

        var result = await service.ClaimAsync(assessment.Id, actorId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Be("Could not assign a peer review, try again");
        (await db.Set<AssessmentPeerReview>().AnyAsync(r => r.ReviewerUserId == actorId))
            .Should().BeFalse();
    }

    // ===== CONTROLLER TESTS (mock pattern from GroupSetServiceTests) =====

    private readonly Mock<IPeerReviewAssignmentService> _svc = new();
    private readonly Mock<IAssessmentService> _assessments = new();
    private readonly Mock<IRubricService> _rubrics = new();
    private readonly Mock<IActorContextAccessor> _actor = new();
    private readonly Mock<IProgramCrudService> _programs = new();
    private readonly Mock<IPermissionQueryService> _permissions = new();
    private readonly Mock<ILogger<PeerReviewsController>> _log = new();

    private PeerReviewsController CreateController(Guid? userId = null, Guid? tenantId = null, Guid? programTenantId = null)
    {
        var uid = userId ?? Guid.NewGuid();
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = uid.ToString(),
            TenantId = tenantId ?? Guid.NewGuid(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        return new PeerReviewsController(
            _svc.Object,
            _assessments.Object,
            _rubrics.Object,
            _actor.Object,
            _programs.Object,
            _permissions.Object,
            _log.Object);
    }

    private void SetupAssessment(Guid assessmentId, Guid courseId, Guid? programTenantId = null)
    {
        var assessment = Assessment.Create(courseId, "Essay", AssessmentType.Assignment, ScoreValue.FromPoints("100"));
        _assessments.Setup(s => s.GetAssessmentByIdAsync(assessmentId)).ReturnsAsync(assessment);
        _programs.Setup(p => p.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid(), TenantId = programTenantId });
    }

    [Fact]
    public async Task Claim_WithoutAuthenticatedSubject_ReturnsUnauthorized()
    {
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = null,
            IsAuthenticated = false,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        var controller = new PeerReviewsController(
            _svc.Object, _assessments.Object, _rubrics.Object, _actor.Object,
            _programs.Object, _permissions.Object, _log.Object);

        var result = await controller.ClaimPeerReview(Guid.NewGuid());

        result.Result.Should().BeOfType<UnauthorizedResult>();
        _svc.Verify(s => s.ClaimAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Claim_WhenAssessmentMissing_ReturnsNotFound()
    {
        var assessmentId = Guid.NewGuid();
        _assessments.Setup(s => s.GetAssessmentByIdAsync(assessmentId)).ReturnsAsync((Assessment?)null);

        var result = await CreateController().ClaimPeerReview(assessmentId);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Claim_WhenActorOutsideProgramTenant_ReturnsForbidden()
    {
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        SetupAssessment(assessmentId, courseId, programTenantId: Guid.NewGuid());
        var actorTenant = Guid.NewGuid();

        var result = await CreateController(tenantId: actorTenant).ClaimPeerReview(assessmentId);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(s => s.ClaimAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Claim_WhenServiceRejects_ReturnsProblemDetails()
    {
        var assessmentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SetupAssessment(assessmentId, Guid.NewGuid());
        _svc.Setup(s => s.ClaimAsync(assessmentId, actorId))
            .ReturnsAsync(Result.Failure<PeerReviewClaimResult>(
                Error.Validation("PeerReview.QuotaReached", "Review quota reached")));

        var result = await CreateController(actorId).ClaimPeerReview(assessmentId);

        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("Review quota reached");
    }

    [Fact]
    public async Task Claim_OnSuccess_ReturnsMaskedDtoWithoutRevieweeIdentity()
    {
        var assessmentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SetupAssessment(assessmentId, Guid.NewGuid());
        _svc.Setup(s => s.ClaimAsync(assessmentId, actorId))
            .ReturnsAsync(Result.Success(new PeerReviewClaimResult(Guid.NewGuid(), "Anonymous submission · attempt 2")));

        var result = await CreateController(actorId).ClaimPeerReview(assessmentId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<PeerReviewClaimDto>().Subject;
        dto.MaskedSubmission.Should().Be("Anonymous submission · attempt 2");
        dto.ReviewId.Should().NotBeEmpty();
        dto.GetType().GetProperties().Select(p => p.Name)
            .Should().NotContain(new[] { "SubmissionId", "UserId", "CourseGroupId", "ReviewerUserId" });
    }

    // ===== FIXTURE =====

    private static PeerReviewAssignmentService CreateService(TestPeerReviewDbContext db) =>
        new(db, NullLogger<PeerReviewAssignmentService>.Instance);

    /// <summary>
    /// Race seam: EF InMemory never throws unique-index violations, so this subclass simulates
    /// N consecutive (ReviewerUserId, SubmissionId) index races before letting the real save through.
    /// </summary>
    private sealed class FlakySaveService(TestPeerReviewDbContext db, int failuresBeforeSuccess)
        : PeerReviewAssignmentService(db, NullLogger<PeerReviewAssignmentService>.Instance)
    {
        private int _calls;

        internal override Task SaveClaimAsync(AssessmentPeerReview review)
        {
            return _calls++ < failuresBeforeSuccess
                ? throw new DbUpdateException("simulated unique index race")
                : base.SaveClaimAsync(review);
        }
    }

    private static async Task<Assessment> SeedPeerReviewAssessmentAsync(
        TestPeerReviewDbContext db, int requiredCount = 3)
    {
        var assessment = Assessment.Create(
            Guid.NewGuid(), "Peer Essay", AssessmentType.Assignment, ScoreValue.FromPoints("100"),
            reviewMethods: ReviewMethods.PeerReview);
        ConfigurePeerReview(assessment, requiredCount);
        db.Add(assessment);
        await db.SaveChangesAsync();
        return assessment;
    }

    private static void ConfigurePeerReview(Assessment assessment, int requiredCount)
    {
        var configuration =
            $$"""
            {"peer":{"aggregation":"mean","claimLeaseMinutes":30,"evidenceWindowMinutes":60,"minimumReviewsToFinalize":{{requiredCount}},"onInsufficientEvidence":"await-instructor-resolution","reviewsPerReviewer":{{requiredCount}},"reviewsRequiredPerSubmission":{{requiredCount}}},"schemaVersion":1}
            """;
        assessment.SetReviewPolicy(
            assessment.ReviewMethods,
            configuration,
            null,
            ContentCompletionMode.OnReleaseAndPass,
            ResultReleaseMode.Manual,
            null);
    }

    private static Task<AssessmentSubmission> SeedOwnSubmittedRowAsync(
        TestPeerReviewDbContext db, Guid assessmentId, Guid userId) =>
        SeedSubmittedRowAsync(db, assessmentId, userId);

    private static async Task<AssessmentSubmission> SeedSubmittedRowAsync(
        TestPeerReviewDbContext db, Guid assessmentId, Guid userId, int attempt = 1, Guid? groupId = null)
    {
        var row = AssessmentSubmission.Start(assessmentId, Guid.NewGuid(), userId, attempt);
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

    private static async Task SeedReviewAsync(
        TestPeerReviewDbContext db, Guid assessmentId, Guid submissionId, Guid reviewerUserId)
    {
        db.Add(AssessmentPeerReview.Create(assessmentId, submissionId, reviewerUserId));
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid GroupSetId, Guid GroupId)> SeedGroupAsync(
        TestPeerReviewDbContext db, string name)
    {
        var set = CourseGroupSet.Create(Guid.NewGuid(), "Sets");
        var group = CourseGroup.Create(set.Id, name, 5);
        db.AddRange(set, group);
        await db.SaveChangesAsync();
        return (set.Id, group.Id);
    }

    private static async Task<(Assessment Assessment, Guid OwnGroupId, Guid TargetGroupId)> SeedGroupAssessmentAsync(
        TestPeerReviewDbContext db)
    {
        var (_, ownGroupId) = await SeedGroupAsync(db, "Own Team");
        var (_, targetGroupId) = await SeedGroupAsync(db, "Target Team");
        var assessment = await SeedPeerReviewAssessmentAsync(db);
        return (assessment, ownGroupId, targetGroupId);
    }

    private static async Task SeedGroupMembershipAsync(
        TestPeerReviewDbContext db, Guid groupId, Guid userId)
    {
        db.Add(CourseGroupMember.Create(groupId, userId));
        await db.SaveChangesAsync();
    }

    private static TestPeerReviewDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestPeerReviewDbContext>()
            .UseInMemoryDatabase($"PeerReviews_{Guid.NewGuid()}")
            .Options;
        return new TestPeerReviewDbContext(options);
    }

    private sealed class TestPeerReviewDbContext(DbContextOptions<TestPeerReviewDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for peer review tests.");
        }
    }
}

using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Assessments.Grading.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public class PeerReviewClaimTests
{
    // ===== CONTROLLER TESTS (mock pattern from GroupSetServiceTests) =====

    private readonly Mock<IPeerReviewAssignmentService> _svc = new();
    private readonly Mock<IAssessmentService> _assessments = new();
    private readonly Mock<IRubricService> _rubrics = new();
    private readonly Mock<IActorContextAccessor> _actor = new();
    private readonly Mock<IProgramCrudService> _programs = new();
    private readonly Mock<IPermissionQueryService> _permissions = new();
    private readonly Mock<ICourseEnrollmentAccessReader> _courseEnrollments = new();
    private readonly Mock<ILogger<PeerReviewsController>> _log = new();

    private PeerReviewsController CreateController(Guid? userId = null, Guid? tenantId = null, Guid? programTenantId = null)
    {
        var uid = userId ?? Guid.NewGuid();
        var effectiveTenantId = tenantId ?? Guid.NewGuid();
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = uid.ToString(),
            TenantId = effectiveTenantId,
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        _permissions.Setup(service => service.IsUserInTenantAsync(
                uid,
                effectiveTenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _courseEnrollments.Setup(service => service.HasActiveEnrollmentAsync(
                It.IsAny<Guid>(),
                uid,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var programReads = new Mock<IProgramReadService>();
        programReads.Setup(service => service.GetProgramByIdAsync(It.IsAny<Guid>()))
            .Returns<Guid>(courseId => _programs.Object.GetProgramByIdAsync(courseId));
        return new PeerReviewsController(
            _svc.Object,
            _assessments.Object,
            _rubrics.Object,
            _actor.Object,
            new CourseAccessEvaluator(
                programReads.Object,
                _courseEnrollments.Object,
                _actor.Object,
                _permissions.Object),
            _log.Object,
            new AssessmentEndpointTestSender(peerReviewService: _svc.Object))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };
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
            Mock.Of<ICourseAccessEvaluator>(), _log.Object,
            new AssessmentEndpointTestSender(peerReviewService: _svc.Object));

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

}

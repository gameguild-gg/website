using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class AuthorTestPeerReviewEndpointTests
{
    [Fact]
    public async Task Claim_UsesTheOwnedSyntheticSubject()
    {
        var fixture = new Fixture();
        var reviewId = Guid.NewGuid();
        fixture.PeerReviews
            .Setup(service => service.ClaimAuthorTestAsync(
                fixture.TestRunId,
                fixture.SubjectId,
                fixture.ActorId))
            .ReturnsAsync(Result.Success(new PeerReviewClaimResult(reviewId, "Anonymous test subject")));

        var response = await fixture.Controller.ClaimAssessmentTestPeerReview(
            fixture.TestRunId,
            new ClaimAssessmentTestPeerReviewRequest(fixture.SubjectId),
            default);

        response.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeEquivalentTo(new PeerReviewClaimResult(reviewId, "Anonymous test subject"));
        fixture.PeerReviews.VerifyAll();
    }

    [Fact]
    public async Task Get_RejectsAClaimFromAnotherTestRun()
    {
        var fixture = new Fixture();
        var foreignSubjectId = Guid.NewGuid();
        var claim = fixture.Claim(foreignSubjectId);
        fixture.PeerReviews.Setup(service => service.GetReviewAsync(claim.Id)).ReturnsAsync(claim);

        var response = await fixture.Controller.GetAssessmentTestPeerReview(
            fixture.TestRunId,
            claim.Id,
            default);

        response.Result.Should().BeOfType<BadRequestObjectResult>();
        fixture.PeerReviews.Verify(
            service => service.SubmitReviewAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<ScoreValue>(),
                It.IsAny<string>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task Submit_ReturnsTheUpdatedRunAfterCanonicalEvidenceIsAccepted()
    {
        var fixture = new Fixture();
        var claim = fixture.Claim(fixture.SubjectId);
        var submitted = claim with
        {
            Status = PeerReviewClaimStatus.Submitted,
            SubmittedAt = DateTime.UtcNow,
        };
        fixture.PeerReviews.Setup(service => service.GetReviewAsync(claim.Id)).ReturnsAsync(claim);
        fixture.PeerReviews
            .Setup(service => service.SubmitReviewAsync(
                claim.Id,
                fixture.ActorId,
                ScoreValue.FromUnits(75),
                "Useful feedback",
                null))
            .ReturnsAsync(Result.Success(submitted));

        var response = await fixture.Controller.SubmitAssessmentTestPeerReview(
            fixture.TestRunId,
            claim.Id,
            new SubmitAssessmentTestPeerReviewRequest(
                ScoreValue.FromUnits(75),
                "Useful feedback"),
            default);

        response.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeSameAs(fixture.Run);
        fixture.Runtime.Verify(
            service => service.GetTestRunAsync(fixture.TestRunId, fixture.ActorId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        fixture.PeerReviews.VerifyAll();
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Run = new AssessmentTestRunViewV1(
                TestRunId,
                AssessmentId,
                Guid.NewGuid(),
                AssessmentTestRunStatus.Running,
                "peer-1",
                "Synthetic learner 1",
                null!,
                true,
                false,
                [],
                [new AssessmentTestRunSubjectViewV1(
                    SubjectId,
                    "peer-1",
                    "Synthetic learner 1",
                    null!)]);
            Runtime
                .Setup(service => service.GetTestRunAsync(TestRunId, ActorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Run);
            Assessments.Setup(service => service.GetAssessmentByIdAsync(AssessmentId))
                .ReturnsAsync(Assessment.Create(CourseId, "Quiz", AssessmentType.Quiz, ScoreValue.FromUnits(100)));
            Access.Setup(service => service.HasCapabilityAsync(
                    CourseId,
                    CourseCapability.Edit,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            Actor.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = ActorId.ToString(),
                IsAuthenticated = true,
                Roles = new HashSet<string>(),
                Permissions = new HashSet<string>(),
            });
            Controller = new AssessmentsController(
                Assessments.Object,
                Actor.Object,
                Mock.Of<IProgramCrudService>(),
                Mock.Of<IEnrollmentService>(),
                Access.Object,
                Mock.Of<IGradingQueueService>(),
                Mock.Of<IAssessmentAuthoringService>(),
                Mock.Of<ILogger<AssessmentsController>>(),
                new AssessmentEndpointTestSender(peerReviewService: PeerReviews.Object),
                Runtime.Object,
                peerReviewService: PeerReviews.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };
        }

        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid TestRunId { get; } = Guid.NewGuid();
        public Guid SubjectId { get; } = Guid.NewGuid();
        public Guid AssessmentId { get; } = Guid.NewGuid();
        public Guid CourseId { get; } = Guid.NewGuid();
        public AssessmentTestRunViewV1 Run { get; }
        public Mock<IAssessmentGradingRuntimeService> Runtime { get; } = new(MockBehavior.Strict);
        public Mock<IPeerReviewAssignmentService> PeerReviews { get; } = new(MockBehavior.Strict);
        public Mock<IAssessmentService> Assessments { get; } = new(MockBehavior.Strict);
        public Mock<ICourseAccessEvaluator> Access { get; } = new(MockBehavior.Strict);
        public Mock<IActorContextAccessor> Actor { get; } = new(MockBehavior.Strict);
        public AssessmentsController Controller { get; }

        public PeerReviewClaimView Claim(Guid reviewerSubjectId) => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AssessmentId,
            null,
            null,
            reviewerSubjectId,
            PeerReviewClaimStatus.Assigned,
            1,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30),
            null,
            1,
            "{}",
            "{}");
    }
}

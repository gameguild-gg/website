using System.Text.Json;
using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// Verifies the HTTP anonymity boundary independently from peer-review assignment.
/// Canonical claims carry reviewer identity for audit, while learner DTOs omit it.
/// </summary>
public sealed class PeerReviewAnonymityTests
{
    private static readonly string[] ForbiddenSubmissionKeys =
        ["userId", "enrollmentId", "score", "passed", "feedback", "gradedBy", "gradedAt", "courseGroupId"];

    [Fact]
    public async Task GetReview_AsAssignedReviewer_ReturnsAnonymousSubmission()
    {
        var fixture = CreateFixture();
        var controller = fixture.CreateController(fixture.ReviewerId, canLearn: true);

        var result = await controller.GetReview(fixture.Review.Id);

        var dto = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AnonymousReviewSubmissionDto>().Subject;
        dto.TextPayload.Should().Be("peer work");
        dto.ReviewId.Should().Be(fixture.Review.Id);
        dto.DeliveryCanonicalJson.Should().Be(fixture.Review.DeliveryCanonicalJson);
        dto.ResponseEnvelopeCanonicalJson.Should().Be(fixture.Review.ResponseEnvelopeCanonicalJson);
        CollectPropertyNames(JsonSerializer.SerializeToElement(dto))
            .Select(value => value.ToLowerInvariant())
            .Should().NotContain(ForbiddenSubmissionKeys);
    }

    [Fact]
    public async Task GetReview_AsAnotherLearner_ReturnsForbidden()
    {
        var fixture = CreateFixture();

        var result = await fixture.CreateController(Guid.NewGuid(), canLearn: true)
            .GetReview(fixture.Review.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SubmitReview_WithValidEvidence_ReturnsCanonicalClaimState()
    {
        var fixture = CreateFixture();
        var submitted = fixture.Review with
        {
            Status = PeerReviewClaimStatus.Submitted,
            SubmittedAt = SystemClock.UtcNow,
        };
        fixture.PeerReviews
            .Setup(service => service.SubmitReviewAsync(
                fixture.Review.Id,
                fixture.ReviewerId,
                Score(80),
                "Strong thesis",
                null))
            .ReturnsAsync(Result.Success(submitted));
        var controller = fixture.CreateController(fixture.ReviewerId, canLearn: true);

        var result = await controller.SubmitReview(
            fixture.Review.Id,
            new PeerReviewSubmitRequest(Score(80), "Strong thesis", null));

        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PeerReviewSubmitDto>().Subject;
        dto.ReviewId.Should().Be(fixture.Review.Id);
        dto.Status.Should().Be(PeerReviewClaimStatus.Submitted);
    }

    [Fact]
    public async Task SubmitReview_WithoutFeedback_IsRejectedBeforeDispatch()
    {
        var fixture = CreateFixture();
        var controller = fixture.CreateController(fixture.ReviewerId, canLearn: true);

        var result = await controller.SubmitReview(
            fixture.Review.Id,
            new PeerReviewSubmitRequest(Score(80), "  ", null));

        result.Should().BeOfType<BadRequestObjectResult>().Subject.StatusCode.Should().Be(400);
        fixture.PeerReviews.Verify(service => service.SubmitReviewAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<ScoreValue>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SubmitReview_WhenCanonicalStageRejectsExpiredWindow_ReturnsConflict()
    {
        var fixture = CreateFixture();
        fixture.PeerReviews
            .Setup(service => service.SubmitReviewAsync(
                fixture.Review.Id,
                fixture.ReviewerId,
                Score(80),
                "Late",
                null))
            .ReturnsAsync(Result.Failure<PeerReviewClaimView>(Error.Conflict(
                "PeerReview.WindowClosed",
                "The canonical peer-review evidence window has closed.")));

        var result = await fixture.CreateController(fixture.ReviewerId, canLearn: true)
            .SubmitReview(fixture.Review.Id, new PeerReviewSubmitRequest(Score(80), "Late", null));

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task GetReceivedReviews_OmitsReviewerIdentity()
    {
        var fixture = CreateFixture();
        fixture.PeerReviews
            .Setup(service => service.IsSubmissionOwnerOrParticipantAsync(fixture.Submission.Id, fixture.OwnerId))
            .ReturnsAsync(true);
        fixture.PeerReviews
            .Setup(service => service.GetReviewsForSubmissionAsync(fixture.Submission.Id))
            .ReturnsAsync([fixture.Evidence]);

        var result = await fixture.CreateController(fixture.OwnerId, canLearn: true)
            .GetReceivedPeerReviews(fixture.Submission.Id);

        var reviews = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IEnumerable<ReceivedPeerReviewDto>>().Subject.ToArray();
        reviews.Should().ContainSingle();
        reviews[0].Feedback.Should().Be("Useful feedback");
        CollectPropertyNames(JsonSerializer.SerializeToElement(reviews))
            .Should().NotContain(value => value.Contains("reviewer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetReceivedReviews_AllowsFrozenCollectiveParticipant()
    {
        var fixture = CreateFixture();
        var participantId = Guid.NewGuid();
        fixture.PeerReviews
            .Setup(service => service.IsSubmissionOwnerOrParticipantAsync(fixture.Submission.Id, participantId))
            .ReturnsAsync(true);
        fixture.PeerReviews
            .Setup(service => service.GetReviewsForSubmissionAsync(fixture.Submission.Id))
            .ReturnsAsync([fixture.Evidence]);

        var result = await fixture.CreateController(participantId, canLearn: true)
            .GetReceivedPeerReviews(fixture.Submission.Id);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPeerReviews_AsStaffReviewer_IncludesAuditIdentity()
    {
        var fixture = CreateFixture();
        fixture.PeerReviews
            .Setup(service => service.GetReviewsForSubmissionAsync(fixture.Submission.Id))
            .ReturnsAsync([fixture.Evidence]);
        fixture.PeerReviews
            .Setup(service => service.GetReviewerDisplayNamesAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(fixture.ReviewerId))))
            .ReturnsAsync(new Dictionary<Guid, string> { [fixture.ReviewerId] = "Grace Hopper" });

        var result = await fixture.CreateController(Guid.NewGuid(), canStaffReview: true)
            .GetPeerReviews(fixture.Submission.Id);

        var reviews = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IEnumerable<InstructorPeerReviewDto>>().Subject.ToArray();
        reviews.Should().ContainSingle();
        reviews[0].ReviewerUserId.Should().Be(fixture.ReviewerId);
        reviews[0].ReviewerName.Should().Be("Grace Hopper");
    }

    [Fact]
    public async Task GetPeerReviews_WithoutStaffReviewCapability_ReturnsForbidden()
    {
        var fixture = CreateFixture();

        var result = await fixture.CreateController(Guid.NewGuid(), canLearn: true)
            .GetPeerReviews(fixture.Submission.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    private static Fixture CreateFixture()
    {
        var courseId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Essay", AssessmentType.Assignment, Score(100));
        assessment.SetDeliverySchedule(null, null, SystemClock.UtcNow.AddDays(1), false, null);
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), ownerId, 1);
        submission.SetPayload(new SubmitAssessmentRequest(TextPayload: "peer work"), SubmissionModality.Text);
        submission.Submit();
        var review = new PeerReviewClaimView(
            Guid.NewGuid(),
            Guid.NewGuid(),
            assessment.Id,
            submission.Id,
            reviewerId,
            null,
            PeerReviewClaimStatus.Assigned,
            0,
            SystemClock.UtcNow,
            SystemClock.UtcNow.AddMinutes(30),
            null,
            1,
            "{\"schemaVersion\":1,\"definitionRevisionId\":\"revision\",\"executionSnapshotHash\":\"hash\",\"itemOrder\":[],\"items\":{}}",
            "{\"schemaVersion\":1,\"contentType\":\"quiz\",\"payloadSchema\":\"quiz-answer/v1\",\"payload\":{\"answers\":{}}}");
        var evidence = new PeerReviewEvidenceView(
            review.Id,
            Guid.NewGuid(),
            reviewerId,
            Score(80),
            "Useful feedback",
            null,
            SystemClock.UtcNow);
        return new Fixture(assessment, submission, review, evidence, ownerId, reviewerId);
    }

    private static HashSet<string> CollectPropertyNames(JsonElement element)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Visit(element, names);
        return names;
    }

    private static void Visit(JsonElement element, ISet<string> names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                names.Add(property.Name);
                Visit(property.Value, names);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Visit(item, names);
            }
        }
    }

    private static ScoreValue Score(int points) => ScoreValue.FromPoints(points.ToString());

    private sealed class Fixture(
        Assessment assessment,
        AssessmentSubmission submission,
        PeerReviewClaimView review,
        PeerReviewEvidenceView evidence,
        Guid ownerId,
        Guid reviewerId)
    {
        public Assessment Assessment { get; } = assessment;
        public AssessmentSubmission Submission { get; } = submission;
        public PeerReviewClaimView Review { get; } = review;
        public PeerReviewEvidenceView Evidence { get; } = evidence;
        public Guid OwnerId { get; } = ownerId;
        public Guid ReviewerId { get; } = reviewerId;
        public Mock<IPeerReviewAssignmentService> PeerReviews { get; } = new(MockBehavior.Strict);

        public PeerReviewsController CreateController(
            Guid actorId,
            bool canLearn = false,
            bool canStaffReview = false)
        {
            var assessments = new Mock<IAssessmentService>(MockBehavior.Strict);
            assessments.Setup(service => service.GetAssessmentByIdAsync(Assessment.Id)).ReturnsAsync(Assessment);
            assessments.Setup(service => service.GetSubmissionByIdAsync(Submission.Id)).ReturnsAsync(Submission);

            PeerReviews.Setup(service => service.GetReviewAsync(Review.Id)).ReturnsAsync(Review);
            var rubrics = new Mock<IRubricService>(MockBehavior.Strict);
            rubrics.Setup(service => service.GetAsync(Assessment.Id))
                .ReturnsAsync(Result.Failure<RubricDto>(Error.NotFound("Rubric", "Rubric not found")));
            rubrics.Setup(service => service.ValidateScoresAsync(
                    Assessment.Id,
                    It.IsAny<ScoreValue>(),
                    It.IsAny<string?>()))
                .ReturnsAsync(Result.Success());
            var actor = new Mock<IActorContextAccessor>(MockBehavior.Strict);
            actor.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = actorId.ToString(),
                TenantId = Guid.NewGuid(),
                IsAuthenticated = true,
                Roles = new HashSet<string>(),
                Permissions = new HashSet<string>(),
            });
            var access = new Mock<ICourseAccessEvaluator>(MockBehavior.Strict);
            access.Setup(evaluator => evaluator.HasCapabilityAsync(
                    Assessment.CourseId,
                    CourseCapability.Learn,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(canLearn);
            access.Setup(evaluator => evaluator.HasCapabilityAsync(
                    Assessment.CourseId,
                    CourseCapability.StaffReview,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(canStaffReview);

            return new PeerReviewsController(
                PeerReviews.Object,
                assessments.Object,
                rubrics.Object,
                actor.Object,
                access.Object,
                Mock.Of<ILogger<PeerReviewsController>>(),
                new AssessmentEndpointTestSender(peerReviewService: PeerReviews.Object))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                },
            };
        }
    }
}

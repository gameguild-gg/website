using FluentAssertions;
using GameGuild.Learning.Assessments;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class EndpointCommandHandlerTests
{
    [Fact]
    public async Task AssessmentHandler_ForwardsEveryEndpointCommand()
    {
        var service = new Mock<IAssessmentService>(MockBehavior.Strict);
        var courseId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var testRunId = Guid.NewGuid();
        var testSubjectId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var cueId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Quiz", AssessmentType.Quiz, Score(100));
        var group = AssessmentGroup.Create(courseId, "Quizzes", Percent(50));
        var submission = AssessmentSubmission.Start(assessmentId, enrollmentId, userId, 1);
        var cue = InteractiveVideoAssessmentCue.Create(assessmentId, Guid.NewGuid(), "intro", 1.5m);
        var create = new CreateAssessmentRequest(courseId, "Quiz", null, AssessmentType.Quiz, Score(100));
        var createGroup = new CreateAssessmentGroupRequest(courseId, "Quizzes", Percent(50));
        var updateGroup = new UpdateAssessmentGroupRequest(Name: "Updated");
        var update = new UpdateAssessmentRequest(assessment.Version, Title: "Updated");
        var assign = new AssignAssessmentGroupRequest(groupId);
        var link = new LinkInteractiveVideoCueRequest(cue.ContentId, cue.CueId, cue.CuePositionSeconds);
        var submit = new SubmitAssessmentRequest(TextPayload: "answer");

        service.Setup(s => s.CreateAssessmentAsync(create)).ReturnsAsync(Result.Success(assessment));
        service.Setup(s => s.CreateAssessmentGroupAsync(createGroup)).ReturnsAsync(Result.Success(group));
        service.Setup(s => s.UpdateAssessmentGroupAsync(groupId, updateGroup)).ReturnsAsync(Result.Success(group));
        service.Setup(s => s.DeleteAssessmentGroupAsync(groupId)).ReturnsAsync(Result.Success());
        service.Setup(s => s.UpdateAssessmentAsync(assessmentId, update)).ReturnsAsync(Result.Success(assessment));
        service.Setup(s => s.AssignAssessmentToGroupAsync(assessmentId, assign)).ReturnsAsync(Result.Success(assessment));
        service.Setup(s => s.LinkInteractiveVideoCueAsync(assessmentId, link)).ReturnsAsync(Result.Success(cue));
        service.Setup(s => s.UnlinkInteractiveVideoCueAsync(assessmentId, cueId)).ReturnsAsync(Result.Success());
        service.Setup(s => s.DeleteAssessmentAsync(assessmentId)).ReturnsAsync(Result.Success());
        service.Setup(s => s.RestoreAssessmentAsync(assessmentId, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        service.Setup(s => s.StartSubmissionAsync(assessmentId, enrollmentId, userId)).ReturnsAsync(Result.Success(submission));
        service.Setup(s => s.SubmitAsync(submissionId, submit)).ReturnsAsync(Result.Success(submission));

        var handler = new AssessmentEndpointCommandHandler(service.Object);

        (await handler.Handle(new CreateAssessmentEndpointCommand(create), default)).Value.Should().BeSameAs(assessment);
        (await handler.Handle(new CreateAssessmentGroupEndpointCommand(createGroup), default)).Value.Should().BeSameAs(group);
        (await handler.Handle(new UpdateAssessmentGroupEndpointCommand(groupId, updateGroup), default)).Value.Should().BeSameAs(group);
        (await handler.Handle(new DeleteAssessmentGroupEndpointCommand(groupId), default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new UpdateAssessmentEndpointCommand(assessmentId, update), default)).Value.Should().BeSameAs(assessment);
        (await handler.Handle(new AssignAssessmentToGroupEndpointCommand(assessmentId, assign), default)).Value.Should().BeSameAs(assessment);
        (await handler.Handle(new LinkInteractiveVideoCueEndpointCommand(assessmentId, link), default)).Value.Should().BeSameAs(cue);
        (await handler.Handle(new UnlinkInteractiveVideoCueEndpointCommand(assessmentId, cueId), default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new DeleteAssessmentEndpointCommand(assessmentId), default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new RestoreAssessmentEndpointCommand(assessmentId), default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new StartAssessmentSubmissionEndpointCommand(assessmentId, enrollmentId, userId), default)).Value.Should().BeSameAs(submission);
        (await handler.Handle(new SubmitAssessmentEndpointCommand(submissionId, submit), default)).Value.Should().BeSameAs(submission);

        service.VerifyAll();
    }

    [Fact]
    public async Task CourseGroupHandler_ForwardsEveryEndpointCommand()
    {
        var service = new Mock<IGroupSetService>(MockBehavior.Strict);
        var courseId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var set = CourseGroupSet.Create(courseId, "Teams");
        var group = CourseGroup.Create(setId, "Team A", 4);
        var member = CourseGroupMember.Create(groupId, userId);

        service.Setup(s => s.CreateGroupSetAsync(courseId, "Teams")).ReturnsAsync(Result.Success(set));
        service.Setup(s => s.CreateGroupAsync(courseId, setId, "Team A", 4)).ReturnsAsync(Result.Success(group));
        service.Setup(s => s.JoinAsync(courseId, groupId, userId)).ReturnsAsync(Result.Success(member));
        service.Setup(s => s.LeaveAsync(courseId, groupId, userId)).ReturnsAsync(Result.Success());
        service.Setup(s => s.AddMemberAsync(courseId, groupId, userId)).ReturnsAsync(Result.Success(member));
        service.Setup(s => s.RemoveMemberAsync(courseId, groupId, userId)).ReturnsAsync(Result.Success());

        var handler = new CourseGroupEndpointCommandHandler(service.Object);

        (await handler.Handle(new CreateCourseGroupSetEndpointCommand(courseId, "Teams"), default)).Value.Should().BeSameAs(set);
        (await handler.Handle(new CreateCourseGroupEndpointCommand(courseId, setId, "Team A", 4), default)).Value.Should().BeSameAs(group);
        (await handler.Handle(new JoinCourseGroupEndpointCommand(courseId, groupId, userId), default)).Value.Should().BeSameAs(member);
        (await handler.Handle(new LeaveCourseGroupEndpointCommand(courseId, groupId, userId), default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new AddCourseGroupMemberEndpointCommand(courseId, groupId, userId), default)).Value.Should().BeSameAs(member);
        (await handler.Handle(new RemoveCourseGroupMemberEndpointCommand(courseId, groupId, userId), default)).IsSuccess.Should().BeTrue();

        service.VerifyAll();
    }

    [Fact]
    public async Task PeerReviewHandler_ForwardsEveryEndpointCommand()
    {
        var service = new Mock<IPeerReviewAssignmentService>(MockBehavior.Strict);
        var assessmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var testRunId = Guid.NewGuid();
        var testSubjectId = Guid.NewGuid();
        var claim = new PeerReviewClaimResult(reviewId, "anonymous submission");
        var review = new PeerReviewClaimView(
            reviewId,
            Guid.NewGuid(),
            assessmentId,
            Guid.NewGuid(),
            userId,
            null,
            Grading.Persistence.PeerReviewClaimStatus.Assigned,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30),
            null,
            1,
            "{}",
            "{}");

        service.Setup(s => s.ClaimAsync(assessmentId, userId)).ReturnsAsync(Result.Success(claim));
        service.Setup(s => s.ClaimAuthorTestAsync(testRunId, testSubjectId, userId))
            .ReturnsAsync(Result.Success(claim));
        service.Setup(s => s.SubmitReviewAsync(reviewId, userId, Score(80), "Good", "{}"))
            .ReturnsAsync(Result.Success(review));

        var handler = new PeerReviewEndpointCommandHandler(service.Object);

        (await handler.Handle(new ClaimPeerReviewEndpointCommand(assessmentId, userId), default)).Value.Should().Be(claim);
        (await handler.Handle(
            new ClaimAuthorTestPeerReviewEndpointCommand(testRunId, testSubjectId, userId),
            default)).Value.Should().Be(claim);
        (await handler.Handle(new SubmitPeerReviewEndpointCommand(reviewId, userId, Score(80), "Good", "{}"), default)).Value.Should().BeSameAs(review);

        service.VerifyAll();
    }

    [Fact]
    public async Task RubricHandler_ForwardsEveryEndpointCommand()
    {
        var service = new Mock<IRubricService>(MockBehavior.Strict);
        var assessmentId = Guid.NewGuid();
        var request = new SaveRubricRequest("Rubric", []);
        var rubric = new RubricDto(Guid.NewGuid(), "Rubric", []);

        service.Setup(s => s.SaveAsync(assessmentId, request)).ReturnsAsync(Result.Success(rubric));
        service.Setup(s => s.DeleteAsync(assessmentId)).ReturnsAsync(Result.Success());

        var handler = new AssessmentRubricEndpointCommandHandler(service.Object);

        (await handler.Handle(new PutAssessmentRubricEndpointCommand(assessmentId, request), default)).Value.Should().Be(rubric);
        (await handler.Handle(new DeleteAssessmentRubricEndpointCommand(assessmentId), default)).IsSuccess.Should().BeTrue();

        service.VerifyAll();
    }
}

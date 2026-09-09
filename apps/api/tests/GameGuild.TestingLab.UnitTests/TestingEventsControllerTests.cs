using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.TestingLab.UnitTests;

public sealed class TestingEventsControllerTests
{
    [Fact]
    public async Task CreateEvent_WithoutTemplateOrConfiguration_Should_Create_Configurable_Draft()
    {
        var mediator = new Mock<IMediator>();
        var startsAt = SystemClock.UtcNow.AddDays(2);
        var request = new CreateTestingEventRequest(
            "Incomplete event",
            null,
            TestingEventMode.Online,
            TestingEventApprovalMode.ManagerOnly,
            startsAt.AddDays(-3),
            startsAt.AddDays(-1),
            startsAt,
            startsAt.AddHours(2),
            true);
        var controller = new TestingEventsController(mediator.Object);
        var projection = new TestingEventProjection(
            Guid.NewGuid(), request.Name, request.Description, request.Mode,
            request.ApprovalMode, TestingEventStatus.Draft, Guid.NewGuid(),
            request.ApplicationsOpenAt, request.ApplicationsCloseAt,
            request.StartsAt, request.EndsAt, request.RequiresFeedback,
            TestingLearningCompletionRequirement.None, null, null, null,
            Guid.NewGuid(), 0, 0);
        mediator.Setup(candidate => candidate.Send(
                It.IsAny<CreateTestingEventCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(projection));

        var result = await controller.CreateEvent(request);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        mediator.Verify(candidate => candidate.Send(
            It.Is<CreateTestingEventCommand>(command =>
                command.TemplateRevisionId == null && command.Configuration == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateEvent_Should_Delegate_Through_Cqrs_And_Return_Created()
    {
        var mediator = new Mock<IMediator>();
        using var cancellation = new CancellationTokenSource();
        var startsAt = SystemClock.UtcNow.AddDays(2);
        var request = new CreateTestingEventRequest(
            "Campus showcase",
            "Student project testing",
            TestingEventMode.InPerson,
            TestingEventApprovalMode.ManagerOnly,
            startsAt.AddDays(-3),
            startsAt.AddDays(-1),
            startsAt,
            startsAt.AddHours(4),
            true,
            Configuration: CompleteConfiguration());
        var projection = new TestingEventProjection(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            request.Mode,
            request.ApprovalMode,
            TestingEventStatus.Draft,
            Guid.NewGuid(),
            request.ApplicationsOpenAt,
            request.ApplicationsCloseAt,
            request.StartsAt,
            request.EndsAt,
            request.RequiresFeedback,
            TestingLearningCompletionRequirement.None,
            null,
            null,
            null,
            Guid.NewGuid(),
            0,
            0);
        mediator.Setup(candidate => candidate.Send(
                It.IsAny<CreateTestingEventCommand>(),
                cancellation.Token))
            .ReturnsAsync(Result.Success(projection));
        var controller = new TestingEventsController(mediator.Object);

        var result = await controller.CreateEvent(request, cancellation.Token);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        mediator.Verify(candidate => candidate.Send(
            It.Is<CreateTestingEventCommand>(command =>
                command.Name == request.Name &&
                command.Description == request.Description &&
                command.Mode == request.Mode &&
                command.ApprovalMode == request.ApprovalMode &&
                command.RequiresFeedback &&
                command.Configuration == request.Configuration),
            cancellation.Token), Times.Once);
    }

    private static ConfigureTestingEventRequest CompleteConfiguration() => new(
        "Respect the code of conduct.",
        "Provide a playable build.",
        "Complete the assigned tasks.",
        new QuestionnaireSchema("Project application", []),
        new QuestionnaireSchema("Tester registration", []));

    [Fact]
    public async Task ApproveApplication_WithoutSlot_Should_Return_BadRequest_Without_Cqrs_Dispatch()
    {
        var mediator = new Mock<IMediator>();
        var controller = new TestingEventsController(mediator.Object);

        var result = await controller.ApproveApplication(
            Guid.NewGuid(),
            new DecideTestingProjectApplicationRequest(null, "Approved"),
            default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        mediator.Verify(candidate => candidate.Send(
            It.IsAny<IRequest<Result<TestingProjectApplicationProjection>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectApplication_Should_Forward_Rationale_And_CancellationToken()
    {
        var mediator = new Mock<IMediator>();
        using var cancellation = new CancellationTokenSource();
        var applicationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projection = new TestingProjectApplicationProjection(
            applicationId,
            eventId,
            projectId,
            null,
            Guid.NewGuid(),
            null,
            TestingApplicationStatus.Rejected,
            null,
            null,
            "Missing playable build",
            SystemClock.UtcNow,
            []);
        mediator.Setup(candidate => candidate.Send(
                It.IsAny<RejectTestingProjectApplicationCommand>(),
                cancellation.Token))
            .ReturnsAsync(Result.Success(projection));
        var controller = new TestingEventsController(mediator.Object);

        var result = await controller.RejectApplication(
            applicationId,
            new DecideTestingProjectApplicationRequest(null, "Missing playable build"),
            cancellation.Token);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(projection);
        mediator.Verify(candidate => candidate.Send(
            It.Is<RejectTestingProjectApplicationCommand>(command =>
                command.ApplicationId == applicationId &&
                command.Rationale == "Missing playable build"),
            cancellation.Token), Times.Once);
    }
}

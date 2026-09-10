using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Projects;
using GameGuild.TestingLab;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using Xunit;

namespace GameGuild.TestingLab.UnitTests;

#region TestingLocation Tests

public class TestingLocationTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var location = new TestingLocation();

        location.Name.Should().BeEmpty();
        location.IsVirtual.Should().BeFalse();
        location.Status.Should().Be(LocationStatus.Active);
    }

    [Fact]
    public void Activate_ShouldSetStatus()
    {
        var location = new TestingLocation();
        location.Deactivate();

        location.Activate();

        location.Status.Should().Be(LocationStatus.Active);
    }

    [Fact]
    public void Deactivate_ShouldSetStatus()
    {
        var location = new TestingLocation();

        location.Deactivate();

        location.Status.Should().Be(LocationStatus.Inactive);
    }

    [Fact]
    public void SetMaintenance_ShouldSetStatus()
    {
        var location = new TestingLocation();

        location.SetMaintenance();

        location.Status.Should().Be(LocationStatus.Maintenance);
    }

    [Fact]
    public void SetCapacity_NegativeValue_ShouldThrow()
    {
        var location = new TestingLocation();

        var act = () => location.SetCapacity(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetCapacity_ValidValue_ShouldSet()
    {
        var location = new TestingLocation();

        location.SetCapacity(50);

        location.Capacity.Should().Be(50);
    }

    [Fact]
    public void SetVirtualInfo_ShouldSetVirtualAndUrl()
    {
        var location = new TestingLocation();

        location.SetVirtualInfo("https://meet.example.com/abc");

        location.IsVirtual.Should().BeTrue();
        location.VirtualUrl.Should().Be("https://meet.example.com/abc");
    }

    [Fact]
    public void IsAvailable_WhenActive_ShouldBeTrue()
    {
        var location = new TestingLocation { Status = LocationStatus.Active };

        location.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WhenMaintenance_ShouldBeFalse()
    {
        var location = new TestingLocation();
        location.SetMaintenance();

        location.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void CanAccommodate_WhenActiveAndSufficientCapacity_ShouldBeTrue()
    {
        var location = new TestingLocation { Capacity = 20 };

        location.CanAccommodate(15).Should().BeTrue();
    }

    [Fact]
    public void CanAccommodate_WhenInsufficientCapacity_ShouldBeFalse()
    {
        var location = new TestingLocation { Capacity = 10 };

        location.CanAccommodate(15).Should().BeFalse();
    }

    [Fact]
    public void CanAccommodate_WhenNullCapacity_ShouldBeTrue()
    {
        var location = new TestingLocation();

        location.CanAccommodate(100).Should().BeTrue();
    }

    [Fact]
    public void FullAddress_ShouldJoinNonEmptyParts()
    {
        var location = new TestingLocation
        {
            Address = "123 Main St",
            City = "San Francisco",
            State = "CA",
            PostalCode = "94105",
            Country = "US"
        };

        location.FullAddress.Should().Be("123 Main St, San Francisco, CA, 94105, US");
    }

    [Fact]
    public void FullAddress_WithNulls_ShouldSkipBlanks()
    {
        var location = new TestingLocation
        {
            City = "Tokyo",
            Country = "Japan"
        };

        location.FullAddress.Should().Be("Tokyo, Japan");
    }
}

#endregion

public sealed class TestingRequestsControllerAuthorizationTests
{
    [Fact]
    public async Task GetTestingRequests_Should_Return_Stable_Projections_With_Project_Context()
    {
        var projectId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Title = "Arena Tactics",
            Slug = "arena-tactics"
        };
        var version = ProjectVersion.Create(projectId, "1.0.0", null, Guid.NewGuid(), null);
        version.Id = versionId;
        version.Project = project;
        version.MarkReadyForTesting();
        version.Release();
        var testingRequest = new TestingRequest
        {
            Id = requestId,
            Title = "Arena playtest",
            ProjectVersionId = versionId,
            ProjectVersion = version,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1)
        };
        var requestService = new Mock<ITestingRequestOperations>();
        requestService
            .Setup(service => service.GetTestingRequestsAsync(0, 50, true))
            .ReturnsAsync([testingRequest]);
        var controller = new TestingRequestsController(
            requestService.Object,
            new ActorContextAccessor(),
            NullLogger<TestingRequestsController>.Instance,
            new Mock<IMediator>().Object);

        var result = await controller.GetTestingRequests(0, 50, true);

        var projection = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<TestingRequestDetailProjection>>()
            .Which.Should().ContainSingle().Subject;
        projection.Id.Should().Be(requestId);
        projection.ProjectVersion.Should().NotBeNull();
        projection.ProjectVersion!.Project.Should().BeEquivalentTo(
            new TestingRequestProjectProjection(projectId, "Arena Tactics", "arena-tactics"));
    }

    [Fact]
    public void SubmitSimpleTestingRequest_Dto_Should_Not_Require_Legacy_TeamIdentifier_For_ProjectBacked_Submissions()
    {
        var property = typeof(CreateSimpleTestingRequestDto)
            .GetProperty(nameof(CreateSimpleTestingRequestDto.TeamIdentifier));

        property.Should().NotBeNull();
        new NullabilityInfoContext().Create(property!).ReadState
            .Should().Be(NullabilityState.Nullable);
    }

    [Fact]
    public void SubmitSimpleTestingRequest_Should_Rely_On_Project_Edit_Authorization()
    {
        var method = typeof(TestingRequestsController).GetMethod(nameof(TestingRequestsController.SubmitSimpleTestingRequest));

        method.Should().NotBeNull();
        method!.GetCustomAttributes(inherit: true)
            .Should().NotContain(attribute => attribute.GetType().Name.StartsWith("RequireResourcePermission", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SubmitSimpleTestingRequest_Should_Return_Stable_Detail_Projection()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var createdRequest = new TestingRequest { Id = requestId, Title = "Created request" };
        var projection = new TestingRequestDetailProjection(
            requestId,
            "Created request",
            "Stable response without EF navigation graphs.",
            null,
            null,
            null,
            4,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            TestingRequestStatus.Draft,
            null,
            null,
            false);
        var requestService = new Mock<ITestingRequestOperations>();
        requestService
            .Setup(service => service.CreateSimpleTestingRequestAsync(It.IsAny<CreateSimpleTestingRequestDto>(), userId))
            .ReturnsAsync(createdRequest);
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(candidate => candidate.Send(
                It.Is<CreateSimpleTestingRequestEndpointCommand>(command => command.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdRequest);
        mediator
            .Setup(candidate => candidate.Send(
                It.Is<GetTestingRequestDetailQuery>(query => query.RequestId == requestId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(projection));
        var actorAccessor = new ActorContextAccessor();
        actorAccessor.SetActorContext(ActorContextBuilder.ForUser(userId).WithTenantId(tenantId).Build());
        var controller = new TestingRequestsController(
            requestService.Object,
            actorAccessor,
            NullLogger<TestingRequestsController>.Instance,
            mediator.Object);

        var result = await controller.SubmitSimpleTestingRequest(new CreateSimpleTestingRequestDto
        {
            ProjectId = Guid.NewGuid(),
            Title = "Created request",
            VersionNumber = "1.0.0",
            InstructionsType = InstructionType.Text
        });

        result.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeSameAs(projection);
    }

    [Fact]
    public async Task UpdateTestingRequest_Should_Return_Stable_Detail_Projection()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var existingRequest = new TestingRequest
        {
            Id = requestId,
            Title = "Original request",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1)
        };
        var projection = new TestingRequestDetailProjection(
            requestId,
            "Updated request",
            "Stable update response without EF navigation graphs.",
            null,
            null,
            null,
            3,
            0,
            existingRequest.StartDate,
            existingRequest.EndDate,
            TestingRequestStatus.Open,
            null,
            null,
            false);
        var requestService = new Mock<ITestingRequestOperations>();
        requestService
            .Setup(service => service.GetTestingRequestByIdAsync(requestId))
            .ReturnsAsync(existingRequest);
        requestService
            .Setup(service => service.UpdateTestingRequestAsync(existingRequest))
            .ReturnsAsync(existingRequest);
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(candidate => candidate.Send(
                It.Is<UpdateTestingRequestEndpointCommand>(command => command.RequestId == requestId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRequest);
        mediator
            .Setup(candidate => candidate.Send(
                It.Is<GetTestingRequestDetailQuery>(query => query.RequestId == requestId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(projection));
        var actorAccessor = new ActorContextAccessor();
        actorAccessor.SetActorContext(ActorContextBuilder.ForUser(userId).WithTenantId(tenantId).Build());
        var controller = new TestingRequestsController(
            requestService.Object,
            actorAccessor,
            NullLogger<TestingRequestsController>.Instance,
            mediator.Object);

        var result = await controller.UpdateTestingRequest(requestId, new UpdateTestingRequestDto
        {
            Title = "Updated request",
            MaxTesters = 3,
            Status = TestingRequestStatus.Open
        });

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(projection);
    }

    [Fact]
    public async Task SubmitSimpleTestingRequest_Should_Return_NotFound_When_Project_Authorization_Is_Denied()
    {
        var userId = Guid.NewGuid();
        var requestService = new Mock<ITestingRequestOperations>();
        requestService
            .Setup(service => service.CreateSimpleTestingRequestAsync(It.IsAny<CreateSimpleTestingRequestDto>(), userId))
            .ThrowsAsync(new KeyNotFoundException("Project not found."));
        var actorAccessor = new ActorContextAccessor();
        actorAccessor.SetActorContext(ActorContextBuilder.ForUser(userId).WithTenantId(Guid.NewGuid()).Build());
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(candidate => candidate.Send(
                It.IsAny<CreateSimpleTestingRequestEndpointCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Project not found."));
        var controller = new TestingRequestsController(
            requestService.Object,
            actorAccessor,
            NullLogger<TestingRequestsController>.Instance,
            mediator.Object);

        var result = await controller.SubmitSimpleTestingRequest(new CreateSimpleTestingRequestDto
        {
            ProjectId = Guid.NewGuid(),
            Title = "Denied submission",
            VersionNumber = "1.0.0",
            DownloadUrl = "https://example.com/build.zip",
            InstructionsType = InstructionType.Text
        });

        result.Result.Should().BeOfType<NotFoundResult>();
    }
}

public sealed class TestingParticipantsControllerResponseTests
{
    [Fact]
    public async Task AddParticipant_Should_Return_Stable_Projection()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var participant = new TestingParticipant
        {
            Id = participantId,
            TestingRequestId = requestId,
            UserId = userId,
            Status = ParticipationStatus.Registered
        };
        var service = new Mock<ITestingParticipantOperations>();
        service.Setup(candidate => candidate.AddParticipantAsync(requestId, userId))
            .ReturnsAsync(participant);
        var sender = new Mock<GameGuild.CQRS.ISender>();
        sender
            .Setup(candidate => candidate.Send(
                It.Is<AddTestingParticipantEndpointCommand>(command =>
                    command.TestingRequestId == requestId && command.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(participant);
        var controller = new TestingParticipantsController(
            service.Object,
            sender.Object,
            new Mock<IActorContextAccessor>().Object);

        var result = await controller.AddParticipant(requestId, userId);

        var projection = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<TestingParticipantMutationProjection>().Subject;
        projection.Id.Should().Be(participantId);
        projection.TestingRequestId.Should().Be(requestId);
        projection.UserId.Should().Be(userId);
        projection.Status.Should().Be(ParticipationStatus.Registered);
    }
}

#region TestingParticipant Tests

public class TestingParticipantTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var participant = new TestingParticipant();

        participant.InstructionsAcknowledged.Should().BeFalse();
        participant.FeedbackCount.Should().Be(0);
        participant.Status.Should().Be(ParticipationStatus.Registered);
    }

    [Fact]
    public void AcknowledgeInstructions_ShouldSetFlag()
    {
        var participant = new TestingParticipant();

        participant.AcknowledgeInstructions();

        participant.InstructionsAcknowledged.Should().BeTrue();
        participant.InstructionsAcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public void Start_WithoutAcknowledgement_ShouldThrow()
    {
        var participant = new TestingParticipant();

        var act = () => participant.Start();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Instructions must be acknowledged*");
    }

    [Fact]
    public void Start_WithAcknowledgement_ShouldSetActive()
    {
        var participant = new TestingParticipant();
        participant.AcknowledgeInstructions();

        participant.Start();

        participant.Status.Should().Be(ParticipationStatus.Active);
        participant.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Complete_ShouldSetStatus()
    {
        var participant = new TestingParticipant();
        participant.AcknowledgeInstructions();
        participant.Start();

        participant.Complete();

        participant.Status.Should().Be(ParticipationStatus.Completed);
        participant.CompletedAt.Should().NotBeNull();
        participant.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void Withdraw_ShouldSetStatus()
    {
        var participant = new TestingParticipant();

        participant.Withdraw();

        participant.Status.Should().Be(ParticipationStatus.Withdrawn);
    }

    [Fact]
    public void RecordTimeSpent_ShouldAccumulate()
    {
        var participant = new TestingParticipant();

        participant.RecordTimeSpent(30);
        participant.RecordTimeSpent(15);

        participant.TimeSpentMinutes.Should().Be(45);
    }

    [Fact]
    public void IncrementFeedbackCount_ShouldIncrement()
    {
        var participant = new TestingParticipant();

        participant.IncrementFeedbackCount();
        participant.IncrementFeedbackCount();

        participant.FeedbackCount.Should().Be(2);
    }

    [Fact]
    public void CanProvideFeedback_WhenAcknowledgedAndActive_ShouldBeTrue()
    {
        var participant = new TestingParticipant();
        participant.AcknowledgeInstructions();
        participant.Start();

        participant.CanProvideFeedback.Should().BeTrue();
    }

    [Fact]
    public void CanProvideFeedback_WhenNotAcknowledged_ShouldBeFalse()
    {
        var participant = new TestingParticipant();

        participant.CanProvideFeedback.Should().BeFalse();
    }
}

#endregion

#region SessionRegistration Tests

public class SessionRegistrationTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var reg = new SessionRegistration();

        reg.RegistrationType.Should().Be(RegistrationType.Tester);
        reg.Status.Should().Be(RegistrationStatus.Registered);
        reg.AttendanceStatus.Should().Be(AttendanceStatus.Registered);
    }

    [Fact]
    public void Confirm_ShouldSetStatus()
    {
        var reg = new SessionRegistration();

        reg.Confirm();

        reg.Status.Should().Be(RegistrationStatus.Confirmed);
        reg.IsConfirmed.Should().BeTrue();
        reg.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_ShouldSetStatusAndNoShow()
    {
        var reg = new SessionRegistration();

        reg.Cancel();

        reg.Status.Should().Be(RegistrationStatus.Cancelled);
        reg.AttendanceStatus.Should().Be(AttendanceStatus.NoShow);
    }

    [Fact]
    public void CheckIn_ShouldSetTimestampAndPresent()
    {
        var reg = new SessionRegistration();

        reg.CheckIn();

        reg.CheckedInAt.Should().NotBeNull();
        reg.IsCheckedIn.Should().BeTrue();
        reg.AttendanceStatus.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void CheckOut_ShouldSetTimestampAndCompleted()
    {
        var reg = new SessionRegistration();
        reg.CheckIn();

        reg.CheckOut();

        reg.CheckedOutAt.Should().NotBeNull();
        reg.IsCheckedOut.Should().BeTrue();
        reg.AttendanceStatus.Should().Be(AttendanceStatus.Completed);
    }

    [Fact]
    public void AttendanceDuration_WhenBothSet_ShouldCalculate()
    {
        var reg = new SessionRegistration();
        reg.CheckIn();
        // Simulate time passing by checking out
        reg.CheckOut();

        reg.AttendanceDuration.Should().NotBeNull();
    }

    [Fact]
    public void MarkNoShow_ShouldSetStatus()
    {
        var reg = new SessionRegistration();

        reg.MarkNoShow();

        reg.AttendanceStatus.Should().Be(AttendanceStatus.NoShow);
    }
}

#endregion

#region TestingFeedback Tests

public class TestingFeedbackTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var feedback = new TestingFeedback();

        feedback.FeedbackData.Should().BeEmpty();
        feedback.IsReported.Should().BeFalse();
    }

    [Fact]
    public void SetOverallRating_ValidRange_ShouldSet()
    {
        var feedback = new TestingFeedback();

        feedback.SetOverallRating(8);

        feedback.OverallRating.Should().Be(8);
    }

    [Fact]
    public void SetOverallRating_BelowRange_ShouldThrow()
    {
        var feedback = new TestingFeedback();

        var act = () => feedback.SetOverallRating(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetOverallRating_AboveRange_ShouldThrow()
    {
        var feedback = new TestingFeedback();

        var act = () => feedback.SetOverallRating(11);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetRecommendation_ShouldUpdateField()
    {
        var feedback = new TestingFeedback();

        feedback.SetRecommendation(true);

        feedback.WouldRecommend.Should().BeTrue();
    }

    [Fact]
    public void Report_ShouldSetAllReportFields()
    {
        var feedback = new TestingFeedback();
        var reporterId = Guid.NewGuid();

        feedback.Report(reporterId, "Spam content");

        feedback.IsReported.Should().BeTrue();
        feedback.ReportedById.Should().Be(reporterId);
        feedback.ReportReason.Should().Be("Spam content");
        feedback.ReportedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsPositive_WhenHighRatingAndRecommend_ShouldBeTrue()
    {
        var feedback = new TestingFeedback
        {
            OverallRating = 9,
            WouldRecommend = true
        };

        feedback.IsPositive.Should().BeTrue();
    }

    [Fact]
    public void IsNegative_WhenLowRating_ShouldBeTrue()
    {
        var feedback = new TestingFeedback { OverallRating = 3 };

        feedback.IsNegative.Should().BeTrue();
    }
}

#endregion

#region Enum Tests

public class TestingLabEnumTests
{
    [Fact]
    public void AttendanceStatus_ShouldHave4Values()
    {
        Enum.GetValues<AttendanceStatus>().Should().HaveCount(4);
    }

    [Fact]
    public void LocationStatus_ShouldHave3Values()
    {
        Enum.GetValues<LocationStatus>().Should().HaveCount(3);
    }

    [Fact]
    public void SessionStatus_ShouldHave4Values()
    {
        Enum.GetValues<SessionStatus>().Should().HaveCount(4);
    }

    [Fact]
    public void TestingContext_ShouldHave2Values()
    {
        Enum.GetValues<TestingContext>().Should().HaveCount(2);
    }

    [Fact]
    public void TestingMode_ShouldHave3Values()
    {
        Enum.GetValues<TestingMode>().Should().HaveCount(3);
    }

    [Fact]
    public void RegistrationType_ShouldHave2Values()
    {
        Enum.GetValues<RegistrationType>().Should().HaveCount(2);
    }

    [Fact]
    public void InstructionType_ShouldHave3Values()
    {
        Enum.GetValues<InstructionType>().Should().HaveCount(3);
    }
}

#endregion

#region TestingRequestOperationsService Tests

public class TestingRequestOperationsServiceTests
{
    [Fact]
    public async Task CreateTestingRequestAsync_WithCrossTenantProjectVersion_ShouldRejectBeforePersisting()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var actorTenantId = Guid.NewGuid();
        AddIdentity(context, actorId, actorTenantId);
        var foreignProject = CreateProject("Foreign version", Guid.NewGuid(), actorId);
        var foreignVersion = ProjectVersion.Create(
            foreignProject.Id,
            "1.0.0",
            null,
            actorId,
            foreignProject.TenantId);
        context.AddRange(foreignProject, foreignVersion);
        await context.SaveChangesAsync();
        var (_, service) = CreateRequestService(context, actorId, actorTenantId);
        var request = NewProjectVersionRequest(foreignVersion.Id, actorId, actorTenantId);

        var act = () => service.CreateTestingRequestAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Set<TestingRequest>().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTestingRequestAsync_WithActiveProjectVersion_ShouldHoldLifecycleLockThroughCommit()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, actorId, tenantId);
        var (project, version) = AddAuthorizedProjectVersion(context, actorId, tenantId, "Locked create");
        await context.SaveChangesAsync();
        var recordingLock = new RecordingProjectLifecycleLock();
        var service = CreateRequestService(context, actorId, tenantId, recordingLock).Service;

        await service.CreateTestingRequestAsync(NewProjectVersionRequest(version.Id, actorId, tenantId));

        recordingLock.AcquiredProjectIds.Should().Equal(project.Id);
        recordingLock.CommitCount.Should().Be(1);
        recordingLock.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task RestoreTestingRequestAsync_WithDeletedProject_ShouldRejectAndRemainDeleted()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, actorId, tenantId);
        var (_, version) = AddAuthorizedProjectVersion(context, actorId, tenantId, "Deleted restore");
        var request = NewProjectVersionRequest(version.Id, actorId, tenantId);
        request.DeletedAt = SystemClock.UtcNow.AddDays(-1);
        context.Add(request);
        await context.SaveChangesAsync();
        version.Project.DeletedAt = SystemClock.UtcNow;
        await context.SaveChangesAsync();
        var service = CreateRequestService(context, actorId, tenantId).Service;

        var act = () => service.RestoreTestingRequestAsync(request.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        request.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RestoreTestingRequestAsync_WithActiveProject_ShouldHoldLifecycleLockThroughCommit()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, actorId, tenantId);
        var (project, version) = AddAuthorizedProjectVersion(context, actorId, tenantId, "Locked restore");
        var request = NewProjectVersionRequest(version.Id, actorId, tenantId);
        request.DeletedAt = SystemClock.UtcNow.AddDays(-1);
        context.Add(request);
        await context.SaveChangesAsync();
        var recordingLock = new RecordingProjectLifecycleLock();
        var service = CreateRequestService(context, actorId, tenantId, recordingLock).Service;

        (await service.RestoreTestingRequestAsync(request.Id)).Should().BeTrue();

        recordingLock.AcquiredProjectIds.Should().Equal(project.Id);
        recordingLock.CommitCount.Should().Be(1);
        recordingLock.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateTestingRequestCommandHandler_WithCrossTenantProjectVersion_ShouldRejectBeforePersisting()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var actorTenantId = Guid.NewGuid();
        AddIdentity(context, actorId, actorTenantId);
        var foreignProject = CreateProject("Command foreign version", Guid.NewGuid(), actorId);
        var foreignVersion = ProjectVersion.Create(
            foreignProject.Id,
            "1.0.0",
            null,
            actorId,
            foreignProject.TenantId);
        context.AddRange(foreignProject, foreignVersion);
        await context.SaveChangesAsync();
        var mediator = new Mock<IMediator>();
        var operations = CreateRequestService(context, actorId, actorTenantId).Service;
        var handler = new CreateTestingRequestCommandHandler(
            Mock.Of<ITestingRequestRepository>(),
            new TestingRequestService(operations, Mock.Of<ITestingParticipantOperations>()),
            mediator.Object);

        var act = () => handler.Handle(NewCreateCommand(foreignVersion.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Set<TestingRequest>().Should().BeEmpty();
        mediator.Verify(candidate => candidate.Publish(It.IsAny<TestingRequestCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_WithoutExistingProject_ShouldThrow()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        await context.SaveChangesAsync();
        var (_, service) = CreateRequestService(context, userId, tenantId);

        var dto = CreateRequestDto(projectId: Guid.NewGuid());

        var act = () => service.CreateSimpleTestingRequestAsync(dto, userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Testing Lab submissions must be linked to an existing project.");
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_WithProjectId_ShouldNotResolveCrossTenantProject()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        var project = CreateProject("Foreign project", Guid.NewGuid(), userId);
        context.Set<Project>().Add(project);
        await context.SaveChangesAsync();
        var (_, service) = CreateRequestService(context, userId, tenantId);

        var act = () => service.CreateSimpleTestingRequestAsync(CreateRequestDto(project.Id), userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Testing Lab submissions must be linked to an existing project.");
        context.Set<ProjectVersion>().Should().BeEmpty();
        context.Set<ProjectRelease>().Should().BeEmpty();
        context.Set<TestingRequest>().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_WithProjectId_ShouldNotResolveSoftDeletedProject()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        var project = CreateProject("Deleted project", tenantId, userId);
        project.DeletedAt = SystemClock.UtcNow;
        context.Set<Project>().Add(project);
        await context.SaveChangesAsync();
        var (_, service) = CreateRequestService(context, userId, tenantId);

        var act = () => service.CreateSimpleTestingRequestAsync(CreateRequestDto(project.Id), userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Testing Lab submissions must be linked to an existing project.");
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_WithLegacyTitle_ShouldResolveOnlyActiveActorTenantProject()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        var expected = CreateProject("Legacy team", tenantId, userId);
        var foreign = CreateProject("Legacy team", Guid.NewGuid(), userId);
        var deleted = CreateProject("Legacy team", tenantId, userId);
        deleted.DeletedAt = SystemClock.UtcNow;
        context.Set<Project>().AddRange(foreign, deleted, expected);
        context.Set<ProjectCollaborator>().Add(new ProjectCollaborator
        {
            ProjectId = expected.Id,
            UserId = userId,
            Role = ProjectRoles.Owner,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var (_, service) = CreateRequestService(context, userId, tenantId);

        var request = await service.CreateSimpleTestingRequestAsync(CreateLegacyRequestDto("Legacy team"), userId);

        request.ProjectVersion!.ProjectId.Should().Be(expected.Id);
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_WithAmbiguousLegacyTitle_ShouldRejectBeforeCreatingRows()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        context.Set<Project>().AddRange(
            CreateProject("Duplicate title", tenantId, userId),
            CreateProject("Duplicate title", tenantId, userId));
        await context.SaveChangesAsync();
        var (_, service) = CreateRequestService(context, userId, tenantId);

        var act = () => service.CreateSimpleTestingRequestAsync(CreateLegacyRequestDto("Duplicate title"), userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Multiple active projects match the legacy team identifier.");
        context.Set<ProjectVersion>().Should().BeEmpty();
        context.Set<ProjectRelease>().Should().BeEmpty();
        context.Set<TestingRequest>().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_WithExistingProject_ShouldCreateProjectBackedRequest()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        var (actorAccessor, service) = CreateRequestService(context, userId, tenantId);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Project-backed Lab Build",
            Slug = "project-backed-lab-build",
            Status = ContentStatus.Published,
            Visibility = ContentVisibility.Public,
            CreatedById = userId,
            TenantId = tenantId,
        };
        context.Set<Project>().Add(project);
        context.Set<ProjectCollaborator>().Add(new ProjectCollaborator
        {
            ProjectId = project.Id,
            UserId = userId,
            Role = ProjectRoles.Owner,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var dto = CreateRequestDto(project.Id);

        var request = await service.CreateSimpleTestingRequestAsync(dto, userId);

        request.ProjectVersionId.Should().NotBeNull();
        request.ProjectVersion!.ProjectId.Should().Be(project.Id);
        request.ProjectVersion.Project.Should().BeSameAs(project);
        request.ProjectVersion.VersionNumber.Should().Be(dto.VersionNumber);
        context.Set<ProjectRelease>().Should().ContainSingle(release =>
            release.ProjectId == project.Id &&
            release.ReleaseVersion == dto.VersionNumber &&
            release.Title == $"{project.Title} {dto.VersionNumber}");
        request.TenantId.Should().Be(tenantId);
        actorAccessor.ActorContext.SubjectIdAsGuid.Should().Be(userId);
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_ShouldHoldProjectLifecycleLockThroughCommit()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        var project = CreateProject("Locked request", tenantId, userId);
        context.Set<Project>().Add(project);
        context.Set<ProjectCollaborator>().Add(new ProjectCollaborator
        {
            ProjectId = project.Id,
            UserId = userId,
            Role = ProjectRoles.Owner,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(ActorContextBuilder.ForUser(userId).WithTenantId(tenantId).Build());
        var recordingLock = new RecordingProjectLifecycleLock();
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(context);
        services.AddSingleton<IProjectChannelAvailabilityService>(new ProjectChannelAvailabilityService(context));
        services.AddSingleton<IProjectAuthorizationService>(new ProjectAuthorizationService(context, accessor));
        services.AddSingleton<IActorContextAccessor>(accessor);
        services.AddSingleton<IProjectLifecycleLock>(recordingLock);
        await using var provider = services.BuildServiceProvider();
        var service = ActivatorUtilities.CreateInstance<TestingRequestOperationsService>(provider);

        await service.CreateSimpleTestingRequestAsync(CreateRequestDto(project.Id), userId);

        recordingLock.AcquiredProjectIds.Should().Equal(project.Id);
        recordingLock.CommitCount.Should().Be(1);
        recordingLock.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_Should_Not_Reuse_CrossTenant_Project_Version()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        var (_, service) = CreateRequestService(context, userId, tenantId);
        var project = new Project
        {
            Title = "Version tenant",
            Slug = "version-tenant",
            Status = ContentStatus.Draft,
            TenantId = tenantId,
            CreatedById = userId
        };
        var staleVersion = ProjectVersion.Create(
            project.Id,
            "0.2.0",
            null,
            userId,
            Guid.NewGuid());
        staleVersion.MarkReadyForTesting();
        context.Set<Project>().Add(project);
        context.Set<ProjectCollaborator>().Add(new ProjectCollaborator
        {
            ProjectId = project.Id,
            UserId = userId,
            Role = ProjectRoles.Owner,
            IsActive = true
        });
        context.Set<ProjectVersion>().Add(staleVersion);
        await context.SaveChangesAsync();

        var request = await service.CreateSimpleTestingRequestAsync(CreateRequestDto(project.Id), userId);

        request.ProjectVersionId.Should().NotBe(staleVersion.Id);
        request.ProjectVersion!.TenantId.Should().Be(tenantId);
    }

    [Theory]
    [InlineData(ContentStatus.Archived)]
    [InlineData(ContentStatus.Deleted)]
    public async Task CreateSimpleTestingRequestAsync_ShouldRejectTerminalProjectLifecycle(ContentStatus status)
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        var (_, service) = CreateRequestService(context, userId, tenantId);
        var project = new Project
        {
            Title = "Unavailable",
            Slug = "unavailable",
            Status = status,
            Visibility = ContentVisibility.Private,
            TenantId = tenantId
        };
        context.Set<Project>().Add(project);
        context.Set<ProjectCollaborator>().Add(new ProjectCollaborator
        {
            ProjectId = project.Id,
            UserId = userId,
            Role = ProjectRoles.Owner,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var act = () => service.CreateSimpleTestingRequestAsync(CreateRequestDto(project.Id), userId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*lifecycle_unavailable*");
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_ShouldRejectCrossTenantAndUnauthorizedCollaborator()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        var (_, service) = CreateRequestService(context, userId, tenantId);
        var crossTenant = new Project
        {
            Title = "Other tenant",
            Slug = "other-tenant",
            Status = ContentStatus.Draft,
            TenantId = Guid.NewGuid()
        };
        var unauthorized = new Project
        {
            Title = "No collaborator",
            Slug = "no-collaborator",
            Status = ContentStatus.Draft,
            TenantId = tenantId
        };
        context.Set<Project>().AddRange(crossTenant, unauthorized);
        await context.SaveChangesAsync();

        var crossTenantAct = () => service.CreateSimpleTestingRequestAsync(CreateRequestDto(crossTenant.Id), userId);
        var unauthorizedAct = () => service.CreateSimpleTestingRequestAsync(CreateRequestDto(unauthorized.Id), userId);

        await crossTenantAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Testing Lab submissions must be linked to an existing project.");
        await unauthorizedAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateSimpleTestingRequestAsync_ShouldRejectInactiveProjectOwner()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId, userActive: false);
        var (_, service) = CreateRequestService(context, userId, tenantId);
        var project = new Project
        {
            Title = "Inactive owner",
            Slug = "inactive-owner",
            Status = ContentStatus.Draft,
            TenantId = tenantId,
            CreatedById = userId
        };
        context.Set<Project>().Add(project);
        await context.SaveChangesAsync();

        var act = () => service.CreateSimpleTestingRequestAsync(CreateRequestDto(project.Id), userId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private static (IActorContextAccessor ActorAccessor, TestingRequestOperationsService Service) CreateRequestService(
        IApplicationDbContext context,
        Guid userId,
        Guid tenantId,
        IProjectLifecycleLock? lifecycleLock = null)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(ActorContextBuilder.ForUser(userId).WithTenantId(tenantId).Build());
        return (accessor, new TestingRequestOperationsService(
            context,
            new ProjectChannelAvailabilityService(context),
            new ProjectAuthorizationService(context, accessor),
            accessor,
            lifecycleLock));
    }

    private static (Project Project, ProjectVersion Version) AddAuthorizedProjectVersion(
        IApplicationDbContext context,
        Guid actorId,
        Guid tenantId,
        string title)
    {
        var project = CreateProject(title, tenantId, actorId);
        var version = ProjectVersion.Create(project.Id, "1.0.0", null, actorId, tenantId);
        version.Project = project;
        version.MarkReadyForTesting();
        context.Set<Project>().Add(project);
        context.Set<ProjectVersion>().Add(version);
        context.Set<ProjectCollaborator>().Add(new ProjectCollaborator
        {
            ProjectId = project.Id,
            UserId = actorId,
            Role = ProjectRoles.Owner,
            IsActive = true
        });
        return (project, version);
    }

    private static TestingRequest NewProjectVersionRequest(Guid projectVersionId, Guid actorId, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        ProjectVersionId = projectVersionId,
        Title = "Guarded testing request",
        InstructionsType = InstructionType.Text,
        StartDate = SystemClock.UtcNow,
        EndDate = SystemClock.UtcNow.AddDays(1),
        CreatedById = actorId,
        TenantId = tenantId
    };

    private static CreateTestingRequestCommand NewCreateCommand(Guid projectVersionId) => new(
        projectVersionId,
        "Guarded command request",
        null,
        null,
        InstructionType.Text,
        "Test instructions",
        null,
        null,
        null,
        4,
        SystemClock.UtcNow,
        SystemClock.UtcNow.AddDays(1));

    private static TestingLabServiceDbContext CreateContext()
        => new(new DbContextOptionsBuilder<TestingLabServiceDbContext>()
            .UseInMemoryDatabase($"testing-lab-service-{Guid.NewGuid():N}")
            .Options);

    private static void AddIdentity(
        IApplicationDbContext context,
        Guid userId,
        Guid tenantId,
        bool userActive = true)
    {
        context.Set<User>().Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@example.com",
            Name = "Testing request actor",
            IsActive = userActive
        });
        context.Set<TenantMember>().Add(new TenantMember
        {
            UserId = userId,
            TenantId = tenantId,
            Role = "Member",
            IsActive = true
        });
    }

    private static CreateSimpleTestingRequestDto CreateRequestDto(Guid projectId) => new()
    {
        ProjectId = projectId,
        Title = "Build feedback pass",
        Description = "Validate onboarding and first-session clarity.",
        VersionNumber = "0.2.0",
        DownloadUrl = "https://example.com/build.zip",
        InstructionsType = InstructionType.Text,
        InstructionsContent = "Install the build and complete the tutorial.",
        FeedbackFormContent = "What blocked you?",
        MaxTesters = 8,
    };

    private static CreateSimpleTestingRequestDto CreateLegacyRequestDto(string teamIdentifier)
    {
        var request = CreateRequestDto(Guid.NewGuid());
        request.ProjectId = null;
        request.TeamIdentifier = teamIdentifier;
        return request;
    }

    private static Project CreateProject(string title, Guid tenantId, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Slug = $"{Project.GenerateSlug(title)}-{Guid.NewGuid():N}",
        Status = ContentStatus.Draft,
        TenantId = tenantId,
        CreatedById = userId
    };

    private sealed class TestingLabServiceDbContext(DbContextOptions<TestingLabServiceDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<Project> Projects => Set<Project>();

        public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();

        public DbSet<ProjectRelease> ProjectReleases => Set<ProjectRelease>();

        public DbSet<TestingRequest> TestingRequests => Set<TestingRequest>();

        public DbSet<User> Users => Set<User>();

        public DbSet<TenantMember> TenantMembers => Set<TenantMember>();

        public DbSet<GameGuild.Identity.Authorization.ResourceUserPermission> ResourceUserPermissions => Set<GameGuild.Identity.Authorization.ResourceUserPermission>();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Transactions are not required for this service regression.");
    }

    private sealed class RecordingProjectLifecycleLock : IProjectLifecycleLock
    {
        public List<Guid> AcquiredProjectIds { get; } = [];
        public int CommitCount { get; private set; }
        public int DisposeCount { get; private set; }

        public Task<IProjectLifecycleLockHandle> AcquireAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            AcquiredProjectIds.Add(projectId);
            return Task.FromResult<IProjectLifecycleLockHandle>(new Handle(this));
        }

        private sealed class Handle(RecordingProjectLifecycleLock owner) : IProjectLifecycleLockHandle
        {
            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                owner.CommitCount++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                owner.DisposeCount++;
                return ValueTask.CompletedTask;
            }
        }
    }
}

#endregion

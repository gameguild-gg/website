using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Resources.Contents.UnitTests;

/// <summary>
///     Issue #329: the editorial workflow endpoints on <see cref="VersioningController"/> are
///     gated by the granular editorial permissions (<c>content:edit</c>, <c>content:draft</c>,
///     <c>content:schedule</c>) with <c>content:write</c>/<c>content:admin</c> kept as
///     broader-manager fallbacks. Unauthenticated authorization (no permission) must fail
///     closed with 403 before any command is dispatched.
/// </summary>
public class VersioningEditorialPermissionGatingTests
{
    [Fact]
    public async Task EditorialEndpoints_FailClosedWith403_WhenActorHasNoEditorialPermissions()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender, []);

        (await controller.CreateDraft(new CreateDraftRequest(Guid.NewGuid(), "Page", "Draft", Guid.NewGuid()), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.UpdateDraft(Guid.NewGuid(), new UpdateDraftRequest(), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.SchedulePublish(Guid.NewGuid(), new ScheduleRequest(DateTime.UtcNow), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.CancelSchedule(Guid.NewGuid(), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.Rollback("Page", Guid.NewGuid(), new RollbackRequest(1), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();

        // The gate must reject before any command reaches the pipeline.
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DraftEndpoints_AllowDispatch_WhenActorHoldsOnlyContentDraft()
    {
        var sender = new Mock<ISender>();
        var version = ContentVersion.Create(Guid.NewGuid(), "Page", 1, "Draft", Guid.NewGuid());
        sender
            .Setup(s => s.Send(It.IsAny<CreateContentDraftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        sender
            .Setup(s => s.Send(It.IsAny<UpdateContentDraftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        sender
            .Setup(s => s.Send(It.IsAny<ScheduleContentPublishCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        sender
            .Setup(s => s.Send(It.IsAny<CancelContentPublishCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        sender
            .Setup(s => s.Send(It.IsAny<RollbackContentVersionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        var controller = CreateController(sender, ["content:draft"]);

        (await controller.CreateDraft(new CreateDraftRequest(Guid.NewGuid(), "Page", "Draft", Guid.NewGuid()), CancellationToken.None))
            .Should().BeOfType<CreatedAtActionResult>();
        (await controller.UpdateDraft(version.Id, new UpdateDraftRequest("Updated"), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();

        // Least privilege: content:draft must not unlock scheduling or rollback.
        (await controller.SchedulePublish(version.Id, new ScheduleRequest(DateTime.UtcNow), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.CancelSchedule(version.Id, CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.Rollback("Page", Guid.NewGuid(), new RollbackRequest(1), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ScheduleEndpoints_AllowDispatch_WhenActorHoldsOnlyContentSchedule()
    {
        var sender = new Mock<ISender>();
        var version = ContentVersion.Create(Guid.NewGuid(), "Page", 2, "Pending", Guid.NewGuid());
        sender
            .Setup(s => s.Send(It.IsAny<ScheduleContentPublishCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        sender
            .Setup(s => s.Send(It.IsAny<CancelContentPublishCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        var controller = CreateController(sender, ["content:schedule"]);

        (await controller.SchedulePublish(version.Id, new ScheduleRequest(new DateTime(2026, 3, 1)), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.CancelSchedule(version.Id, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();

        // Least privilege: content:schedule must not unlock drafting or rollback.
        (await controller.CreateDraft(new CreateDraftRequest(Guid.NewGuid(), "Page", "Draft", Guid.NewGuid()), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.Rollback("Page", Guid.NewGuid(), new RollbackRequest(1), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Rollback_AllowsDispatch_WhenActorHoldsOnlyContentEdit()
    {
        var sender = new Mock<ISender>();
        var version = ContentVersion.Create(Guid.NewGuid(), "Page", 1, "Original", Guid.NewGuid());
        sender
            .Setup(s => s.Send(It.IsAny<RollbackContentVersionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        var controller = CreateController(sender, ["content:edit"]);

        (await controller.Rollback("Page", version.EntityId, new RollbackRequest(1, "restore"), CancellationToken.None))
            .Should().BeOfType<CreatedAtActionResult>();

        // Least privilege: content:edit must not unlock drafting or scheduling.
        (await controller.CreateDraft(new CreateDraftRequest(Guid.NewGuid(), "Page", "Draft", Guid.NewGuid()), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.SchedulePublish(version.Id, new ScheduleRequest(DateTime.UtcNow), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData("content:write")]
    [InlineData("content:admin")]
    public async Task EditorialEndpoints_KeepBroaderContentManagersWorking(string managerPermission)
    {
        var sender = new Mock<ISender>();
        var version = ContentVersion.Create(Guid.NewGuid(), "Page", 1, "Draft", Guid.NewGuid());
        sender
            .Setup(s => s.Send(It.IsAny<CreateContentDraftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        sender
            .Setup(s => s.Send(It.IsAny<UpdateContentDraftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        sender
            .Setup(s => s.Send(It.IsAny<ScheduleContentPublishCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        sender
            .Setup(s => s.Send(It.IsAny<CancelContentPublishCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        sender
            .Setup(s => s.Send(It.IsAny<RollbackContentVersionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        var controller = CreateController(sender, [managerPermission]);

        (await controller.CreateDraft(new CreateDraftRequest(Guid.NewGuid(), "Page", "Draft", Guid.NewGuid()), CancellationToken.None))
            .Should().BeOfType<CreatedAtActionResult>();
        (await controller.UpdateDraft(version.Id, new UpdateDraftRequest(), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.SchedulePublish(version.Id, new ScheduleRequest(DateTime.UtcNow), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.CancelSchedule(version.Id, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.Rollback("Page", Guid.NewGuid(), new RollbackRequest(1), CancellationToken.None))
            .Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task EditorialEndpoints_AllowSystemAdminBypass()
    {
        var sender = new Mock<ISender>();
        var version = ContentVersion.Create(Guid.NewGuid(), "Page", 1, "Draft", Guid.NewGuid());
        sender
            .Setup(s => s.Send(It.IsAny<CreateContentDraftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(version));
        var service = new Mock<IContentVersioningService>();
        var accessor = new Mock<IActorContextAccessor>();
        accessor.Setup(current => current.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(["SystemAdmin"]),
            Permissions = new HashSet<string>()
        });
        var controller = new VersioningController(service.Object, sender.Object, accessor.Object);

        (await controller.CreateDraft(new CreateDraftRequest(Guid.NewGuid(), "Page", "Draft", Guid.NewGuid()), CancellationToken.None))
            .Should().BeOfType<CreatedAtActionResult>();
    }

    private static VersioningController CreateController(Mock<ISender> sender, string[] permissions)
    {
        var service = new Mock<IContentVersioningService>();
        var accessor = new Mock<IActorContextAccessor>();
        accessor.Setup(current => current.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(permissions)
        });
        return new VersioningController(service.Object, sender.Object, accessor.Object);
    }
}

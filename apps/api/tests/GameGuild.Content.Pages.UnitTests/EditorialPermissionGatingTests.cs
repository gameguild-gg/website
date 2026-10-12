using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Content.Pages.UnitTests;

/// <summary>
///     Issue #329: the update surfaces on <see cref="PageController"/> and
///     <see cref="ContentResourceController"/> are gated by the granular editorial permission
///     <c>content:edit</c> with <c>content:write</c>/<c>content:admin</c> kept as
///     broader-manager fallbacks. Callers without an editorial permission must fail closed
///     with 403 before any command is dispatched.
/// </summary>
public class EditorialPermissionGatingTests
{
    [Fact]
    public async Task UpdateEndpoints_FailClosedWith403_WhenActorHasNoEditorialPermissions()
    {
        var sender = new Mock<ISender>();
        var (pageController, resourceController) = CreateControllers(sender, []);

        (await pageController.UpdatePage(Guid.NewGuid(), new UpdatePageDto())).Result
            .Should().BeOfType<ForbidResult>();
        (await pageController.UpdateSection(Guid.NewGuid(), Guid.NewGuid(), new UpdatePageSectionDto())).Result
            .Should().BeOfType<ForbidResult>();
        (await resourceController.Update(Guid.NewGuid(), new UpdateContentResourceDto())).Result
            .Should().BeOfType<ForbidResult>();

        // The gate must reject before any command reaches the pipeline.
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateEndpoints_AllowDispatch_WhenActorHoldsOnlyContentEdit()
    {
        var sender = new Mock<ISender>();
        var page = new Page { Id = Guid.NewGuid(), Slug = "home", Title = "Home", Status = PageStatus.Published };
        var section = new PageSection { Id = Guid.NewGuid(), PageId = page.Id, SectionType = SectionType.Custom };
        var resource = new ContentResource { Id = Guid.NewGuid(), Slug = "guide", Title = "Guide", Status = ContentResourceStatus.Published };
        sender
            .Setup(s => s.Send(It.IsAny<UpdatePageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        sender
            .Setup(s => s.Send(It.IsAny<UpdatePageSectionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(section);
        sender
            .Setup(s => s.Send(It.IsAny<UpdateContentResourceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resource);
        var (pageController, resourceController) = CreateControllers(sender, ["content:edit"]);

        (await pageController.UpdatePage(page.Id, new UpdatePageDto { Title = "Updated" })).Result
            .Should().BeOfType<OkObjectResult>();
        (await pageController.UpdateSection(page.Id, section.Id, new UpdatePageSectionDto { Heading = "Updated" })).Result
            .Should().BeOfType<OkObjectResult>();
        (await resourceController.Update(resource.Id, new UpdateContentResourceDto { Title = "Updated" })).Result
            .Should().BeOfType<OkObjectResult>();
    }

    [Theory]
    [InlineData("content:write")]
    [InlineData("content:admin")]
    public async Task UpdateEndpoints_KeepBroaderContentManagersWorking(string managerPermission)
    {
        var sender = new Mock<ISender>();
        var page = new Page { Id = Guid.NewGuid(), Slug = "home", Title = "Home", Status = PageStatus.Published };
        var resource = new ContentResource { Id = Guid.NewGuid(), Slug = "guide", Title = "Guide", Status = ContentResourceStatus.Published };
        sender
            .Setup(s => s.Send(It.IsAny<UpdatePageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        sender
            .Setup(s => s.Send(It.IsAny<UpdateContentResourceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resource);
        var (pageController, resourceController) = CreateControllers(sender, [managerPermission]);

        (await pageController.UpdatePage(page.Id, new UpdatePageDto())).Result
            .Should().BeOfType<OkObjectResult>();
        (await resourceController.Update(resource.Id, new UpdateContentResourceDto())).Result
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateEndpoints_AllowSystemAdminBypass()
    {
        var sender = new Mock<ISender>();
        var page = new Page { Id = Guid.NewGuid(), Slug = "home", Title = "Home", Status = PageStatus.Published };
        sender
            .Setup(s => s.Send(It.IsAny<UpdatePageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(current => current.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(["SystemAdmin"]),
            Permissions = new HashSet<string>()
        });
        var pageController = new PageController(Mock.Of<IPageService>(), sender.Object, actorAccessor.Object);

        (await pageController.UpdatePage(page.Id, new UpdatePageDto())).Result
            .Should().BeOfType<OkObjectResult>();
    }

    private static (PageController Pages, ContentResourceController Resources) CreateControllers(Mock<ISender> sender, string[] permissions)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(current => current.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(permissions)
        });
        return (
            new PageController(Mock.Of<IPageService>(), sender.Object, actorAccessor.Object),
            new ContentResourceController(Mock.Of<IContentResourceService>(), sender.Object, actorAccessor.Object));
    }
}

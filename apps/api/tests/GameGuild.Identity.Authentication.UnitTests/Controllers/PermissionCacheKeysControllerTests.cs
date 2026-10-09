using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication.UnitTests.Controllers;

public sealed class PermissionCacheKeysControllerTests
{
    [Fact]
    public async Task GetTrackedCacheKeys_ShouldMapFiltersAndReturnOk()
    {
        var mediator = new Mock<IMediator>();
        GetPermissionCacheKeysQuery? captured = null;
        var payload = new PermissionCacheKeysDto
        {
            Items = [new PermissionCacheKeyInfoDto { Key = "acl:subj:user-1:res-1:v1:read", CacheType = "acl" }],
            TotalMatchingEntries = 1,
            TotalTrackedEntries = 1,
            Page = 2,
            PageSize = 25
        };

        mediator
            .Setup(x => x.Send(It.IsAny<GetPermissionCacheKeysQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetPermissionCacheKeysQuery)request)
            .ReturnsAsync(payload);

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.GetTrackedCacheKeys(search: "res-1", cacheType: "acl", page: 2, pageSize: 25);

        captured.Should().NotBeNull();
        captured!.Search.Should().Be("res-1");
        captured.CacheType.Should().Be("acl");
        captured.Page.Should().Be(2);
        captured.PageSize.Should().Be(25);
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(payload);
    }

    [Fact]
    public async Task GetTrackedCacheKeys_ShouldApplyDefaultPaging()
    {
        var mediator = new Mock<IMediator>();
        GetPermissionCacheKeysQuery? captured = null;

        mediator
            .Setup(x => x.Send(It.IsAny<GetPermissionCacheKeysQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetPermissionCacheKeysQuery)request)
            .ReturnsAsync(new PermissionCacheKeysDto());

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.GetTrackedCacheKeys();

        captured.Should().NotBeNull();
        captured!.Search.Should().BeNull();
        captured.CacheType.Should().BeNull();
        captured.Page.Should().Be(1);
        captured.PageSize.Should().Be(50);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task InspectTrackedCacheKey_ShouldReturnOkWithMetadata()
    {
        var mediator = new Mock<IMediator>();
        GetPermissionCacheKeyQuery? captured = null;
        var info = new PermissionCacheKeyInfoDto
        {
            Key = "acl:subj:user-1:res-1:v1:read",
            CacheType = "acl",
            PresentInL1 = true
        };

        mediator
            .Setup(x => x.Send(It.IsAny<GetPermissionCacheKeyQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetPermissionCacheKeyQuery)request)
            .ReturnsAsync(info);

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.InspectTrackedCacheKey("acl:subj:user-1:res-1:v1:read");

        captured.Should().NotBeNull();
        captured!.Key.Should().Be("acl:subj:user-1:res-1:v1:read");
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(info);
    }

    [Fact]
    public async Task InspectTrackedCacheKey_ShouldReturnNotFoundWhenKeyIsUntracked()
    {
        var mediator = new Mock<IMediator>();

        mediator
            .Setup(x => x.Send(It.IsAny<GetPermissionCacheKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PermissionCacheKeyInfoDto?)null);

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.InspectTrackedCacheKey("acl:missing");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }
}

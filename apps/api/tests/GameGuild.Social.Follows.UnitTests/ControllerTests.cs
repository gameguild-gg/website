using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Follows;
using GameGuild.Social.Follows.Commands;
using GameGuild.Social.Follows.Controllers;
using GameGuild.Social.Follows.Services;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Social.Follows.Tests;

public class ControllerTests
{
    private readonly Mock<IFollowerService> _svc = new();
    private readonly Mock<IActorContextAccessor> _actor = new();
    private readonly Mock<ILogger<FollowersController>> _log = new();
    private readonly Mock<ISender> _sender = new();

    private FollowersController CreateController(Guid? userId = null)
    {
        var uid = userId ?? Guid.NewGuid();
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = uid.ToString(),
            TenantId = Guid.NewGuid(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        return new FollowersController(
            _svc.Object,
            _actor.Object,
            _log.Object,
            _sender.Object);
    }

    private FollowersController CreateAnonymousController()
    {
        _actor.Setup(a => a.ActorContext).Returns(ActorContext.Anonymous);
        return new FollowersController(
            _svc.Object,
            _actor.Object,
            _log.Object,
            _sender.Object);
    }

    [Fact] public void Ctor_Creates() => CreateController().Should().NotBeNull();

    [Fact]
    public async Task Follow_Success_Returns201()
    {
        var uid = Guid.NewGuid(); var eid = Guid.NewGuid();
        _svc.Setup(s => s.FollowAsync(uid, eid, "User", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Follow.Create(uid, eid, "User")));
        _sender.Setup(s => s.Send(It.IsAny<FollowEntityEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Follow.Create(uid, eid, "User")));
        var r = await CreateController(uid).Follow(new FollowRequest(eid, "User", true), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Unfollow_Success_Returns204()
    {
        var uid = Guid.NewGuid(); var eid = Guid.NewGuid();
        _svc.Setup(s => s.UnfollowAsync(uid, eid, "User", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _sender.Setup(s => s.Send(It.IsAny<UnfollowEntityEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var r = await CreateController(uid).Unfollow(eid, "User", CancellationToken.None);
        r.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task IsFollowing_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.IsFollowingAsync(uid, It.IsAny<Guid>(), "User", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));
        var r = await CreateController(uid).IsFollowing(Guid.NewGuid(), "User", CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task IsFollowing_AnonymousActor_UsesEmptyUserId()
    {
        var entityId = Guid.NewGuid();
        _svc.Setup(s => s.IsFollowingAsync(Guid.Empty, entityId, "User", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(false));

        var r = await CreateAnonymousController().IsFollowing(entityId, "User", CancellationToken.None);

        r.Result.Should().BeOfType<OkObjectResult>();
        _svc.Verify(s => s.IsFollowingAsync(Guid.Empty, entityId, "User", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateNotifications_ReturnsOk()
    {
        var uid = Guid.NewGuid(); var eid = Guid.NewGuid();
        _svc.Setup(s => s.UpdateNotificationSettingsAsync(uid, eid, "User", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Follow.Create(uid, eid, "User")));
        _sender.Setup(s => s.Send(It.IsAny<UpdateFollowNotificationsEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Follow.Create(uid, eid, "User")));
        var r = await CreateController(uid).UpdateNotifications(new UpdateNotificationsRequest(eid, "User", false), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFollowers_ReturnsOk()
    {
        var eid = Guid.NewGuid();
        _svc.Setup(s => s.GetFollowersAsync(eid, "User", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<Follow>()));
        var r = await CreateController().GetFollowers(eid, "User", 0, 50, CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFollowing_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.GetFollowingAsync(uid, null, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<Follow>()));
        var r = await CreateController(uid).GetFollowing(null, 0, 50, CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFollowerCount_ReturnsOk()
    {
        _svc.Setup(s => s.GetFollowerCountAsync(It.IsAny<Guid>(), "User", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(42));
        var r = await CreateController().GetFollowerCount(Guid.NewGuid(), "User", CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFollowingCount_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.GetFollowingCountAsync(uid, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(10));
        var r = await CreateController(uid).GetFollowingCount(null, CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AreMutualFollowers_ReturnsOk()
    {
        _svc.Setup(s => s.AreMutualFollowersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));
        var r = await CreateController().AreMutualFollowers(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFollowStatusBatch_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.GetFollowStatusBatchAsync(uid, It.IsAny<IEnumerable<Guid>>(), "User", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new Dictionary<Guid, bool>()));
        var r = await CreateController(uid).GetFollowStatusBatch(new BatchStatusRequest(new[] { Guid.NewGuid() }, "User"), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFollowerCountsBatch_ReturnsOk()
    {
        _svc.Setup(s => s.GetFollowerCountsBatchAsync(It.IsAny<IEnumerable<Guid>>(), "User", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new Dictionary<Guid, int>()));
        var r = await CreateController().GetFollowerCountsBatch(new BatchCountsRequest(new[] { Guid.NewGuid() }, "User"), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPrivacySettings_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.GetPrivacySettingsAsync(uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(FollowPrivacySettings.CreateDefault(uid)));
        var r = await CreateController(uid).GetPrivacySettings(CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdatePrivacySettings_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        var req = new UpdatePrivacySettingsRequest(true, true, true, true, true, true);
        _svc.Setup(s => s.UpdatePrivacySettingsAsync(uid, true, true, true, true, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(FollowPrivacySettings.CreateDefault(uid)));
        _sender.Setup(s => s.Send(It.IsAny<UpdateFollowPrivacyEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(FollowPrivacySettings.CreateDefault(uid)));
        var r = await CreateController(uid).UpdatePrivacySettings(req, CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task BlockUser_Returns201()
    {
        var uid = Guid.NewGuid(); var bid = Guid.NewGuid();
        _svc.Setup(s => s.BlockUserAsync(uid, bid, "S", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Block.Create(uid, bid, "S")));
        _sender.Setup(s => s.Send(It.IsAny<BlockUserEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Block.Create(uid, bid, "S")));
        var r = await CreateController(uid).BlockUser(new BlockRequest(bid, "S"), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UnblockUser_Returns204()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.UnblockUserAsync(uid, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _sender.Setup(s => s.Send(It.IsAny<UnblockUserEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var r = await CreateController(uid).UnblockUser(Guid.NewGuid(), CancellationToken.None);
        r.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task IsUserBlocked_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.IsUserBlockedAsync(uid, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(false));
        var r = await CreateController(uid).IsUserBlocked(Guid.NewGuid(), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBlockedUsers_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.GetBlockedUsersAsync(uid, 0, 50, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(new List<Block>()));
        var r = await CreateController(uid).GetBlockedUsers(0, 50, CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task MuteUser_Returns201()
    {
        var uid = Guid.NewGuid(); var mid = Guid.NewGuid();
        _svc.Setup(s => s.MuteUserAsync(uid, mid, "A", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Mute.Create(uid, mid, "A")));
        _sender.Setup(s => s.Send(It.IsAny<MuteUserEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Mute.Create(uid, mid, "A")));
        var r = await CreateController(uid).MuteUser(new MuteRequest(mid, "A", null), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UnmuteUser_Returns204()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.UnmuteUserAsync(uid, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _sender.Setup(s => s.Send(It.IsAny<UnmuteUserEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var r = await CreateController(uid).UnmuteUser(Guid.NewGuid(), CancellationToken.None);
        r.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task IsUserMuted_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.IsUserMutedAsync(uid, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(false));
        var r = await CreateController(uid).IsUserMuted(Guid.NewGuid(), CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMutedUsers_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.GetMutedUsersAsync(uid, 0, 50, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(new List<Mute>()));
        var r = await CreateController(uid).GetMutedUsers(0, 50, CancellationToken.None);
        r.Result.Should().BeOfType<OkObjectResult>();
    }
}

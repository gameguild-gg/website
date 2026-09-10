using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Social.Feed.UnitTests;

public sealed class SocialFeedControllerTests
{
    [Fact]
    public async Task Get_UsesAuthenticatedActorAndRequestedScope()
    {
        var actorId = Guid.NewGuid();
        var expected = new SocialFeedPageDto(Array.Empty<SocialFeedItemDto>(), null);
        var service = new Mock<ISocialFeedQueryService>();
        service.Setup(value => value.GetAsync(
                actorId,
                FeedScope.Following,
                "opaque",
                12,
                "indiedev",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = new SocialFeedController(service.Object, Actor(actorId));

        var result = await controller.Get("following", "opaque", 12, "indiedev", default);

        result.Value.Should().BeSameAs(expected);
        service.VerifyAll();
    }

    [Fact]
    public async Task Get_WithInvalidScope_ReturnsBadRequestWithoutQuerying()
    {
        var service = new Mock<ISocialFeedQueryService>();
        var controller = new SocialFeedController(service.Object, Actor(Guid.NewGuid()));

        var result = await controller.Get("messages", null, 20, null, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Get_WithMalformedCursor_ReturnsBadRequest()
    {
        var service = new Mock<ISocialFeedQueryService>();
        service.Setup(value => value.GetAsync(
                It.IsAny<Guid>(),
                FeedScope.ForYou,
                "bad",
                20,
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidFeedCursorException());
        var controller = new SocialFeedController(service.Object, Actor(Guid.NewGuid()));

        var result = await controller.Get("for-you", "bad", 20, null, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static IActorContextAccessor Actor(Guid? userId)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(userId is null
            ? ActorContext.Anonymous
            : new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = userId.Value.ToString(),
                Roles = new HashSet<string>(),
                Permissions = new HashSet<string>(),
                IsAuthenticated = true
            });
        return accessor.Object;
    }
}

using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class RevokeTokenOwnershipTests
{
    [Theory]
    [InlineData(ActorKind.Anonymous, false, "valid")]
    [InlineData(ActorKind.User, false, "valid")]
    [InlineData(ActorKind.User, true, null)]
    [InlineData(ActorKind.User, true, "invalid")]
    [InlineData(ActorKind.User, true, "empty")]
    [InlineData(ActorKind.Service, true, "valid")]
    [InlineData(ActorKind.System, true, "valid")]
    [InlineData(ActorKind.Webhook, true, "valid")]
    [InlineData(ActorKind.External, true, "valid")]
    public async Task InvalidUserActorIsDeniedBeforeTokenLookup(ActorKind kind, bool authenticated, string? subject)
    {
        var owner = Guid.NewGuid();
        var context = ActorContext.Anonymous with
        {
            ActorKind = kind, IsAuthenticated = authenticated,
            SubjectId = subject switch { "valid" => owner.ToString(), "empty" => Guid.Empty.ToString(), _ => subject }
        };
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(value => value.ActorContext).Returns(context);
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        var tokens = new Mock<IRefreshTokenRepository>(MockBehavior.Strict);
        var hasher = new Mock<IRefreshTokenHasher>(MockBehavior.Strict);
        var handler = new RevokeTokenHandler(auth.Object, NullLogger<RevokeTokenHandler>.Instance,
            actor.Object, tokens.Object, hasher.Object);

        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => handler.Handle(
            new RevokeTokenCommand { RefreshToken = "raw-token", UserId = owner }, CancellationToken.None));

        auth.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
        hasher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("TenantAdmin")]
    [InlineData("SystemAdmin")]
    public async Task AnotherUsersTokenIsDeniedEvenWithSpoofedCommandOwnerOrAdminRole(string role)
    {
        var owner = Guid.NewGuid();
        var caller = Guid.NewGuid();
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(value => value.ActorContext).Returns(ActorContext.Anonymous with
        {
            ActorKind = ActorKind.User, IsAuthenticated = true, SubjectId = caller.ToString(),
            Roles = new HashSet<string> { role }
        });
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        var tokens = new Mock<IRefreshTokenRepository>(MockBehavior.Strict);
        var hasher = new Mock<IRefreshTokenHasher>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        hasher.Setup(value => value.HashToken("raw-token")).Returns("stored-hash");
        tokens.Setup(value => value.GetByTokenAsync("stored-hash", cancellation.Token))
            .ReturnsAsync(new RefreshToken { UserId = owner });
        var handler = new RevokeTokenHandler(auth.Object, NullLogger<RevokeTokenHandler>.Instance,
            actor.Object, tokens.Object, hasher.Object);

        var error = await Assert.ThrowsAsync<AccessDeniedException>(() => handler.Handle(
            new RevokeTokenCommand { RefreshToken = "raw-token", UserId = owner }, cancellation.Token));

        Assert.DoesNotContain(owner.ToString(), error.PublicMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token", error.PublicMessage, StringComparison.Ordinal);
        auth.VerifyNoOtherCalls();
        tokens.Verify(value => value.GetByTokenAsync("stored-hash", cancellation.Token), Times.Once);
        tokens.VerifyNoOtherCalls();
        hasher.Verify(value => value.HashToken("raw-token"), Times.Once);
        hasher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnknownTokenRetainsInvalidTokenContractWithoutDelegatingRevocation()
    {
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(value => value.ActorContext).Returns(ActorContext.Anonymous with
        {
            ActorKind = ActorKind.User, IsAuthenticated = true, SubjectId = Guid.NewGuid().ToString()
        });
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        var tokens = new Mock<IRefreshTokenRepository>();
        var hasher = new Mock<IRefreshTokenHasher>();
        hasher.Setup(value => value.HashToken("raw-token")).Returns("stored-hash");
        tokens.Setup(value => value.GetByTokenAsync("stored-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        var handler = new RevokeTokenHandler(auth.Object, NullLogger<RevokeTokenHandler>.Instance,
            actor.Object, tokens.Object, hasher.Object);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new RevokeTokenCommand { RefreshToken = "raw-token" }, CancellationToken.None));

        Assert.Equal("Invalid token", error.Message);
        auth.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OwnerDelegatesTrustedIpAndCancellationDespiteDifferentCommandUserId()
    {
        var owner = Guid.NewGuid();
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(value => value.ActorContext).Returns(ActorContext.Anonymous with
        {
            ActorKind = ActorKind.User, IsAuthenticated = true, SubjectId = owner.ToString()
        });
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        var tokens = new Mock<IRefreshTokenRepository>();
        var hasher = new Mock<IRefreshTokenHasher>();
        using var cancellation = new CancellationTokenSource();
        hasher.Setup(value => value.HashToken("raw-token")).Returns("stored-hash");
        tokens.Setup(value => value.GetByTokenAsync("stored-hash", cancellation.Token))
            .ReturnsAsync(new RefreshToken { UserId = owner });
        auth.Setup(value => value.RevokeRefreshTokenAsync("raw-token", "127.0.0.1", cancellation.Token))
            .Returns(Task.CompletedTask);
        var handler = new RevokeTokenHandler(auth.Object, NullLogger<RevokeTokenHandler>.Instance,
            actor.Object, tokens.Object, hasher.Object);

        await handler.Handle(new RevokeTokenCommand
        {
            RefreshToken = "raw-token", IpAddress = "127.0.0.1", UserId = Guid.NewGuid()
        }, cancellation.Token);

        auth.Verify(value => value.RevokeRefreshTokenAsync("raw-token", "127.0.0.1", cancellation.Token), Times.Once);
        auth.VerifyNoOtherCalls();
    }
}

using System.Net;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class RevokeAllUserTokensTests
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
    public async Task InvalidActorCannotReachAnyRevocationStore(ActorKind kind, bool authenticated, string? subject)
    {
        var fixture = new HandlerFixture();
        fixture.Actor.SetupGet(value => value.ActorContext).Returns(ActorContext.Anonymous with
        {
            ActorKind = kind, IsAuthenticated = authenticated,
            SubjectId = subject switch { "valid" => fixture.Owner.ToString(), "empty" => Guid.Empty.ToString(), _ => subject }
        });
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => fixture.Handler.Handle(
            new RevokeAllUserTokensCommand("127.0.0.1"), CancellationToken.None));
        fixture.VerifyNoStoreCalls();
    }

    [Fact]
    public async Task UnavailableUserCannotReachMutationStores()
    {
        var fixture = new HandlerFixture();
        fixture.Users.Setup(value => value.GetByIdAsync(fixture.Owner, CancellationToken.None)).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => fixture.Handler.Handle(
            new RevokeAllUserTokensCommand("127.0.0.1"), CancellationToken.None));
        fixture.Users.Verify(value => value.GetByIdAsync(fixture.Owner, CancellationToken.None), Times.Once);
        fixture.Users.VerifyNoOtherCalls();
        fixture.Tokens.VerifyNoOtherCalls();
        fixture.Sessions.VerifyNoOtherCalls();
        fixture.Revocations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AlreadyCancelledRequestCannotReachStores()
    {
        var fixture = new HandlerFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Handler.Handle(
            new RevokeAllUserTokensCommand("127.0.0.1"), cancellation.Token));
        fixture.VerifyNoStoreCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task SelfRevocationForwardsCancellationAdvancesVersionOnceAndReturnsActualSessionCount(int count)
    {
        var fixture = new HandlerFixture();
        using var cancellation = new CancellationTokenSource();
        var user = new User { Id = fixture.Owner, TokenVersion = 7 };
        var calls = ConfigureSuccess(fixture, user, count, cancellation.Token);
        Assert.Equal(count, await fixture.Handler.Handle(new RevokeAllUserTokensCommand("127.0.0.1"), cancellation.Token));
        Assert.Equal(8, user.TokenVersion);
        Assert.Equal(new[] { "refresh", "sessions", "user-update", "user-save", "legacy-store" }, calls);
        fixture.Users.Verify(value => value.GetByIdAsync(fixture.Owner, cancellation.Token), Times.Once);
        fixture.Users.Verify(value => value.UpdateAsync(user, cancellation.Token), Times.Once);
        fixture.Users.Verify(value => value.SaveChangesAsync(cancellation.Token), Times.Once);
        fixture.Tokens.Verify(value => value.RevokeAllForUserAsync(fixture.Owner, "127.0.0.1", cancellation.Token), Times.Once);
        fixture.Sessions.Verify(value => value.TerminateAllUserSessionsAsync(
            fixture.Owner, SessionTerminationReason.UserLogout, null, cancellation.Token), Times.Once);
        fixture.Revocations.Verify(value => value.RevokeAllUserTokensAsync(
            fixture.Owner, 8, "User initiated logout everywhere", cancellation.Token), Times.Once);
        fixture.Audit.Verify(value => value.RecordMutationAsync(It.Is<RefreshTokenLifecycleEvent>(audit =>
            audit.Operation == RefreshTokenLifecycleOperation.AllRevoked && audit.UserId == fixture.Owner), cancellation.Token), Times.Once);
        fixture.VerifyNoStoreCalls();
    }

    [Theory]
    [InlineData("refresh")]
    [InlineData("sessions")]
    [InlineData("user-update")]
    [InlineData("user-save")]
    [InlineData("legacy-store")]
    public async Task StoreFailuresPropagateWithoutReturningFalseSuccessOrContinuingMutations(string failure)
    {
        var fixture = new HandlerFixture();
        var user = new User { Id = fixture.Owner, TokenVersion = 7 };
        var calls = ConfigureSuccess(fixture, user, 2, CancellationToken.None, failure);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(
            new RevokeAllUserTokensCommand("127.0.0.1"), CancellationToken.None));
        Assert.Equal("Synthetic store failure", exception.Message);
        var order = new[] { "refresh", "sessions", "user-update", "user-save", "legacy-store" };
        Assert.Equal(order.Take(Array.IndexOf(order, failure) + 1), calls);
    }

    [Fact]
    public async Task AuditStorageFailureStopsBeforeLegacyRevocationAndDoesNotReturnSuccess()
    {
        var fixture = new HandlerFixture();
        var calls = ConfigureSuccess(fixture, new User { Id = fixture.Owner, TokenVersion = 7 }, 2, CancellationToken.None);
        fixture.Audit.Setup(value => value.RecordMutationAsync(It.IsAny<RefreshTokenLifecycleEvent>(), CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Synthetic audit storage failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new RevokeAllUserTokensCommand("127.0.0.1"), CancellationToken.None));
        Assert.Equal(new[] { "refresh", "sessions", "user-update", "user-save" }, calls);
        fixture.Revocations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ControllerUsesDedicatedSelfCommandWithHostObservedIpAndPreservesResponse()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        sender.Setup(value => value.Send(It.Is<RevokeAllUserTokensCommand>(command => command.IpAddress == "127.0.0.1"), cancellation.Token))
            .ReturnsAsync(2);
        var controller = new SessionController(Mock.Of<ISessionManagementService>(), sender.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        var result = Assert.IsType<OkObjectResult>(await controller.TerminateAllSessions(cancellation.Token));
        var response = Assert.IsType<SessionTerminationResponse>(result.Value);
        Assert.Equal(2, response.TerminatedCount);
        Assert.Equal("All sessions terminated successfully", response.Message);
        sender.VerifyAll();
        sender.VerifyNoOtherCalls();
    }

    private static List<string> ConfigureSuccess(HandlerFixture fixture, User user, int count, CancellationToken cancellation,
        string? failure = null)
    {
        var calls = new List<string>();
        var minimumVersion = user.TokenVersion + 1;
        Task Record(string name)
        {
            calls.Add(name);
            return name == failure ? Task.FromException(new InvalidOperationException("Synthetic store failure")) : Task.CompletedTask;
        }
        fixture.Users.Setup(value => value.GetByIdAsync(fixture.Owner, cancellation)).ReturnsAsync(user);
        fixture.Tokens.Setup(value => value.RevokeAllForUserAsync(fixture.Owner, "127.0.0.1", cancellation))
            .Returns(() => Record("refresh"));
        fixture.Sessions.Setup(value => value.TerminateAllUserSessionsAsync(fixture.Owner, SessionTerminationReason.UserLogout, null, cancellation))
            .Returns(async () => { await Record("sessions"); return count; });
        fixture.Users.Setup(value => value.UpdateAsync(user, cancellation)).Returns(() => Record("user-update"));
        fixture.Users.Setup(value => value.SaveChangesAsync(cancellation)).Returns(() => Record("user-save"));
        fixture.Revocations.Setup(value => value.RevokeAllUserTokensAsync(fixture.Owner, minimumVersion, "User initiated logout everywhere", cancellation))
            .Returns(() => Record("legacy-store"));
        return calls;
    }

    private sealed class HandlerFixture
    {
        public Guid Owner { get; } = Guid.NewGuid();
        public Mock<IActorContextAccessor> Actor { get; } = new();
        public Mock<IUserRepository> Users { get; } = new(MockBehavior.Strict);
        public Mock<IRefreshTokenRepository> Tokens { get; } = new(MockBehavior.Strict);
        public Mock<ISessionManagementService> Sessions { get; } = new(MockBehavior.Strict);
        public Mock<IVersionedUserTokenRevocationService> Revocations { get; } = new(MockBehavior.Strict);
        public Mock<IRefreshTokenLifecycleRecorder> Audit { get; } = new();
        public RevokeAllUserTokensHandler Handler { get; }

        public HandlerFixture()
        {
            Actor.SetupGet(value => value.ActorContext).Returns(ActorContext.Anonymous with
            {
                ActorKind = ActorKind.User, IsAuthenticated = true, SubjectId = Owner.ToString(),
                Roles = new HashSet<string> { "SystemAdmin" }
            });
            Handler = new RevokeAllUserTokensHandler(Actor.Object, Users.Object, Tokens.Object, Sessions.Object, Revocations.Object, Audit.Object);
        }

        public void VerifyNoStoreCalls()
        {
            Users.VerifyNoOtherCalls();
            Tokens.VerifyNoOtherCalls();
            Sessions.VerifyNoOtherCalls();
            Revocations.VerifyNoOtherCalls();
            Audit.VerifyNoOtherCalls();
        }
    }
}

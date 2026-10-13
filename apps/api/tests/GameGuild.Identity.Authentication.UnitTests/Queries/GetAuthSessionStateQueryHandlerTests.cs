using System.Security.Claims;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Queries;

/// <summary>
///     Issue #221: the self-service session-state surface reads the authenticated principal's
///     session, revocation and MFA infrastructure without trusting any caller-supplied identity.
/// </summary>
public class GetAuthSessionStateQueryHandlerTests
{
    private static readonly Guid User = Guid.Parse("40000000-0000-0000-0000-000000000390");
    private static readonly Guid Session = Guid.Parse("40000000-0000-0000-0000-000000000391");
    private readonly Mock<ISessionManagementService> _sessions = new();
    private readonly Mock<IUserMfaConfigurationRepository> _mfaConfigurations = new();
    private readonly Mock<IVersionedUserTokenRevocationService> _tokenRevocations = new();
    private readonly GetAuthSessionStateQueryHandler _handler;

    public GetAuthSessionStateQueryHandlerTests()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(instance => instance.HttpContext).Returns(() => new DefaultHttpContext
        {
            User = Principal(User, Session)
        });
        _handler = new GetAuthSessionStateQueryHandler(
            accessor.Object, _sessions.Object, _mfaConfigurations.Object, _tokenRevocations.Object);
    }

    private static ClaimsPrincipal Principal(
        Guid? userId, Guid? sessionId, string? tokenVersion = "3", bool mfaVerified = true)
    {
        var claims = new List<Claim>();
        if (userId.HasValue) { claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString("D"))); }
        if (sessionId.HasValue) { claims.Add(new Claim(JwtClaimTypes.SessionId, sessionId.Value.ToString("D"))); }
        if (tokenVersion is not null) { claims.Add(new Claim("token_version", tokenVersion)); }
        if (mfaVerified) { claims.Add(new Claim(ClaimNames.MfaVerified, "true")); }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "unit-test", ClaimTypes.NameIdentifier, null));
    }

    private static UserSession ActiveSession(Guid id, DateTime? expiresAt = null) => new()
    {
        Id = id, UserId = User, RefreshToken = "hash", IpAddress = "127.0.0.1", UserAgent = "unit",
        CreatedAt = SystemClock.UtcNow.AddDays(-1), UpdatedAt = SystemClock.UtcNow,
        LastUsedAt = SystemClock.UtcNow.AddMinutes(-5), ExpiresAt = expiresAt ?? SystemClock.UtcNow.AddDays(6),
        IsActive = true
    };

    private void SetupSessions(List<UserSession> active, UserSession? bound)
    {
        _sessions.Setup(service => service.GetUserSessionsAsync(User, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);
        if (bound is not null)
        {
            _sessions.Setup(service => service.GetSessionAsync(bound.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(bound);
        }
    }

    [Fact]
    public async Task ReportsTheFullStateForAValidSessionBoundToken()
    {
        var session = ActiveSession(Session);
        SetupSessions([session, ActiveSession(Guid.NewGuid())], session);
        _mfaConfigurations.Setup(repository => repository.GetByUserIdAsync(User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserMfaConfiguration { IsEnabled = true });

        var state = await _handler.Handle(new GetAuthSessionStateQuery(), CancellationToken.None);

        state.UserId.Should().Be(User);
        state.SessionId.Should().Be(Session);
        state.SessionValid.Should().BeTrue();
        state.SessionExpiresAtUtc.Should().Be(session.ExpiresAt);
        state.SessionLastUsedAtUtc.Should().Be(session.LastUsedAt);
        state.SessionTerminationReason.Should().BeNull();
        state.TokenVersion.Should().Be(3);
        state.ActiveSessionCount.Should().Be(2);
        state.MfaEnrolled.Should().BeTrue();
        state.MfaVerifiedInCurrentToken.Should().BeTrue();
    }

    [Fact]
    public async Task TerminatedSessionsAreReportedInvalid()
    {
        var session = ActiveSession(Session);
        session.IsActive = false;
        session.TerminationReason = "revoked-by-user";
        session.TerminatedAt = SystemClock.UtcNow;
        SetupSessions([], session);

        var state = await _handler.Handle(new GetAuthSessionStateQuery(), CancellationToken.None);

        state.SessionValid.Should().BeFalse();
        state.SessionTerminationReason.Should().Be("revoked-by-user");
        state.ActiveSessionCount.Should().Be(0);
    }

    [Fact]
    public async Task ExpiredSessionsAreReportedInvalid()
    {
        var session = ActiveSession(Session, SystemClock.UtcNow.AddMinutes(-1));
        SetupSessions([], session);

        var state = await _handler.Handle(new GetAuthSessionStateQuery(), CancellationToken.None);

        state.SessionValid.Should().BeFalse();
    }

    [Fact]
    public async Task TokensWithoutASessionBindingReportNoSession()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(instance => instance.HttpContext).Returns(new DefaultHttpContext { User = Principal(User, null) });
        var handler = new GetAuthSessionStateQueryHandler(
            accessor.Object, _sessions.Object, _mfaConfigurations.Object, _tokenRevocations.Object);
        _sessions.Setup(service => service.GetUserSessionsAsync(User, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ActiveSession(Guid.NewGuid())]);

        var state = await handler.Handle(new GetAuthSessionStateQuery(), CancellationToken.None);

        state.SessionId.Should().BeNull();
        state.SessionValid.Should().BeFalse();
        state.ActiveSessionCount.Should().Be(1);
    }

    [Fact]
    public async Task UnenrolledMfaIsReportedFalse()
    {
        var session = ActiveSession(Session);
        SetupSessions([session], session);
        _mfaConfigurations.Setup(repository => repository.GetByUserIdAsync(User, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration?) null);

        var state = await _handler.Handle(new GetAuthSessionStateQuery(), CancellationToken.None);

        state.MfaEnrolled.Should().BeFalse();
        state.MfaVerifiedInCurrentToken.Should().BeTrue();
    }

    [Fact]
    public async Task UnauthenticatedPrincipalsFailClosed()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(instance => instance.HttpContext).Returns(new DefaultHttpContext());
        var handler = new GetAuthSessionStateQueryHandler(
            accessor.Object, _sessions.Object, _mfaConfigurations.Object, _tokenRevocations.Object);

        var act = () => handler.Handle(new GetAuthSessionStateQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task PrincipalsWithoutAResolvableUserIdFailClosed()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(instance => instance.HttpContext)
            .Returns(new DefaultHttpContext { User = Principal(null, Session) });
        var handler = new GetAuthSessionStateQueryHandler(
            accessor.Object, _sessions.Object, _mfaConfigurations.Object, _tokenRevocations.Object);

        var act = () => handler.Handle(new GetAuthSessionStateQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _sessions.Verify(service => service.GetUserSessionsAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

using FluentAssertions;
using GameGuild.API.Core.Security;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.API.UnitTests.Security;

public class RiskEventSessionRevocationHandlerTests
{
    private const string Email = "risk-revocation-owner@example.test";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<ISessionManagementService> _sessions = new();
    private readonly Mock<IVersionedUserTokenRevocationService> _tokenRevocation = new();
    private readonly Mock<IRefreshTokenLifecycleRecorder> _lifecycleRecorder = new();
    private readonly Mock<IAuthenticationAuditEventSink> _auditEventSink = new();
    private readonly User _user = User.Create(Email, "Risk revocation owner");

    public RiskEventSessionRevocationHandlerTests()
    {
        _user.Id = Guid.NewGuid();
        _user.Username = "risk-revocation-owner";
        _users.Setup(repository => repository.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);
        _sessions.Setup(service => service.TerminateAllUserSessionsAsync(
                It.IsAny<Guid>(), It.IsAny<SessionTerminationReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    [Theory]
    [InlineData(SecurityAlertKinds.ImpossibleTravel)]
    [InlineData(SecurityAlertKinds.BruteForceDetected)]
    public async Task HandleAsync_RevokesEverything_OnConfirmedCompromiseKinds(string alertKind)
    {
        var @event = NewEvent(alertKind);
        var initialTokenVersion = _user.TokenVersion;

        await CreateSut().HandleAsync(@event);

        _refreshTokens.Verify(repository => repository.RevokeAllForUserAsync(
                _user.Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _sessions.Verify(service => service.TerminateAllUserSessionsAsync(
                _user.Id, SessionTerminationReason.SecurityViolation, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _users.Verify(repository => repository.UpdateAsync(_user, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _tokenRevocation.Verify(service => service.RevokeAllUserTokensAsync(
                _user.Id, initialTokenVersion + 1, It.Is<string>(reason =>
                    reason.Contains(alertKind, StringComparison.Ordinal)), It.IsAny<CancellationToken>()),
            Times.Once);
        _user.TokenVersion.Should().Be(initialTokenVersion + 1);
        _lifecycleRecorder.Verify(recorder => recorder.RecordMutationAsync(
                It.Is<RefreshTokenLifecycleEvent>(lifecycleEvent =>
                    lifecycleEvent.Operation == RefreshTokenLifecycleOperation.AllRevoked
                    && lifecycleEvent.UserId == _user.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _auditEventSink.Verify(sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.RiskEventRevocation"
                    && auditEvent.UserId == _user.Id
                    && auditEvent.Success),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_ForStepUpKind()
    {
        await CreateSut().HandleAsync(NewEvent(SecurityAlertKinds.LoginStepUpRequired));

        VerifyNoRevocation();
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_ForUnknownKind()
    {
        await CreateSut().HandleAsync(NewEvent("SomeFuturePreventionSignal"));

        VerifyNoRevocation();
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenRevocationIsDisabled()
    {
        await CreateSut(enabled: false).HandleAsync(NewEvent(SecurityAlertKinds.ImpossibleTravel));

        VerifyNoRevocation();
        _users.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenUserIsMissing()
    {
        _users.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await CreateSut().HandleAsync(NewEvent(SecurityAlertKinds.ImpossibleTravel));

        _refreshTokens.Verify(repository => repository.RevokeAllForUserAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.Verify(service => service.TerminateAllUserSessionsAsync(
                It.IsAny<Guid>(), It.IsAny<SessionTerminationReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _tokenRevocation.Verify(service => service.RevokeAllUserTokensAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _auditEventSink.Verify(sink => sink.RecordAsync(
                It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenUserIsDeleted()
    {
        _user.DeletedAt = DateTime.UtcNow;

        await CreateSut().HandleAsync(NewEvent(SecurityAlertKinds.BruteForceDetected));

        VerifyNoRevocation();
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenUserIdIsEmpty()
    {
        await CreateSut().HandleAsync(NewEvent(SecurityAlertKinds.ImpossibleTravel, userId: Guid.Empty));

        _users.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoRevocation();
    }

    [Fact]
    public async Task HandleAsync_StillRevokes_WhenAuditSinkThrows()
    {
        _auditEventSink.Setup(sink => sink.RecordAsync(
                It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit transport unavailable"));

        var act = () => CreateSut().HandleAsync(NewEvent(SecurityAlertKinds.ImpossibleTravel));

        await act.Should().NotThrowAsync();
        _refreshTokens.Verify(repository => repository.RevokeAllForUserAsync(
                _user.Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _tokenRevocation.Verify(service => service.RevokeAllUserTokensAsync(
                _user.Id, It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MapsPlatformTenantSentinelToNullForLifecycleEvidence()
    {
        var tenantId = Guid.NewGuid();
        var @event = NewEvent(SecurityAlertKinds.ImpossibleTravel, tenantId: tenantId);

        await CreateSut().HandleAsync(@event);

        _lifecycleRecorder.Verify(recorder => recorder.RecordMutationAsync(
                It.Is<RefreshTokenLifecycleEvent>(lifecycleEvent => lifecycleEvent.TenantId == tenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _auditEventSink.Verify(sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent => auditEvent.TenantId == tenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private RiskEventSessionRevocationHandler CreateSut(bool enabled = true) =>
        new(_users.Object,
            _refreshTokens.Object,
            _sessions.Object,
            _tokenRevocation.Object,
            _lifecycleRecorder.Object,
            _auditEventSink.Object,
            BuildConfiguration(enabled),
            NullLogger<RiskEventSessionRevocationHandler>.Instance);

    private static IConfiguration BuildConfiguration(bool enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:RiskEventRevocation:Enabled"] = enabled.ToString()
            })
            .Build();

    private SuspiciousLoginDetectedV1 NewEvent(
            string alertKind,
            Guid? userId = null,
            Guid? tenantId = null) =>
        new(userId ?? _user.Id, alertKind, nameof(RiskLevel.High), 75)
        {
            TenantId = tenantId ?? DurableIntegrationEventTenants.Platform,
            ActorId = DurableIntegrationEventActors.System,
            AggregateType = "User",
            AggregateId = (userId ?? _user.Id).ToString()
        };

    private void VerifyNoRevocation()
    {
        _refreshTokens.Verify(repository => repository.RevokeAllForUserAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.Verify(service => service.TerminateAllUserSessionsAsync(
                It.IsAny<Guid>(), It.IsAny<SessionTerminationReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _users.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tokenRevocation.Verify(service => service.RevokeAllUserTokensAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _lifecycleRecorder.Verify(recorder => recorder.RecordMutationAsync(
                It.IsAny<RefreshTokenLifecycleEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _auditEventSink.Verify(sink => sink.RecordAsync(
                It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

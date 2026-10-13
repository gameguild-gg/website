using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Sign-in compliance gate integration (issue #267): the gate must run after
///     credential validation but before any token is generated or any session is
///     created, and every non-allow decision must be audited without leaking
///     verification detail.
/// </summary>
public sealed class SignInComplianceGateTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepoMock = new();
    private readonly Mock<IRefreshTokenLineageRepository> _tokenLineageRepoMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock = new();
    private readonly Mock<IAuthAttemptService> _authAttemptServiceMock = new();
    private readonly Mock<IAuthenticationAnomalyDetectionService> _anomalyDetectionMock = new();
    private readonly Mock<IAuthenticationAuditEventSink> _authenticationAuditEventSinkMock = new();
    private readonly Mock<IUserEnumerationProtectionService> _enumerationProtectionMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ISessionManagementService> _sessionManagementServiceMock = new();
    private readonly Mock<ISignInCompliancePolicy> _compliancePolicyMock = new();
    private readonly Mock<ISignInMfaService> _signInMfaMock = SignInMfaPreparationStub.CreateMock();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly User _user = User.CreateWithPassword(
        "compliance@example.com", "testuser", BCrypt.Net.BCrypt.HashPassword("Password1!"));

    public SignInComplianceGateTests()
    {
        PersistedAuthenticationSessions.Configure(_sessionManagementServiceMock);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "ComplianceTestAgent/1.0";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        _authAttemptServiceMock.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("198.51.100.23");
        _enumerationProtectionMock.Setup(x => x.GetGenericErrorMessage(It.IsAny<string>())).Returns("Authentication failed");
        _enumerationProtectionMock.Setup(x => x.BeginAuthenticationTiming()).Returns(new AuthenticationTimingScope());
        _enumerationProtectionMock.Setup(x => x.AddTimingProtectionDelayAsync(It.IsAny<AuthenticationTimingScope>(), It.IsAny<CredentialWorkClassification>())).Returns(Task.CompletedTask);
        _anomalyDetectionMock
            .Setup(x => x.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        _anomalyDetectionMock
            .Setup(x => x.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalBehavior = true });
        _anomalyDetectionMock
            .Setup(x => x.RecordSuspiciousActivityAsync(It.IsAny<SuspiciousActivity>()))
            .Returns(Task.CompletedTask);
        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns((string token) => $"hash-{token}");
        _authenticationAuditEventSinkMock
            .Setup(x => x.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");
        _userRepoMock.Setup(x => x.GetByEmailAsync("compliance@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _userRepoMock.Setup(x => x.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _senderMock
            .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships =
                [
                    new UserMembershipDto
                    {
                        TenantId = _tenantId,
                        TenantName = "Default tenant",
                        TenantSlug = "default-tenant",
                        TenantIsActive = true,
                        Role = "Member",
                        IsActive = true
                    }
                ]
            });
        _senderMock
            .Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        _compliancePolicyMock
            .Setup(policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SignInComplianceDecision.Allow);
    }

    private LocalAuthService CreateSut(SignInComplianceGateOptions? gateOptions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var passwordHasher = new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration);

        return new LocalAuthService(
            _userRepoMock.Object,
            _refreshTokenRepoMock.Object,
            _tokenLineageRepoMock.Object,
            _jwtTokenServiceMock.Object,
            _refreshTokenHasherMock.Object,
            configuration,
            _authAttemptServiceMock.Object,
            passwordHasher,
            _anomalyDetectionMock.Object,
            _enumerationProtectionMock.Object,
            _httpContextAccessorMock.Object,
            NullLogger<LocalAuthService>.Instance,
            _senderMock.Object,
            _sessionManagementServiceMock.Object,
            _signInMfaMock.Object,
            auditEventSink: _authenticationAuditEventSinkMock.Object,
            complianceGateOptions: gateOptions is null ? null : Options.Create(gateOptions),
            compliancePolicy: _compliancePolicyMock.Object);
    }

    private LocalSignInRequest ValidRequest => new()
    {
        Email = "compliance@example.com",
        Password = "Password1!",
        DeviceFingerprint = "device-fingerprint-42"
    };

    private static SignInComplianceGateOptions Gate(SignInComplianceGateMode mode) => new()
    {
        Enabled = true,
        Mode = mode
    };

    private void SetupPolicyDecision(SignInComplianceDecision decision) =>
        _compliancePolicyMock
            .Setup(policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);

    [Fact]
    public async Task GateDisabledByDefault_PolicyIsNotEvaluatedAndSignInSucceeds()
    {
        var sut = CreateSut(gateOptions: null);

        var result = await sut.LocalSignInAsync(ValidRequest);

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        _compliancePolicyMock.Verify(
            policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sessionManagementServiceMock.Verify(
            sessions => sessions.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GateEnabledWithModeOff_PolicyIsNotEvaluated()
    {
        var sut = CreateSut(new SignInComplianceGateOptions { Enabled = true, Mode = SignInComplianceGateMode.Off });

        var result = await sut.LocalSignInAsync(ValidRequest);

        result.Success.Should().BeTrue();
        _compliancePolicyMock.Verify(
            policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PolicyDenies_ThrowsAccessDeniedBeforeAnySessionOrTokenIsIssued()
    {
        var sut = CreateSut(Gate(SignInComplianceGateMode.Enforce));
        SetupPolicyDecision(SignInComplianceDecision.Deny(SignInComplianceReasons.ComplianceHold));

        var exception = await Assert.ThrowsAsync<AccessDeniedException>(() => sut.LocalSignInAsync(ValidRequest));

        exception.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        _jwtTokenServiceMock.Verify(
            tokens => tokens.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _jwtTokenServiceMock.Verify(
            tokens => tokens.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sessionManagementServiceMock.Verify(
            sessions => sessions.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _authenticationAuditEventSinkMock.Verify(
            sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.ComplianceDenied" &&
                    auditEvent.UserId == _user.Id &&
                    auditEvent.Success == false &&
                    auditEvent.TenantId == _tenantId &&
                    auditEvent.ErrorMessage == SignInComplianceReasons.ComplianceHold),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PolicyChallenges_ReturnsStepUpBeforeAnySessionOrTokenIsIssued()
    {
        var sut = CreateSut(Gate(SignInComplianceGateMode.Enforce));
        SetupPolicyDecision(SignInComplianceDecision.Challenge(SignInComplianceReasons.VerificationPending));

        var result = await sut.LocalSignInAsync(ValidRequest);

        result.Success.Should().BeFalse();
        result.RequiresStepUp.Should().BeTrue();
        result.StepUpToken.Should().NotBeNullOrEmpty();
        result.StepUpExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.UserId.Should().Be(_user.Id);
        result.TenantId.Should().Be(_tenantId);
        _jwtTokenServiceMock.Verify(
            tokens => tokens.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _jwtTokenServiceMock.Verify(
            tokens => tokens.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sessionManagementServiceMock.Verify(
            sessions => sessions.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _authenticationAuditEventSinkMock.Verify(
            sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.ComplianceChallengeRequired" &&
                    auditEvent.UserId == _user.Id &&
                    auditEvent.Success == false &&
                    auditEvent.ErrorMessage == SignInComplianceReasons.VerificationPending),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChallengeOnlyMode_DowngradesDenyToAStepUpChallenge()
    {
        var sut = CreateSut(Gate(SignInComplianceGateMode.ChallengeOnly));
        SetupPolicyDecision(SignInComplianceDecision.Deny(SignInComplianceReasons.VerificationRejected));

        var result = await sut.LocalSignInAsync(ValidRequest);

        result.Success.Should().BeFalse();
        result.RequiresStepUp.Should().BeTrue();
        result.StepUpToken.Should().NotBeNullOrEmpty();
        _sessionManagementServiceMock.Verify(
            sessions => sessions.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _authenticationAuditEventSinkMock.Verify(
            sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.ComplianceChallengeRequired" &&
                    auditEvent.ErrorMessage == SignInComplianceReasons.VerificationRejected),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluationFailureUnderEnforce_FailsClosed()
    {
        var sut = CreateSut(Gate(SignInComplianceGateMode.Enforce));
        _compliancePolicyMock
            .Setup(policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("compliance signals unavailable"));

        await Assert.ThrowsAsync<AccessDeniedException>(() => sut.LocalSignInAsync(ValidRequest));

        _sessionManagementServiceMock.Verify(
            sessions => sessions.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _authenticationAuditEventSinkMock.Verify(
            sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.ComplianceDenied" &&
                    auditEvent.ErrorMessage == SignInComplianceReasons.EvaluationFailed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluationFailureUnderChallengeOnly_FailsOpen()
    {
        var sut = CreateSut(Gate(SignInComplianceGateMode.ChallengeOnly));
        _compliancePolicyMock
            .Setup(policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("compliance signals unavailable"));

        var result = await sut.LocalSignInAsync(ValidRequest);

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        _sessionManagementServiceMock.Verify(
            sessions => sessions.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PolicyReceivesTheSignInComplianceContext()
    {
        var sut = CreateSut(Gate(SignInComplianceGateMode.Enforce));
        SignInComplianceContext? capturedContext = null;
        _compliancePolicyMock
            .Setup(policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), It.IsAny<CancellationToken>()))
            .Callback((SignInComplianceContext context, CancellationToken _) => capturedContext = context)
            .ReturnsAsync(SignInComplianceDecision.Allow);

        await sut.LocalSignInAsync(ValidRequest);

        capturedContext.Should().NotBeNull();
        capturedContext!.UserId.Should().Be(_user.Id);
        capturedContext.TenantId.Should().Be(_tenantId);
        capturedContext.IpAddress.Should().Be("198.51.100.23");
        capturedContext.DeviceFingerprint.Should().Be("device-fingerprint-42");
    }

    [Fact]
    public async Task ComplianceAuditMetadata_OnlyCarriesCoarseGateFields()
    {
        var sut = CreateSut(Gate(SignInComplianceGateMode.Enforce));
        SetupPolicyDecision(SignInComplianceDecision.Deny(SignInComplianceReasons.VerificationSuspended));
        AuthenticationAuditEvent? capturedAuditEvent = null;
        _authenticationAuditEventSinkMock
            .Setup(sink => sink.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback((AuthenticationAuditEvent auditEvent, CancellationToken _) => capturedAuditEvent = auditEvent)
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<AccessDeniedException>(() => sut.LocalSignInAsync(ValidRequest));

        capturedAuditEvent.Should().NotBeNull();
        capturedAuditEvent!.Metadata.Should().NotBeNull();
        var metadataProperties = capturedAuditEvent.Metadata!.GetType().GetProperties().Select(property => property.Name).ToArray();
        metadataProperties.Should().BeEquivalentTo(new[] { "Reason", "GateMode", "CorrelationId" });
        var reason = (string)capturedAuditEvent.Metadata.GetType().GetProperty("Reason")!.GetValue(capturedAuditEvent.Metadata)!;
        reason.Should().Be(SignInComplianceReasons.VerificationSuspended);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NonAllowComplianceDecision_PreventsMfaChallengeFromBypassingTheGate(bool deny)
    {
        ConfigureMfaChallenge();
        SetupPolicyDecision(deny
            ? SignInComplianceDecision.Deny(SignInComplianceReasons.ComplianceHold)
            : SignInComplianceDecision.Challenge(SignInComplianceReasons.VerificationPending));
        var sut = CreateSut(Gate(SignInComplianceGateMode.Enforce));

        if (deny)
        {
            await Assert.ThrowsAsync<AccessDeniedException>(() => sut.LocalSignInAsync(ValidRequest));
        }
        else
        {
            var result = await sut.LocalSignInAsync(ValidRequest);
            result.RequiresStepUp.Should().BeTrue();
            result.RequiresMfa.Should().BeFalse();
            result.MfaToken.Should().BeNull();
        }

        _compliancePolicyMock.Verify(
            policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoMfaPreparation();
        VerifyNoOrdinaryCredentials();
    }

    [Fact]
    public async Task AllowedComplianceDecision_IsEvaluatedBeforeReturningTheMfaChallenge()
    {
        var order = new List<string>();
        _compliancePolicyMock
            .Setup(policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("compliance"))
            .ReturnsAsync(SignInComplianceDecision.Allow);
        _signInMfaMock
            .Setup(service => service.PrepareAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Guid?>(),
                It.IsAny<DeviceInfo>(), It.IsAny<SignInFirstFactor>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("mfa"))
            .ReturnsAsync(CreateMfaChallenge());
        var sut = CreateSut(Gate(SignInComplianceGateMode.Enforce));

        var result = await sut.LocalSignInAsync(ValidRequest);

        order.Should().Equal("compliance", "mfa");
        result.RequiresMfa.Should().BeTrue();
        result.MfaToken.Should().NotBeNullOrEmpty();
        VerifyNoOrdinaryCredentials();
    }

    [Fact]
    public async Task CancelledComplianceEvaluation_PropagatesCancellationBeforeMfaOrCredentials()
    {
        using var cancellation = new CancellationTokenSource();
        _compliancePolicyMock
            .Setup(policy => policy.EvaluateAsync(It.IsAny<SignInComplianceContext>(), cancellation.Token))
            .Callback(() => cancellation.Cancel())
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var sut = CreateSut(Gate(SignInComplianceGateMode.Enforce));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.LocalSignInAsync(ValidRequest, cancellation.Token));

        VerifyNoMfaPreparation();
        VerifyNoOrdinaryCredentials();
        _authenticationAuditEventSinkMock.Verify(
            sink => sink.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private SignInMfaPreparation CreateMfaChallenge() => SignInMfaPreparation.WithOutcome(
        new SignInMfaPendingResponse(SignInMfaChallengeToken.Create(), DateTimeOffset.UtcNow.AddMinutes(5),
            SignInMfaPurpose.VerifyFactor, false, _user, _tenantId));

    private void ConfigureMfaChallenge() => _signInMfaMock
        .Setup(service => service.PrepareAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Guid?>(),
            It.IsAny<DeviceInfo>(), It.IsAny<SignInFirstFactor>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(CreateMfaChallenge());

    private void VerifyNoMfaPreparation() => _signInMfaMock.Verify(
        service => service.PrepareAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Guid?>(),
            It.IsAny<DeviceInfo>(), It.IsAny<SignInFirstFactor>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
        Times.Never);

    private void VerifyNoOrdinaryCredentials()
    {
        _jwtTokenServiceMock.Verify(
            tokens => tokens.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(),
                It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _jwtTokenServiceMock.Verify(
            tokens => tokens.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionManagementServiceMock.Verify(
            sessions => sessions.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }
}

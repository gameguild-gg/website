using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class AuthenticationOrchestrationServiceTests : IDisposable
{
    private const string TrustedFingerprint = "trusted-fingerprint";
    private const string UnknownFingerprint = "unknown-fingerprint";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IAuthAttemptService> _authAttemptService = new();
    private readonly Mock<IUserEnumerationProtectionService> _enumerationProtection = new();
    private readonly Mock<IAuthenticationAnomalyDetectionService> _anomalyDetection = new();
    private readonly Mock<ITrustedDeviceRepository> _trustedDevices = new();
    private readonly Mock<ITotpMfaService> _totpMfa = new();
    private readonly Mock<IBackupCodeMfaService> _backupCodeMfa = new();
    private readonly Mock<IWebAuthnAuthenticationService> _webAuthn = new();
    private readonly Mock<IMfaAttemptTrackingService> _mfaAttemptTracking = new();
    private readonly Mock<IUserMfaConfigurationRepository> _userMfaConfiguration = new();
    private readonly Mock<IRoleRepository> _roleRepository = new();
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();

    private readonly InMemoryFlowStateStore _flowStates = new();
    private readonly User _user = new() { Id = Guid.NewGuid(), Email = "user@example.com", IsActive = true, PasswordHash = "hash" };
    private readonly MutableTimeProvider _clock = new();
    private readonly AuthenticationOrchestrationService _sut;

    public AuthenticationOrchestrationServiceTests()
    {
        _userRepository
            .Setup(repository => repository.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);
        _userRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);
        _passwordHasher
            .Setup(hasher => hasher.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        _passwordHasher
            .Setup(hasher => hasher.VerifyPasswordWithWorkClassification(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new PasswordVerificationResult(IsValid: true, WorkPerformed: true));
        _enumerationProtection
            .Setup(protection => protection.BeginAuthenticationTiming())
            .Returns(new AuthenticationTimingScope());
        _enumerationProtection
            .Setup(protection => protection.AddTimingProtectionDelayAsync(It.IsAny<AuthenticationTimingScope>(), It.IsAny<CredentialWorkClassification>()))
            .Returns(Task.CompletedTask);
        _enumerationProtection
            .Setup(protection => protection.GetGenericErrorMessage(It.IsAny<string>()))
            .Returns("Authentication failed");
        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskScore = 0 });
        _trustedDevices
            .Setup(devices => devices.IsDeviceTrustedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _totpMfa
            .Setup(service => service.VerifyTotpAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _backupCodeMfa
            .Setup(service => service.VerifyBackupCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _webAuthn
            .Setup(service => service.CompleteAuthenticationAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = true, UserId = _user.Id });
        _mfaAttemptTracking
            .Setup(service => service.IsUserLockedOutAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mfaAttemptTracking
            .Setup(service => service.RecordMfaAttemptAsync(It.IsAny<Guid>(), It.IsAny<MfaMethod>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        _mfaAttemptTracking
            .Setup(service => service.RecordMfaAttemptAsync(It.IsAny<Guid>(), It.IsAny<MfaMethod>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mfaAttemptTracking
            .Setup(service => service.RecordFailedMfaAttemptAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<MfaMethod>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        _mfaAttemptTracking
            .Setup(service => service.RecordFailedMfaAttemptAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<MfaMethod>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userMfaConfiguration
            .Setup(repository => repository.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration?)null);
        _roleRepository
            .Setup(repository => repository.GetUserRolesAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Role { Name = "Member", IsActive = true }]);
        _jwtTokenService
            .Setup(service => service.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("orchestrated-access-token");
        _jwtTokenService
            .Setup(service => service.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("orchestrated-refresh-token");

        _sut = new AuthenticationOrchestrationService(
            _flowStates,
            _userRepository.Object,
            _passwordHasher.Object,
            _authAttemptService.Object,
            _enumerationProtection.Object,
            _anomalyDetection.Object,
            _trustedDevices.Object,
            _totpMfa.Object,
            _backupCodeMfa.Object,
            _webAuthn.Object,
            _mfaAttemptTracking.Object,
            _userMfaConfiguration.Object,
            _roleRepository.Object,
            _jwtTokenService.Object,
            Options.Create(new JwtOptions()),
            NullLogger<AuthenticationOrchestrationService>.Instance);

        SystemClock.SetProvider(_clock);
    }

    public void Dispose()
    {
        SystemClock.Reset();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Initiate + happy path
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Initiate_WithValidPrimaryCredentialAndLowRisk_PersistsFlowWithoutExtraChallenges()
    {
        var request = CreateRequest(deviceFingerprint: TrustedFingerprint);

        var state = await _sut.InitiateAuthenticationAsync(request);

        state.FlowId.Should().NotBe(Guid.Empty);
        state.UserId.Should().Be(_user.Id);
        state.RequiredSteps.Should().BeEquivalentTo([AuthenticationStep.PrimaryCredential]);
        state.CompletedSteps.Should().BeEquivalentTo([AuthenticationStep.PrimaryCredential]);
        state.RiskScore.Should().Be(0);
        state.IsExpired.Should().BeFalse();
        state.ExpiresAt.Should().Be(_clock.FrozenUtcNow.Add(AuthenticationOrchestrationService.FlowLifetime).UtcDateTime);
        state.CompletedSteps.Should().Contain(state.RequiredSteps);

        _passwordHasher.Verify(hasher => hasher.VerifyPasswordWithWorkClassification("hash", "correct-password"), Times.Once);
        _userRepository.Verify(repository => repository.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()), Times.Once);
        _authAttemptService.Verify(
            service => service.RecordSuccessfulAttemptAsync("user@example.com", _user.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), "Local"),
            Times.Once);
        _flowStates.Stored.Count.Should().Be(1);
    }

    [Fact]
    public async Task Complete_OnSatisfiedFlow_IssuesTokensThroughJwtTokenService()
    {
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint));

        var result = await _sut.CompleteAuthenticationAsync(state.FlowId);

        result.IsSuccess.Should().BeTrue();
        result.UserId.Should().Be(_user.Id);
        result.AccessToken.Should().Be("orchestrated-access-token");
        result.RefreshToken.Should().Be("orchestrated-refresh-token");
        result.SessionId.Should().NotBeNull();
        result.TokenExpiresAt.Should().Be(_clock.FrozenUtcNow.AddMinutes(new JwtOptions().AccessTokenExpirationMinutes).UtcDateTime);
        result.DeviceTrusted.Should().BeTrue();

        _jwtTokenService.Verify(
            service => service.GenerateAccessTokenAsync(_user.Id, _user.Email, It.Is<string[]>(roles => roles.SequenceEqual(new[] { "Member" })), It.IsAny<Guid?>(), It.IsAny<int>(), result.SessionId!.Value, It.IsAny<CancellationToken>()),
            Times.Once);
        _jwtTokenService.Verify(
            service => service.GenerateRefreshTokenAsync(_user.Id, It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var persisted = await _flowStates.GetByFlowIdAsync(state.FlowId);
        persisted!.IsComplete.Should().BeTrue();
        persisted.CompletedAt.Should().NotBeNull();

        var after = await _sut.GetFlowStateAsync(state.FlowId);
        after!.IsComplete.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Risk ladder
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0.7, new[] { AuthenticationStep.MfaVerification, AuthenticationStep.RiskChallenge })]
    [InlineData(0.95, new[] { AuthenticationStep.MfaVerification, AuthenticationStep.RiskChallenge })]
    [InlineData(0.5, new[] { AuthenticationStep.MfaVerification })]
    [InlineData(0.69, new[] { AuthenticationStep.MfaVerification })]
    [InlineData(0.49, new AuthenticationStep[0])]
    public async Task DetermineRequiredChallenges_AppliesRiskThresholdLadder(double riskScore, AuthenticationStep[] expected)
    {
        var challenges = await _sut.DetermineRequiredChallengesAsync(
            _user.Id,
            riskScore,
            new AuthenticationAttemptContext { DeviceFingerprint = TrustedFingerprint });

        challenges.Should().BeEquivalentTo(expected);
        challenges.Should().BeInAscendingOrder(step => (int)step);
    }

    [Fact]
    public async Task DetermineRequiredChallenges_UnrecognizedDevice_RequiresDeviceTrust()
    {
        _trustedDevices
            .Setup(devices => devices.IsDeviceTrustedAsync(_user.Id, UnknownFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var challenges = await _sut.DetermineRequiredChallengesAsync(
            _user.Id,
            0.2,
            new AuthenticationAttemptContext { DeviceFingerprint = UnknownFingerprint });

        challenges.Should().BeEquivalentTo([AuthenticationStep.DeviceTrust]);
    }

    [Fact]
    public async Task DetermineRequiredChallenges_MissingFingerprint_FailsClosedToDeviceTrust()
    {
        var challenges = await _sut.DetermineRequiredChallengesAsync(
            _user.Id,
            0.1,
            new AuthenticationAttemptContext { DeviceFingerprint = null });

        challenges.Should().BeEquivalentTo([AuthenticationStep.DeviceTrust]);
    }

    [Fact]
    public async Task DetermineRequiredChallenges_HighRiskWithUnrecognizedDevice_StacksAllChallenges()
    {
        _trustedDevices
            .Setup(devices => devices.IsDeviceTrustedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var challenges = await _sut.DetermineRequiredChallengesAsync(
            _user.Id,
            0.8,
            new AuthenticationAttemptContext { DeviceFingerprint = UnknownFingerprint });

        challenges.Should().BeEquivalentTo(
        [
            AuthenticationStep.MfaVerification,
            AuthenticationStep.DeviceTrust,
            AuthenticationStep.RiskChallenge
        ]);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Challenge tiers end-to-end
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task HighRiskTier_RequiresMfaThenRiskChallenge_InOrder_AndEnforcesStepOrder()
    {
        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskScore = 80 });
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint));

        state.RequiredSteps.Should().BeEquivalentTo(
        [
            AuthenticationStep.PrimaryCredential,
            AuthenticationStep.MfaVerification,
            AuthenticationStep.RiskChallenge
        ]);

        // Steps must be processed in order: RiskChallenge cannot jump the queue.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.RiskChallenge, new Dictionary<string, object> { ["totpCode"] = "123456" }));
        // Steps outside the required set are rejected.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.DeviceTrust, new Dictionary<string, object> { ["totpCode"] = "123456" }));
        // The primary credential is satisfied at initiation and cannot be re-processed.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.PrimaryCredential, new Dictionary<string, object>()));

        // Completion is refused while steps are pending.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CompleteAuthenticationAsync(state.FlowId));
        _jwtTokenService.Verify(
            service => service.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var afterMfa = await _sut.ProcessAuthenticationStepAsync(
            state.FlowId,
            AuthenticationStep.MfaVerification,
            new Dictionary<string, object> { ["totpCode"] = "123456" });
        afterMfa.CompletedSteps.Should().Contain(AuthenticationStep.MfaVerification);
        afterMfa.CompletedSteps.Should().NotContain(AuthenticationStep.RiskChallenge);

        // A completed step cannot be completed twice.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.MfaVerification, new Dictionary<string, object> { ["totpCode"] = "123456" }));

        var afterRisk = await _sut.ProcessAuthenticationStepAsync(
            state.FlowId,
            AuthenticationStep.RiskChallenge,
            new Dictionary<string, object> { ["backupCode"] = "AB12CD34" });
        afterRisk.CompletedSteps.Should().Contain(AuthenticationStep.RiskChallenge);
        afterRisk.IsComplete.Should().BeTrue();

        var result = await _sut.CompleteAuthenticationAsync(state.FlowId);
        result.IsSuccess.Should().BeTrue();
        result.MfaEnabled.Should().BeTrue();
        result.RiskScore.Should().Be(0.8);
    }

    [Fact]
    public async Task MediumRiskTier_RequiresOnlyMfaVerification_ThenCompletes()
    {
        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskScore = 60 });
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint));

        state.RequiredSteps.Should().BeEquivalentTo(
        [
            AuthenticationStep.PrimaryCredential,
            AuthenticationStep.MfaVerification
        ]);

        var afterMfa = await _sut.ProcessAuthenticationStepAsync(
            state.FlowId,
            AuthenticationStep.MfaVerification,
            new Dictionary<string, object> { ["totpCode"] = "654321" });
        afterMfa.CompletedSteps.Should().Contain(AuthenticationStep.MfaVerification);
        afterMfa.IsComplete.Should().BeTrue();

        var result = await _sut.CompleteAuthenticationAsync(state.FlowId);
        result.IsSuccess.Should().BeTrue();
        _totpMfa.Verify(
            service => service.VerifyTotpAsync(_user.Id, "654321", TrustedFingerprint, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnrecognizedDeviceTier_RequiresDeviceTrust_VerifiedViaWebAuthn()
    {
        _trustedDevices
            .Setup(devices => devices.IsDeviceTrustedAsync(_user.Id, UnknownFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: UnknownFingerprint));

        state.RequiredSteps.Should().BeEquivalentTo(
        [
            AuthenticationStep.PrimaryCredential,
            AuthenticationStep.DeviceTrust
        ]);

        var afterDeviceTrust = await _sut.ProcessAuthenticationStepAsync(
            state.FlowId,
            AuthenticationStep.DeviceTrust,
            new Dictionary<string, object> { ["webAuthnAssertion"] = "{\"id\":\"cred-1\"}" });
        afterDeviceTrust.CompletedSteps.Should().Contain(AuthenticationStep.DeviceTrust);

        var result = await _sut.CompleteAuthenticationAsync(state.FlowId);
        result.IsSuccess.Should().BeTrue();
        result.DeviceTrusted.Should().BeFalse();
        _webAuthn.Verify(
            service => service.CompleteAuthenticationAsync("{\"id\":\"cred-1\"}", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WebAuthnAssertion_FromDifferentUser_FailsClosed()
    {
        _trustedDevices
            .Setup(devices => devices.IsDeviceTrustedAsync(_user.Id, UnknownFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _webAuthn
            .Setup(service => service.CompleteAuthenticationAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = true, UserId = Guid.NewGuid() });
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: UnknownFingerprint));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.DeviceTrust, new Dictionary<string, object> { ["webAuthnAssertion"] = "{}" }));

        (await _sut.GetFlowStateAsync(state.FlowId))!.CompletedSteps.Should().NotContain(AuthenticationStep.DeviceTrust);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Fail-closed: expiry and abandonment
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExpiredFlow_FailsClosedForLookupStepsAndCompletion()
    {
        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskScore = 60 });
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint));

        _clock.AdvanceBy(AuthenticationOrchestrationService.FlowLifetime.Add(TimeSpan.FromSeconds(1)));

        (await _sut.GetFlowStateAsync(state.FlowId)).Should().BeNull();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.MfaVerification, new Dictionary<string, object> { ["totpCode"] = "123456" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.CompleteAuthenticationAsync(state.FlowId));

        var persisted = await _flowStates.GetByFlowIdAsync(state.FlowId);
        persisted!.AbandonedAt.Should().NotBeNull();
        _jwtTokenService.Verify(
            service => service.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _totpMfa.Verify(
            service => service.VerifyTotpAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AbandonedFlow_FailsClosedForLookupStepsAndCompletion()
    {
        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskScore = 60 });
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint));

        await _sut.AbandonAuthenticationFlowAsync(state.FlowId);

        (await _sut.GetFlowStateAsync(state.FlowId)).Should().BeNull();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.MfaVerification, new Dictionary<string, object> { ["totpCode"] = "123456" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.CompleteAuthenticationAsync(state.FlowId));
        _jwtTokenService.Verify(
            service => service.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnknownFlow_FailsClosed()
    {
        var missing = Guid.NewGuid();

        (await _sut.GetFlowStateAsync(missing)).Should().BeNull();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ProcessAuthenticationStepAsync(missing, AuthenticationStep.MfaVerification, new Dictionary<string, object> { ["totpCode"] = "123456" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.CompleteAuthenticationAsync(missing));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Primary credential validation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Initiate_WithInvalidPassword_RecordsFailedAttemptAndDoesNotCreateFlow()
    {
        _passwordHasher
            .Setup(hasher => hasher.VerifyPasswordWithWorkClassification(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new PasswordVerificationResult(IsValid: false, WorkPerformed: true));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint)));

        _authAttemptService.Verify(
            service => service.RecordFailedAttemptAsync("user@example.com", _user.Id, It.IsAny<string>(), It.IsAny<string>(), "InvalidCredentials", It.IsAny<TimeSpan>(), "Local"),
            Times.Once);
        _flowStates.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Initiate_WithInactiveUser_FailsClosedWithoutFlow()
    {
        _user.IsActive = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint)));

        _flowStates.Stored.Should().BeEmpty();
        _user.IsActive = true;
    }

    [Fact]
    public async Task Initiate_WithUnsupportedAuthMethod_FailsClosed()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint, authMethod: "OAuth")));

        _flowStates.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Initiate_WithoutPassword_FailsClosed()
    {
        var request = CreateRequest(deviceFingerprint: TrustedFingerprint);
        request.Context = new Dictionary<string, object>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.InitiateAuthenticationAsync(request));

        _flowStates.Stored.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Risk analysis fail-closed + delegated lockout
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Initiate_WhenRiskAnalysisFails_AssumesMaximumRisk()
    {
        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ThrowsAsync(new InvalidOperationException("analyzer unavailable"));

        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint));

        state.RiskScore.Should().Be(1.0);
        state.RequiredSteps.Should().Contain(AuthenticationStep.RiskChallenge);
        state.RequiredSteps.Should().Contain(AuthenticationStep.MfaVerification);
    }

    [Fact]
    public async Task ProcessStep_WhenMfaLockoutIsActive_FailsClosed()
    {
        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskScore = 60 });
        _mfaAttemptTracking
            .Setup(service => service.IsUserLockedOutAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.MfaVerification, new Dictionary<string, object> { ["totpCode"] = "123456" }));

        _totpMfa.Verify(
            service => service.VerifyTotpAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessStep_WhenVerificationFails_DelegatesToExistingLockoutTracking()
    {
        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskScore = 60 });
        _totpMfa
            .Setup(service => service.VerifyTotpAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.MfaVerification, new Dictionary<string, object> { ["totpCode"] = "000000" }));

        _mfaAttemptTracking.Verify(
            service => service.RecordMfaAttemptAsync(_user.Id, MfaMethod.Totp, false, "invalid TOTP code", TrustedFingerprint, It.IsAny<CancellationToken>()),
            Times.Once);
        (await _sut.GetFlowStateAsync(state.FlowId))!.CompletedSteps.Should().NotContain(AuthenticationStep.MfaVerification);
    }

    [Fact]
    public async Task ProcessStep_WithoutPayload_FailsClosed()
    {
        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskScore = 60 });
        var state = await _sut.InitiateAuthenticationAsync(CreateRequest(deviceFingerprint: TrustedFingerprint));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.ProcessAuthenticationStepAsync(state.FlowId, AuthenticationStep.MfaVerification, "not-a-payload"));

        _totpMfa.Verify(
            service => service.VerifyTotpAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DI registration
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddAuthenticationData_RegistersOrchestrationServiceAndFlowStateRepository()
    {
        var services = new ServiceCollection();
        services.AddAuthenticationData(new ConfigurationBuilder().Build());

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAuthenticationOrchestrationService)
            && descriptor.ImplementationType == typeof(AuthenticationOrchestrationService)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAuthenticationFlowStateRepository)
            && descriptor.ImplementationType == typeof(AuthenticationFlowStateRepository)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private TestInitiateAuthenticationRequest CreateRequest(string deviceFingerprint, string authMethod = "Local") => new()
    {
        Identifier = "user@example.com",
        CredentialType = "Email",
        AuthMethod = authMethod,
        IpAddress = "203.0.113.5",
        UserAgent = "OrchestrationTests/1.0",
        DeviceFingerprint = deviceFingerprint,
        Context = new Dictionary<string, object> { ["password"] = "correct-password" }
    };

    private sealed class TestInitiateAuthenticationRequest : InitiateAuthenticationRequest;

    /// <summary>
    ///     Deterministic clock: the service reads <see cref="SystemClock"/>, which the tests
    ///     redirect to this provider so flow expiry can be exercised without waiting.
    /// </summary>
    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset FrozenUtcNow => _now;

        public void AdvanceBy(TimeSpan delta) => _now = _now.Add(delta);

        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>
    ///     In-memory stand-in for the flow-state repository. Reads return copies, mirroring
    ///     the detached (AsNoTracking) reads of the EF implementation.
    /// </summary>
    private sealed class InMemoryFlowStateStore : IAuthenticationFlowStateRepository
    {
        public Dictionary<Guid, AuthenticationFlowStateRecord> Stored { get; } = new();

        public Task<AuthenticationFlowStateRecord> CreateAsync(AuthenticationFlowStateRecord record, CancellationToken cancellationToken = default)
        {
            Stored[record.FlowId] = Clone(record);
            return Task.FromResult(Clone(record));
        }

        public Task<AuthenticationFlowStateRecord?> GetByFlowIdAsync(Guid flowId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Stored.TryGetValue(flowId, out var record) ? Clone(record) : null);
        }

        public Task<AuthenticationFlowStateRecord> UpdateAsync(AuthenticationFlowStateRecord record, CancellationToken cancellationToken = default)
        {
            Stored[record.FlowId] = Clone(record);
            return Task.FromResult(Clone(record));
        }

        public Task AbandonAsync(Guid flowId, DateTime abandonedAt, CancellationToken cancellationToken = default)
        {
            if (Stored.TryGetValue(flowId, out var record))
            {
                record.AbandonedAt = abandonedAt;
            }

            return Task.CompletedTask;
        }

        public Task DeleteExpiredAsync(DateTime expiredBefore, CancellationToken cancellationToken = default)
        {
            foreach (var flowId in Stored.Where(pair => pair.Value.ExpiresAt < expiredBefore).Select(pair => pair.Key).ToList())
            {
                Stored.Remove(flowId);
            }

            return Task.CompletedTask;
        }

        private static AuthenticationFlowStateRecord Clone(AuthenticationFlowStateRecord record) => new()
        {
            FlowId = record.FlowId,
            UserId = record.UserId,
            CurrentStep = record.CurrentStep,
            RequiredStepsJson = record.RequiredStepsJson,
            CompletedStepsJson = record.CompletedStepsJson,
            IsComplete = record.IsComplete,
            RiskScore = record.RiskScore,
            InitiatedAt = record.InitiatedAt,
            ExpiresAt = record.ExpiresAt,
            IpAddress = record.IpAddress,
            DeviceFingerprint = record.DeviceFingerprint,
            StepDataJson = record.StepDataJson,
            AbandonedAt = record.AbandonedAt,
            CompletedAt = record.CompletedAt,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }
}

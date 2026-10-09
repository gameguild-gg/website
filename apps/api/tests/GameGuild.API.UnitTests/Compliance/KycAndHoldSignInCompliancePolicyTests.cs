using FluentAssertions;
using GameGuild.API.Core.Compliance;
using GameGuild.Compliance.KYC;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authentication;
using Moq;
using Xunit;

namespace GameGuild.API.UnitTests.Compliance;

/// <summary>
///     Policy matrix for the host-side sign-in compliance adapter (issue #267):
///     verification status x compliance hold x tenant scope.
/// </summary>
public sealed class KycAndHoldSignInCompliancePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IKycService> _kycServiceMock = new(MockBehavior.Strict);
    private readonly Mock<IComplianceHoldStore> _holdStoreMock = new(MockBehavior.Strict);
    private readonly Mock<TimeProvider> _timeProviderMock = new();
    private readonly KycAndHoldSignInCompliancePolicy _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public KycAndHoldSignInCompliancePolicyTests()
    {
        _timeProviderMock.Setup(time => time.GetUtcNow()).Returns(Now);
        _sut = new KycAndHoldSignInCompliancePolicy(
            _kycServiceMock.Object,
            _holdStoreMock.Object,
            _timeProviderMock.Object);
    }

    private void SetupNoActiveHold()
    {
        _holdStoreMock
            .Setup(store => store.IsActiveAsync(
                It.Is<ComplianceHoldScope>(scope =>
                    scope.TenantId == _tenantId &&
                    scope.SubjectHash == EconomySubjectReference.ForUser(_tenantId, _userId) &&
                    scope.Capability == null),
                Now,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private void SetupLatestVerification(KycVerificationStatus status)
    {
        _kycServiceMock
            .Setup(service => service.GetLatestVerificationAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserKycVerification?>.Success(new UserKycVerification
            {
                UserId = _userId,
                Status = status,
                SubmittedAt = Now.UtcDateTime.AddDays(-1)
            }));
    }

    private void SetupVerified(bool isVerified)
    {
        _kycServiceMock
            .Setup(service => service.IsUserVerifiedAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(isVerified));
    }

    public static TheoryData<string, KycVerificationStatus?, bool, SignInComplianceOutcome, string> StatusMatrix => new()
    {
        // no verification on record
        { "missing", null, false, SignInComplianceOutcome.Challenge, SignInComplianceReasons.VerificationMissing },
        { "pending", KycVerificationStatus.Pending, false, SignInComplianceOutcome.Challenge, SignInComplianceReasons.VerificationPending },
        { "in progress", KycVerificationStatus.InProgress, false, SignInComplianceOutcome.Challenge, SignInComplianceReasons.VerificationPending },
        { "expired", KycVerificationStatus.Expired, false, SignInComplianceOutcome.Challenge, SignInComplianceReasons.VerificationExpired },
        { "approved and verified", KycVerificationStatus.Approved, true, SignInComplianceOutcome.Allow, SignInComplianceReasons.Allowed },
        { "approved but lapsed", KycVerificationStatus.Approved, false, SignInComplianceOutcome.Challenge, SignInComplianceReasons.VerificationExpired },
        { "rejected", KycVerificationStatus.Rejected, false, SignInComplianceOutcome.Deny, SignInComplianceReasons.VerificationRejected },
        { "suspended", KycVerificationStatus.Suspended, false, SignInComplianceOutcome.Deny, SignInComplianceReasons.VerificationSuspended }
    };

    [Theory]
    [MemberData(nameof(StatusMatrix))]
    public async Task EvaluateAsync_MapsVerificationStatusToDecision(
        string _,
        KycVerificationStatus? status,
        bool isVerified,
        SignInComplianceOutcome expectedOutcome,
        string expectedReason)
    {
        SetupNoActiveHold();
        if (status.HasValue)
        {
            SetupLatestVerification(status.Value);
        }
        else
        {
            _kycServiceMock
                .Setup(service => service.GetLatestVerificationAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success<UserKycVerification?>(null));
        }

        if (status == KycVerificationStatus.Approved)
        {
            SetupVerified(isVerified);
        }

        var decision = await _sut.EvaluateAsync(
            new SignInComplianceContext(_userId, _tenantId, "198.51.100.7", "fingerprint-1"));

        decision.Outcome.Should().Be(expectedOutcome);
        decision.Reason.Should().Be(expectedReason);
    }

    [Fact]
    public async Task EvaluateAsync_ActiveHold_DeniesAndTakesPrecedenceOverApprovedVerification()
    {
        _holdStoreMock
            .Setup(store => store.IsActiveAsync(It.IsAny<ComplianceHoldScope>(), Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        SetupLatestVerification(KycVerificationStatus.Approved);

        var decision = await _sut.EvaluateAsync(
            new SignInComplianceContext(_userId, _tenantId, "198.51.100.7", "fingerprint-1"));

        decision.Outcome.Should().Be(SignInComplianceOutcome.Deny);
        decision.Reason.Should().Be(SignInComplianceReasons.ComplianceHold);
        _kycServiceMock.Verify(
            service => service.GetLatestVerificationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_QueriesTheHoldWithTheOpaqueTenantBoundSubjectReference()
    {
        ComplianceHoldScope? capturedScope = null;
        _holdStoreMock
            .Setup(store => store.IsActiveAsync(It.IsAny<ComplianceHoldScope>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback((ComplianceHoldScope scope, DateTimeOffset _, CancellationToken _) => capturedScope = scope)
            .ReturnsAsync(false);
        _kycServiceMock
            .Setup(service => service.GetLatestVerificationAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<UserKycVerification?>(null));

        await _sut.EvaluateAsync(
            new SignInComplianceContext(_userId, _tenantId, "198.51.100.7", "fingerprint-1"));

        var expected = new ComplianceHoldScope(
            _tenantId,
            EconomySubjectReference.ForUser(_tenantId, _userId),
            Capability: null);
        capturedScope.Should().Be(expected);
        capturedScope!.Key.Should().NotContain(_userId.ToString());
    }

    [Fact]
    public async Task EvaluateAsync_WithoutTenant_SkipsTheHoldStoreAndUsesVerificationOnly()
    {
        _kycServiceMock
            .Setup(service => service.GetLatestVerificationAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<UserKycVerification?>(null));

        var decision = await _sut.EvaluateAsync(
            new SignInComplianceContext(_userId, TenantId: null, IpAddress: null, DeviceFingerprint: null));

        decision.Outcome.Should().Be(SignInComplianceOutcome.Challenge);
        decision.Reason.Should().Be(SignInComplianceReasons.VerificationMissing);
        _holdStoreMock.Verify(
            store => store.IsActiveAsync(It.IsAny<ComplianceHoldScope>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_LatestVerificationReadFails_ThrowsForCallerFailHandling()
    {
        SetupNoActiveHold();
        _kycServiceMock
            .Setup(service => service.GetLatestVerificationAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<UserKycVerification?>(Error.Failure("KYC.GetFailed", "store unavailable")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.EvaluateAsync(
            new SignInComplianceContext(_userId, _tenantId, "198.51.100.7", "fingerprint-1")));
    }

    [Fact]
    public async Task EvaluateAsync_VerifiedStateReadFails_ThrowsForCallerFailHandling()
    {
        SetupNoActiveHold();
        SetupLatestVerification(KycVerificationStatus.Approved);
        _kycServiceMock
            .Setup(service => service.IsUserVerifiedAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<bool>(Error.Failure("KYC.CheckFailed", "store unavailable")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.EvaluateAsync(
            new SignInComplianceContext(_userId, _tenantId, "198.51.100.7", "fingerprint-1")));
    }

    [Fact]
    public async Task EvaluateAsync_NullContext_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _sut.EvaluateAsync(null!));
    }
}

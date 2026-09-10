using FluentAssertions;
using GameGuild.Compliance.KYC;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class ComplianceKycPayoutEligibilitySourceTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-08-03T12:00:00Z");

    [Fact]
    public async Task ConstructorAndPayeeIdentityFailClosed()
    {
        FluentActions.Invoking(() => new ComplianceKycPayoutEligibilitySource(null!))
            .Should().Throw<ArgumentNullException>();
        var source = new ComplianceKycPayoutEligibilitySource(new KycStub());
        var read = async () => await source.ReadAsync(Guid.Empty, Time);
        await read.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReadAsync_MapsFailureAndMissingVerificationToUnauditableIneligibility()
    {
        var userId = Guid.NewGuid();
        var failure = new ComplianceKycPayoutEligibilitySource(new KycStub
        {
            Latest = _ => Result.Failure<UserKycVerification?>(Error.Failure("kyc-down", "Unavailable"))
        });
        var missing = new ComplianceKycPayoutEligibilitySource(new KycStub
        {
            Latest = _ => Result.Success<UserKycVerification?>(null)
        });

        var failed = await failure.ReadAsync(userId, Time);
        var absent = await missing.ReadAsync(userId, Time);

        failed.IsApproved.Should().BeFalse();
        absent.IsApproved.Should().BeFalse();
        failed.ExpiresAt.Should().Be(Time);
        absent.ExpiresAt.Should().Be(Time);
        failed.EvidenceHash.Should().HaveLength(64).And.NotBe(absent.EvidenceHash);
    }

    [Fact]
    public async Task ReadAsync_BindsCurrentApprovedVerificationAndRejectsExpiredDeletedOrRejectedEvidence()
    {
        var userId = Guid.NewGuid();
        var approved = Verification(userId, KycVerificationStatus.Approved, Time.AddDays(1));
        var source = new ComplianceKycPayoutEligibilitySource(new KycStub
        {
            Latest = _ => Result.Success<UserKycVerification?>(approved)
        });

        var snapshot = await source.ReadAsync(userId, Time);

        snapshot.PayeeId.Should().Be(userId);
        snapshot.Version.Should().Be(1);
        snapshot.IsApproved.Should().BeTrue();
        snapshot.ObservedAt.Should().Be(Time.AddMinutes(-1));
        snapshot.ExpiresAt.Should().Be(Time.AddDays(1));
        snapshot.EvidenceHash.Should().HaveLength(64);

        approved.ExternalVerificationId = null;
        (await source.ReadAsync(userId, Time)).EvidenceHash.Should().HaveLength(64);
        approved.ExternalVerificationId = "kyc_external";

        approved.ExpiresAt = Time.UtcDateTime;
        (await source.ReadAsync(userId, Time)).IsApproved.Should().BeFalse();
        approved.ExpiresAt = Time.AddDays(1).UtcDateTime;
        approved.DeletedAt = Time.UtcDateTime;
        (await source.ReadAsync(userId, Time)).IsApproved.Should().BeFalse();
        approved.DeletedAt = null;
        approved.Status = KycVerificationStatus.Rejected;
        approved.ExpiresAt = null;
        var rejected = await source.ReadAsync(userId, Time);
        rejected.IsApproved.Should().BeFalse();
        rejected.ExpiresAt.Should().Be(Time);
    }

    private static UserKycVerification Verification(
        Guid userId,
        KycVerificationStatus status,
        DateTimeOffset expiresAt) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = status,
            Version = 0,
            ExternalVerificationId = "kyc_external",
            UpdatedAt = Time.AddMinutes(-1).UtcDateTime,
            ExpiresAt = expiresAt.UtcDateTime
        };

    private sealed class KycStub : IKycService
    {
        public Func<Guid, Result<UserKycVerification?>> Latest { get; init; } =
            _ => Result.Success<UserKycVerification?>(null);

        public Task<Result<UserKycVerification?>> GetLatestVerificationAsync(
            Guid userId,
            CancellationToken cancellationToken = default) => Task.FromResult(Latest(userId));

        public Task<Result<UserKycVerification>> SubmitVerificationAsync(Guid userId, KycProvider provider,
            string verificationLevel, string documentTypes, string? documentCountry,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<UserKycVerification>> UpdateVerificationStatusAsync(Guid verificationId,
            KycVerificationStatus status, string? notes, DateTime? completedAt,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<UserKycVerification>> GetVerificationByIdAsync(Guid verificationId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<UserKycVerification>>> GetVerificationsByUserIdAsync(Guid userId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<bool>> IsUserVerifiedAsync(Guid userId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<UserKycVerification>>> GetVerificationsByStatusAsync(KycVerificationStatus status,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<string>> UploadDocumentAsync(Guid verificationId, string documentType,
            Stream documentStream, string fileName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Result<bool>> ProcessProviderWebhookAsync(KycProvider provider, string externalVerificationId,
            KycVerificationStatus status, string? providerData, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Result<KycComplianceReportDto>> GetComplianceReportAsync(DateTime startDate, DateTime endDate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

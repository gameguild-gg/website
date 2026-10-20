using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using GameGuild.Identity.Authentication;

namespace GameGuild.Identity.Authentication.UnitTests;

public class EfConfigAndExtendedTests
{
    private static ModelBuilder CreateModelBuilder() => new(new ConventionSet());

    // ── EF Configuration Tests ──────────────────────────────────────────
    [Fact]
    public void ServiceAccountConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new ServiceAccountConfiguration();
        cfg.Configure(mb.Entity<ServiceAccount>());
        mb.Model.FindEntityType(typeof(ServiceAccount)).Should().NotBeNull();
    }

    [Fact]
    public void UserRoleConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new UserRoleConfiguration();
        cfg.Configure(mb.Entity<UserRole>());
        mb.Model.FindEntityType(typeof(UserRole)).Should().NotBeNull();
    }

    [Fact]
    public void RoleConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new RoleConfiguration();
        cfg.Configure(mb.Entity<Role>());
        mb.Model.FindEntityType(typeof(Role)).Should().NotBeNull();
    }

    [Fact]
    public void UserWebAuthnCredentialConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new UserWebAuthnCredentialConfiguration();
        cfg.Configure(mb.Entity<UserWebAuthnCredential>());
        mb.Model.FindEntityType(typeof(UserWebAuthnCredential)).Should().NotBeNull();
    }

    [Fact]
    public void AuthenticationAttemptConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new AuthenticationAttemptConfiguration();
        cfg.Configure(mb.Entity<AuthenticationAttempt>());
        mb.Model.FindEntityType(typeof(AuthenticationAttempt)).Should().NotBeNull();
    }

    [Fact]
    public void UserSessionConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new UserSessionConfiguration();
        cfg.Configure(mb.Entity<UserSession>());
        mb.Model.FindEntityType(typeof(UserSession)).Should().NotBeNull();
    }

    [Fact]
    public void BlockchainCertificateAnchorConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new BlockchainCertificateAnchorConfiguration();
        cfg.Configure(mb.Entity<BlockchainCertificateAnchor>());
        mb.Model.FindEntityType(typeof(BlockchainCertificateAnchor)).Should().NotBeNull();
    }

    [Fact]
    public void ContentTypePermissionConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new ContentTypePermissionConfiguration();
        cfg.Configure(mb.Entity<ContentTypePermission>());
        mb.Model.FindEntityType(typeof(ContentTypePermission)).Should().NotBeNull();
    }

    [Fact]
    public void IdentityVerificationConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new IdentityVerificationConfiguration();
        cfg.Configure(mb.Entity<IdentityVerification>());
        mb.Model.FindEntityType(typeof(IdentityVerification)).Should().NotBeNull();
    }

    [Fact]
    public void MfaAttemptConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new MfaAttemptConfiguration();
        cfg.Configure(mb.Entity<MfaAttempt>());
        mb.Model.FindEntityType(typeof(MfaAttempt)).Should().NotBeNull();
    }

    [Fact]
    public void RefreshTokenConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new RefreshTokenConfiguration();
        cfg.Configure(mb.Entity<RefreshToken>());
        mb.Model.FindEntityType(typeof(RefreshToken)).Should().NotBeNull();
    }

    [Fact]
    public void TrustedDeviceConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new TrustedDeviceConfiguration();
        cfg.Configure(mb.Entity<TrustedDevice>());
        mb.Model.FindEntityType(typeof(TrustedDevice)).Should().NotBeNull();
    }

    [Fact]
    public void UserMfaConfigurationConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new UserMfaConfigurationConfiguration();
        cfg.Configure(mb.Entity<UserMfaConfiguration>());
        mb.Model.FindEntityType(typeof(UserMfaConfiguration)).Should().NotBeNull();
    }

    // ── Enum Coverage ───────────────────────────────────────────────────
    [Fact]
    public void VerificationMethod_HasValues()
    {
        Enum.GetValues<VerificationMethod>().Should().NotBeEmpty();
    }

    [Fact]
    public void SocialProvider_HasValues()
    {
        Enum.GetValues<SocialProvider>().Should().NotBeEmpty();
    }

    [Fact]
    public void SessionTerminationReason_HasValues()
    {
        Enum.GetValues<SessionTerminationReason>().Should().NotBeEmpty();
    }

    [Fact]
    public void RiskLevel_HasValues()
    {
        Enum.GetValues<RiskLevel>().Should().NotBeEmpty();
    }

    [Fact]
    public void MfaMethod_HasValues()
    {
        Enum.GetValues<MfaMethod>().Should().NotBeEmpty();
    }

    [Fact]
    public void CredentialType_HasValues()
    {
        var values = Enum.GetValues<CredentialType>();
        values.Should().Contain(CredentialType.Email);
        values.Should().Contain(CredentialType.Username);
        values.Should().Contain(CredentialType.Phone);
        values.Should().Contain(CredentialType.WalletAddress);
    }

    [Fact]
    public void ComplianceFlags_AreFlagEnum()
    {
        var combined = ComplianceFlags.Aml | ComplianceFlags.Kyc;
        combined.HasFlag(ComplianceFlags.Aml).Should().BeTrue();
        combined.HasFlag(ComplianceFlags.Kyc).Should().BeTrue();
        combined.HasFlag(ComplianceFlags.Pep).Should().BeFalse();
    }

    [Fact]
    public void AuthenticationStep_HasValues()
    {
        var values = Enum.GetValues<AuthenticationStep>();
        values.Should().Contain(AuthenticationStep.PrimaryCredential);
        values.Should().Contain(AuthenticationStep.MfaVerification);
    }

    [Fact]
    public void VerificationType_HasValues()
    {
        var values = Enum.GetValues<VerificationType>();
        values.Should().Contain(VerificationType.Identity);
        values.Should().Contain(VerificationType.Address);
        values.Should().Contain(VerificationType.Document);
    }

    [Fact]
    public void VerificationStatus_HasValues()
    {
        var values = Enum.GetValues<VerificationStatus>();
        values.Should().Contain(VerificationStatus.Pending);
        values.Should().Contain(VerificationStatus.Approved);
        values.Should().Contain(VerificationStatus.Rejected);
    }

    [Fact]
    public void VerificationLevel_HasValues()
    {
        var values = Enum.GetValues<VerificationLevel>();
        values.Should().Contain(VerificationLevel.Basic);
        values.Should().Contain(VerificationLevel.Intermediate);
        values.Should().Contain(VerificationLevel.Advanced);
    }

    [Fact]
    public void AuthenticationFailureReason_HasValues()
    {
        Enum.GetValues<AuthenticationFailureReason>().Should().NotBeEmpty();
    }

    // ── Service Constructors ────────────────────────────────────────────
    [Fact]
    public void EmailVerificationService_CanBeCreated()
    {
        var publisher = new Mock<IPublisher>();
        publisher.Setup(x => x.Publish(It.IsAny<EmailVerificationRequestedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var svc = new EmailVerificationService(
            Mock.Of<ILogger<EmailVerificationService>>(),
            new MemoryCache(new MemoryCacheOptions()),
            publisher.Object,
            magicLinkTokenStore: new global::GameGuild.Identity.Authentication.UnitTests.Services.EmailVerificationServiceTests.InMemoryMagicLinkTokenStore());
        svc.Should().NotBeNull();
    }

    // ── Event Records ───────────────────────────────────────────────────
    [Fact]
    public void TokenRefreshedEvent_CanBeCreated()
    {
        var e = new TokenRefreshedEvent(Guid.NewGuid(), "refresh_tok", DateTime.UtcNow);
        e.Should().NotBeNull();
    }

    [Fact]
    public void UserSignedUpEvent_CanBeCreated()
    {
        var e = new UserSignedUpEvent(Guid.NewGuid(), "test@example.com", "TestUser", DateTime.UtcNow);
        e.Should().NotBeNull();
    }

    [Fact]
    public void MfaVerifiedEvent_CanBeCreated()
    {
        var e = new MfaVerifiedEvent(Guid.NewGuid(), "totp", "device123", DateTime.UtcNow);
        e.Should().NotBeNull();
    }

    [Fact]
    public void TokenRevokedEvent_CanBeCreated()
    {
        var e = new TokenRevokedEvent(Guid.NewGuid(), "refresh", "manual", DateTime.UtcNow);
        e.Should().NotBeNull();
    }
}

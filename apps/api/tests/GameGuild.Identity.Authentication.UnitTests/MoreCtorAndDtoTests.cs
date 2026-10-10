using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Fido2NetLib;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication.UnitTests;

public class MoreCtorAndDtoTests
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    // ═══════════════════════════════════════════════════════════════════
    // Service constructors
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void WebAuthnService_CanBeConstructed()
    {
        var svc = new WebAuthnService(
            Mock.Of<IWebAuthnRegistrationService>(),
            Mock.Of<IWebAuthnAuthenticationService>(),
            Mock.Of<IWebAuthnCredentialManagementService>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void PolymorphicSignInHandler_CanBeConstructed()
    {
        var timingProtection = new Mock<IUserEnumerationProtectionService>();
        timingProtection.Setup(value => value.BeginAuthenticationTiming()).Returns(new AuthenticationTimingScope());
        var svc = new PolymorphicSignInHandler(
            Mock.Of<IAuthService>(),
            Mock.Of<IUserRepository>(),
            timingProtection.Object,
            NullLogger<PolymorphicSignInHandler>.Instance,
            null);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void BackupCodeMfaService_CanBeConstructed()
    {
        var svc = new BackupCodeMfaService(
            NullLogger<BackupCodeMfaService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptTrackingService>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void LogAnalyticsEventHandler_CanBeConstructed()
    {
        var handler = new LogAnalyticsEventHandler(
            NullLogger<LogAnalyticsEventHandler>.Instance);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void LogoutHandler_CanBeConstructed()
    {
        var handler = new LogoutHandler(
            Mock.Of<ITokenRevocationService>(),
            Mock.Of<IRefreshTokenRepository>(),
            Mock.Of<IUserSessionRepository>(),
            NullLogger<LogoutHandler>.Instance);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ThreatDetectionService_CanBeConstructed()
    {
        var svc = new ThreatDetectionService(
            Mock.Of<IAuthenticationAttemptRepository>(),
            NullLogger<ThreatDetectionService>.Instance,
            EmptyConfig(),
            Mock.Of<ISiemIntegrationService>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void WebAuthnCredentialManagementService_CanBeConstructed()
    {
        var svc = new WebAuthnCredentialManagementService(
            Mock.Of<IWebAuthnCredentialRepository>(),
            NullLogger<WebAuthnCredentialManagementService>.Instance);
        svc.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Controller base classes
    // ═══════════════════════════════════════════════════════════════════

    private class TestAuthController : AuthControllerBase { }

    [Fact]
    public void AuthControllerBase_CanBeSubclassed()
    {
        var controller = new TestAuthController();
        controller.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // DTOs and Records
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void JwtKeyInfoDto_CanBeInstantiated()
    {
        var dto = new JwtKeyInfoDto
        {
            KeyId = "key-1",
            Algorithm = "RS256",
            IsActive = true,
            ValidFrom = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(365),
            KeyVersion = 1
        };
        dto.Should().NotBeNull();
        dto.KeyId.Should().Be("key-1");
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public void JwtKeyInfoDto_WithOptionalFields()
    {
        var dto = new JwtKeyInfoDto
        {
            KeyId = "key-2",
            Algorithm = "RS256",
            IsActive = false,
            ValidFrom = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(365),
            RotatedAt = DateTime.UtcNow,
            RotationReason = "scheduled",
            KeyVersion = 2
        };
        dto.RotatedAt.Should().NotBeNull();
        dto.RotationReason.Should().Be("scheduled");
    }
}

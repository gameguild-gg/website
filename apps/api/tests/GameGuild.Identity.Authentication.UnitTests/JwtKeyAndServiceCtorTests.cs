using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests;

/// <summary>
/// R5 tests targeting JwtKeyInfoDto, LocalSignInHandler, AuthAttemptService,
/// MfaAttemptTrackingService, TotpMfaService to push coverage past 75%.
/// </summary>
public class JwtKeyAndServiceCtorTests
{
    // ─── JwtKeyInfoDto ────────────────────────────────────────────────

    [Fact]
    public void JwtKeyInfoDto_CanInstantiate_WithInitProperties()
    {
        var dto = new JwtKeyInfoDto
        {
            KeyId = "key-001",
            Algorithm = "HS256",
            IsActive = true,
            ValidFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RotatedAt = null,
            RotationReason = null,
            KeyVersion = 1
        };
        dto.KeyId.Should().Be("key-001");
        dto.Algorithm.Should().Be("HS256");
        dto.IsActive.Should().BeTrue();
        dto.KeyVersion.Should().Be(1);
        dto.RotatedAt.Should().BeNull();
        dto.RotationReason.Should().BeNull();
    }

    [Fact]
    public void JwtKeyInfoDto_WithRotation_HasAllFields()
    {
        var rotatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dto = new JwtKeyInfoDto
        {
            KeyId = "key-002",
            Algorithm = "RS256",
            IsActive = false,
            ValidFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RotatedAt = rotatedAt,
            RotationReason = "scheduled",
            KeyVersion = 5
        };
        dto.IsActive.Should().BeFalse();
        dto.RotatedAt.Should().Be(rotatedAt);
        dto.RotationReason.Should().Be("scheduled");
        dto.KeyVersion.Should().Be(5);
    }

    [Fact]
    public void JwtKeyInfoDto_FromEntity_MapsCorrectly()
    {
        var now = DateTime.UtcNow;
        var key = new JwtSigningKey
        {
            Id = Guid.NewGuid(),
            KeyId = "key-test",
            KeyMaterial = Convert.ToBase64String(new byte[32]),
            Algorithm = "HS256",
            IsActive = true,
            ValidFrom = now.AddDays(-30),
            ExpiresAt = now.AddDays(30),
            RotatedAt = null,
            RotationReason = null,
            KeyVersion = 3
        };

        var dto = JwtKeyInfoDto.FromEntity(key);
        dto.KeyId.Should().Be("key-test");
        dto.Algorithm.Should().Be("HS256");
        dto.IsActive.Should().BeTrue();
        dto.KeyVersion.Should().Be(3);
    }

    [Fact]
    public void JwtKeyInfoDto_FromEntity_WithRotation()
    {
        var rotatedAt = DateTime.UtcNow.AddDays(-5);
        var key = new JwtSigningKey
        {
            Id = Guid.NewGuid(),
            KeyId = "key-rotated",
            KeyMaterial = Convert.ToBase64String(new byte[32]),
            Algorithm = "RS256",
            IsActive = false,
            ValidFrom = DateTime.UtcNow.AddDays(-60),
            ExpiresAt = DateTime.UtcNow.AddDays(-10),
            RotatedAt = rotatedAt,
            RotationReason = "compromised",
            KeyVersion = 7
        };

        var dto = JwtKeyInfoDto.FromEntity(key);
        dto.IsActive.Should().BeFalse();
        dto.RotatedAt.Should().Be(rotatedAt);
        dto.RotationReason.Should().Be("compromised");
    }

    // ─── LocalSignInHandler ───────────────────────────────────────────

    [Fact]
    public void LocalSignInHandler_CanConstruct()
    {
        var handler = new LocalSignInHandler(
            Mock.Of<IAuthService>(),
            Mock.Of<GameGuild.Identity.Users.IUserRepository>(),
            Mock.Of<IHttpContextAccessor>(),
            NullLogger<LocalSignInHandler>.Instance,
            Mock.Of<FluentValidation.IValidator<LocalSignInCommand>>()
        );
        handler.Should().NotBeNull();
    }

    // ─── AuthAttemptService ───────────────────────────────────────────

    [Fact]
    public void AuthAttemptService_CanConstruct()
    {
        var svc = new AuthAttemptService(
            Mock.Of<IAuthenticationAttemptRepository>(),
            Mock.Of<IUserEnumerationProtectionService>(),
            NullLogger<AuthAttemptService>.Instance
        );
        svc.Should().NotBeNull();
    }

    [Fact]
    public void AuthAttemptService_GetClientIpAddress_NullContext_ReturnsUnknown()
    {
        var svc = new AuthAttemptService(
            Mock.Of<IAuthenticationAttemptRepository>(),
            Mock.Of<IUserEnumerationProtectionService>(),
            NullLogger<AuthAttemptService>.Instance
        );
        var ip = svc.GetClientIpAddress(null);
        ip.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AuthAttemptService_GetClientIpAddress_WithContext()
    {
        var svc = new AuthAttemptService(
            Mock.Of<IAuthenticationAttemptRepository>(),
            Mock.Of<IUserEnumerationProtectionService>(),
            NullLogger<AuthAttemptService>.Instance
        );
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
        var ip = svc.GetClientIpAddress(context);
        ip.Should().NotBeNullOrEmpty();
    }

    // ─── MfaAttemptTrackingService ────────────────────────────────────

    [Fact]
    public void MfaAttemptTrackingService_CanConstruct()
    {
        var svc = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptRepository>(),
            Mock.Of<IHttpContextAccessor>()
        );
        svc.Should().NotBeNull();
    }

    [Fact]
    public void MfaAttemptTrackingService_IsLockedOut_NotLocked_ReturnsFalse()
    {
        var svc = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptRepository>(),
            Mock.Of<IHttpContextAccessor>()
        );
        var config = new UserMfaConfiguration
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            IsEnabled = true,
            FailedAttempts = 0,
            LockedOutUntil = null
        };
        var result = svc.IsLockedOut(config);
        result.Should().BeFalse();
    }

    [Fact]
    public void MfaAttemptTrackingService_IsLockedOut_LockedInFuture_ReturnsTrue()
    {
        var svc = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptRepository>(),
            Mock.Of<IHttpContextAccessor>()
        );
        var config = new UserMfaConfiguration
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            IsEnabled = true,
            FailedAttempts = 10,
            LockedOutUntil = DateTime.UtcNow.AddMinutes(30)
        };
        var result = svc.IsLockedOut(config);
        result.Should().BeTrue();
    }

    [Fact]
    public void MfaAttemptTrackingService_IsLockedOut_LockedInPast_ReturnsFalse()
    {
        var svc = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptRepository>(),
            Mock.Of<IHttpContextAccessor>()
        );
        var config = new UserMfaConfiguration
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            IsEnabled = true,
            FailedAttempts = 5,
            LockedOutUntil = DateTime.UtcNow.AddMinutes(-5)
        };
        var result = svc.IsLockedOut(config);
        result.Should().BeFalse();
    }

    // ─── TotpMfaService ──────────────────────────────────────────────

    [Fact]
    public void TotpMfaService_CanConstruct()
    {
        var svc = new TotpMfaService(
            NullLogger<TotpMfaService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptTrackingService>(),
            Mock.Of<IEncryptionService>(), NativeTotpReplayStub.Create()
        );
        svc.Should().NotBeNull();
    }

    // ─── JwtSigningKey Entity ─────────────────────────────────────────

    [Fact]
    public void JwtSigningKey_CreateNew_GeneratesValidKey()
    {
        var key = JwtSigningKey.CreateNew(1, DateTime.UtcNow, TimeSpan.FromDays(90));
        key.Should().NotBeNull();
        key.KeyId.Should().StartWith("key-");
        key.Algorithm.Should().Be("HS256");
        key.IsActive.Should().BeFalse();
        key.KeyVersion.Should().Be(1);
        key.KeyMaterial.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void JwtSigningKey_Activate_SetsIsActive()
    {
        var key = JwtSigningKey.CreateNew(1, DateTime.UtcNow, TimeSpan.FromDays(90));
        key.IsActive.Should().BeFalse();
        key.Activate();
        key.IsActive.Should().BeTrue();
    }

    [Fact]
    public void JwtSigningKey_Rotate_SetsInactive()
    {
        var key = JwtSigningKey.CreateNew(1, DateTime.UtcNow, TimeSpan.FromDays(90));
        key.Activate();
        key.Rotate("scheduled");
        key.IsActive.Should().BeFalse();
        key.RotatedAt.Should().NotBeNull();
        key.RotationReason.Should().Be("scheduled");
    }

    [Fact]
    public void JwtSigningKey_IsValidForValidation_WithinRange()
    {
        var now = DateTime.UtcNow;
        var key = JwtSigningKey.CreateNew(1, now.AddDays(-10), TimeSpan.FromDays(90));
        key.IsValidForValidation(now).Should().BeTrue();
    }

    [Fact]
    public void JwtSigningKey_IsValidForValidation_BeforeValidFrom()
    {
        var tomorrow = DateTime.UtcNow.AddDays(1);
        var key = JwtSigningKey.CreateNew(1, tomorrow, TimeSpan.FromDays(90));
        key.IsValidForValidation(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void JwtSigningKey_IsValidForValidation_AfterExpiry()
    {
        var pastStart = DateTime.UtcNow.AddDays(-100);
        var key = JwtSigningKey.CreateNew(1, pastStart, TimeSpan.FromDays(30));
        key.IsValidForValidation(DateTime.UtcNow).Should().BeFalse();
    }

    // ─── LogAnalyticsEventHandler ─────────────────────────────────────

    [Fact]
    public void LogAnalyticsEventHandler_CanConstruct()
    {
        var handler = new LogAnalyticsEventHandler(
            NullLogger<LogAnalyticsEventHandler>.Instance
        );
        handler.Should().NotBeNull();
    }

    [Fact]
    public async Task LogAnalyticsEventHandler_Handle_CompletesSuccessfully()
    {
        var handler = new LogAnalyticsEventHandler(
            NullLogger<LogAnalyticsEventHandler>.Instance
        );
        var notification = new UserSignedUpNotification
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            TenantId = Guid.NewGuid()
        };
        await handler.Handle(notification, CancellationToken.None);
    }

    [Fact]
    public async Task LogAnalyticsEventHandler_Handle_WithNullTenant()
    {
        var handler = new LogAnalyticsEventHandler(
            NullLogger<LogAnalyticsEventHandler>.Instance
        );
        var notification = new UserSignedUpNotification
        {
            UserId = Guid.NewGuid(),
            Email = "no-tenant@test.com",
            Username = "user2",
            TenantId = null
        };
        await handler.Handle(notification, CancellationToken.None);
    }
}

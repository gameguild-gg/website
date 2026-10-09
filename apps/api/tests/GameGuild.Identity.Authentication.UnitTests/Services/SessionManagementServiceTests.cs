using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class SessionManagementServiceTests
{
    private readonly Mock<ILogger<SessionManagementService>> _loggerMock;
    private readonly Mock<IUserSessionRepository> _sessionRepositoryMock;
    private readonly Mock<ITrustedDeviceRepository> _trustedDeviceRepositoryMock;
    private readonly Mock<IRefreshTokenLineageRepository> _tokenLineageRepositoryMock = new();
    private readonly Mock<IAuthenticationAuditEventSink> _auditEventSinkMock;
    private readonly SessionManagementService _service;

    public SessionManagementServiceTests()
    {
        _loggerMock = new Mock<ILogger<SessionManagementService>>();
        _sessionRepositoryMock = new Mock<IUserSessionRepository>();
        _trustedDeviceRepositoryMock = new Mock<ITrustedDeviceRepository>();
        _auditEventSinkMock = new Mock<IAuthenticationAuditEventSink>();
        _tokenLineageRepositoryMock.Setup(repository => repository.BindSessionAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _service = new SessionManagementService(
            _loggerMock.Object,
            _sessionRepositoryMock.Object,
            _trustedDeviceRepositoryMock.Object,
            _tokenLineageRepositoryMock.Object,
            auditEventSink: _auditEventSinkMock.Object
        );
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldCreateNewSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ipAddress = "192.168.1.1";
        var userAgent = "Mozilla/5.0";
        UserSession? createdSession = null;

        _sessionRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .Callback<UserSession, CancellationToken>((s, _) => createdSession = s)
            .ReturnsAsync((UserSession s, CancellationToken _) => s);

        // Act
        var result = await _service.CreateSessionAsync(userId, ipAddress, userAgent);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.IpAddress.Should().Be(ipAddress);
        result.UserAgent.Should().Be(userAgent);
        result.IsActive.Should().BeTrue();
        result.DeviceFingerprint.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromSeconds(5));
        _sessionRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()), Times.Once);
        _auditEventSinkMock.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.SessionCreated" &&
                auditEvent.UserId == userId &&
                auditEvent.IpAddress == ipAddress &&
                auditEvent.UserAgent == userAgent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_WithCustomDeviceFingerprint_ShouldUseProvidedFingerprint()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var customFingerprint = "custom-fingerprint-123";
        UserSession? createdSession = null;

        _sessionRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .Callback<UserSession, CancellationToken>((s, _) => createdSession = s)
            .ReturnsAsync((UserSession s, CancellationToken _) => s);

        // Act
        var result = await _service.CreateSessionAsync(userId, "192.168.1.1", "Mozilla/5.0", customFingerprint);

        // Assert
        result.DeviceFingerprint.Should().Be(customFingerprint);
    }

    [Fact]
    public async Task CreateSessionAsync_EnforcesConfiguredConcurrentSessionLimit()
    {
        var userId = Guid.NewGuid();
        var existingSession = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            IsActive = true,
            CreatedAt = SystemClock.UtcNow.AddHours(-1),
            LastUsedAt = SystemClock.UtcNow.AddMinutes(-10)
        };
        _sessionRepositoryMock
            .Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingSession]);
        _sessionRepositoryMock
            .Setup(repository => repository.UpdateAsync(existingSession, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSession);
        _sessionRepositoryMock
            .Setup(repository => repository.CreateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession session, CancellationToken _) => session);

        var service = new SessionManagementService(
            _loggerMock.Object,
            _sessionRepositoryMock.Object,
            _trustedDeviceRepositoryMock.Object,
            _tokenLineageRepositoryMock.Object,
            new SessionOptions { MaxConcurrentSessions = 1 });

        await service.CreateSessionAsync(userId, "192.0.2.1", "test-agent");

        existingSession.IsActive.Should().BeFalse();
        existingSession.TerminationReason.Should().Be(SessionTerminationReason.MaxSessionsExceeded.ToString());
        _sessionRepositoryMock.Verify(repository => repository.UpdateAsync(existingSession, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_RespectsFingerprintAndLocationPrivacySettings()
    {
        var userId = Guid.NewGuid();
        var service = new SessionManagementService(
            _loggerMock.Object,
            _sessionRepositoryMock.Object,
            _trustedDeviceRepositoryMock.Object,
            _tokenLineageRepositoryMock.Object,
            new SessionOptions { EnableDeviceFingerprinting = false, EnableLocationTracking = false });
        _sessionRepositoryMock
            .Setup(repository => repository.CreateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession session, CancellationToken _) => session);

        var session = await service.CreateSessionAsync(userId, "192.0.2.1", "test-agent", "provided-fingerprint");

        session.IpAddress.Should().Be("unknown");
        session.DeviceFingerprint.Should().BeEmpty();
        (await service.TrustDeviceAsync(userId, "provided-fingerprint", "test-device")).Should().BeFalse();
    }

    [Fact]
    public async Task CreateSessionAsync_WithAuthenticationState_PreservesSessionAndRefreshTokenHash()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = SystemClock.UtcNow;
        var expiresAt = createdAt.AddDays(7);

        _sessionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession session, CancellationToken _) => session);

        var result = await _service.CreateSessionAsync(
            sessionId,
            userId,
            "192.168.1.1",
            "Mozilla/5.0",
            "refresh-token-hash",
            expiresAt,
            "device-fingerprint");

        result.Id.Should().Be(sessionId);
        result.UserId.Should().Be(userId);
        result.RefreshToken.Should().Be("refresh-token-hash");
        result.ExpiresAt.Should().BeCloseTo(createdAt.AddDays(1), TimeSpan.FromMilliseconds(100));
        result.CreatedAt.Should().NotBe(default);
        _tokenLineageRepositoryMock.Verify(repository => repository.BindSessionAsync(
            userId, "refresh-token-hash", sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshSessionAsync_WithAuthenticationState_RotatesRefreshTokenHash()
    {
        var sessionId = Guid.NewGuid();
        var now = SystemClock.UtcNow;
        var expiresAt = now.AddDays(7);
        var session = new UserSession
        {
            Id = sessionId,
            IsActive = true,
            RefreshToken = "old-hash",
            UserId = Guid.NewGuid(),
            CreatedAt = now.AddMinutes(-10),
            LastUsedAt = now.AddMinutes(-5),
            ExpiresAt = expiresAt
        };

        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionRepositoryMock
            .Setup(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _service.RefreshSessionAsync(sessionId, "new-hash", expiresAt);

        result.Should().BeTrue();
        session.RefreshToken.Should().Be("new-hash");
        session.ExpiresAt.Should().Be(session.CreatedAt.AddDays(1));
        _tokenLineageRepositoryMock.Verify(repository => repository.BindSessionAsync(
            session.UserId, "new-hash", sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_ManualSessionDoesNotBindAnAbsentRefreshToken()
    {
        _sessionRepositoryMock.Setup(repository => repository.CreateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession session, CancellationToken _) => session);

        await _service.CreateSessionAsync(Guid.NewGuid(), "192.0.2.1", "test-agent");

        _tokenLineageRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateSessionAsync_MissingIssuedTokenFailsBeforeSuccessAudit()
    {
        _sessionRepositoryMock.Setup(repository => repository.CreateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession session, CancellationToken _) => session);
        _tokenLineageRepositoryMock.Setup(repository => repository.BindSessionAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateSessionAsync(Guid.NewGuid(),
            Guid.NewGuid(), "192.0.2.1", "test-agent", "missing-hash", SystemClock.UtcNow.AddHours(1)));

        _auditEventSinkMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateSessionAsync_BindingFailureOrCancellationPropagatesBeforeSuccessAudit(bool cancelled)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        _sessionRepositoryMock.Setup(repository => repository.CreateAsync(It.IsAny<UserSession>(), cancellation.Token))
            .ReturnsAsync((UserSession session, CancellationToken _) => session);
        Exception failure = cancelled ? new OperationCanceledException(cancellation.Token) : new InvalidOperationException("Synthetic lineage failure");
        _tokenLineageRepositoryMock.Setup(repository => repository.BindSessionAsync(userId, "hash", sessionId, cancellation.Token))
            .ThrowsAsync(failure);

        var actual = await Record.ExceptionAsync(() => _service.CreateSessionAsync(
            sessionId, userId, "192.0.2.1", "test-agent", "hash", SystemClock.UtcNow.AddHours(1), cancellationToken: cancellation.Token));

        actual.Should().BeSameAs(failure);
        _auditEventSinkMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetSessionAsync_ShouldReturnSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var expectedSession = new UserSession { Id = sessionId, UserId = Guid.NewGuid(), IsActive = true };

        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSession);

        // Act
        var result = await _service.GetSessionAsync(sessionId);

        // Assert
        result.Should().Be(expectedSession);
    }

    [Fact]
    public async Task GetSessionByRefreshTokenAsync_ShouldReturnSession()
    {
        // Arrange
        var refreshToken = "test-refresh-token";
        var expectedSession = new UserSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), IsActive = true };

        _sessionRepositoryMock.Setup(x => x.GetByRefreshTokenAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSession);

        // Act
        var result = await _service.GetSessionByRefreshTokenAsync(refreshToken);

        // Assert
        result.Should().Be(expectedSession);
    }

    [Fact]
    public async Task GetUserSessionsAsync_WithActiveOnly_ShouldReturnOnlyActiveSessions()
    {
        // Arrange - the expiry-aware active query already excludes inactive and expired rows;
        // the repository returns rows ordered by LastUsedAt descending, so the mocked list
        // is arranged in that order (ordering is the repository's contract, not the service's).
        var userId = Guid.NewGuid();
        var activeSessions = new List<UserSession>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1), LastUsedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1), LastUsedAt = DateTime.UtcNow.AddHours(-1) }
        };

        _sessionRepositoryMock.Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSessions);

        // Act
        var result = await _service.GetUserSessionsAsync(userId, activeOnly: true);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(s => s.IsActive.Should().BeTrue());
        result[0].LastUsedAt.Should().BeAfter(result[1].LastUsedAt); // Ordered by LastUsedAt descending
        _sessionRepositoryMock.Verify(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _sessionRepositoryMock.Verify(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUserSessionsAsync_WithActiveOnlyFalse_ShouldReturnAllSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = new List<UserSession>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = true, LastUsedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = false, LastUsedAt = DateTime.UtcNow.AddHours(-1) }
        };

        _sessionRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _service.GetUserSessionsAsync(userId, activeOnly: false);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ValidateSessionAsync_WithActiveValidSession_ShouldReturnTrue()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var now = SystemClock.UtcNow;
        var session = new UserSession
        {
            Id = sessionId, 
            UserId = Guid.NewGuid(), 
            IsActive = true,
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.AddDays(1)
        };

        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.ValidateSessionAsync(sessionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSessionAsync_WithInactiveSession_ShouldReturnFalse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new UserSession 
        { 
            Id = sessionId, 
            UserId = Guid.NewGuid(), 
            IsActive = false,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.ValidateSessionAsync(sessionId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSessionAsync_WithExpiredSession_ShouldDeactivateAndReturnFalse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new UserSession 
        { 
            Id = sessionId, 
            UserId = Guid.NewGuid(), 
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired
        };

        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.ValidateSessionAsync(sessionId);

        // Assert
        result.Should().BeFalse();
        session.IsActive.Should().BeFalse();
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateSessionAsync_ExpiresIdleSessionUsingConfiguredTimeout()
    {
        var now = SystemClock.UtcNow;
        var sessionId = Guid.NewGuid();
        var session = new UserSession
        {
            Id = sessionId,
            IsActive = true,
            CreatedAt = now.AddHours(-1),
            LastUsedAt = now.AddMinutes(-11),
            ExpiresAt = now.AddDays(1)
        };
        _sessionRepositoryMock
            .Setup(repository => repository.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessionRepositoryMock
            .Setup(repository => repository.UpdateAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var service = new SessionManagementService(
            _loggerMock.Object,
            _sessionRepositoryMock.Object,
            _trustedDeviceRepositoryMock.Object,
            _tokenLineageRepositoryMock.Object,
            new SessionOptions { IdleTimeoutMinutes = 10 });

        (await service.ValidateSessionAsync(sessionId)).Should().BeFalse();
        session.IsActive.Should().BeFalse();
        session.TerminationReason.Should().Be(SessionTerminationReason.Expired.ToString());
    }

    [Fact]
    public async Task RefreshSessionAsync_WithValidSession_ShouldUpdateTimestamps()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var now = SystemClock.UtcNow;
        var session = new UserSession
        {
            Id = sessionId, 
            UserId = Guid.NewGuid(), 
            IsActive = true,
            CreatedAt = now.AddMinutes(-10),
            LastUsedAt = now.AddMinutes(-5),
            ExpiresAt = now.AddDays(29)
        };

        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.RefreshSessionAsync(sessionId);

        // Assert
        result.Should().BeTrue();
        session.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.ExpiresAt.Should().Be(session.CreatedAt.AddDays(1));
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshSessionAsync_WithInactiveSession_ShouldReturnFalse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new UserSession 
        { 
            Id = sessionId, 
            UserId = Guid.NewGuid(), 
            IsActive = false
        };

        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.RefreshSessionAsync(sessionId);

        // Assert
        result.Should().BeFalse();
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TerminateSessionAsync_WithValidSession_ShouldDeactivateSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new UserSession 
        { 
            Id = sessionId, 
            UserId = Guid.NewGuid(), 
            IsActive = true
        };
        var reason = SessionTerminationReason.UserLogout;

        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.TerminateSessionAsync(sessionId, reason);

        // Assert
        result.Should().BeTrue();
        session.IsActive.Should().BeFalse();
        session.TerminationReason.Should().Be(reason.ToString());
        session.TerminatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TerminateSessionAsync_WithNonExistentSession_ShouldReturnFalse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession?)null);

        // Act
        var result = await _service.TerminateSessionAsync(sessionId, SessionTerminationReason.UserLogout);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TerminateAllUserSessionsAsync_ShouldTerminateAllExceptSpecified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exceptSessionId = Guid.NewGuid();
        var sessions = new List<UserSession>
        {
            new() { Id = exceptSessionId, UserId = userId, IsActive = true },
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = true },
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = true },
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = false } // Already inactive
        };

        _sessionRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _service.TerminateAllUserSessionsAsync(userId, SessionTerminationReason.SecurityViolation, exceptSessionId);

        // Assert
        result.Should().Be(2); // Only 2 active sessions terminated (excluding the except session and already inactive one)
        _sessionRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TrustDeviceAsync_WithNewDevice_ShouldCreateTrustedDevice()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceFingerprint = "device-fingerprint-123";
        var deviceName = "My iPhone";

        _trustedDeviceRepositoryMock.Setup(x => x.GetByUserAndFingerprintAsync(userId, deviceFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrustedDevice?)null);

        // Act
        var result = await _service.TrustDeviceAsync(userId, deviceFingerprint, deviceName);

        // Assert
        result.Should().BeTrue();
        _trustedDeviceRepositoryMock.Verify(x => x.CreateAsync(
            It.Is<TrustedDevice>(d => 
                d.UserId == userId && 
                d.DeviceFingerprint == deviceFingerprint && 
                d.DeviceName == deviceName &&
                d.IsActive), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrustDeviceAsync_UsesConfiguredExpirationAndRevokesOldestAtCapacity()
    {
        var userId = Guid.NewGuid();
        var existingDevice = new TrustedDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceFingerprint = "old-device",
            IsActive = true,
            TrustedAt = SystemClock.UtcNow.AddDays(-10),
            LastUsedAt = SystemClock.UtcNow.AddDays(-8)
        };
        TrustedDevice? createdDevice = null;
        _trustedDeviceRepositoryMock
            .Setup(repository => repository.GetByUserAndFingerprintAsync(userId, "new-device", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrustedDevice?)null);
        _trustedDeviceRepositoryMock
            .Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingDevice]);
        _trustedDeviceRepositoryMock
            .Setup(repository => repository.UpdateAsync(existingDevice, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);
        _trustedDeviceRepositoryMock
            .Setup(repository => repository.CreateAsync(It.IsAny<TrustedDevice>(), It.IsAny<CancellationToken>()))
            .Callback<TrustedDevice, CancellationToken>((device, _) => createdDevice = device)
            .ReturnsAsync((TrustedDevice device, CancellationToken _) => device);
        var service = new SessionManagementService(
            _loggerMock.Object,
            _sessionRepositoryMock.Object,
            _trustedDeviceRepositoryMock.Object,
            _tokenLineageRepositoryMock.Object,
            new SessionOptions { TrustedDeviceDurationDays = 7, MaxTrustedDevices = 1 });

        await service.TrustDeviceAsync(userId, "new-device", "new trusted device");

        existingDevice.IsActive.Should().BeFalse();
        createdDevice.Should().NotBeNull();
        createdDevice!.ExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TrustDeviceAsync_WithExistingActiveDevice_ShouldReturnTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceFingerprint = "device-fingerprint-123";
        var existingDevice = new TrustedDevice 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            DeviceFingerprint = deviceFingerprint,
            IsActive = true
        };

        _trustedDeviceRepositoryMock.Setup(x => x.GetByUserAndFingerprintAsync(userId, deviceFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        // Act
        var result = await _service.TrustDeviceAsync(userId, deviceFingerprint, "Device Name");

        // Assert
        result.Should().BeTrue();
        _trustedDeviceRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<TrustedDevice>(), It.IsAny<CancellationToken>()), Times.Never);
        _trustedDeviceRepositoryMock.Verify(x => x.UpdateAsync(existingDevice, It.IsAny<CancellationToken>()), Times.Once);
        existingDevice.ExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TrustDeviceAsync_WithExistingInactiveDevice_ShouldReactivate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceFingerprint = "device-fingerprint-123";
        var existingDevice = new TrustedDevice 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            DeviceFingerprint = deviceFingerprint,
            IsActive = false
        };

        _trustedDeviceRepositoryMock.Setup(x => x.GetByUserAndFingerprintAsync(userId, deviceFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        // Act
        var result = await _service.TrustDeviceAsync(userId, deviceFingerprint, "Device Name");

        // Assert
        result.Should().BeTrue();
        existingDevice.IsActive.Should().BeTrue();
        existingDevice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _trustedDeviceRepositoryMock.Verify(x => x.UpdateAsync(existingDevice, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsDeviceTrustedAsync_WithTrustedDevice_ShouldReturnTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceFingerprint = "device-fingerprint-123";
        var trustedDevice = new TrustedDevice 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            DeviceFingerprint = deviceFingerprint,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _trustedDeviceRepositoryMock.Setup(x => x.GetByUserAndFingerprintAsync(userId, deviceFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trustedDevice);

        // Act
        var result = await _service.IsDeviceTrustedAsync(userId, deviceFingerprint);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsDeviceTrustedAsync_WithInactiveDevice_ShouldReturnFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceFingerprint = "device-fingerprint-123";
        var trustedDevice = new TrustedDevice 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            DeviceFingerprint = deviceFingerprint,
            IsActive = false
        };

        _trustedDeviceRepositoryMock.Setup(x => x.GetByUserAndFingerprintAsync(userId, deviceFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trustedDevice);

        // Act
        var result = await _service.IsDeviceTrustedAsync(userId, deviceFingerprint);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsDeviceTrustedAsync_WithExpiredDevice_ShouldReturnFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceFingerprint = "device-fingerprint-123";
        var trustedDevice = new TrustedDevice 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            DeviceFingerprint = deviceFingerprint,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired
        };

        _trustedDeviceRepositoryMock.Setup(x => x.GetByUserAndFingerprintAsync(userId, deviceFingerprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trustedDevice);

        // Act
        var result = await _service.IsDeviceTrustedAsync(userId, deviceFingerprint);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetTrustedDevicesAsync_ShouldReturnOnlyActiveDevices()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var devices = new List<TrustedDevice>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = true },
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = false },
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = true }
        };

        _trustedDeviceRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);

        // Act
        var result = await _service.GetTrustedDevicesAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(d => d.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task RevokeTrustedDeviceAsync_WithValidDevice_ShouldDeactivate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var device = new TrustedDevice 
        { 
            Id = deviceId, 
            UserId = userId, 
            IsActive = true
        };

        _trustedDeviceRepositoryMock.Setup(x => x.GetByIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        // Act
        var result = await _service.RevokeTrustedDeviceAsync(userId, deviceId);

        // Assert
        result.Should().BeTrue();
        device.IsActive.Should().BeFalse();
        device.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _trustedDeviceRepositoryMock.Verify(x => x.UpdateAsync(device, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeTrustedDeviceAsync_WithDifferentUserId_ShouldReturnFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var device = new TrustedDevice 
        { 
            Id = deviceId, 
            UserId = Guid.NewGuid(), // Different user
            IsActive = true
        };

        _trustedDeviceRepositoryMock.Setup(x => x.GetByIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        // Act
        var result = await _service.RevokeTrustedDeviceAsync(userId, deviceId);

        // Assert
        result.Should().BeFalse();
        _trustedDeviceRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<TrustedDevice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_ShouldCallRepository()
    {
        // Arrange
        _sessionRepositoryMock.Setup(x => x.DeleteExpiredAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CleanupExpiredSessionsAsync();

        // Assert
        _sessionRepositoryMock.Verify(x => x.DeleteExpiredAsync(
            It.Is<DateTime>(dt => dt <= DateTime.UtcNow && dt >= DateTime.UtcNow.AddSeconds(-5)), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeSessionSecurityAsync_WithLowRisk_ShouldReturnLowRisk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = new List<UserSession>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = true, IpAddress = "192.168.1.1", DeviceFingerprint = "device1" },
            new() { Id = Guid.NewGuid(), UserId = userId, IsActive = false, IpAddress = "192.168.1.1", DeviceFingerprint = "device1" }
        };

        _sessionRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _service.AnalyzeSessionSecurityAsync(userId, "192.168.1.1", "Mozilla/5.0");

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(RiskLevel.Low);
        result.ActiveSessionCount.Should().Be(1);
        result.UnusualActivityDetected.Should().BeFalse();
        result.RiskFactors.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeSessionSecurityAsync_WithManyIPs_ShouldReturnMediumRisk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = new List<UserSession>();
        for (int i = 0; i < 15; i++)
        {
            sessions.Add(new UserSession 
            { 
                Id = Guid.NewGuid(), 
                UserId = userId, 
                IsActive = i < 5, 
                IpAddress = $"192.168.1.{i}", 
                DeviceFingerprint = "device1" 
            });
        }

        _sessionRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _service.AnalyzeSessionSecurityAsync(userId, "192.168.1.1", "Mozilla/5.0");

        // Assert
        result.RiskLevel.Should().Be(RiskLevel.Medium);
        result.UnusualActivityDetected.Should().BeTrue();
        result.RiskFactors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeSessionSecurityAsync_WithManyActiveSessions_ShouldReturnHighRisk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = new List<UserSession>();
        for (int i = 0; i < 12; i++)
        {
            sessions.Add(new UserSession 
            { 
                Id = Guid.NewGuid(), 
                UserId = userId, 
                IsActive = true, 
                IpAddress = "192.168.1.1", 
                DeviceFingerprint = "device1" 
            });
        }

        _sessionRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _service.AnalyzeSessionSecurityAsync(userId, "192.168.1.1", "Mozilla/5.0");

        // Assert
        result.RiskLevel.Should().Be(RiskLevel.High);
        result.ActiveSessionCount.Should().Be(12);
        result.UnusualActivityDetected.Should().BeTrue();
    }

    [Fact]
    public async Task GetActivityTimelineAsync_ShouldIncludeSessionAndDeviceEvents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = new List<UserSession>
        {
            new() 
            { 
                Id = Guid.NewGuid(), 
                UserId = userId, 
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                LastUsedAt = DateTime.UtcNow.AddDays(-4),
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0",
                DeviceFingerprint = "device1"
            }
        };

        var devices = new List<TrustedDevice>
        {
            new() 
            { 
                Id = Guid.NewGuid(), 
                UserId = userId, 
                DeviceName = "My Phone",
                DeviceFingerprint = "device1",
                TrustedAt = DateTime.UtcNow.AddDays(-3)
            }
        };

        _sessionRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _trustedDeviceRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);

        // Act
        var result = await _service.GetActivityTimelineAsync(userId, daysBack: 30);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.Should().Contain(e => e.ActivityType == "SessionCreated");
        result.Should().Contain(e => e.ActivityType == "SessionTerminated");
        result.Should().Contain(e => e.ActivityType == "DeviceTrusted");
        result.Should().BeInDescendingOrder(e => e.Timestamp); // Most recent first
    }
}

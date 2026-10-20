using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
/// Unit tests for the MfaService orchestrator.
/// Each test verifies that the orchestrator correctly delegates to the appropriate sub-service.
/// </summary>
public class MfaServiceTests
{
    private readonly Mock<ILogger<MfaService>> _loggerMock;
    private readonly Mock<ITotpMfaService> _totpMfaServiceMock;
    private readonly Mock<IBackupCodeMfaService> _backupCodeMfaServiceMock;
    private readonly Mock<IMfaAttemptTrackingService> _attemptTrackingServiceMock;
    private readonly Mock<GameGuild.Identity.Users.IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly MfaService _service;

    public MfaServiceTests()
    {
        _loggerMock = new Mock<ILogger<MfaService>>();
        _totpMfaServiceMock = new Mock<ITotpMfaService>();
        _backupCodeMfaServiceMock = new Mock<IBackupCodeMfaService>();
        _attemptTrackingServiceMock = new Mock<IMfaAttemptTrackingService>();
        _userRepositoryMock = new Mock<GameGuild.Identity.Users.IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();

        _service = new MfaService(
            _loggerMock.Object,
            _totpMfaServiceMock.Object,
            _backupCodeMfaServiceMock.Object,
            _attemptTrackingServiceMock.Object,
            _userRepositoryMock.Object,
            _passwordHasherMock.Object);
    }

    #region GetMfaConfigurationAsync Tests

    [Fact]
    public async Task GetMfaConfigurationAsync_ShouldDelegateToAttemptTrackingService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expected = new MfaConfigurationResponse
        {
            IsEnabled = true,
            EnabledMethods = new[] { "Totp", "BackupCode" },
            BackupCodesRemaining = 5
        };

        _attemptTrackingServiceMock
            .Setup(x => x.GetMfaConfigurationAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetMfaConfigurationAsync(userId);

        // Assert
        result.Should().BeSameAs(expected);
        _attemptTrackingServiceMock.Verify(
            x => x.GetMfaConfigurationAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region InitiateMfaSetupAsync Tests

    [Fact]
    public async Task InitiateMfaSetupAsync_ShouldDelegateToTotpAndBackupCodeServices()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedQrUri = "otpauth://totp/GameGuild:user@example.com?secret=ABC123";
        var expectedSecret = "ABC123";
        var expectedExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);

        _totpMfaServiceMock
            .Setup(x => x.SetupTotpAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedQrUri, expectedSecret, expectedExpiresAt));

        _backupCodeMfaServiceMock
            .Setup(x => x.GenerateBackupCode())
            .Returns("ABCD1234");

        _backupCodeMfaServiceMock
            .Setup(x => x.StoreBackupCodesForSetupAsync(userId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.InitiateMfaSetupAsync(userId, "user@example.com");

        // Assert
        result.Success.Should().BeTrue();
        result.SecretKey.Should().Be(expectedSecret);
        result.QrCodeUri.Should().Be(expectedQrUri);
        result.ExpiresAt.Should().Be(expectedExpiresAt);
        result.BackupCodes.Should().HaveCount(10);
        result.Message.Should().Be("MFA setup initiated successfully");

        _totpMfaServiceMock.Verify(
            x => x.SetupTotpAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _backupCodeMfaServiceMock.Verify(
            x => x.GenerateBackupCode(),
            Times.Exactly(10));
        _backupCodeMfaServiceMock.Verify(
            x => x.StoreBackupCodesForSetupAsync(userId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InitiateMfaSetupAsync_ShouldUseConfiguredBackupCodeCount()
    {
        var userId = Guid.NewGuid();
        _totpMfaServiceMock
            .Setup(x => x.SetupTotpAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("otpauth://totp/GameGuild:user@example.com?secret=ABC123", "ABC123", DateTimeOffset.UtcNow.AddMinutes(10)));
        _backupCodeMfaServiceMock.Setup(x => x.GenerateBackupCode()).Returns("ABCD1234");
        _backupCodeMfaServiceMock
            .Setup(x => x.StoreBackupCodesForSetupAsync(userId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new MfaService(
            _loggerMock.Object,
            _totpMfaServiceMock.Object,
            _backupCodeMfaServiceMock.Object,
            _attemptTrackingServiceMock.Object,
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            mfaOptions: new GameGuild.Configuration.ApplicationLayer.MfaOptions { BackupCodesCount = 4 });

        var result = await service.InitiateMfaSetupAsync(userId, "user@example.com");

        result.BackupCodes.Should().HaveCount(4);
        _backupCodeMfaServiceMock.Verify(x => x.GenerateBackupCode(), Times.Exactly(4));
    }

    [Fact]
    public async Task InitiateMfaSetupAsync_WhenTotpSetupFails_ShouldThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _totpMfaServiceMock
            .Setup(x => x.SetupTotpAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Setup failed"));

        // Act & Assert
        var act = () => _service.InitiateMfaSetupAsync(userId, "user@example.com");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Setup failed");
    }

    #endregion

    #region CompleteMfaSetupAsync Tests

    [Fact]
    public async Task CompleteMfaSetupAsync_WhenTotpValid_ShouldReturnSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var totpCode = "123456";

        _totpMfaServiceMock
            .Setup(x => x.VerifyTotpAsync(userId, totpCode, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CompleteMfaSetupAsync(userId, totpCode);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("MFA setup completed successfully");
        _totpMfaServiceMock.Verify(
            x => x.VerifyTotpAsync(userId, totpCode, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteMfaSetupAsync_WhenTotpInvalid_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var totpCode = "000000";

        _totpMfaServiceMock
            .Setup(x => x.VerifyTotpAsync(userId, totpCode, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.CompleteMfaSetupAsync(userId, totpCode);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid TOTP code");
    }

    [Fact]
    public async Task CompleteMfaSetupAsync_WhenExceptionThrown_ShouldThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var totpCode = "123456";

        _totpMfaServiceMock
            .Setup(x => x.VerifyTotpAsync(userId, totpCode, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Verification error"));

        // Act & Assert
        var act = () => _service.CompleteMfaSetupAsync(userId, totpCode);
        await act.Should().ThrowAsync<Exception>().WithMessage("Verification error");
    }

    #endregion

    #region VerifyMfaAsync Tests

    [Fact]
    public async Task VerifyMfaAsync_WithTotpMethod_ShouldDelegateToTotpService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "123456";

        _totpMfaServiceMock
            .Setup(x => x.VerifyTotpAsync(userId, code, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.VerifyMfaAsync(userId, code, MfaMethod.Totp);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("MFA verification successful");
        _totpMfaServiceMock.Verify(
            x => x.VerifyTotpAsync(userId, code, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _backupCodeMfaServiceMock.Verify(
            x => x.VerifyBackupCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyMfaAsync_WithBackupCodeMethod_ShouldDelegateToBackupCodeService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "ABCD1234";

        _backupCodeMfaServiceMock
            .Setup(x => x.VerifyBackupCodeAsync(userId, code, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.VerifyMfaAsync(userId, code, MfaMethod.BackupCode);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("MFA verification successful");
        _backupCodeMfaServiceMock.Verify(
            x => x.VerifyBackupCodeAsync(userId, code, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _totpMfaServiceMock.Verify(
            x => x.VerifyTotpAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyMfaAsync_WithInvalidTotpCode_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "000000";

        _totpMfaServiceMock
            .Setup(x => x.VerifyTotpAsync(userId, code, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.VerifyMfaAsync(userId, code, MfaMethod.Totp);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid MFA code");
    }

    [Fact]
    public async Task VerifyMfaAsync_WithUnsupportedMethod_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "123456";

        // Act
        var result = await _service.VerifyMfaAsync(userId, code, MfaMethod.Sms);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid MFA code");
    }

    [Fact]
    public async Task VerifyMfaAsync_DefaultMethod_ShouldUseTotp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "123456";

        _totpMfaServiceMock
            .Setup(x => x.VerifyTotpAsync(userId, code, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.VerifyMfaAsync(userId, code);

        // Assert
        result.Success.Should().BeTrue();
        _totpMfaServiceMock.Verify(
            x => x.VerifyTotpAsync(userId, code, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyMfaAsync_WhenExceptionThrown_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "123456";

        _totpMfaServiceMock
            .Setup(x => x.VerifyTotpAsync(userId, code, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _service.VerifyMfaAsync(userId, code, MfaMethod.Totp);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Service error");
    }

    #endregion

    #region DisableMfaAsync Tests (issue #244: password re-verification)

    private static GameGuild.Identity.Users.User CreateUserWithPasswordHash(string? passwordHash)
    {
        return new GameGuild.Identity.Users.User { Email = "user@example.com", Name = "User", PasswordHash = passwordHash, IsActive = true };
    }

    [Fact]
    public async Task DisableMfaAsync_WhenPasswordValid_ShouldDelegateToAttemptTrackingService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var password = "Correct-Horse-1!";
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUserWithPasswordHash("hash:$2a$11$storedhash"));
        _passwordHasherMock
            .Setup(x => x.VerifyPassword("hash:$2a$11$storedhash", password))
            .Returns(true);
        _attemptTrackingServiceMock
            .Setup(x => x.DisableMfaAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DisableMfaAsync(userId, password);

        // Assert
        result.Should().BeTrue();
        _passwordHasherMock.Verify(x => x.VerifyPassword("hash:$2a$11$storedhash", password), Times.Once);
        _attemptTrackingServiceMock.Verify(
            x => x.DisableMfaAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisableMfaAsync_WhenPasswordInvalid_ShouldReturnFalseWithoutDisabling()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUserWithPasswordHash("hash:$2a$11$storedhash"));
        _passwordHasherMock
            .Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _service.DisableMfaAsync(userId, "wrong-password");

        // Assert
        result.Should().BeFalse();
        _attemptTrackingServiceMock.Verify(
            x => x.DisableMfaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DisableMfaAsync_WhenUserUnknown_ShouldReturnFalseWithoutDisabling()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameGuild.Identity.Users.User?)null);

        // Act
        var result = await _service.DisableMfaAsync(userId, "any-password");

        // Assert
        result.Should().BeFalse();
        _attemptTrackingServiceMock.Verify(
            x => x.DisableMfaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DisableMfaAsync_WhenUserHasNoPasswordCredential_ShouldReturnFalse()
    {
        // Arrange: passwordless (OAuth-only) accounts cannot disable MFA this way.
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUserWithPasswordHash(null!));

        // Act
        var result = await _service.DisableMfaAsync(userId, "any-password");

        // Assert
        result.Should().BeFalse();
        _attemptTrackingServiceMock.Verify(
            x => x.DisableMfaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GenerateBackupCodesAsync Tests

    [Fact]
    public async Task GenerateBackupCodesAsync_ShouldDelegateToBackupCodeService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expected = new[] { "CODE1", "CODE2", "CODE3" };

        _backupCodeMfaServiceMock
            .Setup(x => x.GenerateBackupCodesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GenerateBackupCodesAsync(userId);

        // Assert
        result.Should().BeSameAs(expected);
        _backupCodeMfaServiceMock.Verify(
            x => x.GenerateBackupCodesAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region VerifyBackupCodeAsync Tests

    [Fact]
    public async Task VerifyBackupCodeAsync_ShouldDelegateToBackupCodeService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var backupCode = "ABCD1234";
        var deviceId = "device-123";

        _backupCodeMfaServiceMock
            .Setup(x => x.VerifyBackupCodeAsync(userId, backupCode, deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.VerifyBackupCodeAsync(userId, backupCode, deviceId);

        // Assert
        result.Should().BeTrue();
        _backupCodeMfaServiceMock.Verify(
            x => x.VerifyBackupCodeAsync(userId, backupCode, deviceId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_WithNullDeviceId_ShouldDelegateCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var backupCode = "ABCD1234";

        _backupCodeMfaServiceMock
            .Setup(x => x.VerifyBackupCodeAsync(userId, backupCode, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.VerifyBackupCodeAsync(userId, backupCode);

        // Assert
        result.Should().BeFalse();
        _backupCodeMfaServiceMock.Verify(
            x => x.VerifyBackupCodeAsync(userId, backupCode, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GenerateQrCodeAsync Tests

    [Fact]
    public async Task GenerateQrCodeAsync_ShouldDelegateToTotpService()
    {
        // Arrange
        var qrCodeData = "otpauth://totp/GameGuild:user@test.com?secret=ABC123";
        var expected = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header bytes

        _totpMfaServiceMock
            .Setup(x => x.GenerateQrCodeAsync(qrCodeData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GenerateQrCodeAsync(qrCodeData);

        // Assert
        result.Should().BeSameAs(expected);
        _totpMfaServiceMock.Verify(
            x => x.GenerateQrCodeAsync(qrCodeData, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region IsMfaEnabledAsync Tests

    [Fact]
    public async Task IsMfaEnabledAsync_ShouldDelegateToAttemptTrackingService()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _attemptTrackingServiceMock
            .Setup(x => x.GetMfaStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsMfaEnabledAsync(userId);

        // Assert
        result.Should().BeTrue();
        _attemptTrackingServiceMock.Verify(
            x => x.GetMfaStatusAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IsMfaEnabledAsync_WhenDisabled_ShouldReturnFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _attemptTrackingServiceMock
            .Setup(x => x.GetMfaStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsMfaEnabledAsync(userId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsMfaRequiredAsync Tests

    [Fact]
    public async Task IsMfaRequiredAsync_ShouldDelegateToIsMfaRequiredByPolicyAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _attemptTrackingServiceMock
            .Setup(x => x.IsMfaRequiredByPolicyAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsMfaRequiredAsync(userId);

        // Assert
        result.Should().BeTrue();
        _attemptTrackingServiceMock.Verify(
            x => x.IsMfaRequiredByPolicyAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ResetMfaFailedAttemptsAsync Tests

    [Fact]
    public async Task ResetMfaFailedAttemptsAsync_ShouldDelegateToAttemptTrackingService()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _attemptTrackingServiceMock
            .Setup(x => x.ResetFailedAttemptsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.ResetMfaFailedAttemptsAsync(userId);

        // Assert
        _attemptTrackingServiceMock.Verify(
            x => x.ResetFailedAttemptsAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region IsUserLockedOutAsync Tests

    [Fact]
    public async Task IsUserLockedOutAsync_ShouldDelegateToAttemptTrackingService()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _attemptTrackingServiceMock
            .Setup(x => x.IsUserLockedOutAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsUserLockedOutAsync(userId);

        // Assert
        result.Should().BeTrue();
        _attemptTrackingServiceMock.Verify(
            x => x.IsUserLockedOutAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IsUserLockedOutAsync_WhenNotLockedOut_ShouldReturnFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _attemptTrackingServiceMock
            .Setup(x => x.IsUserLockedOutAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsUserLockedOutAsync(userId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}

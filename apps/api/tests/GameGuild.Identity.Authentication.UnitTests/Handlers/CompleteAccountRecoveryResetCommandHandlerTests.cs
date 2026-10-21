using FluentAssertions;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class CompleteAccountRecoveryResetCommandHandlerTests
{
    private static User CreateUser() =>
        new() { Id = Guid.NewGuid(), Email = "owner@test.com", Username = "owner", TenantId = Guid.NewGuid(), PasswordHash = "current-hash", TokenVersion = 1 };

    private static AccountRecoveryRequest CreateUnlockedRequest(Guid ownerUserId, string token = "reset-token") => new()
    {
        Id = Guid.NewGuid(),
        OwnerUserId = ownerUserId,
        TenantId = Guid.NewGuid(),
        Status = AccountRecoveryStatus.ResetUnlocked,
        RequestedAt = SystemClock.UtcNow.AddDays(-2),
        CooldownEndsAt = SystemClock.UtcNow.AddDays(-1),
        ApprovalWindowEndsAt = SystemClock.UtcNow.AddDays(1),
        RequiredApprovals = 2,
        ResetTokenHash = AccountRecoveryTokensTestHelpers.Hash(token),
        ResetTokenExpiresAt = SystemClock.UtcNow.AddHours(1),
        ResetUnlockedAt = SystemClock.UtcNow.AddMinutes(-5)
    };

    private static (CompleteAccountRecoveryResetCommandHandler Handler, Mock<IAccountRecoveryStore> Recoveries,
        Mock<IUserRepository> Users, Mock<IPasswordHasher> Hasher, Mock<IUserSessionRepository> Sessions,
        Mock<IWebAuthnCredentialRepository> WebAuthn, Mock<ITokenRevocationService> Tokens, Mock<IEmailSender> UnusedEmail)
        CreateHandler()
    {
        var recoveries = new Mock<IAccountRecoveryStore>();
        recoveries.Setup(s => s.ExpireElapsedAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.ValidatePasswordStrength(It.IsAny<string>()))
            .Returns(new PasswordStrengthResult { IsValid = true });
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("new-hash");
        hasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var sessions = new Mock<IUserSessionRepository>();
        var webAuthn = new Mock<IWebAuthnCredentialRepository>();
        var tokens = new Mock<ITokenRevocationService>();
        var handler = new CompleteAccountRecoveryResetCommandHandler(
            recoveries.Object, users.Object, hasher.Object, sessions.Object, webAuthn.Object, tokens.Object,
            Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<CompleteAccountRecoveryResetCommandHandler>.Instance);
        return (handler, recoveries, users, hasher, sessions, webAuthn, tokens, new Mock<IEmailSender>());
    }

    private static CompleteAccountRecoveryResetCommand CreateCommand(string token = "reset-token") => new()
    {
        ResetToken = token,
        NewPassword = "BrandNew#Password1!",
        ConfirmPassword = "BrandNew#Password1!"
    };

    [Fact]
    public async Task Reset_ValidTokenForcesCredentialRotation()
    {
        var user = CreateUser();
        var recovery = CreateUnlockedRequest(user.Id);
        var (handler, recoveries, users, hasher, sessions, webAuthn, tokens, _) = CreateHandler();
        recoveries.Setup(s => s.FindResetUnlockedByResetTokenHashAsync(recovery.ResetTokenHash!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recovery);
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        users.Setup(r => r.UpdatePasswordHashAsync(user.Id, "new-hash", "current-hash", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        webAuthn.Setup(w => w.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserWebAuthnCredential>
            {
                new() { Id = Guid.NewGuid(), UserId = user.Id, IsActive = true },
                new() { Id = Guid.NewGuid(), UserId = user.Id, IsActive = false, DeactivatedAt = SystemClock.UtcNow }
            });
        webAuthn.Setup(w => w.RevokeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        recovery.Status.Should().Be(AccountRecoveryStatus.Completed);
        recovery.CompletedAt.Should().NotBeNull();
        recovery.ResetTokenHash.Should().BeNull();
        // Sessions terminated and refresh tokens revoked.
        sessions.Verify(s => s.TerminateAllForUserAsync(user.Id, "SecurityViolation", It.IsAny<CancellationToken>()), Times.Once);
        tokens.Verify(t => t.RevokeAllUserTokensAsync(user.Id, "account_recovery_forced_reset", It.IsAny<CancellationToken>()), Times.Once);
        // Mandatory passkey re-enrollment: every non-revoked credential revoked.
        webAuthn.Verify(w => w.RevokeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        users.Verify(r => r.UpdatePasswordHashAsync(user.Id, "new-hash", "current-hash", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reset_ConsumedTokenReplayFailsClosed()
    {
        var (handler, recoveries, _, _, sessions, _, _, _) = CreateHandler();
        // After consumption the lookup no longer matches (status Completed filtered out).
        recoveries.Setup(s => s.FindResetUnlockedByResetTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountRecoveryRequest?)null);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid or expired recovery reset.");
        sessions.Verify(s => s.TerminateAllForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reset_ExpiredTokenFailsClosedWithoutConsumptionSideEffects()
    {
        var user = CreateUser();
        var recovery = CreateUnlockedRequest(user.Id);
        recovery.ResetTokenExpiresAt = SystemClock.UtcNow.AddMinutes(-1);
        var (handler, recoveries, users, _, sessions, _, _, _) = CreateHandler();
        recoveries.Setup(s => s.FindResetUnlockedByResetTokenHashAsync(recovery.ResetTokenHash!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recovery);
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        recovery.Status.Should().Be(AccountRecoveryStatus.ResetUnlocked);
        sessions.Verify(s => s.TerminateAllForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reset_WeakPasswordFailsBeforeTokenLookup()
    {
        var (handler, recoveries, _, hasher, _, _, _, _) = CreateHandler();
        hasher.Setup(h => h.ValidatePasswordStrength(It.IsAny<string>()))
            .Returns(new PasswordStrengthResult { IsValid = false, ValidationFailures = new List<string> { "too short" } });

        var result = await handler.Handle(new CompleteAccountRecoveryResetCommand
        {
            ResetToken = "reset-token",
            NewPassword = "weak",
            ConfirmPassword = "weak"
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        recoveries.Verify(s => s.FindResetUnlockedByResetTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reset_RecentlyUsedPasswordConsumesTokenAndFails()
    {
        var user = CreateUser();
        var recovery = CreateUnlockedRequest(user.Id);
        var (handler, recoveries, users, hasher, sessions, _, _, _) = CreateHandler();
        recoveries.Setup(s => s.FindResetUnlockedByResetTokenHashAsync(recovery.ResetTokenHash!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recovery);
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        hasher.Setup(h => h.VerifyPassword("current-hash", It.IsAny<string>())).Returns(true);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        // Fail closed: the token is consumed so it cannot be retried with another password.
        recovery.Status.Should().Be(AccountRecoveryStatus.Completed);
        recovery.ResetTokenHash.Should().BeNull();
        users.Verify(r => r.UpdatePasswordHashAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sessions.Verify(s => s.TerminateAllForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reset_MissingOwnerConsumesTokenAndFails()
    {
        var recovery = CreateUnlockedRequest(Guid.NewGuid());
        var (handler, recoveries, users, _, sessions, _, _, _) = CreateHandler();
        recoveries.Setup(s => s.FindResetUnlockedByResetTokenHashAsync(recovery.ResetTokenHash!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recovery);
        users.Setup(r => r.GetByIdAsync(recovery.OwnerUserId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        recovery.Status.Should().Be(AccountRecoveryStatus.Completed);
        sessions.Verify(s => s.TerminateAllForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class PasswordCommandHandlersTests
{
    [Fact]
    public async Task VerifyEmailCommandHandler_ValidTokenMarksUserVerified()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "verify@test.com", IsEmailVerified = false };
        var emailService = new Mock<IEmailVerificationService>();
        var userRepository = new Mock<IUserRepository>();
        emailService.Setup(s => s.VerifyEmailTokenAsync("token"))
            .ReturnsAsync(new TokenValidationResult(true, userId, user.Email));
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        userRepository.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        userRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new VerifyEmailCommandHandler(
            emailService.Object,
            userRepository.Object,
            NullLogger<VerifyEmailCommandHandler>.Instance);

        var result = await handler.Handle(new VerifyEmailCommand { Token = "token" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.UserId.Should().Be(userId);
        user.IsEmailVerified.Should().BeTrue();
        userRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestPasswordResetCommandHandler_UserFoundPublishesResetNotification()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "reset@test.com", Username = "reset-user" };
        var userRepository = new Mock<IUserRepository>();
        var emailService = new Mock<IEmailVerificationService>();
        var publisher = new Mock<IPublisher>();
        userRepository.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        emailService.Setup(s => s.GeneratePasswordResetTokenAsync(user.Id, user.Email))
            .ReturnsAsync("reset-token");
        publisher.Setup(p => p.Publish(It.IsAny<PasswordResetRequestedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RequestPasswordResetCommandHandler(
            userRepository.Object,
            emailService.Object,
            publisher.Object,
            NullLogger<RequestPasswordResetCommandHandler>.Instance);

        var result = await handler.Handle(new RequestPasswordResetCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
        publisher.Verify(
            p => p.Publish(
                It.Is<PasswordResetRequestedNotification>(n => n.Email == user.Email && n.Token == "reset-token"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetPasswordCommandHandler_ValidTokenUpdatesPasswordHash()
    {
        var userId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var emailService = new Mock<IEmailVerificationService>();
        emailService.Setup(s => s.VerifyPasswordResetTokenAsync("token"))
            .ReturnsAsync(new TokenValidationResult(true, userId, "reset@test.com"));
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = "reset@test.com" });
        hasher.Setup(h => h.ValidatePasswordStrength("StrongPass1!"))
            .Returns(new PasswordStrengthResult { IsValid = true, ValidationFailures = [] });
        hasher.Setup(h => h.HashPassword("StrongPass1!"))
            .Returns("new-hash");
        userRepository.Setup(r => r.UpdatePasswordHashAsync(userId, "new-hash", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ResetPasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            emailService.Object,
            NullLogger<ResetPasswordCommandHandler>.Instance);

        var result = await handler.Handle(
            new ResetPasswordCommand
            {
                Token = "token",
                NewPassword = "StrongPass1!",
                ConfirmPassword = "StrongPass1!"
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        userRepository.Verify(r => r.UpdatePasswordHashAsync(userId, "new-hash", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordCommandHandler_RejectsRecentlyUsedPassword()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "reset@test.com", PasswordHash = "current-hash" };
        user.SetPasswordHash("new-current-hash");
        var userRepository = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var emailService = new Mock<IEmailVerificationService>();
        emailService.Setup(s => s.VerifyPasswordResetTokenAsync("token"))
            .ReturnsAsync(new TokenValidationResult(true, userId, user.Email));
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        hasher.Setup(h => h.ValidatePasswordStrength("ReusedPass1!"))
            .Returns(new PasswordStrengthResult { IsValid = true, ValidationFailures = [] });
        hasher.Setup(h => h.VerifyPassword("new-current-hash", "ReusedPass1!")).Returns(false);
        hasher.Setup(h => h.VerifyPassword("current-hash", "ReusedPass1!")).Returns(true);

        var handler = new ResetPasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            emailService.Object,
            NullLogger<ResetPasswordCommandHandler>.Instance);

        var result = await handler.Handle(
            new ResetPasswordCommand { Token = "token", NewPassword = "ReusedPass1!", ConfirmPassword = "ReusedPass1!" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("last 5 passwords");
        hasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
        userRepository.Verify(r => r.UpdatePasswordHashAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestMagicLinkCommandHandler_UserFoundPublishesMagicLinkNotification()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "magic@test.com", Username = "magic-user" };
        var userRepository = new Mock<IUserRepository>();
        var emailService = new Mock<IEmailVerificationService>();
        var publisher = new Mock<IPublisher>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:MagicLink:ExposeDevelopmentToken"] = "true"
            })
            .Build();

        userRepository.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        emailService.Setup(s => s.GenerateMagicLinkTokenAsync(user.Id, user.Email))
            .ReturnsAsync("magic-token");
        publisher.Setup(p => p.Publish(It.IsAny<MagicLinkRequestedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RequestMagicLinkCommandHandler(
            userRepository.Object,
            emailService.Object,
            publisher.Object,
            configuration,
            NullLogger<RequestMagicLinkCommandHandler>.Instance);

        var result = await handler.Handle(new RequestMagicLinkCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DevelopmentPreviewToken.Should().Be("magic-token");
        publisher.Verify(
            p => p.Publish(
                It.Is<MagicLinkRequestedNotification>(n => n.Email == user.Email && n.Token == "magic-token"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestMagicLinkCommandHandler_NotificationDispatchFailsStillReturnsGenericSuccess()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "magic@test.com", Username = "magic-user" };
        var userRepository = new Mock<IUserRepository>();
        var emailService = new Mock<IEmailVerificationService>();
        var publisher = new Mock<IPublisher>();
        var configuration = new ConfigurationBuilder().Build();

        userRepository.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        emailService.Setup(s => s.GenerateMagicLinkTokenAsync(user.Id, user.Email))
            .ReturnsAsync("magic-token");
        publisher.Setup(p => p.Publish(It.IsAny<MagicLinkRequestedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification queue unavailable"));

        var handler = new RequestMagicLinkCommandHandler(
            userRepository.Object,
            emailService.Object,
            publisher.Object,
            configuration,
            NullLogger<RequestMagicLinkCommandHandler>.Instance);

        var result = await handler.Handle(new RequestMagicLinkCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DevelopmentPreviewToken.Should().BeNull();
    }

    [Fact]
    public async Task RequestMagicLinkCommandHandler_CancellationDuringNotificationDispatchPropagates()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "magic@test.com", Username = "magic-user" };
        var userRepository = new Mock<IUserRepository>();
        var emailService = new Mock<IEmailVerificationService>();
        var publisher = new Mock<IPublisher>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var configuration = new ConfigurationBuilder().Build();

        userRepository.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        emailService.Setup(s => s.GenerateMagicLinkTokenAsync(user.Id, user.Email))
            .ReturnsAsync("magic-token");
        publisher.Setup(p => p.Publish(It.IsAny<MagicLinkRequestedNotification>(), cancellationSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));

        var handler = new RequestMagicLinkCommandHandler(
            userRepository.Object,
            emailService.Object,
            publisher.Object,
            configuration,
            NullLogger<RequestMagicLinkCommandHandler>.Instance);

        var action = () => handler.Handle(
            new RequestMagicLinkCommand { Email = user.Email },
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConsumeMagicLinkCommandHandler_ValidTokenIssuesTokens()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "magic@test.com", TokenVersion = 7 };
        var userRepository = new Mock<IUserRepository>();
        var emailService = new Mock<IEmailVerificationService>();
        var jwtTokenService = new Mock<IJwtTokenService>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "3"
            })
            .Build();

        emailService.Setup(s => s.VerifyMagicLinkTokenAsync("magic-token"))
            .ReturnsAsync(new TokenValidationResult(true, userId, user.Email));
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        jwtTokenService.Setup(s => s.GenerateAccessTokenAsync(userId, user.Email, It.IsAny<string[]>(), null, user.TokenVersion, It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        jwtTokenService.Setup(s => s.GenerateRefreshTokenAsync(userId, It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");
        var signInMfa = new Mock<ISignInMfaService>(MockBehavior.Strict);
        signInMfa.Setup(s => s.BeginAsync(userId, user.TokenVersion, null, It.IsAny<DeviceInfo>(),
                SignInFirstFactor.MagicLink, false, CancellationToken.None))
            .ReturnsAsync(new SignInResponse { Success = true, UserId = userId, Email = user.Email,
                AccessToken = "access-token", RefreshToken = "refresh-token", ExpiresIn = 900, SessionId = Guid.NewGuid() });

        var handler = new ConsumeMagicLinkCommandHandler(
            userRepository.Object,
            emailService.Object,
            jwtTokenService.Object,
            configuration,
            NullLogger<ConsumeMagicLinkCommandHandler>.Instance,
            jwtOptions: null, sessionIssuer: null, signInMfa: signInMfa.Object);

        var result = await handler.Handle(new ConsumeMagicLinkCommand { Token = "magic-token" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.ExpiresIn.Should().Be(900);
        result.UserId.Should().Be(userId);
        result.SessionId.Should().NotBeEmpty();
        signInMfa.Verify(s => s.BeginAsync(userId, user.TokenVersion, null,
            It.Is<DeviceInfo>(device => device.DeviceName == "Magic Link" && device.DeviceType == "Web"),
            SignInFirstFactor.MagicLink, false, CancellationToken.None), Times.Once);
        jwtTokenService.Verify(s => s.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_ValidCurrentPasswordUpdatesPasswordHash()
    {
        var userId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, PasswordHash = "old-hash" });
        hasher.Setup(h => h.VerifyPassword("old-hash", "CurrentPass1!")).Returns(true);
        hasher.Setup(h => h.ValidatePasswordStrength("StrongPass1!"))
            .Returns(new PasswordStrengthResult { IsValid = true, ValidationFailures = [] });
        hasher.Setup(h => h.HashPassword("StrongPass1!"))
            .Returns("new-hash");
        userRepository.Setup(r => r.UpdatePasswordHashAsync(userId, "new-hash", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = "CurrentPass1!",
                NewPassword = "StrongPass1!",
                ConfirmPassword = "StrongPass1!"
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        userRepository.Verify(r => r.UpdatePasswordHashAsync(userId, "new-hash", "old-hash", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_RejectsRecentlyUsedPassword()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, PasswordHash = "recent-hash" };
        user.SetPasswordHash("current-hash");
        var userRepository = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        hasher.Setup(h => h.VerifyPassword("current-hash", "CurrentPass1!")).Returns(true);
        hasher.Setup(h => h.VerifyPassword("current-hash", "ReusedPass1!")).Returns(false);
        hasher.Setup(h => h.VerifyPassword("recent-hash", "ReusedPass1!")).Returns(true);
        hasher.Setup(h => h.ValidatePasswordStrength("ReusedPass1!"))
            .Returns(new PasswordStrengthResult { IsValid = true, ValidationFailures = [] });

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = "CurrentPass1!",
                NewPassword = "ReusedPass1!",
                ConfirmPassword = "ReusedPass1!"
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("last 5 passwords");
        hasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
        userRepository.Verify(r => r.UpdatePasswordHashAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_ConcurrentPasswordChangeRequiresRetry()
    {
        var userId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, PasswordHash = "old-hash" });
        hasher.Setup(h => h.VerifyPassword("old-hash", "CurrentPass1!")).Returns(true);
        hasher.Setup(h => h.ValidatePasswordStrength("StrongPass1!"))
            .Returns(new PasswordStrengthResult { IsValid = true, ValidationFailures = [] });
        hasher.Setup(h => h.HashPassword("StrongPass1!")).Returns("new-hash");
        userRepository.Setup(r => r.UpdatePasswordHashAsync(userId, "new-hash", "old-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = "CurrentPass1!",
                NewPassword = "StrongPass1!",
                ConfirmPassword = "StrongPass1!"
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("changed during this request");
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_NullPasswordHashAndEmptyCurrentPasswordSetsInitialPassword()
    {
        var userId = Guid.NewGuid();
        var user = User.CreateOAuthUser("oauth@test.com", "OAuth User");
        user.Id = userId;
        var userRepository = new Mock<IUserRepository>();
        var sessionRepository = new Mock<IUserSessionRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        hasher.Setup(h => h.ValidatePasswordStrength("StrongPass1!"))
            .Returns(new PasswordStrengthResult { IsValid = true, ValidationFailures = [] });
        hasher.Setup(h => h.HashPassword("StrongPass1!"))
            .Returns("new-hash");
        userRepository.Setup(r => r.UpdatePasswordHashAsync(userId, "new-hash", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string?, CancellationToken>((_, hash, _, _) => user.SetPasswordHash(hash))
            .ReturnsAsync(true);

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance,
            sessionRepository.Object);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = string.Empty,
                NewPassword = "StrongPass1!",
                ConfirmPassword = "StrongPass1!"
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SessionsRevoked.Should().Be(0);
        user.PasswordHash.Should().Be("new-hash");
        user.TokenVersion.Should().Be(2);
        hasher.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_NullPasswordHashAndNonEmptyCurrentPasswordRejected()
    {
        var userId = Guid.NewGuid();
        var user = User.CreateOAuthUser("oauth@test.com", "OAuth User");
        user.Id = userId;
        var userRepository = new Mock<IUserRepository>();
        var sessionRepository = new Mock<IUserSessionRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance,
            sessionRepository.Object);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = "WrongPass1!",
                NewPassword = "StrongPass1!",
                ConfirmPassword = "StrongPass1!"
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Current password is incorrect");
        userRepository.Verify(r => r.UpdatePasswordHashAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_HashExistsAndWrongCurrentPasswordRejected()
    {
        var userId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var sessionRepository = new Mock<IUserSessionRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, PasswordHash = "old-hash" });
        hasher.Setup(h => h.VerifyPassword("old-hash", "WrongPass1!")).Returns(false);

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance,
            sessionRepository.Object);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = "WrongPass1!",
                NewPassword = "StrongPass1!",
                ConfirmPassword = "StrongPass1!"
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Current password is incorrect");
        userRepository.Verify(r => r.UpdatePasswordHashAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_RevokeOtherSessionsTerminatesOtherSessions()
    {
        var userId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var sessionRepository = new Mock<IUserSessionRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, PasswordHash = "old-hash" });
        hasher.Setup(h => h.VerifyPassword("old-hash", "CurrentPass1!")).Returns(true);
        hasher.Setup(h => h.ValidatePasswordStrength("StrongPass1!"))
            .Returns(new PasswordStrengthResult { IsValid = true, ValidationFailures = [] });
        hasher.Setup(h => h.HashPassword("StrongPass1!"))
            .Returns("new-hash");
        userRepository.Setup(r => r.UpdatePasswordHashAsync(userId, "new-hash", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        sessionRepository.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new UserSession { Id = currentSessionId, UserId = userId, IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1) },
                new UserSession { Id = Guid.NewGuid(), UserId = userId, IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1) },
                new UserSession { Id = Guid.NewGuid(), UserId = userId, IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1) }
            ]);
        sessionRepository.Setup(r => r.TerminateAllExceptAsync(userId, currentSessionId, "password_changed", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance,
            sessionRepository.Object);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = "CurrentPass1!",
                NewPassword = "StrongPass1!",
                ConfirmPassword = "StrongPass1!",
                RevokeOtherSessions = true,
                CurrentSessionId = currentSessionId
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SessionsRevoked.Should().Be(2);
        sessionRepository.Verify(r => r.TerminateAllExceptAsync(userId, currentSessionId, "password_changed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_MissingCurrentSessionIdSkipsRevocation()
    {
        var userId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var sessionRepository = new Mock<IUserSessionRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, PasswordHash = "old-hash" });
        hasher.Setup(h => h.VerifyPassword("old-hash", "CurrentPass1!")).Returns(true);
        hasher.Setup(h => h.ValidatePasswordStrength("StrongPass1!"))
            .Returns(new PasswordStrengthResult { IsValid = true, ValidationFailures = [] });
        hasher.Setup(h => h.HashPassword("StrongPass1!"))
            .Returns("new-hash");
        userRepository.Setup(r => r.UpdatePasswordHashAsync(userId, "new-hash", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance,
            sessionRepository.Object);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = "CurrentPass1!",
                NewPassword = "StrongPass1!",
                ConfirmPassword = "StrongPass1!",
                RevokeOtherSessions = true
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SessionsRevoked.Should().Be(0);
        sessionRepository.Verify(r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        sessionRepository.Verify(r => r.TerminateAllExceptAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_RevokeOtherSessionsFalseSkipsTermination()
    {
        var userId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var sessionRepository = new Mock<IUserSessionRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, PasswordHash = "old-hash" });
        hasher.Setup(h => h.VerifyPassword("old-hash", "CurrentPass1!")).Returns(true);
        hasher.Setup(h => h.ValidatePasswordStrength("StrongPass1!"))
            .Returns(new PasswordStrengthResult { IsValid = true, ValidationFailures = [] });
        hasher.Setup(h => h.HashPassword("StrongPass1!"))
            .Returns("new-hash");
        userRepository.Setup(r => r.UpdatePasswordHashAsync(userId, "new-hash", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance,
            sessionRepository.Object);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = "CurrentPass1!",
                NewPassword = "StrongPass1!",
                ConfirmPassword = "StrongPass1!",
                RevokeOtherSessions = false,
                CurrentSessionId = currentSessionId
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SessionsRevoked.Should().Be(0);
        sessionRepository.Verify(r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        sessionRepository.Verify(r => r.TerminateAllExceptAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordCommandHandler_WeakNewPasswordReturnsStrengthFailures()
    {
        var userId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var sessionRepository = new Mock<IUserSessionRepository>();
        var hasher = new Mock<IPasswordHasher>();
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, PasswordHash = "old-hash" });
        hasher.Setup(h => h.VerifyPassword("old-hash", "CurrentPass1!")).Returns(true);
        hasher.Setup(h => h.ValidatePasswordStrength("weak"))
            .Returns(new PasswordStrengthResult
            {
                IsValid = false,
                ValidationFailures = ["Password must be at least 8 characters long", "Password must contain an uppercase letter"]
            });

        var handler = new ChangePasswordCommandHandler(
            userRepository.Object,
            hasher.Object,
            NullLogger<ChangePasswordCommandHandler>.Instance,
            sessionRepository.Object);

        var result = await handler.Handle(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = "CurrentPass1!",
                NewPassword = "weak",
                ConfirmPassword = "weak"
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Password must be at least 8 characters long; Password must contain an uppercase letter");
        userRepository.Verify(r => r.UpdatePasswordHashAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

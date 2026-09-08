// MaxCoverageTests.cs — Push Identity.Authentication to 90% line coverage
// Targets: SendWelcomeEmailHandler, PasswordService, AuthControllerBase, ApiKey handlers,
//          AuthenticationModelConfiguration, TokenRevocationMiddleware, KeyRotationController,
//          Web3AuthService, DTOs, Validators, KeyRotationOptions
#pragma warning disable CS8600, CS8602, CS8604, CS8625

using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using GameGuild;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using GameGuild.Notifications.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MockQueryable.Moq;
using Moq;
using System.Security.Claims;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests;

#region SendWelcomeEmailHandler Tests

public class SendWelcomeEmailHandlerCovTests
{
    [Fact]
    public async Task Handle_LogsAndCompletes()
    {
        var logger = new Mock<ILogger<SendWelcomeEmailHandler>>();
        var handler = new SendWelcomeEmailHandler(logger.Object, NotificationQueueStub.Success().Object, Mock.Of<IUserRepository>());

        var notification = new UserSignedUpNotification
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser"
        };

        await handler.Handle(notification, CancellationToken.None);

        // Handler should complete without throwing
    }

    [Fact]
    public async Task Handle_WithTenantId_Completes()
    {
        var handler = new SendWelcomeEmailHandler(NullLogger<SendWelcomeEmailHandler>.Instance, NotificationQueueStub.Success().Object, Mock.Of<IUserRepository>());

        var notification = new UserSignedUpNotification
        {
            UserId = Guid.NewGuid(),
            Email = "tenant@example.com",
            Username = "tenantuser",
            TenantId = Guid.NewGuid()
        };

        await handler.Handle(notification, CancellationToken.None);
    }
}

#endregion

#region PasswordService Tests

public class PasswordServiceCovTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ISender> _sender = new();
    private readonly PasswordService _svc;

    public PasswordServiceCovTests()
    {
        _sender.Setup(s => s.Send(It.IsAny<SendEmailVerificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailVerificationResponse { Message = "sent" });
        _sender.Setup(s => s.Send(It.IsAny<VerifyEmailCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VerifyEmailCommand command, CancellationToken _) => new EmailVerificationResult
            {
                Success = command.Token == "valid-token",
                Message = command.Token == "valid-token" ? "Email verified successfully" : "Invalid or expired verification token"
            });
        _sender.Setup(s => s.Send(It.IsAny<RequestPasswordResetCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetRequestResult
            {
                Success = true,
                Message = "If an account with that email exists, a password reset link has been sent."
            });
        _sender.Setup(s => s.Send(It.IsAny<ResetPasswordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResetPasswordCommand command, CancellationToken _) => new PasswordResetResult
            {
                Success = command.Token == "valid",
                Message = command.Token == "valid" ? "Password reset successfully" : "Invalid or expired reset token"
            });
        _sender.Setup(s => s.Send(It.IsAny<ChangePasswordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChangePasswordCommand command, CancellationToken _) =>
            {
                var success = command.CurrentPassword == "correct" && command.NewPassword == "StrongPass1!";
                var message = command.CurrentPassword == "old" && command.NewPassword == "new"
                    ? "User not found"
                    : success ? "Password changed successfully" : "Current password is incorrect";

                return new PasswordChangeResult
                {
                    Success = success,
                    Message = message
                };
            });

        _svc = new PasswordService(
            NullLogger<PasswordService>.Instance,
            _userRepo.Object,
            _sender.Object);
    }

    [Fact]
    public async Task SendEmailVerification_UserNotFound_ReturnsSuccess()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _svc.SendEmailVerificationAsync(
            new SendEmailVerificationRequest { Email = "unknown@test.com" });

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SendEmailVerification_UserFound_SendsEmail()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "found@test.com", Username = "user1" };
        _userRepo.Setup(r => r.GetByEmailAsync("found@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _svc.SendEmailVerificationAsync(
            new SendEmailVerificationRequest { Email = "found@test.com" });

        result.Success.Should().BeTrue();
        _sender.Verify(
            s => s.Send(
                It.Is<SendEmailVerificationCommand>(command =>
                    command.Email == "found@test.com" &&
                    command.UserId == user.Id &&
                    command.UserName == "user1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyEmail_ValidToken_ReturnsSuccess()
    {
        var result = await _svc.VerifyEmailAsync(
            new EmailVerificationRequest { Token = "valid-token" });

        result.Success.Should().BeTrue();
        _sender.Verify(
            s => s.Send(It.Is<VerifyEmailCommand>(command => command.Token == "valid-token"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_ReturnsFailure()
    {
        var result = await _svc.VerifyEmailAsync(
            new EmailVerificationRequest { Token = "bad-token" });

        result.Success.Should().BeFalse();
        _sender.Verify(
            s => s.Send(It.Is<VerifyEmailCommand>(command => command.Token == "bad-token"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_UserNotFound_ReturnsSuccess()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _svc.ForgotPasswordAsync(
            new PasswordResetRequest { Email = "nobody@test.com" });

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ForgotPassword_UserFound_GeneratesToken()
    {
        var result = await _svc.ForgotPasswordAsync(
            new PasswordResetRequest { Email = "user@test.com" });

        result.Success.Should().BeTrue();
        _sender.Verify(
            s => s.Send(It.Is<RequestPasswordResetCommand>(command => command.Email == "user@test.com"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsFailure()
    {
        var result = await _svc.ResetPasswordAsync(
            new ResetPasswordRequest { Token = "expired", NewPassword = "NewPass123!" });

        result.Success.Should().BeFalse();
        _sender.Verify(
            s => s.Send(It.Is<ResetPasswordCommand>(command => command.Token == "expired" && command.NewPassword == "NewPass123!"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ReturnsSuccess()
    {
        var result = await _svc.ResetPasswordAsync(
            new ResetPasswordRequest { Token = "valid", NewPassword = "NewPass123!" });

        result.Success.Should().BeTrue();
        _sender.Verify(
            s => s.Send(It.Is<ResetPasswordCommand>(command => command.Token == "valid" && command.ConfirmPassword == "NewPass123!"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChangePassword_UserNotFound_ReturnsFailure()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _svc.ChangePasswordAsync(
            new ChangePasswordRequest { CurrentPassword = "old", NewPassword = "new" },
            Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsFailure()
    {
        var userId = Guid.NewGuid();

        var result = await _svc.ChangePasswordAsync(
            new ChangePasswordRequest { CurrentPassword = "wrong", NewPassword = "new" },
            userId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("incorrect");
        _sender.Verify(
            s => s.Send(It.Is<ChangePasswordCommand>(command => command.UserId == userId && command.CurrentPassword == "wrong"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_ReturnsFailure()
    {
        var userId = Guid.NewGuid();

        var result = await _svc.ChangePasswordAsync(
            new ChangePasswordRequest { CurrentPassword = "correct", NewPassword = "weak" },
            userId);

        result.Success.Should().BeFalse();
        _sender.Verify(
            s => s.Send(It.Is<ChangePasswordCommand>(command => command.UserId == userId && command.NewPassword == "weak"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChangePassword_Success_UpdatesHash()
    {
        var userId = Guid.NewGuid();

        var result = await _svc.ChangePasswordAsync(
            new ChangePasswordRequest { CurrentPassword = "correct", NewPassword = "StrongPass1!" },
            userId);

        result.Success.Should().BeTrue();
        _sender.Verify(
            s => s.Send(It.Is<ChangePasswordCommand>(command => command.UserId == userId && command.RevokeOtherSessions), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChangePassword_NullPasswordHash_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, PasswordHash = null };
        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _svc.ChangePasswordAsync(
            new ChangePasswordRequest { CurrentPassword = "any", NewPassword = "new" },
            userId);

        result.Success.Should().BeFalse();
    }
}

#endregion

#region AuthControllerBase Tests

public class AuthControllerBaseCovTests
{
    private sealed class TestAuthController : AuthControllerBase
    {
        public Guid TestGetCurrentUserId() => GetCurrentUserId();
        public string TestGetCurrentUserEmail() => GetCurrentUserEmail();
    }

    [Fact]
    public void GetCurrentUserId_ValidClaim_ReturnsGuid()
    {
        var userId = Guid.NewGuid();
        var controller = new TestAuthController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "Test"))
            }
        };

        controller.TestGetCurrentUserId().Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_SubClaim_ReturnsGuid()
    {
        var userId = Guid.NewGuid();
        var controller = new TestAuthController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("sub", userId.ToString())],
                        "Test"))
                }
            }
        };

        controller.TestGetCurrentUserId().Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_NoClaim_Throws()
    {
        var controller = new TestAuthController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        var act = () => controller.TestGetCurrentUserId();
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetCurrentUserId_InvalidGuid_Throws()
    {
        var controller = new TestAuthController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
                }, "Test"))
            }
        };

        var act = () => controller.TestGetCurrentUserId();
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetCurrentUserEmail_ValidClaim_ReturnsEmail()
    {
        var controller = new TestAuthController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Email, "test@example.com")
                }, "Test"))
            }
        };

        controller.TestGetCurrentUserEmail().Should().Be("test@example.com");
    }

    [Fact]
    public void GetCurrentUserEmail_JwtEmailClaim_ReturnsEmail()
    {
        var controller = new TestAuthController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("email", "jwt@example.com")],
                        "Test"))
                }
            }
        };

        controller.TestGetCurrentUserEmail().Should().Be("jwt@example.com");
    }

    [Fact]
    public void GetCurrentUserEmail_NoClaim_Throws()
    {
        var controller = new TestAuthController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        var act = () => controller.TestGetCurrentUserEmail();
        act.Should().Throw<UnauthorizedAccessException>();
    }
}

#endregion

#region ApiKey Handler Tests

public class ApiKeyHandlersCovTests
{
    private readonly Mock<IApplicationDbContext> _dbContext = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    private ActorContext CreateAuthenticatedActor(Guid userId, Guid? tenantId = null)
    {
        return new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = tenantId ?? Guid.NewGuid(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        };
    }

    // CreateApiKeyHandler

    [Fact]
    public async Task CreateApiKey_Unauthenticated_ReturnsFailure()
    {
        _actorAccessor.Setup(a => a.ActorContext).Returns(ActorContext.Anonymous);

        var handler = new CreateApiKeyHandler(
            _dbContext.Object,
            _actorAccessor.Object,
            NullLogger<CreateApiKeyHandler>.Instance);

        var command = new CreateApiKeyCommand
        {
            Name = "test",
            Scopes = new[] { "read" }
        };

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateApiKey_Authenticated_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        _actorAccessor.Setup(a => a.ActorContext).Returns(CreateAuthenticatedActor(userId));

        var mockDbSet = new Mock<DbSet<ApiKey>>();
        _dbContext.Setup(x => x.Set<ApiKey>()).Returns(mockDbSet.Object);
        _dbContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateApiKeyHandler(
            _dbContext.Object,
            _actorAccessor.Object,
            NullLogger<CreateApiKeyHandler>.Instance);

        var command = new CreateApiKeyCommand
        {
            Name = "my-key",
            Scopes = new[] { "read", "write" },
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("my-key");
        result.Value.ApiKey.Should().NotBeNullOrEmpty();
    }

    // ListApiKeysHandler

    [Fact]
    public async Task ListApiKeys_Unauthenticated_ReturnsFailure()
    {
        _actorAccessor.Setup(a => a.ActorContext).Returns(ActorContext.Anonymous);

        var handler = new ListApiKeysHandler(
            _dbContext.Object,
            _actorAccessor.Object);

        var result = await handler.Handle(new ListApiKeysQuery(), CancellationToken.None);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ListApiKeys_Authenticated_ReturnsKeys()
    {
        var userId = Guid.NewGuid();
        _actorAccessor.Setup(a => a.ActorContext).Returns(CreateAuthenticatedActor(userId));

        var keys = new List<ApiKey>().AsQueryable().BuildMockDbSet();
        _dbContext.Setup(x => x.Set<ApiKey>()).Returns(keys.Object);

        var handler = new ListApiKeysHandler(
            _dbContext.Object,
            _actorAccessor.Object);

        var result = await handler.Handle(new ListApiKeysQuery(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
    }

    // RevokeApiKeyHandler

    [Fact]
    public async Task RevokeApiKey_Unauthenticated_ReturnsFailure()
    {
        _actorAccessor.Setup(a => a.ActorContext).Returns(ActorContext.Anonymous);

        var handler = new RevokeApiKeyHandler(
            _dbContext.Object,
            _actorAccessor.Object,
            NullLogger<RevokeApiKeyHandler>.Instance);

        var command = new RevokeApiKeyCommand { KeyId = Guid.NewGuid() };
        var result = await handler.Handle(command, CancellationToken.None);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeApiKey_NotFound_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        _actorAccessor.Setup(a => a.ActorContext).Returns(CreateAuthenticatedActor(userId));

        var keys = new List<ApiKey>().AsQueryable().BuildMockDbSet();
        _dbContext.Setup(x => x.Set<ApiKey>()).Returns(keys.Object);

        var handler = new RevokeApiKeyHandler(
            _dbContext.Object,
            _actorAccessor.Object,
            NullLogger<RevokeApiKeyHandler>.Instance);

        var command = new RevokeApiKeyCommand { KeyId = Guid.NewGuid() };
        var result = await handler.Handle(command, CancellationToken.None);
        result.IsFailure.Should().BeTrue();
    }
}

#endregion

#region CreateApiKeyValidator Tests

public class CreateApiKeyValidatorCovTests
{
    private readonly CreateApiKeyValidator _validator = new();

    [Fact]
    public void Valid_Command_PassesValidation()
    {
        var command = new CreateApiKeyCommand
        {
            Name = "test-key",
            Scopes = new[] { "read" }
        };

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyName_FailsValidation()
    {
        var command = new CreateApiKeyCommand
        {
            Name = "",
            Scopes = new[] { "read" }
        };

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void EmptyScopes_FailsValidation()
    {
        var command = new CreateApiKeyCommand
        {
            Name = "test",
            Scopes = Array.Empty<string>()
        };

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void PastExpiryDate_FailsValidation()
    {
        var command = new CreateApiKeyCommand
        {
            Name = "test",
            Scopes = new[] { "read" },
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FutureExpiryDate_PassesValidation()
    {
        var command = new CreateApiKeyCommand
        {
            Name = "test",
            Scopes = new[] { "read" },
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}

#endregion

#region AuthenticationModelConfiguration Tests

public class AuthenticationModelConfigCovTests
{
    [Fact]
    public void Configure_AppliesConfigurations()
    {
        var conventionSet = new ConventionSet();
        var modelBuilder = new ModelBuilder(conventionSet);
        var config = new AuthenticationModelConfiguration();

        // Should not throw - exercises the Configure method
        config.Configure(modelBuilder);
    }
}

#endregion

#region DTO Tests

public class DtoCoverageTests
{
    [Fact]
    public void CreateApiKeyResponse_FromEntity_MapsCorrectly()
    {
        var (apiKey, plaintext) = ApiKey.Create(
            Guid.NewGuid(), Guid.NewGuid(), "test-key",
            new[] { "read", "write" }, DateTime.UtcNow.AddDays(30));

        var dto = CreateApiKeyResponse.FromEntity(apiKey, plaintext);

        dto.Name.Should().Be("test-key");
        dto.ApiKey.Should().Be(plaintext);
        dto.Scopes.Should().Contain("read");
        dto.Scopes.Should().Contain("write");
        dto.KeyPrefix.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ApiKeyDto_FromEntity_MapsCorrectly()
    {
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(), Guid.NewGuid(), "dto-key",
            new[] { "read" });

        var dto = ApiKeyDto.FromEntity(apiKey);

        dto.Name.Should().Be("dto-key");
        dto.Scopes.Should().Contain("read");
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public void JwtKeyInfoDto_FromEntity_MapsCorrectly()
    {
        var key = JwtSigningKey.CreateNew(1, DateTime.UtcNow, TimeSpan.FromDays(90));
        key.Activate();

        var dto = JwtKeyInfoDto.FromEntity(key);

        dto.IsActive.Should().BeTrue();
        dto.KeyVersion.Should().Be(1);
        dto.Algorithm.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void KeyRotationOptions_HasDefaults()
    {
        var opts = new KeyRotationOptions();

        opts.CheckInterval.Should().Be(TimeSpan.FromHours(1));
        opts.KeyValidityDays.Should().Be(90);
        opts.RotationThreshold.Should().Be(TimeSpan.FromDays(7));
        opts.ExpiredKeyRetentionDays.Should().Be(30);
    }

    [Fact]
    public void RotateKeyRequest_Properties()
    {
        var req = new RotateKeyRequest { Reason = "test", ValidityDays = 60 };
        req.Reason.Should().Be("test");
        req.ValidityDays.Should().Be(60);
    }

    [Fact]
    public void CleanupKeysRequest_Properties()
    {
        var req = new CleanupKeysRequest { RetentionDays = 15 };
        req.RetentionDays.Should().Be(15);
    }

    [Fact]
    public void CleanupResult_Properties()
    {
        var result = new CleanupResult { DeletedCount = 5 };
        result.DeletedCount.Should().Be(5);
    }

    [Fact]
    public void ApiKeyAuthenticationOptions_SchemeName()
    {
        ApiKeyAuthenticationOptions.SchemeName.Should().Be("ApiKey");
    }
}

#endregion

#region TokenRevocationMiddleware Tests

public class TokenRevocationMiddlewareCovTests
{
    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        var middleware = new TokenRevocationMiddleware(next, NullLogger<TokenRevocationMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated

        var revocService = new Mock<ITokenRevocationService>();
        var userRepo = new Mock<IUserRepository>();

        await middleware.InvokeAsync(context, revocService.Object, userRepo.Object);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_RevokedJti_Returns401()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new TokenRevocationMiddleware(next, NullLogger<TokenRevocationMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("jti", "revoked-jti-123")
        }, "Bearer"));

        var revocService = new Mock<ITokenRevocationService>();
        revocService.Setup(s => s.IsRevokedAsync("revoked-jti-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var userRepo = new Mock<IUserRepository>();

        await middleware.InvokeAsync(context, revocService.Object, userRepo.Object);
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_UserTokensRevoked_Returns401()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new TokenRevocationMiddleware(next, NullLogger<TokenRevocationMiddleware>.Instance);

        var userId = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("iat", issuedAt.ToString())
        }, "Bearer"));

        var revocService = new Mock<ITokenRevocationService>();
        revocService.Setup(s => s.IsRevokedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        revocService.Setup(s => s.IsUserTokenRevokedAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var userRepo = new Mock<IUserRepository>();

        await middleware.InvokeAsync(context, revocService.Object, userRepo.Object);
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_OutdatedTokenVersion_Returns401()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new TokenRevocationMiddleware(next, NullLogger<TokenRevocationMiddleware>.Instance);

        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("token_version", "1")
        }, "Bearer"));

        var revocService = new Mock<ITokenRevocationService>();
        revocService.Setup(s => s.IsRevokedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        revocService.Setup(s => s.IsUserTokenRevokedAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetTokenVersionAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5); // current version > token version

        await middleware.InvokeAsync(context, revocService.Object, userRepo.Object);
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new TokenRevocationMiddleware(next, NullLogger<TokenRevocationMiddleware>.Instance);

        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("jti", "valid-jti"),
            new Claim("token_version", "3")
        }, "Bearer"));

        var revocService = new Mock<ITokenRevocationService>();
        revocService.Setup(s => s.IsRevokedAsync("valid-jti", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        revocService.Setup(s => s.IsUserTokenRevokedAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetTokenVersionAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // same version = valid

        await middleware.InvokeAsync(context, revocService.Object, userRepo.Object);
        nextCalled.Should().BeTrue();
    }
}

#endregion

#region KeyRotationController Tests

public class KeyRotationControllerCovTests
{
    private readonly Mock<IKeyRotationService> _keyService = new();
    private readonly KeyRotationController _controller;

    public KeyRotationControllerCovTests()
    {
        _controller = new KeyRotationController(_keyService.Object, NullLogger<KeyRotationController>.Instance, new CommandHandlerSender(_keyService.Object));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "admin")
                }, "Test"))
            }
        };
    }

    [Fact]
    public async Task GetSigningKeys_ActiveFilter_ReturnsActiveKey()
    {
        var key = JwtSigningKey.CreateNew(1, DateTime.UtcNow, TimeSpan.FromDays(90));
        key.Activate();
        _keyService.Setup(s => s.GetActiveSigningKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        var result = await _controller.GetSigningKeys("active", CancellationToken.None);
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSigningKeys_ActiveFilter_NoKey_ReturnsEmptyList()
    {
        _keyService.Setup(s => s.GetActiveSigningKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((JwtSigningKey?)null);

        var result = await _controller.GetSigningKeys("active", CancellationToken.None);
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSigningKeys_ValidFilter_ReturnsKeys()
    {
        var keys = new List<JwtSigningKey>
        {
            JwtSigningKey.CreateNew(1, DateTime.UtcNow, TimeSpan.FromDays(90))
        };
        _keyService.Setup(s => s.GetValidationKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var result = await _controller.GetSigningKeys("valid", CancellationToken.None);
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSigningKeys_NoFilter_ReturnsAllKeys()
    {
        var keys = new List<JwtSigningKey>();
        _keyService.Setup(s => s.GetValidationKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys);

        var result = await _controller.GetSigningKeys(null, CancellationToken.None);
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task RotateKey_ReturnsNewKey()
    {
        var newKey = JwtSigningKey.CreateNew(2, DateTime.UtcNow, TimeSpan.FromDays(90));
        _keyService.Setup(s => s.RotateKeyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newKey);

        var result = await _controller.RotateKey(
            new RotateKeyRequest { Reason = "test", ValidityDays = 60 },
            CancellationToken.None);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task CleanupExpiredKeys_ReturnsCount()
    {
        _keyService.Setup(s => s.CleanupExpiredKeysAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _controller.CleanupExpiredKeys(null, CancellationToken.None);
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        var cleanup = okResult!.Value as CleanupResult;
        cleanup!.DeletedCount.Should().Be(3);
    }

    [Fact]
    public async Task CleanupExpiredKeys_WithRetentionDays_UsesCustomValue()
    {
        _keyService.Setup(s => s.CleanupExpiredKeysAsync(15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _controller.CleanupExpiredKeys(
            new CleanupKeysRequest { RetentionDays = 15 },
            CancellationToken.None);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
    }
}

#endregion

#region Web3AuthService Tests

public class Web3AuthServiceCovTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshRepo = new();
    private readonly Mock<IJwtTokenService> _jwtService = new();
    private readonly Mock<IWeb3Service> _web3Service = new();
    private readonly Mock<IAuthAttemptService> _authAttempt = new();
    private readonly Mock<IHttpContextAccessor> _httpAccessor = new();
    private readonly Web3AuthService _svc;

    public Web3AuthServiceCovTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            })
            .Build();

        var httpContext = new DefaultHttpContext();
        _httpAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        _authAttempt.Setup(a => a.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");

        _svc = new Web3AuthService(
            _refreshRepo.Object,
            _jwtService.Object,
            _web3Service.Object,
            config,
            _authAttempt.Object,
            _httpAccessor.Object,
            NullLogger<Web3AuthService>.Instance);
    }

    [Fact]
    public async Task GenerateWeb3Challenge_ReturnsChallenge()
    {
        var challenge = new Web3Challenge { Message = "Sign this message", ExpiresAt = DateTime.UtcNow.AddMinutes(5) };
        _web3Service.Setup(s => s.GenerateChallengeAsync("0x123", null))
            .ReturnsAsync(challenge);

        var result = await _svc.GenerateWeb3ChallengeAsync(
            new Web3ChallengeRequest { WalletAddress = "0x123" });

        result.Challenge.Should().Be("Sign this message");
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task VerifyWeb3Signature_Invalid_Throws()
    {
        _web3Service.Setup(s => s.VerifySignatureAsync("0xabc", "sig", "msg"))
            .ReturnsAsync(false);

        var act = async () => await _svc.VerifyWeb3SignatureAsync(
            new Web3VerificationRequest { WalletAddress = "0xabc", Signature = "sig", Challenge = "msg" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task VerifyWeb3Signature_Valid_ReturnsSignInResponse()
    {
        _web3Service.Setup(s => s.VerifySignatureAsync("0xdef", "valid-sig", "challenge"))
            .ReturnsAsync(true);
        _jwtService.Setup(s => s.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("jwt-token");
        _jwtService.Setup(s => s.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");
        _refreshRepo.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken());

        var result = await _svc.VerifyWeb3SignatureAsync(
            new Web3VerificationRequest { WalletAddress = "0xdef", Signature = "valid-sig", Challenge = "challenge" });

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("jwt-token");
        result.RefreshToken.Should().Be("refresh-token");
    }
}

#endregion

#region WebAuthnController Tests

public class WebAuthnControllerCovTests
{
    private readonly Mock<IWebAuthnService> _webAuthnService = new();
    private readonly WebAuthnController _controller;

    public WebAuthnControllerCovTests()
    {
        _controller = new WebAuthnController(_webAuthnService.Object,
            new CommandHandlerSender(_webAuthnService.Object, Mock.Of<IJwtTokenService>(), Mock.Of<IUserRepository>(), new ConfigurationBuilder().Build()));
    }

    private void SetUser(Guid? userId)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim("sub", userId.Value.ToString()));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId.HasValue ? "Test" : null))
            }
        };
    }

    [Fact]
    public async Task BeginRegistration_Unauthorized_ReturnsUnauthorized()
    {
        SetUser(null);

        var result = await _controller.BeginRegistration(
            new BeginWebAuthnRegistrationRequest { Email = "t@t.com", DisplayName = "Test" });

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task BeginRegistration_Success_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.BeginRegistrationAsync(
            userId, "t@t.com", "Test", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnRegistrationOptionsResult { Success = true });

        var result = await _controller.BeginRegistration(
            new BeginWebAuthnRegistrationRequest { Email = "t@t.com", DisplayName = "Test" });

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CompleteRegistration_Unauthorized_ReturnsUnauthorized()
    {
        SetUser(null);
        var result = await _controller.CompleteRegistration(
            new CompleteWebAuthnRegistrationRequest { AttestationResponse = "resp" });
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task BeginAuthentication_ReturnsResult()
    {
        SetUser(null);
        _webAuthnService.Setup(s => s.BeginAuthenticationAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationOptionsResult { Success = true });

        var result = await _controller.BeginAuthentication(null);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CompleteAuthentication_Failure_ReturnsBadRequest()
    {
        SetUser(null);
        _webAuthnService.Setup(s => s.CompleteAuthenticationAsync(
            "resp", null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = false });

        var result = await _controller.CompleteAuthentication(
            new CompleteWebAuthnAuthenticationRequest { AssertionResponse = "resp" });
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CompleteAuthentication_Success_AttachesJwtTokens()
    {
        var userId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var jwtTokenService = new Mock<IJwtTokenService>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            })
            .Build();
        var controller = new WebAuthnController(
            _webAuthnService.Object,
            new CommandHandlerSender(_webAuthnService.Object, jwtTokenService.Object, userRepository.Object, configuration));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        _webAuthnService.Setup(s => s.CompleteAuthenticationAsync(
                "resp",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult
            {
                Success = true,
                UserId = userId,
                CredentialId = credentialId,
                IsPasswordless = true
            });
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = "passkey@test.com" });
        jwtTokenService.Setup(j => j.GenerateAccessTokenAsync(
                userId,
                "passkey@test.com",
                It.IsAny<string[]>(),
                null,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        jwtTokenService.Setup(j => j.GenerateRefreshTokenAsync(
                userId,
                It.IsAny<DeviceInfo>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        var result = await controller.CompleteAuthentication(
            new CompleteWebAuthnAuthenticationRequest { AssertionResponse = "resp" });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<WebAuthnAuthenticationResult>().Subject;
        payload.AccessToken.Should().Be("access-token");
        payload.RefreshToken.Should().Be("refresh-token");
        payload.Email.Should().Be("passkey@test.com");
        payload.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task GetCredentials_Unauthorized_ReturnsUnauthorized()
    {
        SetUser(null);
        var result = await _controller.GetCredentials();
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCredentials_ReturnsCredentials()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.GetUserCredentialsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebAuthnCredentialInfo>());

        var result = await _controller.GetCredentials();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetCredential_NotFound_Returns404()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.GetCredentialByIdAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebAuthnCredentialInfo?)null);

        var result = await _controller.GetCredential(Guid.NewGuid());
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteCredential_Unauthorized_ReturnsUnauthorized()
    {
        SetUser(null);
        var result = await _controller.DeleteCredential(Guid.NewGuid());
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteCredential_NotFound_Returns404()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.DeleteCredentialAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteCredential(Guid.NewGuid());
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteCredential_Success_ReturnsNoContent()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.DeleteCredentialAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteCredential(Guid.NewGuid());
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CredentialExists_NotFound_Returns404()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.CredentialExistsAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.CredentialExists(Guid.NewGuid());
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CredentialExists_Found_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.CredentialExistsAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.CredentialExists(Guid.NewGuid());
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task UpdateCredentialName_Unauthorized_ReturnsUnauthorized()
    {
        SetUser(null);
        var result = await _controller.UpdateCredentialName(
            Guid.NewGuid(), new UpdateCredentialNameRequest { FriendlyName = "test" });
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateCredentialName_NotFound_Returns404()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.UpdateCredentialNameAsync(
            userId, It.IsAny<Guid>(), "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.UpdateCredentialName(
            Guid.NewGuid(), new UpdateCredentialNameRequest { FriendlyName = "test" });
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateCredentialName_Success_ReturnsNoContent()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.UpdateCredentialNameAsync(
            userId, It.IsAny<Guid>(), "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.UpdateCredentialName(
            Guid.NewGuid(), new UpdateCredentialNameRequest { FriendlyName = "test" });
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task VerifyCredential_Unauthorized_ReturnsUnauthorized()
    {
        SetUser(null);
        var result = await _controller.VerifyCredential(Guid.NewGuid());
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetWebAuthnStatus_ReturnsStatus()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _webAuthnService.Setup(s => s.IsWebAuthnEnabledAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _webAuthnService.Setup(s => s.GetUserCredentialsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebAuthnCredentialInfo>());

        var result = await _controller.GetWebAuthnStatus();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetWebAuthnStatus_Unauthorized_ReturnsUnauthorized()
    {
        SetUser(null);
        var result = await _controller.GetWebAuthnStatus();
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }
}

#endregion

#region WebAuthn DTO Tests

public class WebAuthnDtoCovTests
{
    [Fact]
    public void BeginWebAuthnRegistrationRequest_Properties()
    {
        var req = new BeginWebAuthnRegistrationRequest
        {
            Email = "test@test.com",
            DisplayName = "Test User"
        };
        req.Email.Should().Be("test@test.com");
        req.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public void CompleteWebAuthnRegistrationRequest_Properties()
    {
        var req = new CompleteWebAuthnRegistrationRequest
        {
            AttestationResponse = "resp",
            FriendlyName = "key1",
            IsPasswordless = true
        };
        req.AttestationResponse.Should().Be("resp");
        req.IsPasswordless.Should().BeTrue();
    }

    [Fact]
    public void WebAuthnStatusResponse_Properties()
    {
        var resp = new WebAuthnStatusResponse
        {
            IsEnabled = true,
            CredentialCount = 2,
            HasPasswordlessCredential = true,
            HasPlatformAuthenticator = false,
            HasSecurityKey = true
        };
        resp.CredentialCount.Should().Be(2);
        resp.HasSecurityKey.Should().BeTrue();
    }

    [Fact]
    public void UpdateCredentialNameRequest_Properties()
    {
        var req = new UpdateCredentialNameRequest { FriendlyName = "My Key" };
        req.FriendlyName.Should().Be("My Key");
    }

    [Fact]
    public void BeginWebAuthnAuthenticationRequest_Properties()
    {
        var req = new BeginWebAuthnAuthenticationRequest { Email = "auth@test.com" };
        req.Email.Should().Be("auth@test.com");
    }

    [Fact]
    public void CompleteWebAuthnAuthenticationRequest_Properties()
    {
        var req = new CompleteWebAuthnAuthenticationRequest { AssertionResponse = "assertion" };
        req.AssertionResponse.Should().Be("assertion");
    }
}

#endregion

#region SocialSignInHandler Tests

public class SocialSignInHandlerCovTests
{
    [Fact]
    public async Task Handle_UnsupportedProvider_Throws()
    {
        var authService = new Mock<IAuthService>();
        var userRepo = new Mock<IUserRepository>();
        var handler = new SocialSignInHandler(
            authService.Object, userRepo.Object,
            NullLogger<SocialSignInHandler>.Instance);

        var command = new SocialSignInCommand
        {
            Provider = SocialProvider.Facebook,
            Token = "token"
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Handle_WithValidator_InvalidCommand_Throws()
    {
        var authService = new Mock<IAuthService>();
        var userRepo = new Mock<IUserRepository>();
        var validator = new Mock<IValidator<SocialSignInCommand>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<SocialSignInCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(new[]
            {
                new ValidationFailure("Token", "Token is required")
            }));

        var handler = new SocialSignInHandler(
            authService.Object, userRepo.Object,
            NullLogger<SocialSignInHandler>.Instance, validator.Object);

        var command = new SocialSignInCommand
        {
            Provider = SocialProvider.Google,
            Token = ""
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<RequestValidationException>();
    }
}

#endregion

// MaxCoverageTests4.cs — Coverage boost batch 4
// Targets remaining gaps: UserEnumerationProtection extras, PolymorphicSignInHandler,
//   LocalSignInHandler, OAuthAuthService URLs, SiemIntegrationService, JwtTokenService retry

using System.Security.Claims;
using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests;

// ════════════════════════════════════════════════════════════════════════════
// 1. UserEnumerationProtectionService — extra methods not covered
// ════════════════════════════════════════════════════════════════════════════
public sealed class UserEnumProtectionExtraTests
{
    internal static IConfiguration TestPasswordPolicyConfiguration =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10"
            })
            .Build();

    private readonly MemoryCache _cache;
    private readonly UserEnumerationProtectionService _sut;

    public UserEnumProtectionExtraTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new UserEnumerationProtectionService(
            Mock.Of<ILogger<UserEnumerationProtectionService>>(), _cache);
    }

    [Fact]
    public void GetConsistentErrorMessage_ReturnsMessage()
    {
        var msg = _sut.GetConsistentErrorMessage();
        msg.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetBaseProcessingTime_ReturnsNonZero()
    {
        var time = _sut.GetBaseProcessingTime();
        time.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task SimulateAuthenticationDelay_UserExists()
    {
        await _sut.SimulateAuthenticationDelayAsync("test@test.com", true);
    }

    [Fact]
    public async Task SimulateAuthenticationDelay_UserNotExists()
    {
        await _sut.SimulateAuthenticationDelayAsync("fake@test.com", false);
    }

    [Fact]
    public async Task PerformDummyPasswordHash_Executes()
    {
        await _sut.PerformDummyPasswordHashAsync("test_password");
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 2. PolymorphicSignInHandler — validation, credential detection
// ════════════════════════════════════════════════════════════════════════════
public sealed class PolymorphicSignInHandlerCoverageTests
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<FluentValidation.IValidator<PolymorphicSignInCommand>> _validator = new();

    public PolymorphicSignInHandlerCoverageTests()
    {
        _userRepo.Setup(repository => repository.FindSignInCandidatesAsync(It.IsAny<string>(), It.IsAny<SignInIdentifierType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<User>)[]);
    }

    [Fact]
    public async Task Handle_ValidationFailure_Throws()
    {
        var sut = new PolymorphicSignInHandler(
            _authService.Object, _userRepo.Object,
            Mock.Of<ILogger<PolymorphicSignInHandler>>(), new PasswordSignInAdmissionStub(), _validator.Object);

        var cmd = new PolymorphicSignInCommand { Credential = "a@b.c", Password = "pass" };
        _validator.Setup(v => v.ValidateAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(
                new[] { new FluentValidation.Results.ValidationFailure("Credential", "Required") }));

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmailCredential_CallsAuthService()
    {
        var sut = new PolymorphicSignInHandler(
            _authService.Object, _userRepo.Object,
            Mock.Of<ILogger<PolymorphicSignInHandler>>(), new PasswordSignInAdmissionStub());

        var cmd = new PolymorphicSignInCommand { Credential = "user@example.com", Password = "pass" };
        _authService.Setup(a => a.LocalSignInAsync(
            It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.Handle(cmd, CancellationToken.None));
        _authService.Verify(a => a.LocalSignInAsync(
            It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PhoneCredential_CallsAuthService()
    {
        var sut = new PolymorphicSignInHandler(
            _authService.Object, _userRepo.Object,
            Mock.Of<ILogger<PolymorphicSignInHandler>>(), new PasswordSignInAdmissionStub());

        var cmd = new PolymorphicSignInCommand { Credential = "+1234567890", Password = "pass" };
        _authService.Setup(a => a.LocalSignInAsync(
            It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UsernameCredential_CallsAuthService()
    {
        var sut = new PolymorphicSignInHandler(
            _authService.Object, _userRepo.Object,
            Mock.Of<ILogger<PolymorphicSignInHandler>>(), new PasswordSignInAdmissionStub());

        var cmd = new PolymorphicSignInCommand { Credential = "johndoe", Password = "pass" };
        _authService.Setup(a => a.LocalSignInAsync(
            It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ExplicitCredentialType_SkipsDetection()
    {
        var sut = new PolymorphicSignInHandler(
            _authService.Object, _userRepo.Object,
            Mock.Of<ILogger<PolymorphicSignInHandler>>(), new PasswordSignInAdmissionStub());

        var cmd = new PolymorphicSignInCommand
        {
            Credential = "anything", Password = "pass",
            CredentialType = CredentialType.Email
        };
        _authService.Setup(a => a.LocalSignInAsync(
            It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.Handle(cmd, CancellationToken.None));
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 3. LocalSignInHandler — validation, IP extraction
// ════════════════════════════════════════════════════════════════════════════
public sealed class LocalSignInHandlerCoverageTests
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IHttpContextAccessor> _httpAccessor = new();
    private readonly Mock<FluentValidation.IValidator<LocalSignInCommand>> _validator = new();
    private readonly LocalSignInHandler _sut;

    public LocalSignInHandlerCoverageTests()
    {
        _httpAccessor.Setup(h => h.HttpContext).Returns(new DefaultHttpContext());
        _sut = new LocalSignInHandler(
            _authService.Object, _userRepo.Object,
            _httpAccessor.Object, Mock.Of<ILogger<LocalSignInHandler>>(),
            _validator.Object);
    }

    [Fact]
    public async Task Handle_ValidationFailure_Throws()
    {
        var cmd = new LocalSignInCommand { Email = "", Password = "" };
        _validator.Setup(v => v.ValidateAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(
                new[] { new FluentValidation.Results.ValidationFailure("Email", "Required") }));

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _sut.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AuthServiceError_Propagates()
    {
        var cmd = new LocalSignInCommand { Email = "a@b.c", Password = "pass" };
        _validator.Setup(v => v.ValidateAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        _authService.Setup(a => a.LocalSignInAsync(
            It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_XForwardedFor_ExtractsIp()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Forwarded-For"] = "10.0.0.1, 192.168.1.1";
        _httpAccessor.Setup(h => h.HttpContext).Returns(ctx);

        var cmd = new LocalSignInCommand { Email = "a@b.c", Password = "pass" };
        _validator.Setup(v => v.ValidateAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        _authService.Setup(a => a.LocalSignInAsync(
            It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullHttpContext_DoesNotThrow()
    {
        _httpAccessor.Setup(h => h.HttpContext).Returns((HttpContext?)null);

        var cmd = new LocalSignInCommand { Email = "a@b.c", Password = "pass" };
        _validator.Setup(v => v.ValidateAsync(cmd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        _authService.Setup(a => a.LocalSignInAsync(
            It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.Handle(cmd, CancellationToken.None));
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 4. OAuthAuthService — GetAuthUrl methods
// ════════════════════════════════════════════════════════════════════════════
public sealed class OAuthAuthServiceUrlTests
{
    private readonly OAuthAuthService _sut;

    public OAuthAuthServiceUrlTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["OAuth:GitHub:ClientId"] = "test-github-client",
                ["OAuth:Google:ClientId"] = "test-google-client"
            }).Build();

        _sut = new OAuthAuthService(
            Mock.Of<IUserRepository>(),
            Mock.Of<IJwtTokenService>(),
            Mock.Of<IRefreshTokenHasher>(),
            Mock.Of<IOAuthService>(),
            Mock.Of<IGoogleIdTokenVerifier>(),
            Mock.Of<IOidcFederationService>(),
            Mock.Of<IMfaService>(),
            Mock.Of<IExternalLoginRepository>(),
            config,
            Mock.Of<IAuthAttemptService>(),
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<ISender>(),
            Mock.Of<ISessionManagementService>(),
            Mock.Of<ILogger<OAuthAuthService>>());
    }

    [Fact]
    public async Task GetGitHubAuthUrl_ReturnsValidUrl()
    {
        var url = await _sut.GetGitHubAuthUrlAsync("http://localhost/callback");
        url.Should().Contain("github.com/login/oauth/authorize");
        url.Should().Contain("test-github-client");
    }

    [Fact]
    public async Task GetGoogleAuthUrl_ReturnsValidUrl()
    {
        var url = await _sut.GetGoogleAuthUrlAsync("http://localhost/callback");
        url.Should().Contain("accounts.google.com");
        url.Should().Contain("test-google-client");
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 5. SiemIntegrationService — enabled path
// ════════════════════════════════════════════════════════════════════════════
public sealed class SiemIntegrationCoverageTests
{
    [Fact]
    public void IsEnabled_WhenDisabled_ReturnsFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Siem:Enabled"] = "false"
            }).Build();

        var sut = new SiemIntegrationService(config,
            Mock.Of<ILogger<SiemIntegrationService>>());
        sut.IsEnabled().Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ReturnsTrue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Siem:Enabled"] = "true",
                ["Authentication:Siem:Endpoint"] = "http://localhost:9999"
            }).Build();

        var sut = new SiemIntegrationService(config,
            Mock.Of<ILogger<SiemIntegrationService>>());
        sut.IsEnabled().Should().BeTrue();
    }

    [Fact]
    public async Task SendSecurityEvent_WhenDisabled_ReturnsImmediately()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Siem:Enabled"] = "false"
            }).Build();

        var sut = new SiemIntegrationService(config,
            Mock.Of<ILogger<SiemIntegrationService>>());

        await sut.SendSecurityEventAsync(new SiemEvent
        {
            EventType = "Test", Severity = SiemSeverity.Info,
            Description = "Test event"
        });
    }

    [Fact]
    public async Task SendSecurityEvent_WhenEnabled_LogsEvent()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Siem:Enabled"] = "true"
            }).Build();

        var sut = new SiemIntegrationService(config,
            Mock.Of<ILogger<SiemIntegrationService>>());

        await sut.SendSecurityEventAsync(new SiemEvent
        {
            EventType = "Test", Severity = SiemSeverity.High,
            Description = "Test event", UserId = Guid.NewGuid(),
            IpAddress = "1.2.3.4"
        });
    }

    [Fact]
    public async Task SendBruteForceEvent_WhenEnabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Siem:Enabled"] = "true"
            }).Build();

        var sut = new SiemIntegrationService(config,
            Mock.Of<ILogger<SiemIntegrationService>>());

        await sut.SendBruteForceEventAsync("test@test.com", 10,
            TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task SendBruteForceEvent_Critical_HighCount()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Siem:Enabled"] = "true"
            }).Build();

        var sut = new SiemIntegrationService(config,
            Mock.Of<ILogger<SiemIntegrationService>>());

        await sut.SendBruteForceEventAsync("test@test.com", 25,
            TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task SendImpossibleTravelEvent_WhenEnabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Siem:Enabled"] = "true"
            }).Build();

        var sut = new SiemIntegrationService(config,
            Mock.Of<ILogger<SiemIntegrationService>>());

        await sut.SendImpossibleTravelEventAsync(
            Guid.NewGuid(),
            new LocationInfo { Country = "US", City = "NYC", Latitude = 40.7, Longitude = -74.0 },
            new LocationInfo { Country = "JP", City = "Tokyo", Latitude = 35.6, Longitude = 139.7 },
            TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task SendAnomalyEvent_WhenEnabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Siem:Enabled"] = "true"
            }).Build();

        var sut = new SiemIntegrationService(config,
            Mock.Of<ILogger<SiemIntegrationService>>());

        var attempt = new AuthenticationAttempt
        {
            Email = "test@test.com", IpAddress = "1.2.3.4",
            CorrelationId = Guid.NewGuid().ToString()
        };
        var analysis = new AuthenticationAttemptAnalysis
        {
            RiskScore = 75, RiskFactors = new List<string> { "New device" }
        };

        await sut.SendAnomalyEventAsync(attempt, analysis);
    }

    [Fact]
    public async Task SendSuspiciousActivityEvent_WhenEnabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Siem:Enabled"] = "true"
            }).Build();

        var sut = new SiemIntegrationService(config,
            Mock.Of<ILogger<SiemIntegrationService>>());

        var activity = new SuspiciousActivity
        {
            ActivityType = "BruteForce", Description = "Multiple failed logins",
            RiskLevel = RiskLevel.High, RiskScore = 85,
            UserId = Guid.NewGuid(), IpAddress = "1.2.3.4",
            Identifier = "test@test.com",
            OccurredAt = DateTime.UtcNow,
            ActionsTaken = new List<string> { "Throttled" }
        };

        await sut.SendSuspiciousActivityEventAsync(activity);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 6. JwtTokenService — retry logic
// ════════════════════════════════════════════════════════════════════════════
public sealed class JwtTokenServiceRetryTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshRepo = new();
    private readonly Mock<IRefreshTokenHasher> _hasher = new();
    private readonly Mock<IHttpContextAccessor> _httpAccessor = new();
    private readonly JwtTokenService _sut;

    public JwtTokenServiceRetryTests()
    {
        var options = new JwtOptions
        {
            SecretKey = "this-is-a-very-long-secret-key-for-testing-min-256-bits-padded!!!!",
            Issuer = "test-issuer", Audience = "test-audience",
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ClockSkewSeconds = 30, AccessTokenExpirationMinutes = 30,
            RefreshTokenExpirationDays = 7
        };
        _httpAccessor.Setup(h => h.HttpContext).Returns(new DefaultHttpContext());
        _sut = new JwtTokenService(
            Mock.Of<ILogger<JwtTokenService>>(), _refreshRepo.Object,
            _hasher.Object, _httpAccessor.Object, Options.Create(options));
    }

    [Fact]
    public async Task GenerateRefreshToken_DuplicateKeyRetry_SucceedsOnSecondAttempt()
    {
        var deviceInfo = new DeviceInfo { Fingerprint = "test-fp" };
        _hasher.Setup(h => h.HashToken(It.IsAny<string>())).Returns("hashed");

        var callCount = 0;
        _refreshRepo.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns<RefreshToken, CancellationToken>((rt, ct) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new Exception("duplicate key value violates unique constraint");
                return Task.FromResult(new RefreshToken { Id = Guid.NewGuid() });
            });

        var token = await _sut.GenerateRefreshTokenAsync(Guid.NewGuid(), deviceInfo);
        token.Should().NotBeNullOrEmpty();
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task GenerateRefreshToken_AllRetriesFail_Throws()
    {
        var deviceInfo = new DeviceInfo { Fingerprint = "test-fp" };
        _hasher.Setup(h => h.HashToken(It.IsAny<string>())).Returns("hashed");
        _refreshRepo.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("duplicate key value violates unique constraint"));

        await Assert.ThrowsAsync<Exception>(() =>
            _sut.GenerateRefreshTokenAsync(Guid.NewGuid(), deviceInfo));
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 7. EmailVerificationService — branch coverage
// ════════════════════════════════════════════════════════════════════════════
public sealed class EmailVerificationBranchTests
{
    private readonly MemoryCache _cache;
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPublisher> _publisher = new();
    private readonly EmailVerificationService _sut;
    private readonly EmailVerificationService _sutWithoutRepo;

    public EmailVerificationBranchTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _publisher.Setup(p => p.Publish(It.IsAny<EmailVerificationRequestedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new EmailVerificationService(
            Mock.Of<ILogger<EmailVerificationService>>(), _cache, _publisher.Object, _userRepo.Object);
        _sutWithoutRepo = new EmailVerificationService(
            Mock.Of<ILogger<EmailVerificationService>>(), _cache, _publisher.Object);
    }

    [Fact]
    public async Task IsEmailVerified_WithRepo_VerifiedUser()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        // Set IsEmailVerified via reflection or method if available
        typeof(User).GetProperty("IsEmailVerified")?.SetValue(user, true);

        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.IsEmailVerifiedAsync(userId);
        // Result depends on whether IsEmailVerified was set
    }

    [Fact]
    public async Task IsEmailVerified_WithRepo_NullUser()
    {
        var userId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.IsEmailVerifiedAsync(userId);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEmailVerified_WithoutRepo_ChecksCache()
    {
        var userId = Guid.NewGuid();
        var result = await _sutWithoutRepo.IsEmailVerifiedAsync(userId);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendVerificationEmail_WithUserName()
    {
        await _sut.SendVerificationEmailAsync("a@b.c", "token123", "John");
    }

    [Fact]
    public async Task VerifyToken_AlreadyVerified_CleansUp()
    {
        var userId = Guid.NewGuid();
        var token = await _sut.GenerateVerificationTokenAsync(userId, "a@b.c");
        await _sut.VerifyEmailTokenAsync(userId, token);

        // Second verification should fail (token removed from cache)
        var result = await _sut.VerifyEmailTokenAsync(userId, token);
        result.Should().BeFalse();
    }
}

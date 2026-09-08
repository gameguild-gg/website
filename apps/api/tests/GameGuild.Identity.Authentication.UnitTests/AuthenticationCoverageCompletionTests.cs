using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using GameGuild.Notifications.Services;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests;

public sealed class AuthenticationCoverageCompletionTests
{
    [Fact]
    public async Task EmailVerificationService_CoversTypedTokensAndValidationBranches()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var publisher = new Mock<IPublisher>();
        publisher.Setup(x => x.Publish(It.IsAny<EmailVerificationRequestedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new EmailVerificationService(
            NullLogger<EmailVerificationService>.Instance,
            cache,
            publisher.Object);

        var userId = Guid.NewGuid();
        var passwordResetToken = await service.GeneratePasswordResetTokenAsync(userId, "User@Example.com");
        var resetResult = await service.VerifyPasswordResetTokenAsync(passwordResetToken);
        resetResult.Success.Should().BeTrue();
        resetResult.UserId.Should().Be(userId);
        resetResult.Email.Should().Be("user@example.com");

        var emailToken = await service.GenerateVerificationTokenAsync(userId, "User@Example.com");
        var emailResult = await service.VerifyEmailTokenAsync(emailToken);
        emailResult.Success.Should().BeTrue();
        (await service.IsEmailVerifiedAsync(userId)).Should().BeTrue();

        var blankResult = await service.VerifyEmailTokenAsync(" ");
        blankResult.Success.Should().BeFalse();
        blankResult.FailureReason.Should().Be("Token is required");

        var wrongTypeToken = await service.GeneratePasswordResetTokenAsync(userId, "user@example.com");
        var wrongTypeResult = await service.VerifyEmailTokenAsync(wrongTypeToken);
        wrongTypeResult.Success.Should().BeFalse();
        wrongTypeResult.FailureReason.Should().Be("Invalid token type");

        SetTokenInfo(cache, "expired-token", userId, "user@example.com", "email_verification", SystemClock.UtcNow.AddMinutes(-1));
        var expiredResult = await service.VerifyEmailTokenAsync("expired-token");
        expiredResult.Success.Should().BeFalse();
        expiredResult.FailureReason.Should().Be("Expired token");

        SetTokenInfo(cache, "unsupported-token", userId, "user@example.com", "unsupported", SystemClock.UtcNow.AddMinutes(5));
        (await service.IsTokenValidAsync("unsupported-token")).Should().BeFalse();

        SetTokenInfo(cache, "future-token", userId, "user@example.com", "password_reset", SystemClock.UtcNow.AddMinutes(5));
        (await service.IsTokenValidAsync("future-token")).Should().BeTrue();

        SetTokenInfo(cache, "expired-valid-token", userId, "user@example.com", "email_verification", SystemClock.UtcNow.AddMinutes(-5));
        (await service.IsTokenValidAsync("expired-valid-token")).Should().BeFalse();
    }

    [Fact]
    public async Task DistributedCacheTokenRevocationService_CoversStoreAndCleanupPaths()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new DistributedCacheTokenRevocationService(
            cache,
            NullLogger<DistributedCacheTokenRevocationService>.Instance);

        await service.RevokeTokenAsync(" token-one ", SystemClock.UtcNow.AddMinutes(5), "unit-test");
        (await service.IsRevokedAsync("token-one")).Should().BeTrue();
        (await service.IsRevokedAsync(" ")).Should().BeFalse();

        await service.RevokeTokenAsync("already-expired", SystemClock.UtcNow.AddMinutes(-1), "expired");
        (await service.IsRevokedAsync("already-expired")).Should().BeFalse();

        var userId = Guid.NewGuid();
        await service.RevokeAllUserTokensAsync(userId, "unit-test");
        (await service.IsUserTokenRevokedAsync(userId, SystemClock.UtcNow.AddMinutes(-5))).Should().BeTrue();
        (await service.IsUserTokenRevokedAsync(userId, SystemClock.UtcNow.AddMinutes(5))).Should().BeFalse();
        (await service.IsUserTokenRevokedAsync(Guid.NewGuid(), SystemClock.UtcNow)).Should().BeFalse();

        (await service.CleanupExpiredAsync()).Should().Be(0);
    }

    [Fact]
    public void ApiKeyAuthentication_CoversHashConstructorAndRegistration()
    {
        var hash = InvokePrivateStatic<string>(
            typeof(ApiKeyAuthenticationHandler),
            "ComputeHash",
            "modu-api-key");

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[a-f0-9]{64}$");
        hash.Should().Be(hash.ToLowerInvariant());

        var options = new Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        options.Setup(x => x.Get(It.IsAny<string>())).Returns(new ApiKeyAuthenticationOptions());
        options.SetupGet(x => x.CurrentValue).Returns(new ApiKeyAuthenticationOptions());

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var handler = new ApiKeyAuthenticationHandler(
            options.Object,
            loggerFactory,
            UrlEncoder.Default,
            Mock.Of<IApplicationDbContext>());
        handler.Should().NotBeNull();

        var services = new ServiceCollection();
        var builder = services.AddAuthentication().AddApiKeyAuthentication();
        builder.Should().NotBeNull();
        services.Should().Contain(d => d.ServiceType == typeof(IConfigureOptions<AuthenticationOptions>));
    }

    [Fact]
    public void PasswordHasher_CoversRemainingPolicyBranches()
    {
        var hasher = new PasswordHasher(NullLogger<PasswordHasher>.Instance, EmptyConfiguration());

        hasher.ValidatePasswordStrength("aaaaaaaaa1!").IsValid.Should().BeFalse();
        hasher.ValidatePasswordStrength("AAAAAAAAA1!").IsValid.Should().BeFalse();
        hasher.ValidatePasswordStrength("Password!").IsValid.Should().BeFalse();
        hasher.ValidatePasswordStrength("Password1").IsValid.Should().BeFalse();
        hasher.ValidatePasswordStrength("x").StrengthLevel.Should().Be("Very Weak");

        var relaxedConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PasswordPolicy:MinPasswordLength"] = "1",
                ["PasswordPolicy:MaxPasswordLength"] = "4",
                ["PasswordPolicy:RequireUppercase"] = "false",
                ["PasswordPolicy:RequireLowercase"] = "false",
                ["PasswordPolicy:RequireDigit"] = "false",
                ["PasswordPolicy:RequireSpecialChar"] = "false"
            })
            .Build();
        var relaxedHasher = new PasswordHasher(NullLogger<PasswordHasher>.Instance, relaxedConfig);

        relaxedHasher.ValidatePasswordStrength("abcde").IsValid.Should().BeFalse();
        relaxedHasher.ValidatePasswordStrength("abc").ValidationFailures.Should().NotContain(
            failure => failure.Contains("uppercase", StringComparison.OrdinalIgnoreCase) ||
                failure.Contains("lowercase", StringComparison.OrdinalIgnoreCase) ||
                failure.Contains("digit", StringComparison.OrdinalIgnoreCase) ||
                failure.Contains("special", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EncryptionService_CoversBase64PaddingCase()
    {
        var service = new EncryptionService(NullLogger<EncryptionService>.Instance, EmptyConfiguration());

        (await service.ValidateSecureTokenAsync("AA")).Should().BeFalse();
    }

    [Fact]
    public async Task InMemoryTokenRevocationService_CoversEmptyCheckAndUserCleanup()
    {
        var service = new InMemoryTokenRevocationService(NullLogger<InMemoryTokenRevocationService>.Instance);

        (await service.IsRevokedAsync("")).Should().BeFalse();

        await service.RevokeTokenAsync("expired", SystemClock.UtcNow.AddMinutes(-5));

        var field = typeof(InMemoryTokenRevocationService).GetField(
            "_userRevocationTimes",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var userRevocations = (ConcurrentDictionary<Guid, DateTime>)field!.GetValue(service)!;
        var staleUserId = Guid.NewGuid();
        userRevocations[staleUserId] = SystemClock.UtcNow.AddDays(-2);

        var cleaned = await service.CleanupExpiredAsync();
        cleaned.Should().Be(1);
        userRevocations.ContainsKey(staleUserId).Should().BeFalse();
    }

    [Fact]
    public async Task JwtTokenService_CoversErrorAndPayloadBranches()
    {
        var validService = CreateJwtTokenService();
        var payloadToken = CreateUnsignedJwtWithInvalidTenant();

        var payload = await validService.GetTokenPayloadAsync(payloadToken);
        payload.Should().NotBeNull();
        payload!.TenantId.Should().BeNull();

        var noSubjectPayload = await validService.GetTokenPayloadAsync(CreateUnsignedJwtWithoutSubject());
        noSubjectPayload.Should().NotBeNull();
        noSubjectPayload!.UserId.Should().Be(Guid.Empty);

        var malformedReadableToken = "eyJhbGciOiJIUzI1NiJ9.invalid.signature";
        (await validService.GetTokenPayloadAsync(malformedReadableToken)).Should().BeNull();

        var invalidOptions = new JwtOptions
        {
            SecretKey = "short",
            Issuer = "issuer",
            Audience = "audience",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };
        var invalidService = CreateJwtTokenService(invalidOptions);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            invalidService.GenerateAccessTokenAsync(Guid.NewGuid(), "user@example.com", [], null));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            invalidService.GenerateServiceAccountTokenAsync(
                "svc",
                "client",
                "Service",
                new HashSet<string> { "read" },
                null));
    }

    [Fact]
    public void JwtTokenService_EnsureExpectedSigningAlgorithm_Rejects_NonHs256Tokens()
    {
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken();
        var nonJwtToken = new Mock<Microsoft.IdentityModel.Tokens.SecurityToken>().Object;

        FluentActions.Invoking(() =>
                InvokePrivateStatic<object?>(typeof(JwtTokenService), "EnsureExpectedSigningAlgorithm", token))
            .Should()
            .Throw<TargetInvocationException>()
            .WithInnerException<Microsoft.IdentityModel.Tokens.SecurityTokenException>()
            .WithMessage("Invalid token signing algorithm.");

        FluentActions.Invoking(() =>
                InvokePrivateStatic<object?>(typeof(JwtTokenService), "EnsureExpectedSigningAlgorithm", nonJwtToken))
            .Should()
            .Throw<TargetInvocationException>()
            .WithInnerException<Microsoft.IdentityModel.Tokens.SecurityTokenException>()
            .WithMessage("Invalid token signing algorithm.");
    }

    [Theory]
    [InlineData(null, 15, 15)]
    [InlineData("", 15, 15)]
    [InlineData("0", 15, 15)]
    [InlineData("-1", 15, 15)]
    [InlineData("not-int", 15, 15)]
    [InlineData("30", 15, 30)]
    public void MagicLinkCommandHandler_ParsePositiveInt_Covers_All_Branches(string? value, int fallback, int expected)
    {
        InvokePrivateStatic<int>(typeof(ConsumeMagicLinkCommandHandler), "ParsePositiveInt", value, fallback)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void SiemIntegrationService_CoversRiskSeverityMapping()
    {
        InvokePrivateStatic<SiemSeverity>(typeof(SiemIntegrationService), "MapRiskScoreToSeverity", 95)
            .Should().Be(SiemSeverity.Critical);
        InvokePrivateStatic<SiemSeverity>(typeof(SiemIntegrationService), "MapRiskScoreToSeverity", 70)
            .Should().Be(SiemSeverity.High);
        InvokePrivateStatic<SiemSeverity>(typeof(SiemIntegrationService), "MapRiskScoreToSeverity", 40)
            .Should().Be(SiemSeverity.Medium);
        InvokePrivateStatic<SiemSeverity>(typeof(SiemIntegrationService), "MapRiskScoreToSeverity", 20)
            .Should().Be(SiemSeverity.Low);
        InvokePrivateStatic<SiemSeverity>(typeof(SiemIntegrationService), "MapRiskScoreToSeverity", 1)
            .Should().Be(SiemSeverity.Info);
    }

    [Fact]
    public void AbacEvaluationResult_ComputedPropertiesReflectDecision()
    {
        var result = new TestAbacEvaluationResult();

        result.Decision = AbacDecision.Allow;
        result.IsAllowed.Should().BeTrue();

        result.Decision = AbacDecision.Deny;
        result.IsDenied.Should().BeTrue();

        result.Decision = AbacDecision.NotApplicable;
        result.IsNotApplicable.Should().BeTrue();
    }

    [Fact]
    public void KeyRotationBackgroundService_CanBeConstructed()
    {
        var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var service = new KeyRotationBackgroundService(
            serviceProvider,
            NullLogger<KeyRotationBackgroundService>.Instance,
            Options.Create(new KeyRotationOptions()));

        service.Should().NotBeNull();
    }

    [Fact]
    public async Task MfaAttemptTrackingService_CoversPolicyAndLockoutBranches()
    {
        var accessor = new HttpContextAccessor();
        var service = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptRepository>(),
            accessor);

        service.IsLockedOut(new UserMfaConfiguration
        {
            FailedAttempts = 5,
            LockedOutUntil = null
        }).Should().BeFalse();

        service.IsLockedOut(new UserMfaConfiguration
        {
            FailedAttempts = 5,
            LockedOutUntil = SystemClock.UtcNow.AddMinutes(5)
        }).Should().BeTrue();

        (await service.IsMfaRequiredByPolicyAsync(Guid.NewGuid())).Should().BeFalse();

        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, "Admin")],
                "unit-test"))
        };
        (await service.IsMfaRequiredByPolicyAsync(Guid.NewGuid())).Should().BeTrue();

        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, "SystemAdmin")],
                "unit-test"))
        };
        (await service.IsMfaRequiredByPolicyAsync(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public void AuthenticationModuleAndTokenRevocationExtensions_RegisterMiddleware()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();
        services.AddAuthorization();
        var app = new ApplicationBuilder(services.BuildServiceProvider());

        app.UseAuthenticationModule().Should().BeSameAs(app);
        app.UseTokenRevocation().Should().BeSameAs(app);
    }

    [Fact]
    public void AuthenticationData_CoversDistributedRevocationRegistration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:TokenRevocation:UseDistributedCache"] = "true"
            })
            .Build();

        services.AddAuthenticationData(configuration);

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(ITokenRevocationService) &&
            descriptor.ImplementationType == typeof(DistributedCacheTokenRevocationService));
    }

    [Fact]
    public void SessionController_PrivateHelpers_CoverFingerprintAndCurrentSessionBranches()
    {
        var sessionId = Guid.NewGuid();
        var controller = new SessionController(Mock.Of<ISessionManagementService>(), new CommandHandlerSender())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("session_id", Guid.NewGuid().ToString())],
                        "unit-test"))
                }
            }
        };

        InvokePrivateInstance<bool>(controller, "IsCurrentSession", sessionId).Should().BeFalse();

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("session_id", sessionId.ToString())],
            "unit-test"));
        InvokePrivateInstance<bool>(controller, "IsCurrentSession", sessionId).Should().BeTrue();

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        InvokePrivateInstance<bool>(controller, "IsCurrentSession", sessionId).Should().BeFalse();

        var fingerprint = InvokePrivateStatic<string>(
            typeof(SessionController),
            "GenerateDeviceFingerprint",
            "127.0.0.1",
            "UnitTestAgent");
        fingerprint.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void WebAuthnController_PrivateHelpers_CoverFallbackBranches()
    {
        var controller = new WebAuthnController(Mock.Of<IWebAuthnService>(), new CommandHandlerSender())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("id", Guid.NewGuid().ToString())],
                        "unit-test"))
                }
            }
        };

        InvokePrivateInstance<Guid?>(controller, "GetCurrentUserId").Should().NotBeNull();
        InvokePrivateInstance<string?>(controller, "GetClientIpAddress").Should().BeNull();

        controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        InvokePrivateInstance<string?>(controller, "GetClientIpAddress").Should().Be("127.0.0.1");

        InvokePrivateStatic<int>(typeof(WebAuthnMutationCommandHandler), "ParsePositiveInt", null, 30).Should().Be(30);
        InvokePrivateStatic<int>(typeof(WebAuthnMutationCommandHandler), "ParsePositiveInt", "0", 30).Should().Be(30);
        InvokePrivateStatic<int>(typeof(WebAuthnMutationCommandHandler), "ParsePositiveInt", "12", 30).Should().Be(12);
    }

    [Fact]
    public async Task Web3Service_CoversImplementedSignatureRejectionPath()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new Web3Service(NullLogger<Web3Service>.Instance, cache);
        var address = "0x1234567890abcdef1234567890abcdef12345678";
        var challenge = await service.GenerateChallengeAsync(address);

        var result = await service.VerifySignatureAsync(address, "0x" + new string('a', 130), challenge.Message);

        result.Should().BeFalse();

        var emptySignatureResult = await InvokePrivateInstance<Task<bool>>(
            service,
            "VerifyEthereumSignature",
            "",
            address);
        emptySignatureResult.Should().BeFalse();
    }

    [Fact]
    public void LoginAttemptAnalysisService_ParseLocation_CoversFallbacks()
    {
        InvokePrivateStatic<LocationInfo?>(typeof(LoginAttemptAnalysisService), "ParseLocation", (object?)null)
            .Should().BeNull();

        var city = InvokePrivateStatic<LocationInfo>(typeof(LoginAttemptAnalysisService), "ParseLocation", "US-New York");
        city.Country.Should().Be("US");
        city.City.Should().Be("New York");

        var country = InvokePrivateStatic<LocationInfo>(typeof(LoginAttemptAnalysisService), "ParseLocation", "Canada");
        country.Country.Should().Be("Canada");
        country.City.Should().BeNull();
    }

    [Fact]
    public void MfaController_MaskPhoneNumber_CoversShortInput()
    {
        InvokePrivateStatic<string>(typeof(MfaController), "MaskPhoneNumber", "").Should().Be("****");
        InvokePrivateStatic<string>(typeof(MfaController), "MaskPhoneNumber", "123").Should().Be("****");
        InvokePrivateStatic<string>(typeof(MfaController), "MaskPhoneNumber", "15551234567").Should().Be("***-***-4567");
    }

    [Fact]
    public void AuthAttemptAndLocalSignIn_IpFallbacks_CoverEmptyForwardedValues()
    {
        var authAttemptService = new AuthAttemptService(
            Mock.Of<IAuthenticationAttemptRepository>(),
            Mock.Of<IUserEnumerationProtectionService>(),
            NullLogger<AuthAttemptService>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = " , ";

        authAttemptService.GetClientIpAddress(context).Should().Be("Unknown");

        InvokePrivateStatic<string?>(
            typeof(LocalSignInHandler),
            "GetClientIpAddress",
            new DefaultHttpContext()).Should().BeNull();

        var localContext = new DefaultHttpContext();
        localContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        InvokePrivateStatic<string?>(
            typeof(LocalSignInHandler),
            "GetClientIpAddress",
            localContext).Should().Be("127.0.0.1");
    }

    [Fact]
    public void PolymorphicCredentialConverter_CoversNullTypeBranch()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PolymorphicCredentialConverter());

        var act = () => JsonSerializer.Deserialize<ICredentialData>("{\"type\":null}", options);

        act.Should().Throw<JsonException>()
            .WithMessage("Unknown credential type:*");
    }

    [Fact]
    public void EntityBooleanHelpers_CoverRemainingTrueBranches()
    {
        var apiKey = new ApiKey
        {
            IsActive = true,
            ExpiresAt = SystemClock.UtcNow.AddMinutes(-1)
        };
        apiKey.IsValid().Should().BeFalse();

        new ApiKey
        {
            IsActive = true,
            ExpiresAt = null
        }.IsValid().Should().BeTrue();

        new ApiKey
        {
            IsActive = true,
            RevokedAt = SystemClock.UtcNow,
            ExpiresAt = null
        }.IsValid().Should().BeFalse();

        new BlockchainCertificateAnchor
        {
            ExpiresAt = SystemClock.UtcNow.AddMinutes(5)
        }.IsValid.Should().BeTrue();

        new BlockchainCertificateAnchor
        {
            ExpiresAt = null
        }.IsValid.Should().BeTrue();

        new BlockchainCertificateAnchor
        {
            ExpiresAt = SystemClock.UtcNow.AddMinutes(-5)
        }.IsValid.Should().BeFalse();

        new IdentityVerification
        {
            Status = "Approved",
            ExpiresAt = SystemClock.UtcNow.AddMinutes(5)
        }.IsValid.Should().BeTrue();

        new IdentityVerification
        {
            Status = "Approved",
            ExpiresAt = null
        }.IsValid.Should().BeTrue();

        new IdentityVerification
        {
            Status = "Approved",
            ExpiresAt = SystemClock.UtcNow.AddMinutes(-5)
        }.IsValid.Should().BeFalse();

        new ServiceAccount
        {
            IsActive = true,
            IsLocked = false,
            ExpiresAt = SystemClock.UtcNow.AddMinutes(5)
        }.CanAuthenticate.Should().BeTrue();

        new ServiceAccount
        {
            IsActive = true,
            IsLocked = false,
            ExpiresAt = null
        }.CanAuthenticate.Should().BeTrue();

        new ServiceAccount
        {
            IsActive = true,
            IsLocked = false,
            ExpiresAt = SystemClock.UtcNow.AddMinutes(-5)
        }.CanAuthenticate.Should().BeFalse();

        new TestVerifiableCredential
        {
            ExpirationDate = SystemClock.UtcNow.AddMinutes(5)
        }.IsValid.Should().BeTrue();

        new TestVerifiableCredential
        {
            ExpirationDate = null
        }.IsValid.Should().BeTrue();

        new TestVerifiableCredential
        {
            ExpirationDate = SystemClock.UtcNow.AddMinutes(-5)
        }.IsValid.Should().BeFalse();

        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var permission = new TestResourcePermission(userId, Guid.NewGuid(), resourceId);

        permission.IsForUserAndResource(userId, resourceId).Should().BeTrue();
        permission.IsForUserAndResource(Guid.NewGuid(), resourceId).Should().BeFalse();
        permission.IsForUserAndResource(userId, Guid.NewGuid()).Should().BeFalse();
        permission.UserId = null;
        permission.IsForUserAndResource(userId, resourceId).Should().BeFalse();
    }

    [Fact]
    public async Task ServiceAccountService_CoversGetAll()
    {
        var repository = new Mock<IServiceAccountRepository>();
        repository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceAccount>());

        var service = new ServiceAccountService(
            repository.Object,
            Mock.Of<IRefreshTokenHasher>(),
            NullLogger<ServiceAccountService>.Instance);

        var accounts = await service.GetAllAsync();

        accounts.Should().BeEmpty();
    }

    [Fact]
    public void OAuthAndTotpPrivateHelpers_CoverRemainingBranches()
    {
        var oauth = new OAuthService(new HttpClient(), EmptyConfiguration(), NullLogger<OAuthService>.Instance);

        InvokePrivateInstance<string>(
            oauth,
            "BuildGoogleAuthUrl",
            "client",
            "https://app.example/callback",
            "state",
            null).Should().Contain("openid%20email%20profile");

        InvokePrivateInstance<string>(
            oauth,
            "BuildGoogleAuthUrl",
            "client",
            "https://app.example/callback",
            "state",
            Array.Empty<string>()).Should().Contain("openid%20email%20profile");

        InvokePrivateInstance<string>(
            oauth,
            "BuildGoogleAuthUrl",
            "client",
            "https://app.example/callback",
            "state",
            new[] { "openid", "email", "calendar.read" }).Should().Contain("calendar.read");

        const string secret = "JBSWY3DPEHPK3PXP";
        var timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var code = InvokePrivateStatic<string>(typeof(TotpMfaService), "GenerateTotpCode", secret, timeStep);

        InvokePrivateStatic<bool>(typeof(TotpMfaService), "VerifyTotpCode", secret, code, 0).Should().BeTrue();

        var act = () => InvokePrivateStatic<byte[]>(typeof(TotpMfaService), "Base32Decode", "INVALID!");
        act.Should().Throw<TargetInvocationException>()
            .Where(ex => ex.InnerException is ArgumentException);
    }

    [Fact]
    public void ControllerAndHandlerNullGuardBranches_AreCovered()
    {
        var mediator = Mock.Of<IMediator>();

        Assert.Throws<ArgumentNullException>(() => new AbacPolicyController(null!, NullLogger<AbacPolicyController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new AbacPolicyController(mediator, null!));
        Assert.Throws<ArgumentNullException>(() => new AccessReviewAnalyticsController(null!, NullLogger<AccessReviewAnalyticsController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new AccessReviewAnalyticsController(mediator, null!));
        Assert.Throws<ArgumentNullException>(() => new AccessReviewCampaignController(null!, NullLogger<AccessReviewCampaignController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new AccessReviewCampaignController(mediator, null!));
        Assert.Throws<ArgumentNullException>(() => new AccessReviewItemController(null!, NullLogger<AccessReviewItemController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new AccessReviewItemController(mediator, null!));
        Assert.Throws<ArgumentNullException>(() => new ConditionalPolicyCrudController(null!, NullLogger<ConditionalPolicyCrudController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new ConditionalPolicyCrudController(mediator, null!));
        Assert.Throws<ArgumentNullException>(() => new ConditionalPolicyEvaluationController(null!, NullLogger<ConditionalPolicyEvaluationController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new ConditionalPolicyEvaluationController(mediator, null!));
        Assert.Throws<ArgumentNullException>(() => new PermissionAdminController(null!, NullLogger<PermissionAdminController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new PermissionAdminController(mediator, null!));
        Assert.Throws<ArgumentNullException>(() => new PermissionEvaluationController(null!, NullLogger<PermissionEvaluationController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new PermissionEvaluationController(mediator, null!));
        Assert.Throws<ArgumentNullException>(() => new PermissionGrantsController(null!, NullLogger<PermissionGrantsController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new PermissionGrantsController(mediator, null!));

        var authService = Mock.Of<IAuthService>();
        var users = Mock.Of<IUserRepository>();
        var validator = Mock.Of<IValidator<RefreshTokenCommand>>();

        Assert.Throws<ArgumentNullException>(() =>
            new RefreshTokenHandler(null!, users, NullLogger<RefreshTokenHandler>.Instance, validator));
        Assert.Throws<ArgumentNullException>(() =>
            new RefreshTokenHandler(authService, null!, NullLogger<RefreshTokenHandler>.Instance, validator));
        Assert.Throws<ArgumentNullException>(() =>
            new RefreshTokenHandler(authService, users, null!, validator));

        Assert.Throws<ArgumentNullException>(() =>
            new RevokeTokenHandler(null!, NullLogger<RevokeTokenHandler>.Instance));
        Assert.Throws<ArgumentNullException>(() =>
            new RevokeTokenHandler(authService, null!));
    }

    [Fact]
    public async Task DeprecatedAuthenticationMiddlewares_CoverConstructorsAndInvoke()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

#pragma warning disable CS0618
        var permissionCaching = new PermissionCachingMiddleware(
            next,
            NullLogger<PermissionCachingMiddleware>.Instance);
        await permissionCaching.InvokeAsync(context);

        var abac = new AbacPolicyMiddleware(
            next,
            NullLogger<AbacPolicyMiddleware>.Instance);
        await abac.InvokeAsync(context);

        var accessReview = new AccessReviewMiddleware(
            next,
            NullLogger<AccessReviewMiddleware>.Instance);
        await accessReview.InvokeAsync(context);
#pragma warning restore CS0618

        nextCalled.Should().BeTrue();
        context.Response.Headers.Should().ContainKey("X-Permission-Cache");
        context.Response.Headers.Should().ContainKey("X-ABAC-Policies");
        context.Response.Headers.Should().ContainKey("X-Access-Review");
    }

    [Fact]
    public void RepositoryConstructorsAndPrivateSetProperties_AreCovered()
    {
        AssertRepositoryProperty(new MfaAttemptRepository(ContextWithSet<MfaAttempt>()), "MfaAttempts");
        AssertRepositoryProperty(new RefreshTokenRepository(ContextWithSet<RefreshToken>()), "RefreshTokens");
        AssertRepositoryProperty(new ServiceAccountRepository(ContextWithSet<ServiceAccount>()), "ServiceAccounts");
        AssertRepositoryProperty(new TrustedDeviceRepository(ContextWithSet<TrustedDevice>()), "TrustedDevices");
        AssertRepositoryProperty(new UserMfaConfigurationRepository(ContextWithSet<UserMfaConfiguration>()), "UserMfaConfigurations");
        AssertRepositoryProperty(new UserSessionRepository(ContextWithSet<UserSession>()), "UserSessions");
        AssertRepositoryProperty(new WebAuthnCredentialRepository(ContextWithSet<UserWebAuthnCredential>()), "Credentials");
    }

    [Fact]
    public async Task UserSessionRepository_CreateAsync_PreservesProvidedSessionId()
    {
        var sessionId = Guid.NewGuid();
        var session = new UserSession { Id = sessionId, UserId = Guid.NewGuid() };
        var set = new Mock<DbSet<UserSession>>();
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Set<UserSession>()).Returns(set.Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var repository = new UserSessionRepository(context.Object);

        var result = await repository.CreateAsync(session);

        result.Id.Should().Be(sessionId);
        set.Verify(x => x.Add(session), Times.Once);
    }

    [Fact]
    public async Task UserSessionRepository_CreateAsync_GeneratesIdWhenMissing()
    {
        var session = new UserSession { UserId = Guid.NewGuid() };
        var set = new Mock<DbSet<UserSession>>();
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Set<UserSession>()).Returns(set.Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var repository = new UserSessionRepository(context.Object);

        var result = await repository.CreateAsync(session);

        result.Id.Should().NotBeEmpty();
        set.Verify(x => x.Add(session), Times.Once);
    }

    [Fact]
    public void CommandAndEventHandlerConstructors_AreCovered()
    {
        var dbContext = Mock.Of<IApplicationDbContext>();

        new ApplyPermissionTemplateCommandHandler(
            dbContext,
            NullLogger<ApplyPermissionTemplateCommandHandler>.Instance).Should().NotBeNull();
        new GetPermissionTemplatesQueryHandler(
            dbContext,
            NullLogger<GetPermissionTemplatesQueryHandler>.Instance).Should().NotBeNull();
        new SendPasswordResetRequestedHandler(
            NullLogger<SendPasswordResetRequestedHandler>.Instance,
            Mock.Of<INotificationService>(),
            Mock.Of<IUserRepository>()).Should().NotBeNull();

        new RevokeContentTypePermissionByIdHandler(dbContext).Should().NotBeNull();
        new RevokeResourcePermissionByIdHandler(dbContext).Should().NotBeNull();
        new RevokeTenantPermissionByIdHandler(dbContext).Should().NotBeNull();

        new AuthenticationFailedEventHandler(NullLogger<AuthenticationFailedEventHandler>.Instance).Should().NotBeNull();
        new GenerateWeb3ChallengeHandler(Mock.Of<IWeb3Service>()).Should().NotBeNull();
        new MfaEventHandler(NullLogger<MfaEventHandler>.Instance).Should().NotBeNull();
        new RefreshTokenEventHandler(NullLogger<RefreshTokenEventHandler>.Instance).Should().NotBeNull();
        new LocalSignUpHandler(
            Mock.Of<IAuthService>(),
            Mock.Of<IUserRepository>(),
            NullLogger<LocalSignUpHandler>.Instance).Should().NotBeNull();
        new GoogleIdTokenSignInHandler(
            Mock.Of<IAuthService>(),
            Mock.Of<IUserRepository>(),
            NullLogger<GoogleIdTokenSignInHandler>.Instance,
            Mock.Of<IValidator<GoogleIdTokenSignInCommand>>()).Should().NotBeNull();
    }

    [Fact]
    public void LogoutHandler_NullGuardBranches_AreCovered()
    {
        var tokenRevocation = Mock.Of<ITokenRevocationService>();
        var refreshTokens = Mock.Of<IRefreshTokenRepository>();
        var userSessions = Mock.Of<IUserSessionRepository>();
        var logger = NullLogger<LogoutHandler>.Instance;

        Assert.Throws<ArgumentNullException>(() =>
            new LogoutHandler(null!, refreshTokens, userSessions, logger));
        Assert.Throws<ArgumentNullException>(() =>
            new LogoutHandler(tokenRevocation, null!, userSessions, logger));
        Assert.Throws<ArgumentNullException>(() =>
            new LogoutHandler(tokenRevocation, refreshTokens, null!, logger));
        Assert.Throws<ArgumentNullException>(() =>
            new LogoutHandler(tokenRevocation, refreshTokens, userSessions, null!));
    }

    private static IConfiguration EmptyConfiguration()
    {
        return new ConfigurationBuilder().AddInMemoryCollection().Build();
    }

    private static JwtTokenService CreateJwtTokenService(JwtOptions? options = null)
    {
        options ??= new JwtOptions
        {
            SecretKey = "this-is-a-very-long-secret-key-for-testing-min-256-bits-padded!!!!",
            Issuer = "issuer",
            Audience = "audience",
            AccessTokenExpirationMinutes = 30,
            RefreshTokenExpirationDays = 7,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true
        };

        return new JwtTokenService(
            NullLogger<JwtTokenService>.Instance,
            Mock.Of<IRefreshTokenRepository>(),
            Mock.Of<IRefreshTokenHasher>(),
            Mock.Of<IHttpContextAccessor>(),
            Options.Create(options));
    }

    private static string CreateUnsignedJwtWithInvalidTenant()
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "issuer",
            audience: "audience",
            claims:
            [
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, "user@example.com"),
                new Claim("tenant_id", "not-a-guid")
            ],
            expires: SystemClock.UtcNow.AddMinutes(5));

        return handler.WriteToken(token);
    }

    private static string CreateUnsignedJwtWithoutSubject()
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "issuer",
            audience: "audience",
            claims:
            [
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, "user@example.com")
            ],
            expires: SystemClock.UtcNow.AddMinutes(5));

        return handler.WriteToken(token);
    }

    private static void SetTokenInfo(
        IMemoryCache cache,
        string token,
        Guid userId,
        string email,
        string type,
        DateTime expiresAt)
    {
        var tokenInfoType = typeof(EmailVerificationService).Assembly
            .GetType("GameGuild.Identity.Authentication.TokenInfo", throwOnError: true)!;
        var tokenInfo = Activator.CreateInstance(tokenInfoType)!;

        tokenInfoType.GetProperty("UserId")!.SetValue(tokenInfo, userId);
        tokenInfoType.GetProperty("Email")!.SetValue(tokenInfo, email);
        tokenInfoType.GetProperty("Type")!.SetValue(tokenInfo, type);
        tokenInfoType.GetProperty("ExpiresAt")!.SetValue(tokenInfo, expiresAt);

        cache.Set("emailverify:token:" + token, tokenInfo);
    }

    private static IApplicationDbContext ContextWithSet<TEntity>()
        where TEntity : class
    {
        var set = new Mock<DbSet<TEntity>>();
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Set<TEntity>()).Returns(set.Object);
        return context.Object;
    }

    private static void AssertRepositoryProperty(object repository, string propertyName)
    {
        var value = repository.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(repository);

        value.Should().NotBeNull();
    }

    private static T InvokePrivateStatic<T>(Type type, string methodName, params object?[] args)
    {
        return (T)type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args)!;
    }

    private static T InvokePrivateInstance<T>(object instance, string methodName, params object?[] args)
    {
        return (T)instance.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, args)!;
    }

    private sealed class TestAbacEvaluationResult : AbacEvaluationResult;

    private sealed class TestVerifiableCredential : VerifiableCredential;

    private sealed class TestResource : EntityBase;

    private sealed class TestResourcePermission : ResourcePermission<TestResource>
    {
        public TestResourcePermission(Guid userId, Guid tenantId, Guid resourceId)
            : base(userId, tenantId, resourceId)
        {
        }
    }
}

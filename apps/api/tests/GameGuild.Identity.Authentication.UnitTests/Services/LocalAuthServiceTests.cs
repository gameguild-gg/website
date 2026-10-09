using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authentication.UnitTests.Infrastructure;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class LocalAuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepoMock = new();
    private readonly Mock<IRefreshTokenLineageRepository> _tokenLineageRepoMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock = new();
    private readonly Mock<IAuthAttemptService> _authAttemptServiceMock = new();
    private readonly Mock<IAuthenticationAnomalyDetectionService> _anomalyDetectionMock = new();
    private readonly Mock<IAuthenticationAuditEventSink> _authenticationAuditEventSinkMock = new();
    private readonly Mock<IUserEnumerationProtectionService> _enumerationProtectionMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ISessionManagementService> _sessionManagementServiceMock = new();
    private readonly IConfiguration _configuration;
    private readonly LocalAuthService _sut;

    public LocalAuthServiceTests()
    {
        PersistedAuthenticationSessions.Configure(_sessionManagementServiceMock);
        var configData = new Dictionary<string, string?>
        {
            { "Jwt:RefreshTokenExpiryInDays", "7" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        var passwordHasher = new PasswordHasher(NullLogger<PasswordHasher>.Instance, _configuration);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "TestAgent/1.0";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
        _authAttemptServiceMock.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
        _enumerationProtectionMock.Setup(x => x.GetGenericErrorMessage(It.IsAny<string>())).Returns("Authentication failed");
        _enumerationProtectionMock.Setup(x => x.AddTimingProtectionDelayAsync(It.IsAny<bool>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _anomalyDetectionMock
            .Setup(x => x.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalBehavior = true });
        _anomalyDetectionMock
            .Setup(x => x.RecordSuspiciousActivityAsync(It.IsAny<SuspiciousActivity>()))
            .Returns(Task.CompletedTask);
        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns((string token) => $"hash-{token}");
        _refreshTokenRepoMock.Setup(x => x.TryRevokeForRotationAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _publisherMock.Setup(x => x.Publish(It.IsAny<UserSignedUpNotification>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _authenticationAuditEventSinkMock
            .Setup(x => x.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var tenantId = Guid.NewGuid();
        _senderMock
            .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships =
                [
                    new UserMembershipDto
                    {
                        TenantId = tenantId,
                        TenantName = "Default tenant",
                        TenantSlug = "default-tenant",
                        TenantIsActive = true,
                        Role = "Member",
                        IsActive = true
                    }
                ]
            });
        _senderMock
            .Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        _sut = new LocalAuthService(
            _userRepoMock.Object,
            _refreshTokenRepoMock.Object,
            _tokenLineageRepoMock.Object,
            _jwtTokenServiceMock.Object,
            _refreshTokenHasherMock.Object,
            _configuration,
            _authAttemptServiceMock.Object,
            passwordHasher,
            _anomalyDetectionMock.Object,
            _enumerationProtectionMock.Object,
            _httpContextAccessorMock.Object,
            NullLogger<LocalAuthService>.Instance,
            _senderMock.Object,
            _sessionManagementServiceMock.Object,
            auditEventSink: _authenticationAuditEventSinkMock.Object
        );
    }

    // ── LocalSignInAsync ──────────────────────────────────────

    [Fact]
    public async Task LocalSignInAsync_UserNotFound_ThrowsUnauthorizedAccessException()
    {
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new LocalSignInRequest { Email = "unknown@example.com", Password = "Password1!" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LocalSignInAsync(request));
    }

    [Fact]
    public async Task LocalSignInAsync_UserNotFound_RecordsFailedAttempt()
    {
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new LocalSignInRequest { Email = "unknown@example.com", Password = "Password1!" };

        try { await _sut.LocalSignInAsync(request); } catch { /* expected */ }

        _authAttemptServiceMock.Verify(
            x => x.RecordFailedAttemptAsync("unknown@example.com", null, "127.0.0.1", It.IsAny<string>(), "InvalidCredentials", It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [Fact]
    public async Task LocalSignInAsync_InvalidPassword_ThrowsUnauthorizedAccessException()
    {
        var user = User.CreateWithPassword("user@example.com", "testuser", BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!"));
        _userRepoMock.Setup(x => x.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new LocalSignInRequest { Email = "user@example.com", Password = "WrongPassword!" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LocalSignInAsync(request));
    }

    [Fact]
    public async Task LocalSignInAsync_ValidCredentials_LowRisk_ReturnsSuccessResponse()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password1!");
        var user = User.CreateWithPassword("user@example.com", "testuser", passwordHash);
        var userId = user.Id;

        _userRepoMock.Setup(x => x.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepoMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _anomalyDetectionMock.Setup(x => x.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low, RiskScore = 0, DetectedAnomalies = new List<string>() });
        _anomalyDetectionMock.Setup(x => x.AnalyzeBehavioralPatternsAsync(userId, It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult
            {
                MatchesTypicalBehavior = false,
                RiskScore = 35,
                RiskLevel = RiskLevel.Medium,
                Confidence = 0.9,
                DetectedAnomalies = ["UnfamiliarIp"]
            });

        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(userId, "user@example.com", It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(userId, It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        var request = new LocalSignInRequest { Email = "user@example.com", Password = "Password1!" };

        var result = await _sut.LocalSignInAsync(request);

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.UserId.Should().Be(userId);
        result.Email.Should().Be("user@example.com");
        result.SessionId.Should().NotBeEmpty();
        _authenticationAuditEventSinkMock.Verify(x => x.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.ThreatDetected" &&
                    auditEvent.UserId == userId &&
                    auditEvent.Success &&
                    auditEvent.Method == "Password" &&
                    auditEvent.AssessedRiskLevel == RiskLevel.Medium &&
                    auditEvent.Metadata != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _anomalyDetectionMock.Verify(x => x.RecordSuspiciousActivityAsync(
                It.Is<SuspiciousActivity>(activity =>
                    activity.UserId == userId &&
                    activity.ActivityType == "Authentication.ThreatDetected" &&
                    activity.RiskScore == 35 &&
                    activity.RiskLevel == RiskLevel.Medium)),
            Times.Once);
        _sessionManagementServiceMock.Verify(
            x => x.CreateSessionAsync(
                result.SessionId,
                userId,
                "127.0.0.1",
                It.IsAny<string>(),
                "hash-refresh-token",
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LocalSignInAsync_ValidCredentialsWithoutActiveMembership_ThrowsAccessDeniedException()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password1!");
        var user = User.CreateWithPassword("unassigned@example.com", "unassigned", passwordHash);
        var userId = user.Id;

        _userRepoMock.Setup(x => x.GetByEmailAsync("unassigned@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepoMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _anomalyDetectionMock.Setup(x => x.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        _senderMock
            .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse());

        var request = new LocalSignInRequest { Email = "unassigned@example.com", Password = "Password1!" };

        await Assert.ThrowsAsync<AccessDeniedException>(() => _sut.LocalSignInAsync(request));

        _jwtTokenServiceMock.Verify(
            x => x.GenerateAccessTokenAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LocalSignInAsync_InactiveDefaultMembership_ReactivatesItBeforeIssuingTenantToken()
    {
        var defaultTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "GameGuild",
            Slug = "gameguild",
            IsDefault = true,
            IsActive = true
        };
        var user = User.CreateWithPassword(
            "admin@example.com",
            "admin",
            BCrypt.Net.BCrypt.HashPassword("Password1!"));
        AddTenantMemberCommand? capturedCommand = null;
        Guid? capturedTenantId = null;

        _userRepoMock.Setup(x => x.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepoMock.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _anomalyDetectionMock.Setup(x => x.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        _senderMock.Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(defaultTenant);
        _senderMock
            .SetupSequence(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships =
                [
                    new UserMembershipDto
                    {
                        TenantId = defaultTenant.Id,
                        TenantName = defaultTenant.Name,
                        TenantSlug = defaultTenant.Slug,
                        TenantIsActive = true,
                        Role = "SystemAdmin",
                        IsActive = false
                    }
                ]
            })
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships =
                [
                    new UserMembershipDto
                    {
                        TenantId = defaultTenant.Id,
                        TenantName = defaultTenant.Name,
                        TenantSlug = defaultTenant.Slug,
                        TenantIsActive = true,
                        Role = "SystemAdmin",
                        IsActive = true
                    }
                ]
            });
        _senderMock
            .Setup(x => x.Send(It.IsAny<AddTenantMemberCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AddTenantMemberResponse>, CancellationToken>((request, _) => capturedCommand = (AddTenantMemberCommand)request)
            .ReturnsAsync(new AddTenantMemberResponse { Success = true, MemberId = Guid.NewGuid() });
        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessTokenAsync(user.Id, user.Email, It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string[], Guid?, int, Guid, CancellationToken>((_, _, _, tenantId, _, _, _) => capturedTenantId = tenantId)
            .ReturnsAsync("access-token");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user.Id, It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        var result = await _sut.LocalSignInAsync(new LocalSignInRequest { Email = user.Email, Password = "Password1!" });

        result.TenantId.Should().Be(defaultTenant.Id);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.TenantId.Should().Be(defaultTenant.Id);
        capturedCommand.Role.Should().Be("SystemAdmin");
        capturedTenantId.Should().Be(defaultTenant.Id);
    }

    [Fact]
    public async Task LocalSignInAsync_ValidCredentials_HighRisk_RequiresStepUp()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password1!");
        var user = User.CreateWithPassword("user@example.com", "testuser", passwordHash);

        _userRepoMock.Setup(x => x.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _anomalyDetectionMock.Setup(x => x.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult
            {
                RiskLevel = RiskLevel.High,
                RiskScore = 70,
                DetectedAnomalies = new List<string> { "IpAddressChange", "ImpossibleTravel" }
            });

        var request = new LocalSignInRequest { Email = "user@example.com", Password = "Password1!" };

        var result = await _sut.LocalSignInAsync(request);

        result.Success.Should().BeFalse();
        result.RequiresStepUp.Should().BeTrue();
        result.StepUpToken.Should().NotBeNullOrEmpty();
        result.RiskLevel.Should().Be(RiskLevel.High);

        _authenticationAuditEventSinkMock.Verify(x => x.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.StepUpRequired" &&
                    auditEvent.UserId == user.Id &&
                    !auditEvent.Success &&
                    auditEvent.Method == "Password" &&
                    auditEvent.ErrorMessage == "StepUpRequired"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LocalSignInAsync_FailedLoginWithDetectedThreat_AuditsEvenWhenAttemptRecordingFails()
    {
        _userRepoMock.Setup(x => x.GetByEmailAsync("unknown@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _authAttemptServiceMock
            .Setup(x => x.RecordFailedAttemptAsync(
                "unknown@example.com",
                null,
                "127.0.0.1",
                It.IsAny<string>(),
                "InvalidCredentials",
                It.IsAny<TimeSpan>()))
            .ThrowsAsync(new InvalidOperationException("Attempt store unavailable"));
        _anomalyDetectionMock.Setup(x => x.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult
            {
                IsAnomalous = true,
                RiskLevel = RiskLevel.High,
                RiskScore = 85,
                DetectedAnomalies = ["BruteForceDetected"]
            });

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.LocalSignInAsync(new LocalSignInRequest { Email = "unknown@example.com", Password = "WrongPassword!" }));

        exception.Message.Should().Be("Authentication failed");
        _authenticationAuditEventSinkMock.Verify(x => x.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.ThreatDetected" &&
                    auditEvent.UserId == null &&
                    !auditEvent.Success &&
                    auditEvent.Method == "Password" &&
                    auditEvent.ErrorMessage == "InvalidCredentials"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LocalSignInAsync_ValidCredentials_RecordsSuccessfulAttempt()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password1!");
        var user = User.CreateWithPassword("user@example.com", "testuser", passwordHash);
        var userId = user.Id;

        _userRepoMock.Setup(x => x.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepoMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _anomalyDetectionMock.Setup(x => x.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });

        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("at");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("rt");

        var request = new LocalSignInRequest { Email = "user@example.com", Password = "Password1!" };
        await _sut.LocalSignInAsync(request);

        _authAttemptServiceMock.Verify(
            x => x.RecordSuccessfulAttemptAsync("user@example.com", userId, "127.0.0.1", It.IsAny<string>(), It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [Fact]
    public async Task LocalSignInAsync_UnexpectedException_RecordsFailedAttemptAndThrows()
    {
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var request = new LocalSignInRequest { Email = "user@example.com", Password = "pass" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LocalSignInAsync(request));

        _authAttemptServiceMock.Verify(
            x => x.RecordFailedAttemptAsync("user@example.com", It.IsAny<Guid?>(), "127.0.0.1", It.IsAny<string>(), "SystemError", It.IsAny<TimeSpan>()),
            Times.Once);
    }

    // ── LocalSignUpAsync ──────────────────────────────────────

    [Theory]
    [InlineData("Matheus Martins", "matheus-martins")]
    [InlineData("MátHeus Martíns", "matheus-martins")]
    [InlineData("User.Name_1", "user.name_1")]
    [InlineData("  User Name  ", "user-name")]
    public async Task LocalSignUpAsync_PersistsCanonicalUsernameAndOriginalDisplayName(string input, string expected)
    {
        User? persisted = null;
        _userRepoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => persisted = user).Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync("access-token");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync("refresh-token");

        var response = await _sut.LocalSignUpAsync(new LocalSignUpRequest
        {
            Email = "username@example.test", Password = "Password1!", Username = input
        });

        response.Success.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Username.Should().Be(expected);
        persisted.Name.Should().Be(input);
        persisted.Email.Should().Be("username@example.test");
        persisted.IntegrationEvents.Should().ContainSingle();
        _sessionManagementServiceMock.Verify(x => x.CreateSessionAsync(It.IsAny<Guid>(), persisted.Id,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("---")]
    [InlineData("...")]
    [InlineData("東京")]
    [InlineData("  ab  ")]
    [InlineData("a\u0000b")]
    public async Task LocalSignUpAsync_UnusableUsernameRejectsBeforePersistenceAndTokenIssuance(string? input)
    {
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => _sut.LocalSignUpAsync(
            new LocalSignUpRequest { Email = "username@example.test", Password = "Password1!", Username = input! }));

        exception.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Username");
        _userRepoMock.Verify(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _jwtTokenServiceMock.Verify(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LocalSignUpAsync_NewUser_ReturnsSuccess()
    {
        _userRepoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), "new@example.com", It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        var request = new LocalSignUpRequest { Email = "new@example.com", Password = "Password1!", Username = "newuser" };

        var before = SystemClock.UtcNow;
        var result = await _sut.LocalSignUpAsync(request);
        var after = SystemClock.UtcNow;

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Sign-up successful");
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.Email.Should().Be("new@example.com");
        // ponytail: sign-up must return access-token lifetime, not refresh-token lifetime
        result.ExpiresIn.Should().Be(3600, "default AccessTokenExpirationMinutes is 60");
        result.AccessTokenExpiresAt.Should().BeOnOrAfter(before.AddMinutes(59));
        result.AccessTokenExpiresAt.Should().BeOnOrBefore(after.AddMinutes(61));
    }

    [Fact]
    public async Task LocalSignUpAsync_CustomAccessTokenExpiration_ParsedFromConfig()
    {
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:RefreshTokenExpiryInDays", "7" },
                { "Jwt:AccessTokenExpirationMinutes", "30" }
            })
            .Build();

        var sut = new LocalAuthService(
            _userRepoMock.Object,
            _refreshTokenRepoMock.Object,
            _tokenLineageRepoMock.Object,
            _jwtTokenServiceMock.Object,
            _refreshTokenHasherMock.Object,
            customConfig,
            _authAttemptServiceMock.Object,
            new PasswordHasher(NullLogger<PasswordHasher>.Instance, customConfig),
            _anomalyDetectionMock.Object,
            _enumerationProtectionMock.Object,
            _httpContextAccessorMock.Object,
            NullLogger<LocalAuthService>.Instance,
            _senderMock.Object,
            _sessionManagementServiceMock.Object
        );

        _userRepoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        var request = new LocalSignUpRequest { Email = "custom@example.com", Password = "Password1!", Username = "customuser" };

        var before = SystemClock.UtcNow;
        var result = await sut.LocalSignUpAsync(request);
        var after = SystemClock.UtcNow;

        result.ExpiresIn.Should().Be(1800, "30 minutes * 60 seconds");
        result.AccessTokenExpiresAt.Should().BeOnOrBefore(after.AddMinutes(31));
        result.AccessTokenExpiresAt.Should().BeOnOrAfter(before.AddMinutes(29));
    }

    [Fact]
    public async Task LocalSignUpAsync_ExistingEmail_ThrowsInvalidOperationException()
    {
        _userRepoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new LocalSignUpRequest { Email = "existing@example.com", Password = "Password1!", Username = "user" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.LocalSignUpAsync(request));
    }

    [Fact]
    public async Task LocalSignUpAsync_WeakPassword_RejectsBeforeCheckingEmailAvailability()
    {
        var request = new LocalSignUpRequest
        {
            Email = "weak-password@example.com",
            Password = "weak",
            Username = "weakpassword"
        };

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => _sut.LocalSignUpAsync(request));

        exception.Errors.Should().Contain(error => error.PropertyName == "Password");
        _userRepoMock.Verify(
            repository => repository.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LocalSignUpAsync_NewUser_PersistsUserToRepository()
    {
        _userRepoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("at");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("rt");

        var request = new LocalSignUpRequest { Email = "new@example.com", Password = "Password1!", Username = "newuser" };
        await _sut.LocalSignUpAsync(request);

        _userRepoMock.Verify(x => x.AddAsync(It.Is<User>(u => u.Email == "new@example.com"), It.IsAny<CancellationToken>()), Times.Once);
        _userRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LocalSignUpAsync_UserWithoutMembership_ProvisionsTheDefaultTenant()
    {
        var defaultTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "GameGuild",
            Slug = "gameguild",
            IsDefault = true,
            IsActive = true
        };

        _userRepoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _senderMock
            .Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultTenant);
        _senderMock
            .SetupSequence(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse())
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships =
                [
                    new UserMembershipDto
                    {
                        TenantId = defaultTenant.Id,
                        TenantName = defaultTenant.Name,
                        TenantSlug = defaultTenant.Slug,
                        TenantIsActive = true,
                        Role = "Member",
                        IsActive = true
                    }
                ]
            });
        _senderMock
            .Setup(x => x.Send(It.IsAny<AddTenantMemberCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddTenantMemberResponse { Success = true, MemberId = Guid.NewGuid() });
        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        var result = await _sut.LocalSignUpAsync(new LocalSignUpRequest
        {
            Email = "new-member@example.com",
            Password = "Password1!",
            Username = "new-member"
        });

        result.TenantId.Should().Be(defaultTenant.Id);
        _senderMock.Verify(
            x => x.Send(
                It.Is<AddTenantMemberCommand>(command =>
                    command.TenantId == defaultTenant.Id && command.UserId == result.UserId && command.Role == "Member"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task LocalSignUpAsync_RecordsSuccessfulRegistration()
    {
        _userRepoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("at");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("rt");

        var request = new LocalSignUpRequest { Email = "new@example.com", Password = "P@ss1word", Username = "newuser" };
        await _sut.LocalSignUpAsync(request);

        _authAttemptServiceMock.Verify(
            x => x.RecordSuccessfulAttemptAsync("new@example.com", It.IsAny<Guid>(), "127.0.0.1", It.IsAny<string>(), It.IsAny<TimeSpan>(), "Registration"),
            Times.Once);
    }

    [Fact]
    public async Task LocalSignUpAsync_RecordsDurableUserCreatedEventBeforeSave()
    {
        User? created = null;
        var eventPresentAtSave = false;
        _userRepoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => created = user)
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => eventPresentAtSave = created?.IntegrationEvents.OfType<UserCreatedEvent>().Any() == true)
            .Returns(Task.CompletedTask);
        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("at");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("rt");

        var request = new LocalSignUpRequest { Email = "new@example.com", Password = "P@ss1word", Username = "newuser" };

        await _sut.LocalSignUpAsync(request);

        eventPresentAtSave.Should().BeTrue();
        created!.IntegrationEvents.OfType<UserCreatedEvent>().Should().ContainSingle()
            .Which.UserId.Should().Be(created.Id);
        _publisherMock.VerifyNoOtherCalls();
    }

    // ── RefreshTokenAsync ─────────────────────────────────────

    [Theory]
    [InlineData("Family", false)]
    [InlineData("Account", true)]
    public async Task RefreshReplayUsesTheSelectedServerScope(string policy, bool accountScope)
    {
        _configuration["Jwt:RefreshTokenReplayContainmentScope"] = policy;
        var owner = User.Create("synthetic-scope@example.test", "Synthetic policy owner");
        var version = owner.TokenVersion;
        var sessionId = Guid.NewGuid();
        var root = new RefreshToken { Id = Guid.NewGuid(), UserId = owner.Id, SessionId = sessionId,
            Token = "hashed", IsRevoked = true, ExpiresAt = SystemClock.UtcNow.AddDays(1) };
        _refreshTokenHasherMock.Setup(value => value.HashToken(It.IsAny<string>())).Returns("hashed");
        _refreshTokenRepoMock.Setup(value => value.GetByTokenAsync("hashed", It.IsAny<CancellationToken>())).ReturnsAsync(root);
        _tokenLineageRepoMock.Setup(value => value.RevokeFamilyAsync(owner.Id, root.Id, "127.0.0.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)sessionId);
        _sessionManagementServiceMock.Setup(value => value.TerminateSessionAsync(sessionId,
            SessionTerminationReason.SecurityViolation, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _userRepoMock.Setup(value => value.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owner);

        AssertCommittedRefreshDenial(await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "synthetic" }));
        _tokenLineageRepoMock.Verify(value => value.RevokeFamilyAsync(owner.Id, root.Id, "127.0.0.1", It.IsAny<CancellationToken>()),
            accountScope ? Times.Never() : Times.Once());
        _sessionManagementServiceMock.Verify(value => value.TerminateSessionAsync(sessionId,
            SessionTerminationReason.SecurityViolation, It.IsAny<CancellationToken>()), accountScope ? Times.Never() : Times.Once());
        _refreshTokenRepoMock.Verify(value => value.RevokeAllForUserAsync(owner.Id, "127.0.0.1", It.IsAny<CancellationToken>()),
            accountScope ? Times.Once() : Times.Never());
        _sessionManagementServiceMock.Verify(value => value.TerminateAllUserSessionsAsync(owner.Id,
            SessionTerminationReason.SecurityViolation, null, It.IsAny<CancellationToken>()), accountScope ? Times.Once() : Times.Never());
        Assert.Equal(version + (accountScope ? 1 : 0), owner.TokenVersion);
    }

    [Fact]
    public async Task RefreshTokenAsync_EmptyToken_ThrowsUnauthorizedAccessException()
    {
        var request = new RefreshTokenRequest { RefreshToken = "" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.RefreshTokenAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_NullToken_ThrowsUnauthorizedAccessException()
    {
        var request = new RefreshTokenRequest { RefreshToken = " " };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.RefreshTokenAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenNotFound_ThrowsUnauthorizedAccessException()
    {
        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns("hashed");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hashed", default)).ReturnsAsync((RefreshToken?)null);

        var request = new RefreshTokenRequest { RefreshToken = "invalid-token" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.RefreshTokenAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_RevokedToken_ReturnsCommittedDenialAfterContainment()
    {
        var user = User.CreateWithPassword("replay@example.com", "replay", BCrypt.Net.BCrypt.HashPassword("Password1!"));
        var originalTokenVersion = user.TokenVersion;
        var storedToken = new RefreshToken
        {
            UserId = user.Id,
            Token = "hashed",
            ExpiresAt = SystemClock.UtcNow.AddDays(-1),
            IsRevoked = true,
            CreatedByIp = "127.0.0.1"
        };

        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns("hashed");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hashed", default)).ReturnsAsync(storedToken);
        _refreshTokenRepoMock.Setup(x => x.RevokeAllForUserAsync(user.Id, "127.0.0.1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sessionManagementServiceMock.Setup(x => x.TerminateAllUserSessionsAsync(
                user.Id,
                SessionTerminationReason.SecurityViolation,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _userRepoMock.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepoMock.Setup(x => x.UpdateAsync(user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new RefreshTokenRequest { RefreshToken = "token" };

        AssertCommittedRefreshDenial(await _sut.RefreshTokenAsync(request));

        user.TokenVersion.Should().Be(originalTokenVersion + 1);
        _refreshTokenRepoMock.Verify(x => x.RevokeAllForUserAsync(user.Id, "127.0.0.1", It.IsAny<CancellationToken>()), Times.Once);
        _sessionManagementServiceMock.Verify(x => x.TerminateAllUserSessionsAsync(
            user.Id, SessionTerminationReason.SecurityViolation, null, It.IsAny<CancellationToken>()), Times.Once);
        _userRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _jwtTokenServiceMock.Verify(x => x.GenerateRefreshTokenAsync(
            It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenAsync_RecentlyRotatedTokenFromSameIp_ReturnsCommittedDenial()
    {
        var userId = Guid.NewGuid();
        var rotatedToken = new RefreshToken
        {
            UserId = userId,
            Token = "old-hash",
            ExpiresAt = SystemClock.UtcNow.AddDays(5),
            IsRevoked = true,
            RevokedAt = SystemClock.UtcNow.AddSeconds(-5),
            RevokedByIp = "127.0.0.1",
            ReplacedByToken = "replacement-token"
        };
        var replacementToken = new RefreshToken
        {
            UserId = userId,
            Token = "replacement-hash",
            ExpiresAt = SystemClock.UtcNow.AddDays(5),
            IsRevoked = false,
            CreatedByIp = "127.0.0.1",
            CreatedAt = SystemClock.UtcNow.AddMinutes(-10)
        };
        _refreshTokenHasherMock.Setup(x => x.HashToken("rotated-token")).Returns("old-hash");
        _refreshTokenHasherMock.Setup(x => x.HashToken("replacement-token")).Returns("replacement-hash");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("old-hash", default)).ReturnsAsync(rotatedToken);
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("replacement-hash", default)).ReturnsAsync(replacementToken);
        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessTokenAsync(userId, It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("replacement-access-token");

        AssertCommittedRefreshDenial(await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "rotated-token" }));
    }

    [Theory]
    [InlineData(-31, "127.0.0.1")]
    [InlineData(-5, "10.0.0.2")]
    public async Task RefreshTokenAsync_RotatedTokenOutsideGuardedRetry_ReturnsCommittedDenial(
        int revokedSecondsAgo,
        string revokedByIp)
    {
        var rotatedToken = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            Token = "old-hash",
            ExpiresAt = SystemClock.UtcNow.AddDays(5),
            IsRevoked = true,
            RevokedAt = SystemClock.UtcNow.AddSeconds(revokedSecondsAgo),
            RevokedByIp = revokedByIp,
            ReplacedByToken = "replacement-token"
        };

        _refreshTokenHasherMock.Setup(x => x.HashToken("rotated-token")).Returns("old-hash");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("old-hash", default)).ReturnsAsync(rotatedToken);

        AssertCommittedRefreshDenial(await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "rotated-token" }));
    }

    [Fact]
    public async Task RefreshTokenAsync_ExpiredToken_ThrowsUnauthorizedAccessException()
    {
        var storedToken = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            Token = "hashed",
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // expired
            IsRevoked = false,
            CreatedByIp = "127.0.0.1"
        };

        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns("hashed");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hashed", default)).ReturnsAsync(storedToken);

        var request = new RefreshTokenRequest { RefreshToken = "expired-token" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.RefreshTokenAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        var user = User.Create("refresh-valid@example.test", "Refresh valid user");
        var userId = user.Id;
        _userRepoMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var storedToken = new RefreshToken
        {
            UserId = userId,
            Token = "hashed",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            IsRevoked = false,
            CreatedByIp = "127.0.0.1",
            CreatedAt = DateTime.UtcNow.AddHours(-3)
        };

        _refreshTokenHasherMock.Setup(x => x.HashToken("valid-token")).Returns("hashed");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hashed", default)).ReturnsAsync(storedToken);

        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(userId, It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.Is<DateTimeOffset>(value => value.UtcDateTime == storedToken.CreatedAt), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-access-token");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(userId, It.IsAny<DeviceInfo>(), It.Is<DateTimeOffset>(value => value.UtcDateTime == storedToken.CreatedAt), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-refresh-token");

        _refreshTokenRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RefreshToken>(), default))
            .ReturnsAsync(storedToken);

        var request = new RefreshTokenRequest { RefreshToken = "valid-token" };

        var result = await _sut.RefreshTokenAsync(request);

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task RefreshTokenAsync_InactiveDefaultMembership_ReactivatesItBeforeIssuingTenantToken()
    {
        var defaultTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "GameGuild",
            Slug = "gameguild",
            IsDefault = true,
            IsActive = true
        };
        var user = User.CreateWithPassword("refresh@example.com", "refresh", BCrypt.Net.BCrypt.HashPassword("Password1!"));
        var storedToken = new RefreshToken
        {
            UserId = user.Id,
            Token = "hashed",
            ExpiresAt = SystemClock.UtcNow.AddDays(5),
            IsRevoked = false,
            CreatedAt = SystemClock.UtcNow.AddMinutes(-5)
        };
        AddTenantMemberCommand? capturedCommand = null;

        _refreshTokenHasherMock.Setup(x => x.HashToken("valid-token")).Returns("hashed");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hashed", default)).ReturnsAsync(storedToken);
        _userRepoMock.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _senderMock.Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(defaultTenant);
        _senderMock
            .SetupSequence(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships = [new UserMembershipDto { TenantId = defaultTenant.Id, Role = "Member", IsActive = false }]
            })
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships = [new UserMembershipDto { TenantId = defaultTenant.Id, TenantName = defaultTenant.Name, TenantSlug = defaultTenant.Slug, TenantIsActive = true, Role = "Member", IsActive = true }]
            });
        _senderMock
            .Setup(x => x.Send(It.IsAny<AddTenantMemberCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AddTenantMemberResponse>, CancellationToken>((request, _) => capturedCommand = (AddTenantMemberCommand)request)
            .ReturnsAsync(new AddTenantMemberResponse { Success = true, MemberId = Guid.NewGuid() });
        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(user.Id, user.Email, It.IsAny<string[]>(), defaultTenant.Id, It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-access-token");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user.Id, It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-refresh-token");
        _refreshTokenRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RefreshToken>(), default)).ReturnsAsync(storedToken);

        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "valid-token" });

        result.TenantId.Should().Be(defaultTenant.Id);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Role.Should().Be("Member");
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_RevokesOldToken()
    {
        var user = User.Create("refresh-rotation@example.test", "Refresh rotation user");
        var userId = user.Id;
        _userRepoMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "hashed",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            IsRevoked = false,
            CreatedByIp = "127.0.0.1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _refreshTokenHasherMock.Setup(x => x.HashToken("token")).Returns("old-hash");
        _refreshTokenHasherMock.Setup(x => x.HashToken("new-rt")).Returns("new-hash");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("old-hash", default)).ReturnsAsync(storedToken);

        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("at");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-rt");
        _refreshTokenRepoMock.Setup(x => x.TryRevokeForRotationAsync(
                storedToken.Id,
                "old-hash",
                "new-hash",
                It.IsAny<DateTime>(),
                "127.0.0.1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "token" });

        _refreshTokenRepoMock.Verify(x => x.TryRevokeForRotationAsync(
            storedToken.Id,
            "old-hash",
            "new-hash",
            It.IsAny<DateTime>(),
            "127.0.0.1",
            It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepoMock.Verify(x => x.UpdateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenLineageRepoMock.Verify(repository => repository.RecordRotationAsync(
            userId, storedToken.Id, "new-hash", response.SessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_ConcurrentRotationClaimLost_InvalidatesSessionsAfterPersistingReplacementSession()
    {
        var operationOrder = new List<string>();
        var user = User.CreateWithPassword("race@example.com", "race", BCrypt.Net.BCrypt.HashPassword("Password1!"));
        var originalTokenVersion = user.TokenVersion;
        var sessionId = Guid.NewGuid();
        var existingSession = new UserSession { Id = sessionId, UserId = user.Id, IsActive = true };
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "current-hash",
            ExpiresAt = SystemClock.UtcNow.AddDays(5),
            IsRevoked = false,
            CreatedAt = SystemClock.UtcNow.AddMinutes(-2)
        };

        _refreshTokenHasherMock.Setup(x => x.HashToken("racing-token")).Returns("current-hash");
        _refreshTokenHasherMock.Setup(x => x.HashToken("replacement-token")).Returns("replacement-hash");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("current-hash", default)).ReturnsAsync(storedToken);
        _userRepoMock.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _sessionManagementServiceMock.Setup(x => x.GetSessionByRefreshTokenAsync("current-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSession);
        _sessionManagementServiceMock.Setup(x => x.RefreshSessionAsync(
                sessionId,
                "replacement-hash",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => operationOrder.Add("persist-session"))
            .ReturnsAsync(true);
        _refreshTokenRepoMock.Setup(x => x.TryRevokeForRotationAsync(
                storedToken.Id,
                "current-hash",
                "replacement-hash",
                It.IsAny<DateTime>(),
                "127.0.0.1",
                It.IsAny<CancellationToken>()))
            .Callback(() => operationOrder.Add("claim-rotation"))
            .ReturnsAsync(false);
        _tokenLineageRepoMock.Setup(x => x.RevokeFamilyAsync(user.Id, storedToken.Id, "127.0.0.1", It.IsAny<CancellationToken>()))
            .Callback(() => operationOrder.Add("resolve-legacy-family"))
            .ReturnsAsync((Guid?)null);
        _refreshTokenRepoMock.Setup(x => x.RevokeAllForUserAsync(user.Id, "127.0.0.1", It.IsAny<CancellationToken>()))
            .Callback(() => operationOrder.Add("revoke-refresh-tokens"))
            .Returns(Task.CompletedTask);
        _sessionManagementServiceMock.Setup(x => x.TerminateAllUserSessionsAsync(
                user.Id,
                SessionTerminationReason.SecurityViolation,
                null,
                It.IsAny<CancellationToken>()))
            .Callback(() => operationOrder.Add("terminate-sessions"))
            .ReturnsAsync(2);
        _userRepoMock.Setup(x => x.UpdateAsync(user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => operationOrder.Add("save-token-version"))
            .Returns(Task.CompletedTask);
        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(
                user.Id, user.Email, It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(),
                It.IsAny<DateTimeOffset>(), sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(
                user.Id, It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("replacement-token");

        AssertCommittedRefreshDenial(await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "racing-token" }));

        user.TokenVersion.Should().Be(originalTokenVersion + 1);
        operationOrder.Should().Equal(
            "persist-session",
            "claim-rotation",
            "resolve-legacy-family",
            "revoke-refresh-tokens",
            "terminate-sessions",
            "save-token-version");
        _tokenLineageRepoMock.Verify(x => x.RevokeFamilyAsync(user.Id, storedToken.Id, "127.0.0.1", It.IsAny<CancellationToken>()), Times.Once);
        _tokenLineageRepoMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshTokenAsync_LineageFailureOrCancellationCannotReturnIssuedTokens(bool cancelled)
    {
        var user = User.Create("lineage-failure@example.test", "Synthetic lineage account");
        var token = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, Token = "old-hash",
            ExpiresAt = SystemClock.UtcNow.AddHours(1), CreatedAt = SystemClock.UtcNow.AddMinutes(-1) };
        using var cancellation = new CancellationTokenSource();
        var order = new List<string>();
        _userRepoMock.Setup(repository => repository.GetByIdAsync(user.Id, cancellation.Token)).ReturnsAsync(user);
        _refreshTokenRepoMock.Setup(repository => repository.GetByTokenAsync("old-hash", cancellation.Token)).ReturnsAsync(token);
        _refreshTokenHasherMock.Setup(hasher => hasher.HashToken("old-token")).Returns("old-hash");
        _jwtTokenServiceMock.Setup(service => service.GenerateRefreshTokenAsync(user.Id, It.IsAny<DeviceInfo>(),
            It.IsAny<DateTimeOffset>(), cancellation.Token)).ReturnsAsync("replacement-token");
        _refreshTokenRepoMock.Setup(repository => repository.TryRevokeForRotationAsync(token.Id, "old-hash",
            "hash-replacement-token", It.IsAny<DateTime>(), "127.0.0.1", cancellation.Token))
            .Callback(() => order.Add("claim")).ReturnsAsync(true);
        Exception failure = cancelled ? new OperationCanceledException(cancellation.Token) : new InvalidOperationException("Synthetic lineage failure");
        _tokenLineageRepoMock.Setup(repository => repository.RecordRotationAsync(user.Id, token.Id,
            "hash-replacement-token", It.IsAny<Guid>(), cancellation.Token))
            .Callback(() => order.Add("lineage")).ThrowsAsync(failure);

        var actual = await Record.ExceptionAsync(() => _sut.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = "old-token" }, cancellation.Token));

        actual.Should().BeSameAs(failure);
        order.Should().Equal("claim", "lineage");
    }

    [Fact]
    public async Task RefreshTokenAsync_UnavailableUser_DeniesBeforeProvisioningIssuanceOrSessionMutation()
    {
        using var cancellation = new CancellationTokenSource();
        var userId = Guid.NewGuid();
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = userId, Token = "stored-hash",
            ExpiresAt = SystemClock.UtcNow.AddDays(1), CreatedAt = SystemClock.UtcNow.AddHours(-1)
        };
        _refreshTokenHasherMock.Setup(x => x.HashToken("previously-issued")).Returns("stored-hash");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("stored-hash", cancellation.Token)).ReturnsAsync(storedToken);
        _userRepoMock.Setup(x => x.GetByIdAsync(userId, cancellation.Token)).ReturnsAsync((User?)null);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "previously-issued" }, cancellation.Token));

        Assert.Equal("Invalid refresh token", exception.Message);
        Assert.False(storedToken.IsRevoked);
        Assert.Null(storedToken.ReplacedByToken);
        _refreshTokenRepoMock.Verify(x => x.GetByTokenAsync("stored-hash", cancellation.Token), Times.Once);
        _refreshTokenRepoMock.VerifyNoOtherCalls();
        _userRepoMock.Verify(x => x.GetByIdAsync(userId, cancellation.Token), Times.Once);
        _userRepoMock.VerifyNoOtherCalls();
        _senderMock.VerifyNoOtherCalls();
        _sessionManagementServiceMock.VerifyNoOtherCalls();
        _jwtTokenServiceMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("replaced")]
    public async Task RefreshTokenAsync_ReplayedTokenForUnavailableUser_StillCommitsContainmentWithoutIssuance(string kind)
    {
        var userId = Guid.NewGuid();
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = userId, Token = "hash-replay",
            IsRevoked = kind == "revoked", ReplacedByToken = kind == "replaced" ? "replacement-hash" : null,
            ExpiresAt = SystemClock.UtcNow.AddDays(1)
        };
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hash-replay", default)).ReturnsAsync(token);
        _userRepoMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        AssertCommittedRefreshDenial(await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "replay" }));

        _refreshTokenRepoMock.Verify(x => x.RevokeAllForUserAsync(userId, "127.0.0.1", It.IsAny<CancellationToken>()), Times.Once);
        _sessionManagementServiceMock.Verify(x => x.TerminateAllUserSessionsAsync(
            userId, SessionTerminationReason.SecurityViolation, null, It.IsAny<CancellationToken>()), Times.Once);
        _userRepoMock.Verify(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _userRepoMock.VerifyNoOtherCalls();
        _jwtTokenServiceMock.VerifyNoOtherCalls();
        _senderMock.VerifyNoOtherCalls();
    }

    // ── RevokeRefreshTokenAsync ───────────────────────────────

    private static void AssertCommittedRefreshDenial(SignInResponse response)
    {
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Invalid refresh token");
        response.AccessToken.Should().BeEmpty();
        response.RefreshToken.Should().BeEmpty();
        response.UserId.Should().Be(Guid.Empty);
        response.SessionId.Should().Be(Guid.Empty);
        response.Email.Should().BeEmpty();
        response.Should().BeAssignableTo<ICommitOnFailureOutcome>();
        CommandOutcome.IsFailure(response).Should().BeTrue();
        CommandOutcome.ShouldRollback(response).Should().BeFalse();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_TokenNotFound_ThrowsArgumentException()
    {
        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns("hashed");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hashed", default)).ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.RevokeRefreshTokenAsync("bad-token", "1.2.3.4"));
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_AlreadyRevoked_ThrowsArgumentException()
    {
        var token = new RefreshToken
        {
            Token = "hashed",
            UserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true,
            CreatedByIp = "127.0.0.1"
        };

        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns("hashed");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hashed", default)).ReturnsAsync(token);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.RevokeRefreshTokenAsync("token", "1.2.3.4"));
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_ValidToken_RevokesAndUpdates()
    {
        var token = new RefreshToken
        {
            Token = "hashed",
            UserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false,
            CreatedByIp = "127.0.0.1"
        };

        _refreshTokenHasherMock.Setup(x => x.HashToken("the-token")).Returns("hashed");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hashed", default)).ReturnsAsync(token);
        _refreshTokenRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RefreshToken>(), default)).ReturnsAsync(token);

        await _sut.RevokeRefreshTokenAsync("the-token", "1.2.3.4");

        token.IsRevoked.Should().BeTrue();
        token.RevokedByIp.Should().Be("1.2.3.4");
        _refreshTokenRepoMock.Verify(x => x.UpdateAsync(token, default), Times.Once);
    }
}

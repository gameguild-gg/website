using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
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
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock = new();
    private readonly Mock<IAuthAttemptService> _authAttemptServiceMock = new();
    private readonly Mock<IAuthenticationAnomalyDetectionService> _anomalyDetectionMock = new();
    private readonly Mock<IUserEnumerationProtectionService> _enumerationProtectionMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ISessionManagementService> _sessionManagementServiceMock = new();
    private readonly IConfiguration _configuration;
    private readonly LocalAuthService _sut;

    public LocalAuthServiceTests()
    {
        var configData = new Dictionary<string, string?>
        {
            { "Jwt:RefreshTokenExpiryInDays", "7" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "TestAgent/1.0";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
        _authAttemptServiceMock.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
        _enumerationProtectionMock.Setup(x => x.GetGenericErrorMessage(It.IsAny<string>())).Returns("Authentication failed");
        _enumerationProtectionMock.Setup(x => x.AddTimingProtectionDelayAsync(It.IsAny<bool>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns((string token) => $"hash-{token}");
        _publisherMock.Setup(x => x.Publish(It.IsAny<UserSignedUpNotification>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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
            _jwtTokenServiceMock.Object,
            _refreshTokenHasherMock.Object,
            _configuration,
            _authAttemptServiceMock.Object,
            _anomalyDetectionMock.Object,
            _enumerationProtectionMock.Object,
            _httpContextAccessorMock.Object,
            NullLogger<LocalAuthService>.Instance,
            _senderMock.Object,
            _sessionManagementServiceMock.Object
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
            _jwtTokenServiceMock.Object,
            _refreshTokenHasherMock.Object,
            customConfig,
            _authAttemptServiceMock.Object,
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
            x => x.RecordSuccessfulAttemptAsync("new@example.com", It.IsAny<Guid>(), "127.0.0.1", It.IsAny<string>(), It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [Fact]
    public async Task LocalSignUpAsync_RegistersDurableUserCreatedEvent()
    {
        User? persistedUser = null;
        _userRepoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => persistedUser = user)
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("at");
        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("rt");

        var request = new LocalSignUpRequest { Email = "new@example.com", Password = "P@ss1word", Username = "newuser" };

        await _sut.LocalSignUpAsync(request);

        persistedUser.Should().NotBeNull();
        persistedUser!.IntegrationEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserCreatedEvent>()
            .Which.UserId.Should().Be(persistedUser.Id);
    }

    // ── RefreshTokenAsync ─────────────────────────────────────

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
    public async Task RefreshTokenAsync_RevokedToken_ThrowsUnauthorizedAccessException()
    {
        var storedToken = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            Token = "hashed",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true,
            CreatedByIp = "127.0.0.1"
        };

        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns("hashed");
        _refreshTokenRepoMock.Setup(x => x.GetByTokenAsync("hashed", default)).ReturnsAsync(storedToken);

        var request = new RefreshTokenRequest { RefreshToken = "token" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.RefreshTokenAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_RecentlyRotatedTokenFromSameIp_ThrowsUnauthorizedAccessException()
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

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "rotated-token" }));
    }

    [Theory]
    [InlineData(-31, "127.0.0.1")]
    [InlineData(-5, "10.0.0.2")]
    public async Task RefreshTokenAsync_RotatedTokenOutsideGuardedRetry_ThrowsUnauthorizedAccessException(
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

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "rotated-token" }));
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
        var userId = Guid.NewGuid();
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
        var userId = Guid.NewGuid();
        var storedToken = new RefreshToken
        {
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
        _refreshTokenRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RefreshToken>(), default))
            .ReturnsAsync(storedToken);

        await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "token" });

        storedToken.IsRevoked.Should().BeTrue();
        storedToken.ReplacedByToken.Should().Be("new-hash");
        storedToken.ReplacedByToken.Should().NotBe("new-rt");
    }

    // ── RevokeRefreshTokenAsync ───────────────────────────────

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

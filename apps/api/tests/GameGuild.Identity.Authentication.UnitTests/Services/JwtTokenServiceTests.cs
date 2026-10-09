using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
/// Unit tests for JwtTokenService
/// </summary>
public class JwtTokenServiceTests
{
    private readonly Mock<ILogger<JwtTokenService>> _loggerMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<IOptions<JwtOptions>> _jwtOptionsMock;
    private readonly JwtTokenService _service;
    private readonly JwtOptions _jwtOptions;

    public JwtTokenServiceTests()
    {
        _loggerMock = new Mock<ILogger<JwtTokenService>>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _refreshTokenHasherMock = new Mock<IRefreshTokenHasher>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        
        _jwtOptions = new JwtOptions
        {
            SecretKey = "ThisIsAVerySecureSecretKeyForTestingPurposesOnly12345",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };

        _jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        _jwtOptionsMock.Setup(x => x.Value).Returns(_jwtOptions);

        _service = new JwtTokenService(
            _loggerMock.Object,
            _refreshTokenRepositoryMock.Object,
            _refreshTokenHasherMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptionsMock.Object
        );
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_WithValidData_ShouldReturnToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "test@example.com";
        var roles = new[] { "User", "Admin" };
        var tenantId = Guid.NewGuid();

        // Act
        var token = await _service.GenerateAccessTokenAsync(
            userId, 
            email, 
            roles, 
            tenantId, 
            1, // tokenVersion
            CancellationToken.None
        );

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3); // JWT has 3 parts
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var authenticatedAt = long.Parse(jwt.Claims.Single(claim => claim.Type == "auth_time").Value);
        DateTimeOffset.FromUnixTimeSeconds(authenticatedAt).Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_RolesUseApiRoleClaimContract()
    {
        var token = await _service.GenerateAccessTokenAsync(
            Guid.NewGuid(),
            "admin@example.com",
            ["SystemAdmin"],
            Guid.NewGuid(),
            1,
            CancellationToken.None);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var role = jwt.Claims.Single(claim => claim.Value == "SystemAdmin");

        role.Type.Should().Be("role");
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_IncludesActiveTenantGroupClaims()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var groupIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.Empty };
        var groupMembershipProvider = new Mock<IAuthorizationGroupMembershipProvider>();
        groupMembershipProvider
            .Setup(provider => provider.GetActiveGroupIdsAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(groupIds);
        var service = new JwtTokenService(
            _loggerMock.Object,
            _refreshTokenRepositoryMock.Object,
            _refreshTokenHasherMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptionsMock.Object,
            groupMembershipProvider.Object,
            Options.Create(new AuthorizationTokenOptions { GroupIdClaimType = "group_id" }));

        var token = await service.GenerateAccessTokenAsync(userId, "member@example.com", ["User"], tenantId, 1);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Where(claim => claim.Type == "group_id").Select(claim => claim.Value)
            .Should().BeEquivalentTo(groupIds.Where(id => id != Guid.Empty).Select(id => id.ToString("D")));
        groupMembershipProvider.Verify(
            provider => provider.GetActiveGroupIdsAsync(userId, tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_WithAuthenticationTime_ShouldPreserveAuthenticationTime()
    {
        var authenticatedAt = DateTimeOffset.UtcNow.AddMinutes(-12);

        var token = await _service.GenerateAccessTokenAsync(
            Guid.NewGuid(),
            "test@example.com",
            ["User"],
            Guid.NewGuid(),
            1,
            authenticatedAt,
            CancellationToken.None);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var claim = long.Parse(jwt.Claims.Single(candidate => candidate.Type == "auth_time").Value);
        DateTimeOffset.FromUnixTimeSeconds(claim).Should().Be(authenticatedAt.AddTicks(-(authenticatedAt.Ticks % TimeSpan.TicksPerSecond)));
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_WithAuthenticationTimeAndSession_ShouldPreserveBothClaims()
    {
        var authenticatedAt = DateTimeOffset.UtcNow.AddMinutes(-4);
        var sessionId = Guid.NewGuid();

        var token = await _service.GenerateAccessTokenAsync(
            Guid.NewGuid(),
            "test@example.com",
            ["User"],
            Guid.NewGuid(),
            2,
            authenticatedAt,
            sessionId,
            CancellationToken.None);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Single(candidate => candidate.Type == "session_id").Value.Should().Be(sessionId.ToString());
        var claim = long.Parse(jwt.Claims.Single(candidate => candidate.Type == "auth_time").Value);
        DateTimeOffset.FromUnixTimeSeconds(claim)
            .Should().Be(authenticatedAt.AddTicks(-(authenticatedAt.Ticks % TimeSpan.TicksPerSecond)));
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_WithoutTenantId_ShouldReturnToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "test@example.com";
        var roles = new[] { "User" };

        // Act
        var token = await _service.GenerateAccessTokenAsync(
            userId, 
            email, 
            roles, 
            null, 
            1, // tokenVersion
            CancellationToken.None
        );

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_WithNullRoles_ShouldThrowArgumentNullException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "test@example.com";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GenerateAccessTokenAsync(userId, email, null!, null, 1, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_WithValidData_ShouldReturnToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceInfo = new DeviceInfo
        {
            Fingerprint = "device-123",
            DeviceName = "Test Device",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        // The service hashes the token before storing, so set up the hasher mock
        _refreshTokenHasherMock
            .Setup(x => x.HashToken(It.IsAny<string>()))
            .Returns((string t) => $"hashed_{t}");

        _refreshTokenRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken rt, CancellationToken _) => rt);

        // Act
        var token = await _service.GenerateRefreshTokenAsync(
            userId, 
            deviceInfo, 
            CancellationToken.None
        );

        // Assert
        token.Should().NotBeNullOrEmpty();
        
        // Token stored in DB is the HASH, not the plaintext token
        _refreshTokenRepositoryMock.Verify(
            x => x.CreateAsync(It.Is<RefreshToken>(rt => 
                rt.UserId == userId && 
                !rt.IsRevoked), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Theory]
    [InlineData("device-123", "device-123")]
    [InlineData("Device-AbC", "Device-AbC")]
    [InlineData("device-秘密", "device-秘密")]
    [InlineData("device-{UserId}", "device-{UserId}")]
    [InlineData("", "")]
    [InlineData("device\r\nFORGED EVENT", "device␀␀FORGED EVENT")]
    [InlineData("device\rFORGED EVENT", "device␀FORGED EVENT")]
    [InlineData("device\nFORGED EVENT", "device␀FORGED EVENT")]
    [InlineData("device\u0000\u001FFORGED EVENT", "device␀␀FORGED EVENT")]
    [InlineData("device\u007F\u0085\u009FFORGED EVENT", "device␀␀␀FORGED EVENT")]
    [InlineData("device\u2028\u2029FORGED EVENT", "device␀␀FORGED EVENT")]
    [InlineData("device\u001B[2J\nFORGED EVENT", "device␀[2J␀FORGED EVENT")]
    public async Task GenerateRefreshTokenAsync_ShouldSanitizeOnlyTheLoggedDeviceId(string fingerprint, string expectedLoggedId)
    {
        var userId = Guid.NewGuid();
        var deviceInfo = new DeviceInfo { Fingerprint = fingerprint, IpAddress = "127.0.0.1" };
        const string storedHash = "hashed-refresh-token";
        _refreshTokenHasherMock.Setup(hasher => hasher.HashToken(It.IsAny<string>())).Returns(storedHash);
        _refreshTokenRepositoryMock
            .Setup(repository => repository.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken token, CancellationToken _) => token);

        var rawToken = await _service.GenerateRefreshTokenAsync(userId, deviceInfo, CancellationToken.None);

        const string template = "Generating refresh token for user: {UserId}, Device: {DeviceId}";
        var log = _loggerMock.Invocations.Single(invocation =>
            invocation.Method.Name == nameof(ILogger.Log) &&
            invocation.Arguments[2] is IEnumerable<KeyValuePair<string, object?>> properties &&
            properties.Any(property => property.Key == "{OriginalFormat}" && Equals(property.Value, template)));
        var loggedProperties = (IEnumerable<KeyValuePair<string, object?>>)log.Arguments[2];
        log.Arguments[0].Should().Be(LogLevel.Information);
        log.Arguments[3].Should().BeNull();
        loggedProperties.Single(property => property.Key == "UserId").Value.Should().Be(userId);
        loggedProperties.Single(property => property.Key == "DeviceId").Value.Should().Be(expectedLoggedId);
        log.Arguments[2].ToString().Should().Be($"Generating refresh token for user: {userId}, Device: {expectedLoggedId}");
        deviceInfo.Fingerprint.Should().Be(fingerprint);
        deviceInfo.DeviceId.Should().Be(fingerprint);
        Convert.FromBase64String(rawToken).Should().NotBeEmpty();
        rawToken.Should().NotBe(storedHash);
        _refreshTokenHasherMock.Verify(hasher => hasher.HashToken(rawToken), Times.Once);
        _refreshTokenRepositoryMock.Verify(repository => repository.CreateAsync(
            It.Is<RefreshToken>(token => token.UserId == userId && token.Token == storedHash && !token.IsRevoked),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_WithNullDeviceInfo_ShouldThrowArgumentNullException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GenerateRefreshTokenAsync(userId, null!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_ShouldSetCorrectExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceInfo = new DeviceInfo
        {
            Fingerprint = "device-123",
            DeviceName = "Test Device",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        RefreshToken? capturedToken = null;

        _refreshTokenRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((token, _) => capturedToken = token)
            .ReturnsAsync((RefreshToken rt, CancellationToken _) => rt);

        // Act
        await _service.GenerateRefreshTokenAsync(userId, deviceInfo, CancellationToken.None);

        // Assert
        capturedToken.Should().NotBeNull();
        capturedToken!.ExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays), 
            TimeSpan.FromMinutes(1)
        );
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_WithAuthenticationTime_ShouldPreserveSessionStart()
    {
        var userId = Guid.NewGuid();
        var authenticatedAt = DateTimeOffset.UtcNow.AddHours(-3);
        var deviceInfo = new DeviceInfo { Fingerprint = "device-123", IpAddress = "127.0.0.1" };
        RefreshToken? capturedToken = null;
        _refreshTokenHasherMock.Setup(x => x.HashToken(It.IsAny<string>())).Returns("hash");
        _refreshTokenRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((token, _) => capturedToken = token)
            .ReturnsAsync((RefreshToken token, CancellationToken _) => token);

        await _service.GenerateRefreshTokenAsync(userId, deviceInfo, authenticatedAt, CancellationToken.None);

        capturedToken.Should().NotBeNull();
        capturedToken!.CreatedAt.Should().Be(authenticatedAt.UtcDateTime);
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_ShouldGenerateUniqueTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceInfo = new DeviceInfo
        {
            Fingerprint = "device-123",
            DeviceName = "Test Device",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        _refreshTokenRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken rt, CancellationToken _) => rt);

        // Act
        var token1 = await _service.GenerateRefreshTokenAsync(userId, deviceInfo, CancellationToken.None);
        var token2 = await _service.GenerateRefreshTokenAsync(userId, deviceInfo, CancellationToken.None);

        // Assert
        token1.Should().NotBe(token2);
    }
}

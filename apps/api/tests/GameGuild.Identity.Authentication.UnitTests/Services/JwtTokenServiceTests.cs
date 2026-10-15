using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
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
            SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
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

    [Theory]
    [InlineData("message")]
    [InlineData("inner")]
    [InlineData("data")]
    public async Task GenerateAccessTokenAsync_LogPrivacy_GroupLookupFailurePreservesException(string channel)
    {
        var email = $"private-{Guid.NewGuid():N}@example.test";
        var privateDetail = Guid.NewGuid().ToString("N");
        var failure = PrivateFailure(channel, email, privateDetail);
        var groups = new Mock<IAuthorizationGroupMembershipProvider>();
        groups.Setup(provider => provider.GetActiveGroupIdsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var service = new JwtTokenService(_loggerMock.Object, _refreshTokenRepositoryMock.Object,
            _refreshTokenHasherMock.Object, _httpContextAccessorMock.Object, _jwtOptionsMock.Object,
            groups.Object);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAccessTokenAsync(Guid.NewGuid(), email, ["Member"], Guid.NewGuid()));

        result.Should().BeSameAs(failure);
        AssertPrivateLogs(failure.GetType().FullName!, email, privateDetail);
    }

    [Theory]
    [InlineData("message")]
    [InlineData("inner")]
    [InlineData("data")]
    public async Task GenerateRefreshTokenAsync_LogPrivacy_RepositoryFailurePreservesException(string channel)
    {
        var privateDetail = Guid.NewGuid().ToString("N");
        var failure = PrivateFailure(channel, privateDetail);
        _refreshTokenHasherMock.Setup(hasher => hasher.HashToken(It.IsAny<string>()))
            .Returns(Guid.NewGuid().ToString("N"));
        _refreshTokenRepositoryMock.Setup(repository => repository.CreateAsync(
                It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GenerateRefreshTokenAsync(Guid.NewGuid(), new DeviceInfo(), CancellationToken.None));

        result.Should().BeSameAs(failure);
        _refreshTokenRepositoryMock.Verify(repository => repository.CreateAsync(
            It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        AssertPrivateLogs(failure.GetType().FullName!, privateDetail);
    }

    [Theory]
    [InlineData("message")]
    [InlineData("inner")]
    [InlineData("data")]
    public async Task RevokeRefreshTokenAsync_LogPrivacy_RepositoryFailureReturnsFalse(string channel)
    {
        var privateValue = Guid.NewGuid().ToString("N");
        var failure = PrivateFailure(channel, privateValue);
        var digest = Guid.NewGuid().ToString("N");
        _refreshTokenHasherMock.Setup(hasher => hasher.HashToken(privateValue)).Returns(digest);
        _refreshTokenRepositoryMock.Setup(repository => repository.GetByTokenAsync(
                digest, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var result = await _service.RevokeRefreshTokenAsync(privateValue);

        result.Should().BeFalse();
        _refreshTokenRepositoryMock.Verify(repository => repository.UpdateAsync(
            It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        AssertPrivateLogs(failure.GetType().FullName!, privateValue);
    }

    [Theory]
    [InlineData("message")]
    [InlineData("inner")]
    [InlineData("data")]
    public async Task GenerateServiceAccountTokenAsync_LogPrivacy_ScopeFailurePreservesException(string channel)
    {
        var serviceId = Guid.NewGuid().ToString("N");
        var clientId = Guid.NewGuid().ToString("N");
        var privateDetail = Guid.NewGuid().ToString("N");
        var failure = PrivateFailure(channel, privateDetail);
        var scopes = new Mock<IReadOnlySet<string>>();
        scopes.Setup(value => value.GetEnumerator()).Throws(failure);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GenerateServiceAccountTokenAsync(serviceId, clientId, "Private service",
                scopes.Object, Guid.NewGuid()));

        result.Should().BeSameAs(failure);
        AssertPrivateLogs(failure.GetType().FullName!, serviceId, clientId, privateDetail);
        var information = _loggerMock.Invocations.Should().ContainSingle(call =>
            call.Method.Name == nameof(ILogger.Log) && Equals(call.Arguments[0], LogLevel.Information)).Subject;
        information.Arguments[2].ToString().Should().Contain(LogRedaction.RedactId(serviceId, "said"))
            .And.Contain(LogRedaction.RedactId(clientId, "cid"));
    }

    [Fact]
    public async Task ValidateTokenAsync_LogPrivacy_MalformedTokenStillReturnsFalse()
    {
        var privateValue = $"private-{Guid.NewGuid():N}";

        var result = await _service.ValidateTokenAsync(privateValue);

        result.Should().BeFalse();
        AssertPrivateLogs(null, privateValue);
    }

    [Fact]
    public async Task ValidateTokenAsync_LogPrivacy_InvalidSignatureStillReturnsFalse()
    {
        var privateEmail = $"private-{Guid.NewGuid():N}@example.test";
        var signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: _jwtOptions.Issuer, audience: _jwtOptions.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Email, privateEmail)],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)));

        var result = await _service.ValidateTokenAsync(token);

        result.Should().BeFalse();
        AssertPrivateLogs(null, privateEmail, token, signingKey);
    }

    [Fact]
    public async Task ValidateTokenAsync_LogPrivacy_ExpiredTokenStillReturnsFalse()
    {
        _jwtOptions.ValidateLifetime = true;
        var privateEmail = $"private-{Guid.NewGuid():N}@example.test";
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: _jwtOptions.Issuer, audience: _jwtOptions.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Email, privateEmail)],
            notBefore: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)),
                SecurityAlgorithms.HmacSha256)));

        var result = await _service.ValidateTokenAsync(token);

        result.Should().BeFalse();
        AssertPrivateLogs(typeof(SecurityTokenExpiredException).FullName!, privateEmail, token);
    }

    private static InvalidOperationException PrivateFailure(string channel, params string[] privateValues)
    {
        var payload = string.Join("; ", privateValues);
        var failure = new InvalidOperationException(
            channel == "message" ? payload : "Dependency unavailable",
            channel == "inner" ? new Exception(payload) : null);
        if (channel == "data") { failure.Data["detail"] = payload; }
        return failure;
    }

    private void AssertPrivateLogs(string? expectedErrorType, params string[] privateValues)
    {
        var failures = new List<Moq.IInvocation>();
        foreach (var entry in _loggerMock.Invocations)
        {
            if (entry.Method.Name != nameof(ILogger.Log)) { continue; }
            entry.Arguments[3].Should().BeNull("exception objects can contain private payloads");
            var fields = entry.Arguments[2].Should().BeAssignableTo<IEnumerable<KeyValuePair<string, object?>>>().Subject;
            foreach (var privateValue in privateValues)
            {
                entry.Arguments[2].ToString().Should().NotContain(privateValue);
                foreach (var field in fields)
                {
                    (field.Value?.ToString() ?? string.Empty).Should().NotContain(privateValue);
                }
            }
            if (Equals(entry.Arguments[0], LogLevel.Error) || Equals(entry.Arguments[0], LogLevel.Warning))
            {
                failures.Add(entry);
            }
        }
        var failure = failures.Should().ContainSingle().Subject;
        var failureFields = failure.Arguments[2].Should().BeAssignableTo<IEnumerable<KeyValuePair<string, object?>>>().Subject;
        failureFields.Should().Contain(pair => pair.Key == "ErrorType" && pair.Value is string);
        if (expectedErrorType is not null)
        {
            failureFields.Should().Contain(pair => pair.Key == "ErrorType" && Equals(pair.Value, expectedErrorType));
        }
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

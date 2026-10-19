using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class JwtTokenLoggingTests
{
    private readonly Mock<ILogger<JwtTokenService>> _logger = new();
    private readonly Mock<IRefreshTokenRepository> _repository = new();
    private readonly Mock<IRefreshTokenHasher> _hasher = new();
    private readonly JwtOptions _options = new()
    {
        SecretKey = "ThisIsAVerySecureSecretKeyForTestingPurposesOnly12345",
        Issuer = "LoggingTests",
        Audience = "LoggingTests",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    private JwtTokenService CreateService() => new(
        _logger.Object,
        _repository.Object,
        _hasher.Object,
        new HttpContextAccessor(),
        Options.Create(_options));

    [Fact]
    public async Task AccessToken_PreservesClaimsWithoutLoggingPrivateIdentifiers()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        const string email = "private-account@example.test";

        var token = await CreateService().GenerateAccessTokenAsync(userId, email, ["User"], tenantId);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(userId.ToString(), parsed.Subject);
        Assert.Equal(email, parsed.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(tenantId.ToString(), parsed.Claims.Single(claim => claim.Type == "tenant_id").Value);
        Assert.Equal("User", parsed.Claims.Single(claim => claim.Type == "role").Value);
        AssertPrivateLogs(2, email, token);
    }

    [Theory]
    [InlineData("private-device")]
    [InlineData("private-device\r\nFORGED-ENTRY")]
    public async Task RefreshToken_PreservesPersistenceWithoutLoggingDeviceInput(string fingerprint)
    {
        var userId = Guid.NewGuid();
        var authenticatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiresAt = DateTime.UtcNow.AddDays(2);
        using var cancellation = new CancellationTokenSource();
        RefreshToken? persisted = null;
        string? hashedInput = null;
        _hasher.Setup(hasher => hasher.HashToken(It.IsAny<string>()))
            .Callback<string>(value => hashedInput = value)
            .Returns("stored-hash");
        _repository.Setup(repository => repository.CreateAsync(It.IsAny<RefreshToken>(), cancellation.Token))
            .Callback<RefreshToken, CancellationToken>((row, _) => persisted = row)
            .ReturnsAsync((RefreshToken row, CancellationToken _) => row);

        var token = await CreateService().GenerateRefreshTokenAsync(
            userId, new DeviceInfo { Fingerprint = fingerprint }, authenticatedAt, expiresAt, cancellation.Token);

        Assert.NotNull(persisted);
        Assert.Equal(userId, persisted.UserId);
        Assert.Equal("stored-hash", persisted.Token);
        Assert.Equal(authenticatedAt.UtcDateTime, persisted.CreatedAt);
        Assert.Equal(expiresAt, persisted.ExpiresAt);
        Assert.Equal(token, hashedInput);
        Assert.NotEqual(token, persisted.Token);
        _repository.Verify(repository => repository.CreateAsync(It.IsAny<RefreshToken>(), cancellation.Token), Times.Once);
        AssertPrivateLogs(2, fingerprint, "FORGED-ENTRY", token);
    }

    [Fact]
    public async Task AccessToken_PreservesSigningFailureWithoutLoggingPrivateIdentifiers()
    {
        _options.SecretKey = string.Empty;
        var userId = Guid.NewGuid();
        const string email = "private-account@example.test";

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().GenerateAccessTokenAsync(userId, email, ["User"], null));

        AssertPrivateLogs(2, email);
    }

    [Fact]
    public async Task RefreshToken_RetriesDuplicateFailuresWithoutLoggingExceptionDetails()
    {
        const string detail = "duplicate key value: private-account@example.test\r\ncredential-marker";
        var userId = Guid.NewGuid();
        _hasher.Setup(hasher => hasher.HashToken(It.IsAny<string>())).Returns("stored-hash");
        _repository.SetupSequence(repository => repository.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(detail))
            .ThrowsAsync(new InvalidOperationException(detail))
            .ReturnsAsync(new RefreshToken());

        var token = await CreateService().GenerateRefreshTokenAsync(userId, new DeviceInfo { Fingerprint = detail });

        Assert.NotEmpty(token);
        _repository.Verify(repository => repository.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        AssertPrivateLogs(4, detail, "credential-marker", token);
    }

    [Fact]
    public async Task RefreshToken_PropagatesFailureWithoutLoggingPrivateExceptionDetails()
    {
        var userId = Guid.NewGuid();
        const string detail = "private-account@example.test\r\ncredential-marker";
        var failure = new InvalidOperationException(detail);
        _hasher.Setup(hasher => hasher.HashToken(It.IsAny<string>())).Returns("stored-hash");
        _repository.Setup(repository => repository.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().GenerateRefreshTokenAsync(userId, new DeviceInfo { Fingerprint = detail }));

        Assert.Same(failure, actual);
        AssertPrivateLogs(2, detail, "credential-marker");
        Assert.Contains(_logger.Invocations, call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0] == LogLevel.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokeRefreshToken_PreservesResultWithoutLoggingStoredIdentifiers(bool alreadyRevoked)
    {
        var row = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "stored-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = alreadyRevoked
        };
        const string token = "private-token\r\nFORGED-ENTRY";
        _hasher.Setup(hasher => hasher.HashToken(token)).Returns("stored-hash");
        _repository.Setup(repository => repository.GetByTokenAsync("stored-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        _repository.Setup(repository => repository.UpdateAsync(row, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        Assert.True(await CreateService().RevokeRefreshTokenAsync(token));

        Assert.True(row.IsRevoked);
        _repository.Verify(repository => repository.UpdateAsync(row, It.IsAny<CancellationToken>()),
            alreadyRevoked ? Times.Never() : Times.Once());
        AssertPrivateLogs(2, token);
    }

    [Fact]
    public async Task RevokeRefreshToken_ReturnsFailureWithoutLoggingRepositoryDetails()
    {
        const string detail = "private-account@example.test\r\ncredential-marker";
        _hasher.Setup(hasher => hasher.HashToken(It.IsAny<string>())).Returns("stored-hash");
        _repository.Setup(repository => repository.GetByTokenAsync("stored-hash", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(detail));

        Assert.False(await CreateService().RevokeRefreshTokenAsync(detail));

        AssertPrivateLogs(2, detail, "credential-marker");
    }

    [Fact]
    public async Task ServiceAccountToken_PreservesClaimsWithoutLoggingCallerIdentifiers()
    {
        const string accountId = "private-service\r\nFORGED-ENTRY";
        const string clientId = "private-client\r\nFORGED-ENTRY";
        const string serviceName = "private-service-name";
        var tenantId = Guid.NewGuid();

        var result = await CreateService().GenerateServiceAccountTokenAsync(
            accountId, clientId, serviceName, new HashSet<string> { "read" }, tenantId);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(accountId, parsed.Subject);
        Assert.Equal(clientId, parsed.Claims.Single(claim => claim.Type == "client_id").Value);
        Assert.Equal(serviceName, parsed.Claims.Single(claim => claim.Type == "service_name").Value);
        Assert.Equal("read", parsed.Claims.Single(claim => claim.Type == "scope").Value);
        Assert.Equal(tenantId.ToString(), parsed.Claims.Single(claim => claim.Type == "tenant_id").Value);
        AssertPrivateLogs(2, accountId, clientId, serviceName, result.Token);
    }

    [Fact]
    public async Task ServiceAccountToken_PreservesSigningFailureWithoutLoggingCallerIdentifiers()
    {
        _options.SecretKey = string.Empty;
        const string accountId = "private-service\r\nFORGED-ENTRY";
        const string clientId = "private-client\r\nFORGED-ENTRY";

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().GenerateServiceAccountTokenAsync(
            accountId, clientId, "private-service-name", new HashSet<string>(), null));

        AssertPrivateLogs(2, accountId, clientId, "FORGED-ENTRY");
    }

    [Fact]
    public async Task ValidationFailure_DoesNotLogTokenOrExceptionDetails()
    {
        const string token = "private-account@example.test\r\ncredential-marker";

        Assert.False(await CreateService().ValidateTokenAsync(token));

        AssertPrivateLogs(1, token, "credential-marker");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedSignedToken_DoesNotLogPrivateClaimsOrValidationDetails(bool invalidIssuer)
    {
        _options.ValidateIssuer = true;
        _options.ValidateLifetime = true;
        _options.ClockSkewSeconds = 0;
        const string privateValue = "private-account@example.test";
        var now = DateTime.UtcNow;
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: invalidIssuer ? privateValue : _options.Issuer,
            audience: _options.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Email, privateValue)],
            notBefore: now.AddHours(-1),
            expires: invalidIssuer ? now.AddMinutes(5) : now.AddMinutes(-5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)), SecurityAlgorithms.HmacSha256)));

        Assert.False(await CreateService().ValidateTokenAsync(token));

        AssertPrivateLogs(1, token, privateValue);
        Assert.Contains(_logger.Invocations, call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0] == LogLevel.Warning);
    }

    [Fact]
    public async Task PayloadFailure_DoesNotLogMalformedToken()
    {
        const string token = "private-account@example.test\r\ncredential-marker";

        Assert.Null(await CreateService().GetTokenPayloadAsync(token));

        AssertPrivateLogs(1, token, "credential-marker");
    }

    private void AssertPrivateLogs(int expectedCount, params string[] forbiddenValues)
    {
        var calls = _logger.Invocations.Where(call => call.Method.Name == "Log").ToArray();
        Assert.Equal(expectedCount, calls.Length);
        foreach (var call in calls)
        {
            Assert.Null(call.Arguments[3]);
            var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(call.Arguments[2]);
            var text = call.Arguments[2].ToString() ?? string.Empty;
            Assert.All(state.Where(entry => entry.Key != "{OriginalFormat}"),
                entry => Assert.True(entry.Value is DateTime or int or Guid or string));
            foreach (var forbidden in forbiddenValues)
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
                Assert.All(state, entry => Assert.DoesNotContain(forbidden, entry.Value?.ToString() ?? string.Empty, StringComparison.Ordinal));
            }
        }
    }
}

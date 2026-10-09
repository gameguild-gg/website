using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class Web3AuthServiceTests
{
    private readonly Web3IdentityTestHarness _identity = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IWeb3Service> _web3ServiceMock = new();
    private readonly Mock<IAuthAttemptService> _authAttemptServiceMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();

    private readonly Web3AuthService _sut;

    public Web3AuthServiceTests()
    {
        var configData = new Dictionary<string, string?>
        {
            { "Jwt:RefreshTokenExpirationDays", "7" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "TestAgent/1.0";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
        _authAttemptServiceMock.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("jwt-access-token");
        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("jwt-refresh-token");
        _web3ServiceMock
            .Setup(x => x.VerifySignatureAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(true);

        _sut = new Web3AuthService(
            _identity.Users.Object,
            _identity.Links.Object,
            _jwtTokenServiceMock.Object,
            _identity.Hashes.Object,
            _identity.Sessions.Object,
            _identity.Sender.Object,
            _web3ServiceMock.Object,
            configuration,
            _authAttemptServiceMock.Object,
            _httpContextAccessorMock.Object,
            NullLogger<Web3AuthService>.Instance);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("owner")]
    [InlineData("session")]
    [InlineData("hash")]
    [InlineData("expired")]
    [InlineData("inactive")]
    [InlineData("longer")]
    [InlineData("terminated")]
    public async Task InvalidPersistedSessionCannotReturnWalletCredentials(string invalid)
    {
        _identity.Sessions.Setup(value => value.CreateSessionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid owner, string _, string _, string hash, DateTime expires,
                string? _, CancellationToken _) => invalid == "null" ? null! : new UserSession
            {
                Id = invalid == "session" ? Guid.NewGuid() : id,
                UserId = invalid == "owner" ? Guid.NewGuid() : owner,
                RefreshToken = invalid == "hash" ? "unrelated-hash" : hash,
                IsActive = invalid != "inactive",
                ExpiresAt = invalid == "expired" ? SystemClock.UtcNow.AddMinutes(-1)
                    : invalid == "longer" ? expires.AddMinutes(1) : expires,
                TerminatedAt = invalid == "terminated" ? SystemClock.UtcNow : null
            });
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.VerifyWeb3SignatureAsync(
            new Web3VerificationRequest { WalletAddress = "0xsynthetic", Signature = "synthetic-signature", Challenge = "synthetic-challenge" }));
        _authAttemptServiceMock.Verify(value => value.RecordSuccessfulAttemptAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<TimeSpan>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WalletCredentialsReportTheShorterPersistedSessionDeadline()
    {
        var deadline = SystemClock.UtcNow.AddMinutes(5);
        _identity.Sessions.Setup(value => value.CreateSessionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid owner, string _, string _, string hash, DateTime _,
                string? _, CancellationToken _) => new UserSession
            { Id = id, UserId = owner, RefreshToken = hash, IsActive = true, ExpiresAt = deadline });
        var result = await _sut.VerifyWeb3SignatureAsync(new Web3VerificationRequest
            { WalletAddress = "0xsynthetic", Signature = "synthetic-signature", Challenge = "synthetic-challenge" });
        Assert.Equal(deadline, result.RefreshTokenExpiresAt);
        Assert.Equal(deadline, result.ExpiresAt);
    }

    [Fact]
    public async Task VerifyWeb3SignatureAsync_ShouldReturnAccessTokenLifetime_InExpiresIn()
    {
        var request = new Web3VerificationRequest
        {
            WalletAddress = "0xabc123",
            Signature = "0xsignature",
            Challenge = "challenge-nonce"
        };

        var before = SystemClock.UtcNow;
        var result = await _sut.VerifyWeb3SignatureAsync(request);
        var after = SystemClock.UtcNow;

        // ponytail: Web3 sign-in must report access-token lifetime, not refresh-token lifetime
        result.ExpiresIn.Should().Be(3600, "default AccessTokenExpirationMinutes is 60");
        result.AccessTokenExpiresAt.Should().BeOnOrAfter(before.AddMinutes(59));
        result.AccessTokenExpiresAt.Should().BeOnOrBefore(after.AddMinutes(61));
    }

    [Fact]
    public async Task VerifyWeb3SignatureAsync_CustomAccessTokenExpiration_ParsedFromConfig()
    {
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:RefreshTokenExpirationDays", "7" },
                { "Jwt:AccessTokenExpirationMinutes", "10" }
            })
            .Build();

        var sut = new Web3AuthService(
            _identity.Users.Object,
            _identity.Links.Object,
            _jwtTokenServiceMock.Object,
            _identity.Hashes.Object,
            _identity.Sessions.Object,
            _identity.Sender.Object,
            _web3ServiceMock.Object,
            customConfig,
            _authAttemptServiceMock.Object,
            _httpContextAccessorMock.Object,
            NullLogger<Web3AuthService>.Instance);

        var request = new Web3VerificationRequest
        {
            WalletAddress = "0xabc123",
            Signature = "0xsignature",
            Challenge = "challenge-nonce"
        };

        var result = await sut.VerifyWeb3SignatureAsync(request);

        result.ExpiresIn.Should().Be(600, "10 minutes * 60 seconds");
        result.AccessTokenExpiresAt.Should().BeOnOrAfter(SystemClock.UtcNow.AddMinutes(9));
        result.AccessTokenExpiresAt.Should().BeOnOrBefore(SystemClock.UtcNow.AddMinutes(11));
    }

    [Fact]
    public async Task VerifiedWalletUsesStoredIdentityVersionTenantAndActualRequestedSessionBinding()
    {
        using var cancellation = new CancellationTokenSource();
        var result = await _sut.VerifyWeb3SignatureAsync(new Web3VerificationRequest
        {
            WalletAddress = "synthetic-verified-wallet", Signature = "synthetic-signature", Challenge = "synthetic-challenge",
            DeviceFingerprint = "synthetic-device", TenantId = _identity.TenantId
        }, cancellation.Token);
        result.UserId.Should().Be(_identity.User.Id);
        result.Email.Should().Be(_identity.User.Email);
        result.TenantId.Should().Be(_identity.TenantId);
        result.SessionId.Should().NotBeEmpty();
        _jwtTokenServiceMock.Verify(value => value.GenerateAccessTokenAsync(_identity.User.Id, _identity.User.Email,
            It.Is<string[]>(roles => roles.Contains("Member") && roles.Contains("User")), _identity.TenantId,
            _identity.User.TokenVersion, result.SessionId, cancellation.Token), Times.Once);
        _identity.Sessions.Verify(value => value.CreateSessionAsync(result.SessionId, _identity.User.Id,
            "127.0.0.1", "TestAgent/1.0", "synthetic-hash-jwt-refresh-token", It.IsAny<DateTime>(), "synthetic-device", cancellation.Token), Times.Once);
        _jwtTokenServiceMock.Verify(value => value.GenerateRefreshTokenAsync(_identity.User.Id,
            It.Is<DeviceInfo>(device => device.Fingerprint == "synthetic-device"), cancellation.Token), Times.Once);
        _identity.Users.Verify(value => value.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _identity.Links.Verify(value => value.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidSignatureCannotLookUpIdentityOrIssueCredentials()
    {
        _web3ServiceMock.Setup(value => value.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(false);
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => _sut.VerifyWeb3SignatureAsync(new Web3VerificationRequest()));
        _identity.Links.Verify(value => value.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _jwtTokenServiceMock.Verify(value => value.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeOrAfterVerificationCannotPersistOrIssue(bool afterVerification)
    {
        using var cancellation = new CancellationTokenSource();
        if (afterVerification)
        {
            _web3ServiceMock.Setup(value => value.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Callback(() => cancellation.Cancel()).ReturnsAsync(true);
        }
        else
        {
            cancellation.Cancel();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _sut.VerifyWeb3SignatureAsync(new Web3VerificationRequest(), cancellation.Token));
        _identity.Links.Verify(value => value.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _jwtTokenServiceMock.Verify(value => value.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequiredSessionFailureCannotReturnSuccessOrRecordSuccessfulAttempt()
    {
        _identity.Sessions.Setup(value => value.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Synthetic binding failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.VerifyWeb3SignatureAsync(new Web3VerificationRequest()));
        _authAttemptServiceMock.Verify(value => value.RecordSuccessfulAttemptAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<TimeSpan>(), "Web3"), Times.Never);
    }
}

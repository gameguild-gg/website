using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authentication.UnitTests.Infrastructure;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Remember-me / persistent session coverage (issue #265):
///     - the single policy-aware lifetime resolver (standard vs persistent);
///     - every issuing path (local/polymorphic funnels into local, OAuth, Web3) honors the flag;
///     - refresh rotation renews the session's originating duration instead of collapsing onto the standard one;
///     - logout revokes persistent refresh tokens.
/// </summary>
public class RememberMeSessionLifetimeTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(30);

    // ── RefreshTokenLifetimeResolver ─────────────────────────

    [Fact]
    public void Resolver_WithoutOptionsOrConfig_UsesStandardDefaultOfSevenDays()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        RefreshTokenLifetimeResolver.ResolveExpirationDays(null, configuration, persistent: false)
            .Should().Be(7);
    }

    [Fact]
    public void Resolver_WithoutOptionsOrConfig_UsesPersistentDefaultOfThirtyDays()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        RefreshTokenLifetimeResolver.ResolveExpirationDays(null, configuration, persistent: true)
            .Should().Be(30);
    }

    [Fact]
    public void Resolver_StandardHonorsLegacyConfigurationKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Jwt:RefreshTokenExpiryInDays", "9" } })
            .Build();

        RefreshTokenLifetimeResolver.ResolveExpirationDays(null, configuration, persistent: false)
            .Should().Be(9);
    }

    [Fact]
    public void Resolver_PersistentHonorsPersistentConfigurationKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:RefreshTokenExpirationDays", "7" },
                { "Jwt:PersistentRefreshTokenExpirationDays", "45" }
            })
            .Build();

        RefreshTokenLifetimeResolver.ResolveExpirationDays(null, configuration, persistent: false)
            .Should().Be(7);
        RefreshTokenLifetimeResolver.ResolveExpirationDays(null, configuration, persistent: true)
            .Should().Be(45);
    }

    [Fact]
    public void Resolver_TypedOptionsTakePrecedenceOverRawConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Jwt:PersistentRefreshTokenExpirationDays", "45" } })
            .Build();
        var options = Options.Create(new JwtOptions { RefreshTokenExpirationDays = 8, PersistentRefreshTokenExpirationDays = 60 });

        RefreshTokenLifetimeResolver.ResolveExpirationDays(options, configuration, persistent: false)
            .Should().Be(8);
        RefreshTokenLifetimeResolver.ResolveExpirationDays(options, configuration, persistent: true)
            .Should().Be(60);
    }

    [Fact]
    public void ResolveOriginatingLifetime_RecoversThePersistedDuration()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expiresAt = createdAt.AddDays(30);

        RefreshTokenLifetimeResolver.ResolveOriginatingLifetime(createdAt, expiresAt, TimeSpan.FromDays(7))
            .Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void ResolveOriginatingLifetime_FallsBackForMalformedRows()
    {
        RefreshTokenLifetimeResolver
            .ResolveOriginatingLifetime(DateTime.UnixEpoch, DateTime.UnixEpoch.AddDays(30), TimeSpan.FromDays(7))
            .Should().Be(TimeSpan.FromDays(7));
        var created = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        RefreshTokenLifetimeResolver
            .ResolveOriginatingLifetime(created, created.AddDays(-1), TimeSpan.FromDays(7))
            .Should().Be(TimeSpan.FromDays(7));
    }

    // ── Local sign-in path (also covers polymorphic sign-in, which funnels into it) ─────────────────────────

    [Theory]
    [InlineData(null, 7)]
    [InlineData(false, 7)]
    [InlineData(true, 30)]
    public async Task LocalSignIn_RefreshDeadlineFollowsRememberMeFlag(bool? rememberMe, int expectedDays)
    {
        var harness = new LocalRememberMeHarness();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(LocalRememberMeHarness.Password);
        var user = User.CreateWithPassword(LocalRememberMeHarness.Email, "localuser", passwordHash);
        harness.ArrangeUser(user);

        var response = await harness.Sut.LocalSignInAsync(new LocalSignInRequest
        {
            Email = LocalRememberMeHarness.Email,
            Password = LocalRememberMeHarness.Password,
            RememberMe = rememberMe
        });

        response.Success.Should().BeTrue();
        response.RefreshTokenExpiresAt.Should().BeWithin(Tolerance).After(DateTime.UtcNow.AddDays(expectedDays));
        harness.Jwt.Verify(
            x => x.GenerateRefreshTokenAsync(
                user.Id,
                It.IsAny<DeviceInfo>(),
                It.IsAny<DateTimeOffset>(),
                It.Is<DateTime?>(value => value != null && value > DateTime.UtcNow.AddDays(expectedDays - 1)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Sessions.Verify(
            x => x.CreateSessionAsync(
                It.IsAny<Guid>(),
                user.Id,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<DateTime>(value => value > DateTime.UtcNow.AddDays(expectedDays - 1)),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── OAuth path ─────────────────────────

    [Theory]
    [InlineData(null, 7)]
    [InlineData(false, 7)]
    [InlineData(true, 30)]
    public async Task GoogleIdTokenSignIn_RefreshDeadlineFollowsRememberMeFlag(bool? rememberMe, int expectedDays)
    {
        var harness = new OAuthRememberMeHarness();
        harness.ArrangeNewGoogleUser();

        var response = await harness.Sut.GoogleIdTokenSignInAsync(new GoogleIdTokenRequest
        {
            IdToken = "google-id-token",
            RememberMe = rememberMe
        });

        response.Success.Should().BeTrue();
        response.RefreshTokenExpiresAt.Should().BeWithin(Tolerance).After(DateTime.UtcNow.AddDays(expectedDays));
        harness.Jwt.Verify(
            x => x.GenerateRefreshTokenAsync(
                It.IsAny<Guid>(),
                It.IsAny<DeviceInfo>(),
                It.IsAny<DateTimeOffset>(),
                It.Is<DateTime?>(value => value != null && value > DateTime.UtcNow.AddDays(expectedDays - 1)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Web3 path ─────────────────────────

    [Theory]
    [InlineData(null, 7)]
    [InlineData(false, 7)]
    [InlineData(true, 30)]
    public async Task Web3SignatureSignIn_RefreshDeadlineFollowsRememberMeFlag(bool? rememberMe, int expectedDays)
    {
        var harness = new Web3RememberMeHarness();

        var response = await harness.Sut.VerifyWeb3SignatureAsync(new Web3VerificationRequest
        {
            WalletAddress = "0xremember",
            Signature = "0xsignature",
            Challenge = "challenge",
            RememberMe = rememberMe
        });

        response.Success.Should().BeTrue();
        response.RefreshTokenExpiresAt.Should().BeWithin(Tolerance).After(DateTime.UtcNow.AddDays(expectedDays));
        harness.Jwt.Verify(
            x => x.GenerateRefreshTokenAsync(
                harness.Identity.User.Id,
                It.IsAny<DeviceInfo>(),
                It.IsAny<DateTimeOffset>(),
                It.Is<DateTime?>(value => value != null && value > DateTime.UtcNow.AddDays(expectedDays - 1)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Refresh rotation honors the originating (persistent) duration ─────────────────────────

    [Fact]
    public async Task RefreshToken_PersistentSessionRenewsPersistentDeadlineInsteadOfCollapsingToStandard()
    {
        var harness = new LocalRememberMeHarness();
        var user = User.Create("refresh-persistent@example.test", "Refresh persistent user");
        harness.ArrangeUser(user);
        var now = DateTime.UtcNow;
        var storedToken = new RefreshToken
        {
            UserId = user.Id,
            Token = "hashed-persistent",
            // Issued as a 30-day "remember me" session one day ago.
            CreatedAt = now.AddDays(-1),
            ExpiresAt = now.AddDays(29),
            IsRevoked = false,
            CreatedByIp = "127.0.0.1"
        };
        harness.Tokens.Setup(x => x.GetByTokenAsync("hashed-persistent", It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);
        harness.Hashes.Setup(x => x.HashToken("persistent-token")).Returns("hashed-persistent");

        var response = await harness.Sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "persistent-token" });

        response.Success.Should().BeTrue();
        // Renewal keeps the originating 30-day window instead of collapsing onto the 7-day standard lifetime.
        response.RefreshTokenExpiresAt.Should().BeWithin(Tolerance).After(now.AddDays(30));
        response.RefreshTokenExpiresAt.Should().BeAfter(now.AddDays(7).Add(Tolerance));
    }

    [Fact]
    public async Task RefreshToken_MalformedLegacyRowFallsBackToConfiguredStandardDeadline()
    {
        var harness = new LocalRememberMeHarness();
        var user = User.Create("refresh-legacy@example.test", "Refresh legacy user");
        harness.ArrangeUser(user);
        var now = DateTime.UtcNow;
        var storedToken = new RefreshToken
        {
            UserId = user.Id,
            Token = "hashed-legacy",
            CreatedAt = default,
            ExpiresAt = now.AddDays(5),
            IsRevoked = false,
            CreatedByIp = "127.0.0.1"
        };
        harness.Tokens.Setup(x => x.GetByTokenAsync("hashed-legacy", It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);
        harness.Hashes.Setup(x => x.HashToken("legacy-token")).Returns("hashed-legacy");

        var response = await harness.Sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "legacy-token" });

        response.Success.Should().BeTrue();
        response.RefreshTokenExpiresAt.Should().BeWithin(Tolerance).After(now.AddDays(7));
    }

    // ── Logout revokes persistent sessions ─────────────────────────

    [Fact]
    public async Task Logout_RevokesPersistentRefreshTokenAndTerminatesItsSession()
    {
        var harness = new LocalRememberMeHarness();
        var user = User.Create("logout-persistent@example.test", "Logout persistent user");
        harness.ArrangeUser(user);
        var now = DateTime.UtcNow;
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "hashed-logout",
            CreatedAt = now.AddDays(-1),
            ExpiresAt = now.AddDays(29),
            IsRevoked = false,
            CreatedByIp = "127.0.0.1"
        };
        harness.Tokens.Setup(x => x.GetByTokenAsync("hashed-logout", It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);
        harness.Tokens.Setup(x => x.UpdateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);
        harness.Hashes.Setup(x => x.HashToken("logout-token")).Returns("hashed-logout");
        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, RefreshToken = "hashed-logout", IsActive = true };
        harness.Sessions.Setup(x => x.GetSessionByRefreshTokenAsync("hashed-logout", It.IsAny<CancellationToken>())).ReturnsAsync(session);
        harness.Sessions
            .Setup(x => x.TerminateSessionAsync(session.Id, SessionTerminationReason.UserLogout, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await harness.Sut.RevokeRefreshTokenAsync("logout-token", "127.0.0.1");

        storedToken.IsRevoked.Should().BeTrue();
        storedToken.RevokedAt.Should().NotBeNull();
        harness.Sessions.Verify(
            x => x.TerminateSessionAsync(session.Id, SessionTerminationReason.UserLogout, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Tokens.Verify(x => x.UpdateAsync(storedToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshToken_AfterLogout_RejectsTheRevokedPersistentToken()
    {
        var harness = new LocalRememberMeHarness();
        var user = User.Create("revoked-persistent@example.test", "Revoked persistent user");
        harness.ArrangeUser(user);
        var now = DateTime.UtcNow;
        var storedToken = new RefreshToken
        {
            UserId = user.Id,
            Token = "hashed-revoked",
            CreatedAt = now.AddDays(-1),
            ExpiresAt = now.AddDays(29),
            IsRevoked = true,
            RevokedAt = now.AddMinutes(-5),
            CreatedByIp = "127.0.0.1"
        };
        harness.Tokens.Setup(x => x.GetByTokenAsync("hashed-revoked", It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);
        harness.Hashes.Setup(x => x.HashToken("revoked-token")).Returns("hashed-revoked");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => harness.Sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "revoked-token" }));
    }

    // ── Harnesses ─────────────────────────

    private sealed class LocalRememberMeHarness
    {
        public const string Email = "remember@example.com";
        public const string Password = "Password1!";

        public readonly Mock<IUserRepository> Users = new();
        public readonly Mock<IRefreshTokenRepository> Tokens = new();
        public readonly Mock<IRefreshTokenLineageRepository> Lineages = new();
        public readonly Mock<IJwtTokenService> Jwt = new();
        public readonly Mock<IRefreshTokenHasher> Hashes = new();
        public readonly Mock<IAuthAttemptService> Attempts = new();
        public readonly Mock<IAuthenticationAnomalyDetectionService> Anomalies = new();
        public readonly Mock<IAuthenticationAuditEventSink> AuditSink = new();
        public readonly Mock<IUserEnumerationProtectionService> Enumeration = new();
        public readonly Mock<IHttpContextAccessor> HttpContextAccessor = new();
        public readonly Mock<IPublisher> Publisher = new();
        public readonly Mock<ISender> Sender = new();
        public readonly Mock<ISessionManagementService> Sessions = new();
        public readonly LocalAuthService Sut;

        public LocalRememberMeHarness()
        {
            PersistedAuthenticationSessions.Configure(Sessions);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:RefreshTokenExpiryInDays", "7" },
                    { "Jwt:PersistentRefreshTokenExpirationDays", "30" }
                })
                .Build();
            var passwordHasher = new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.UserAgent = "TestAgent/1.0";
            HttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
            Attempts.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            Enumeration.Setup(x => x.GetGenericErrorMessage(It.IsAny<string>())).Returns("Authentication failed");
            Enumeration.Setup(x => x.AddTimingProtectionDelayAsync(It.IsAny<bool>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
            Anomalies
                .Setup(x => x.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
                .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalBehavior = true });
            Anomalies
                .Setup(x => x.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
                .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low, RiskScore = 0, DetectedAnomalies = [] });
            Hashes.Setup(x => x.HashToken(It.IsAny<string>())).Returns((string token) => $"hash-{token}");
            Tokens.Setup(x => x.TryRevokeForRotationAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            Publisher.Setup(x => x.Publish(It.IsAny<UserSignedUpNotification>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            AuditSink.Setup(x => x.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var tenantId = Guid.NewGuid();
            Sender
                .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetUserMembershipsResponse
                {
                    TotalCount = 1,
                    Memberships =
                    [
                        new UserMembershipDto { TenantId = tenantId, TenantName = "Remember tenant", TenantSlug = "remember", TenantIsActive = true, Role = "Member", IsActive = true }
                    ]
                });
            Sender
                .Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Tenant?)null);
            Jwt.Setup(x => x.GenerateAccessTokenAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("access-token");
            Jwt.Setup(x => x.GenerateRefreshTokenAsync(
                    It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("refresh-token");

            Sut = new LocalAuthService(
                Users.Object,
                Tokens.Object,
                Lineages.Object,
                Jwt.Object,
                Hashes.Object,
                configuration,
                Attempts.Object,
                passwordHasher,
                Anomalies.Object,
                Enumeration.Object,
                HttpContextAccessor.Object,
                NullLogger<LocalAuthService>.Instance,
                Sender.Object,
                Sessions.Object,
                auditEventSink: AuditSink.Object);
        }

        public void ArrangeUser(User user)
        {
            Users.Setup(x => x.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            Users.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        }
    }

    private sealed class OAuthRememberMeHarness
    {
        public readonly Mock<IUserRepository> Users = new();
        public readonly Mock<IJwtTokenService> Jwt = new();
        public readonly Mock<IRefreshTokenHasher> Hashes = new();
        public readonly Mock<IOAuthService> Oauth = new();
        public readonly Mock<IGoogleIdTokenVerifier> GoogleVerifier = new();
        public readonly Mock<IExternalLoginRepository> ExternalLogins = new();
        public readonly Mock<IAuthAttemptService> Attempts = new();
        public readonly Mock<IHttpContextAccessor> HttpContextAccessor = new();
        public readonly Mock<ISender> Sender = new();
        public readonly Mock<ISessionManagementService> Sessions = new();
        public readonly OAuthAuthService Sut;

        public OAuthRememberMeHarness()
        {
            PersistedAuthenticationSessions.Configure(Sessions);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:RefreshTokenExpiryInDays", "7" },
                    { "Jwt:RefreshTokenExpirationDays", "7" },
                    { "Jwt:PersistentRefreshTokenExpirationDays", "30" },
                    { "Jwt:AccessTokenExpirationMinutes", "60" }
                })
                .Build();

            GoogleVerifier
                .Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VerifiedGoogleUser { Sub = "google-remember-sub", Email = "remember-oauth@example.com", EmailVerified = true, Name = "Remember OAuth" });
            Jwt.Setup(x => x.GenerateAccessTokenAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("access-token");
            Jwt.Setup(x => x.GenerateRefreshTokenAsync(
                    It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("refresh-token");
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.UserAgent = "TestAgent/1.0";
            HttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
            Attempts.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            Hashes.Setup(x => x.HashToken(It.IsAny<string>())).Returns((string token) => $"hash-{token}");
            Users.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Users.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            ExternalLogins
                .Setup(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExternalLogin dto, CancellationToken _) =>
                {
                    dto.Id = Guid.NewGuid();
                    dto.CreatedAt = DateTime.UtcNow;
                    dto.UpdatedAt = DateTime.UtcNow;
                    return dto;
                });
            Sender
                .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetUserMembershipsResponse());
            Sender
                .Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Tenant?)null);

            Sut = new OAuthAuthService(
                Users.Object,
                Jwt.Object,
                Hashes.Object,
                Oauth.Object,
                GoogleVerifier.Object,
                ExternalLogins.Object,
                configuration,
                Attempts.Object,
                HttpContextAccessor.Object,
                Sender.Object,
                Sessions.Object,
                NullLogger<OAuthAuthService>.Instance);
        }

        public void ArrangeNewGoogleUser()
        {
            ExternalLogins
                .Setup(x => x.GetByProviderKeyAsync("google", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExternalLogin?)null);
            Users.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);
        }
    }

    private sealed class Web3RememberMeHarness
    {
        public readonly Web3IdentityTestHarness Identity = new();
        public readonly Mock<IJwtTokenService> Jwt = new();
        public readonly Mock<IWeb3Service> Web3 = new();
        public readonly Mock<IAuthAttemptService> Attempts = new();
        public readonly Mock<IHttpContextAccessor> HttpContextAccessor = new();
        public readonly Web3AuthService Sut;

        public Web3RememberMeHarness()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:RefreshTokenExpirationDays", "7" },
                    { "Jwt:PersistentRefreshTokenExpirationDays", "30" },
                    { "Jwt:AccessTokenExpirationMinutes", "60" }
                })
                .Build();

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.UserAgent = "TestAgent/1.0";
            HttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
            Attempts.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            Jwt.Setup(x => x.GenerateAccessTokenAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("access-token");
            Jwt.Setup(x => x.GenerateRefreshTokenAsync(
                    It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("refresh-token");
            Web3
                .Setup(x => x.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(true);

            Sut = new Web3AuthService(
                Identity.Users.Object,
                Identity.Links.Object,
                Jwt.Object,
                Identity.Hashes.Object,
                Identity.Sessions.Object,
                Identity.Sender.Object,
                Web3.Object,
                configuration,
                Attempts.Object,
                HttpContextAccessor.Object,
                NullLogger<Web3AuthService>.Instance);
        }
    }
}

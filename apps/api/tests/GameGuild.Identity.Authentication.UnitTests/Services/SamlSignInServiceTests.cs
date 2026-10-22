using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication.UnitTests.Infrastructure;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     SamlSignInAsync coverage mirroring the OidcSignInServiceTests suite: link by
/// provider key, unverified-email refusal (SAML defaults to unverified), JIT creation,
/// the fail-closed MFA policy gate against the assertion's AuthnContext, the provider
/// tenant allowlist binding, and the fail-closed unconfigured-host guard.
/// </summary>
public class SamlSignInServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock = new();
    private readonly Mock<IOAuthService> _oauthServiceMock = new();
    private readonly Mock<IGoogleIdTokenVerifier> _googleVerifierMock = new();
    private readonly Mock<IOidcFederationService> _oidcFederationServiceMock = new();
    private readonly Mock<ISamlFederationService> _samlFederationServiceMock = new();
    private readonly Mock<IMfaService> _mfaServiceMock = new();
    private readonly Mock<IExternalLoginRepository> _externalLoginRepoMock = new();
    private readonly Mock<IAuthAttemptService> _authAttemptServiceMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ISessionManagementService> _sessionManagementServiceMock = new();
    private readonly IConfiguration _configuration;

    private const string Slug = "corp-idp";
    private static readonly Guid AllowedTenant = Guid.NewGuid();
    private static readonly Guid OtherTenant = Guid.NewGuid();

    public SamlSignInServiceTests()
    {
        PersistedAuthenticationSessions.Configure(_sessionManagementServiceMock);
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:RefreshTokenExpiryInDays", "7" },
                { "Jwt:RefreshTokenExpirationDays", "7" },
                { "Jwt:AccessTokenExpirationMinutes", "60" }
            })
            .Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "TestAgent/1.0";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
        _authAttemptServiceMock.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
        _refreshTokenHasherMock
            .Setup(x => x.HashToken(It.IsAny<string>()))
            .Returns((string token) => $"hash-{token}");

        _mfaServiceMock
            .Setup(x => x.IsMfaRequiredAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _externalLoginRepoMock
            .Setup(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin dto, CancellationToken _) =>
            {
                dto.Id = Guid.NewGuid();
                dto.CreatedAt = DateTime.UtcNow;
                dto.UpdatedAt = DateTime.UtcNow;
                return dto;
            });

        _senderMock
            .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse());
        _senderMock
            .Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        _samlFederationServiceMock
            .Setup(x => x.ConsumeAssertionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "jane@corp.example.test",
                Email = "jane@corp.example.test",
                EmailVerified = false,
                Name = "Jane Corp",
                AuthnContextClassRefs = ["urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport"]
            });
    }

    private OAuthAuthService CreateSut() => new(
        _userRepoMock.Object,
        _jwtTokenServiceMock.Object,
        _refreshTokenHasherMock.Object,
        _oauthServiceMock.Object,
        _googleVerifierMock.Object,
        _oidcFederationServiceMock.Object,
        _mfaServiceMock.Object,
        _externalLoginRepoMock.Object,
        _configuration,
        _authAttemptServiceMock.Object,
        _httpContextAccessorMock.Object,
        _senderMock.Object,
        _sessionManagementServiceMock.Object,
        NullLogger<OAuthAuthService>.Instance,
        samlFederationService: _samlFederationServiceMock.Object);

    private static SamlSignInRequest Request(Guid? tenantId = null) => new()
    {
        Slug = Slug,
        SamlResponse = "c2FtbC1yZXNwb25zZQ==",
        RelayState = "relay-1",
        TenantId = tenantId
    };

    private void SetUpMemberships(params (Guid TenantId, bool IsDefault)[] tenants)
    {
        _senderMock
            .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                Memberships = tenants.Select(tenant => new UserMembershipDto
                {
                    MembershipId = Guid.NewGuid(),
                    TenantId = tenant.TenantId,
                    TenantName = $"tenant-{tenant.TenantId:N}",
                    TenantSlug = $"tenant-{tenant.TenantId:N}",
                    TenantIsActive = true,
                    TenantIsDefault = tenant.IsDefault,
                    Role = "User",
                    IsActive = true
                }).ToArray()
            });
    }

    // ── (1) Existing ExternalLogin (saml-<slug>) → reuse, no new user/link ───

    [Fact]
    public async Task SamlSignInAsync_ExistingExternalLogin_ReusesUser_NoNewUserOrLink()
    {
        var existingUser = User.CreateOAuthUser("jane@corp.example.test", "Jane Corp");
        var existingLink = new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = existingUser.Id,
            Provider = $"saml-{Slug}",
            ProviderKey = "jane@corp.example.test"
        };

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync($"saml-{Slug}", "jane@corp.example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);
        _userRepoMock.Setup(x => x.GetByIdAsync(existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var result = await CreateSut().SamlSignInAsync(Request());

        result.Success.Should().BeTrue();
        result.UserId.Should().Be(existingUser.Id);

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── (2) SAML email is unverified by default → collision refused ───────────

    [Fact]
    public async Task SamlSignInAsync_UnverifiedEmailCollision_RejectsWithoutLinkingExistingAccount()
    {
        var existingUser = User.CreateWithPassword(
            "jane@corp.example.test",
            "existing",
            BCrypt.Net.BCrypt.HashPassword("irrelevant"));

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync("jane@corp.example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var act = () => CreateSut().SamlSignInAsync(Request());

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*not verified*");

        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── (3) Trusted provider (verified email) → verified merge allowed ────────

    [Fact]
    public async Task SamlSignInAsync_TrustedProviderEmailMatch_LinksToExistingUser()
    {
        var existingUser = User.CreateWithPassword(
            "jane@corp.example.test",
            "existing",
            BCrypt.Net.BCrypt.HashPassword("irrelevant"));

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync("jane@corp.example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _samlFederationServiceMock
            .Setup(x => x.ConsumeAssertionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "jane@corp.example.test",
                Email = "jane@corp.example.test",
                EmailVerified = true,
                Name = "Jane Corp"
            });

        var result = await CreateSut().SamlSignInAsync(Request());

        result.UserId.Should().Be(existingUser.Id);

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(l => l.Provider == $"saml-{Slug}" && l.ProviderKey == "jane@corp.example.test" && l.UserId == existingUser.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (4) Brand-new identity → JIT user creation keyed saml-<slug> ──────────

    [Fact]
    public async Task SamlSignInAsync_NewIdentity_CreatesUserAndExternalLogin()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().SamlSignInAsync(Request());

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(l => l.Provider == $"saml-{Slug}" && l.ProviderKey == "jane@corp.example.test"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (5) AuthnContext surfaced on the response ──────────────────────────────

    [Fact]
    public async Task SamlSignInAsync_MapsAuthnContextOntoSignInResponse()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _samlFederationServiceMock
            .Setup(x => x.ConsumeAssertionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "jane@corp.example.test",
                Email = "jane@corp.example.test",
                EmailVerified = true,
                Name = "Jane Corp",
                AuthnContextClassRefs = ["urn:oasis:names:tc:SAML:2.0:ac:classes:TimeSyncToken"],
                HasMfaProof = true
            });

        var result = await CreateSut().SamlSignInAsync(Request());

        result.AuthenticationContextClassReference.Should().Be("urn:oasis:names:tc:SAML:2.0:ac:classes:TimeSyncToken");
        result.MfaVerifiedByProvider.Should().BeTrue();
    }

    // ── (6) MFA policy fail-closed against the assertion AuthnContext ─────────

    [Fact]
    public async Task SamlSignInAsync_MfaPolicyRequiredWithoutProviderProof_Rejected()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mfaServiceMock
            .Setup(x => x.IsMfaRequiredAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().SamlSignInAsync(Request());

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*multi-factor*");

        _jwtTokenServiceMock.Verify(
            x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sessionManagementServiceMock.Verify(
            x => x.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SamlSignInAsync_MfaPolicyRequiredWithProviderMfaProof_Succeeds()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mfaServiceMock
            .Setup(x => x.IsMfaRequiredAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samlFederationServiceMock
            .Setup(x => x.ConsumeAssertionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "jane@corp.example.test",
                Email = "jane@corp.example.test",
                EmailVerified = true,
                Name = "Jane Corp",
                AuthnContextClassRefs = ["urn:oasis:names:tc:SAML:2.0:ac:classes:TimeSyncToken"],
                HasMfaProof = true
            });

        var result = await CreateSut().SamlSignInAsync(Request());

        result.Success.Should().BeTrue();
    }

    // ── (7) Tenant allowlist binding (requested tenant) ───────────────────────

    [Fact]
    public async Task SamlSignInAsync_RequestedTenantOutsideProviderAllowlist_Rejected()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _samlFederationServiceMock
            .Setup(x => x.ConsumeAssertionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "jane@corp.example.test",
                Email = "jane@corp.example.test",
                EmailVerified = true,
                TenantAllowlist = [AllowedTenant]
            });

        var act = () => CreateSut().SamlSignInAsync(Request(OtherTenant));

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*does not serve the requested tenant*");
    }

    [Fact]
    public async Task SamlSignInAsync_ResolvedTenantOutsideProviderAllowlist_Rejected()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        SetUpMemberships((OtherTenant, true));
        _samlFederationServiceMock
            .Setup(x => x.ConsumeAssertionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "jane@corp.example.test",
                Email = "jane@corp.example.test",
                EmailVerified = true,
                TenantAllowlist = [AllowedTenant]
            });

        var act = () => CreateSut().SamlSignInAsync(Request());

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*does not serve the resolved tenant*");
    }

    [Fact]
    public async Task SamlSignInAsync_ResolvedTenantInsideProviderAllowlist_Succeeds()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        SetUpMemberships((AllowedTenant, true));
        _samlFederationServiceMock
            .Setup(x => x.ConsumeAssertionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "jane@corp.example.test",
                Email = "jane@corp.example.test",
                EmailVerified = true,
                TenantAllowlist = [AllowedTenant]
            });

        var result = await CreateSut().SamlSignInAsync(Request(AllowedTenant));

        result.Success.Should().BeTrue();
        result.TenantId.Should().Be(AllowedTenant);
    }

    // ── (8) Fail closed when SAML federation is not wired on the host ─────────

    [Fact]
    public async Task SamlSignInAsync_WithoutFederationService_FailsClosed()
    {
        var sut = new OAuthAuthService(
            _userRepoMock.Object,
            _jwtTokenServiceMock.Object,
            _refreshTokenHasherMock.Object,
            _oauthServiceMock.Object,
            _googleVerifierMock.Object,
            _oidcFederationServiceMock.Object,
            _mfaServiceMock.Object,
            _externalLoginRepoMock.Object,
            _configuration,
            _authAttemptServiceMock.Object,
            _httpContextAccessorMock.Object,
            _senderMock.Object,
            _sessionManagementServiceMock.Object,
            NullLogger<OAuthAuthService>.Instance);

        var act = () => sut.SamlSignInAsync(Request());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not configured*");
    }
}

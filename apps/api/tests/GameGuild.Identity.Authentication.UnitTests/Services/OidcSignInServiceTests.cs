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
///     OidcSignInAsync coverage mirroring the Discord/Google JIT-collision tests:
/// link-by-provider-key, verified-email merge, unverified-email refusal, JIT creation,
/// and the fail-closed MFA policy gate.
/// </summary>
public class OidcSignInServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock = new();
    private readonly Mock<IOAuthService> _oauthServiceMock = new();
    private readonly Mock<IGoogleIdTokenVerifier> _googleVerifierMock = new();
    private readonly Mock<IOidcFederationService> _oidcFederationServiceMock = new();
    private readonly Mock<IMfaService> _mfaServiceMock = new();
    private readonly Mock<IExternalLoginRepository> _externalLoginRepoMock = new();
    private readonly Mock<IAuthAttemptService> _authAttemptServiceMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ISessionManagementService> _sessionManagementServiceMock = new();
    private readonly IConfiguration _configuration;

    private const string Slug = "corp-idp";

    public OidcSignInServiceTests()
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

        _oidcFederationServiceMock
            .Setup(x => x.AuthenticateCallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "corp-sub-42",
                Email = "jane@corp.example.test",
                EmailVerified = true,
                Name = "Jane Corp",
                Amr = ["pwd"],
                Acr = "urn:corp:acr:1fa"
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
        NullLogger<OAuthAuthService>.Instance);

    private static OidcSignInRequest Request() => new()
    {
        Slug = Slug,
        Code = "provider-auth-code",
        State = "state-123",
        RedirectUri = "https://web.example.test/api/auth/callback/oidc"
    };

    // ── (1) Existing ExternalLogin (oidc-<slug>) → reuse, no new user/link ───

    [Fact]
    public async Task OidcSignInAsync_ExistingExternalLogin_ReusesUser_NoNewUserOrLink()
    {
        var existingUser = User.CreateOAuthUser("jane@corp.example.test", "Jane Corp");
        var existingLink = new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = existingUser.Id,
            Provider = $"oidc-{Slug}",
            ProviderKey = "corp-sub-42"
        };

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync($"oidc-{Slug}", "corp-sub-42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);
        _userRepoMock.Setup(x => x.GetByIdAsync(existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var result = await CreateSut().OidcSignInAsync(Request());

        result.Success.Should().BeTrue();
        result.UserId.Should().Be(existingUser.Id);

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── (2) Verified email match → link, no duplicate user ──────────────────

    [Fact]
    public async Task OidcSignInAsync_VerifiedEmailMatch_LinksToExistingUser_NoDuplicate()
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

        var result = await CreateSut().OidcSignInAsync(Request());

        result.UserId.Should().Be(existingUser.Id);

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(l => l.Provider == $"oidc-{Slug}" && l.ProviderKey == "corp-sub-42" && l.UserId == existingUser.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (3) Unverified email collision → refuse (no account takeover) ───────

    [Fact]
    public async Task OidcSignInAsync_UnverifiedEmailCollision_RejectsWithoutLinkingExistingAccount()
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
        _oidcFederationServiceMock
            .Setup(x => x.AuthenticateCallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "corp-sub-42",
                Email = "jane@corp.example.test",
                EmailVerified = false,
                Name = "Unverified Jane"
            });

        var act = () => CreateSut().OidcSignInAsync(Request());

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*not verified*");

        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── (4) Brand-new identity → JIT user creation keyed oidc-<slug> ─────────

    [Fact]
    public async Task OidcSignInAsync_NewIdentity_CreatesUserAndExternalLogin()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().OidcSignInAsync(Request());

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(l => l.Provider == $"oidc-{Slug}" && l.ProviderKey == "corp-sub-42"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (5) amr/acr surfaced on the response ─────────────────────────────────

    [Fact]
    public async Task OidcSignInAsync_MapsAmrAndAcrOntoSignInResponse()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _oidcFederationServiceMock
            .Setup(x => x.AuthenticateCallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "corp-sub-42",
                Email = "jane@corp.example.test",
                EmailVerified = true,
                Name = "Jane Corp",
                Amr = ["pwd", "mfa"],
                Acr = "urn:corp:acr:2fa"
            });

        var result = await CreateSut().OidcSignInAsync(Request());

        result.AuthenticationMethodReferences.Should().BeEquivalentTo(["pwd", "mfa"]);
        result.AuthenticationContextClassReference.Should().Be("urn:corp:acr:2fa");
        result.MfaVerifiedByProvider.Should().BeTrue();
    }

    // ── (6) MFA policy fail-closed ────────────────────────────────────────────

    [Fact]
    public async Task OidcSignInAsync_MfaPolicyRequiredWithoutProviderProof_Rejected()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mfaServiceMock
            .Setup(x => x.IsMfaRequiredAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().OidcSignInAsync(Request());

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*multi-factor*");

        // Fail closed: no access token, no session — the JIT identity may be linked, but it
        // cannot obtain a session until the provider attests MFA.
        _jwtTokenServiceMock.Verify(
            x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sessionManagementServiceMock.Verify(
            x => x.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OidcSignInAsync_MfaPolicyRequiredWithProviderMfaProof_Succeeds()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mfaServiceMock
            .Setup(x => x.IsMfaRequiredAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _oidcFederationServiceMock
            .Setup(x => x.AuthenticateCallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "corp-sub-42",
                Email = "jane@corp.example.test",
                EmailVerified = true,
                Name = "Jane Corp",
                Amr = ["pwd", "mfa"]
            });

        var result = await CreateSut().OidcSignInAsync(Request());

        result.Success.Should().BeTrue();
        result.MfaVerifiedByProvider.Should().BeTrue();
    }

    // ── (7) Provider rejection propagates as 401-equivalent ─────────────────

    [Fact]
    public async Task OidcSignInAsync_ProviderTokenInvalid_PropagatesUnauthorizedAccess()
    {
        _oidcFederationServiceMock
            .Setup(x => x.AuthenticateCallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("OIDC ID token for provider 'corp-idp' failed validation."));

        var act = () => CreateSut().OidcSignInAsync(Request());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── (8) No email claim → refuse ──────────────────────────────────────────

    [Fact]
    public async Task OidcSignInAsync_IdentityWithoutEmail_Rejected()
    {
        _oidcFederationServiceMock
            .Setup(x => x.AuthenticateCallbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcFederatedIdentity
            {
                Slug = Slug,
                ProviderKey = "corp-sub-42",
                Email = null,
                EmailVerified = false
            });

        var act = () => CreateSut().OidcSignInAsync(Request());

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*no email claim*");
    }
}

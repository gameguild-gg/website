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
///     Platform-side sign-in tests for the new provider callbacks (issue #246):
///     Steam OpenID 2.0, Epic Games, Facebook, and Microsoft flow through the shared
///     auto-link/JIT policy exactly like the existing GitHub callback.
/// </summary>
public class ProviderCallbackSignInServiceTests
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

    public ProviderCallbackSignInServiceTests()
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

        _mfaServiceMock
            .Setup(x => x.IsMfaRequiredAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

        _oauthServiceMock
            .Setup(x => x.ResolveAuthorizationScopes(It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns(new[] { "scope" });
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

    private void SetupProviderProfile(string provider, OAuthUserProfile profile)
    {
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync(provider, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(profile);
    }

    // ── Steam: OpenID supplies no email claim ──────────────────────────────

    [Fact]
    public async Task SteamCallbackSignInAsync_NewSteamAccount_CreatesUserWithInternalIdentifier()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("steam", "76561198000000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        SetupProviderProfile("steam", new OAuthUserProfile
        {
            ProviderId = "76561198000000001",
            Provider = "Steam",
            Email = null,
            EmailVerified = false,
            Name = "Steam User",
            Username = "steamuser"
        });

        var result = await CreateSut().SteamCallbackSignInAsync(new SteamCallbackSignInRequest
        {
            Code = "openid-assertion",
            State = "state-123",
            RedirectUri = "https://web.example.com/api/auth/callback/steam"
        });

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Steam sign-in successful");

        // The provider callback was verified through the generic dispatcher.
        _oauthServiceMock.Verify(
            x => x.HandleCallbackAsync("steam", "openid-assertion", "state-123", "https://web.example.com/api/auth/callback/steam"),
            Times.Once);

        // Email-less OpenID identity → internal unverified identifier, never a real-email link.
        _userRepoMock.Verify(
            x => x.AddAsync(It.Is<User>(user => user.Email == "76561198000000001@steam.local" && !user.IsEmailVerified), It.IsAny<CancellationToken>()),
            Times.Once);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(link => link.Provider == "steam" && link.ProviderKey == "76561198000000001"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SteamCallbackSignInAsync_ExistingSteamLink_ReusesLinkedUser()
    {
        var linkedUser = User.CreateOAuthUser("76561198000000001@steam.local", "Steam User");
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("steam", "76561198000000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin
            {
                Id = Guid.NewGuid(),
                UserId = linkedUser.Id,
                Provider = "steam",
                ProviderKey = "76561198000000001"
            });
        _userRepoMock.Setup(x => x.GetByIdAsync(linkedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(linkedUser);
        SetupProviderProfile("steam", new OAuthUserProfile
        {
            ProviderId = "76561198000000001",
            Provider = "Steam",
            Email = null,
            EmailVerified = false,
            Name = "Steam User"
        });

        var result = await CreateSut().SteamCallbackSignInAsync(new SteamCallbackSignInRequest
        {
            Code = "openid-assertion",
            State = "state-123",
            RedirectUri = "https://web.example.com/api/auth/callback/steam"
        });

        result.UserId.Should().Be(linkedUser.Id);
        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SteamCallbackSignInAsync_SynthesizedIdentifierNeverTakesOverAnExistingEmailAccount()
    {
        // Someone registering the internal identifier locally must not be hijackable:
        // the Steam identity is unverified, so the auto-link policy refuses the merge.
        var squatter = User.Create("76561198000000001@steam.local", "Local Squatter", "+15550001000");
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("steam", "76561198000000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync("76561198000000001@steam.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(squatter);
        SetupProviderProfile("steam", new OAuthUserProfile
        {
            ProviderId = "76561198000000001",
            Provider = "Steam",
            Email = null,
            EmailVerified = false
        });

        var act = () => CreateSut().SteamCallbackSignInAsync(new SteamCallbackSignInRequest
        {
            Code = "openid-assertion",
            State = "state-123",
            RedirectUri = "https://web.example.com/api/auth/callback/steam"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Email is not verified by Steam*");
        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Epic / Facebook / Microsoft: email-backed providers ────────────────

    [Theory]
    [InlineData("epic", "Epic", "epic-account-1", "epic@example.com", "Epic sign-in successful")]
    [InlineData("facebook", "Facebook", "facebook-user-1", "facebook@example.com", "Facebook sign-in successful")]
    [InlineData("microsoft", "Microsoft", "microsoft-sub-1", "microsoft@example.com", "Microsoft sign-in successful")]
    public async Task ProviderCallbackSignInAsync_NewEmail_CreatesUserAndProviderExternalLogin(
        string providerKey, string providerName, string providerId, string email, string expectedMessage)
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(providerKey, providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        SetupProviderProfile(providerKey, new OAuthUserProfile
        {
            ProviderId = providerId,
            Provider = providerName,
            Email = email,
            EmailVerified = true,
            Name = $"{providerName} User"
        });

        var result = providerKey switch
        {
            "epic" => await CreateSut().EpicCallbackSignInAsync(new EpicCallbackSignInRequest { Code = "code", State = "state", RedirectUri = "https://web.example.com/cb" }),
            "facebook" => await CreateSut().FacebookCallbackSignInAsync(new FacebookCallbackSignInRequest { Code = "code", State = "state", RedirectUri = "https://web.example.com/cb" }),
            "microsoft" => await CreateSut().MicrosoftCallbackSignInAsync(new MicrosoftCallbackSignInRequest { Code = "code", State = "state", RedirectUri = "https://web.example.com/cb" }),
            _ => throw new InvalidOperationException($"Unknown provider case {providerKey}.")
        };

        result.Success.Should().BeTrue();
        result.Message.Should().Be(expectedMessage);
        result.Email.Should().Be(email);
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");

        _oauthServiceMock.Verify(
            x => x.HandleCallbackAsync(providerKey, "code", "state", "https://web.example.com/cb"),
            Times.Once);
        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(link => link.Provider == providerKey && link.ProviderKey == providerId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("epic")]
    [InlineData("facebook")]
    [InlineData("microsoft")]
    public async Task ProviderCallbackSignInAsync_ProfileWithoutEmail_IsRefused(string providerKey)
    {
        // Email-backed providers must not synthesize identifiers — only the OpenID
        // Steam flow is allowed to (its profile never carries an email at all).
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(providerKey, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        SetupProviderProfile(providerKey, new OAuthUserProfile
        {
            ProviderId = "provider-id",
            Provider = providerKey,
            Email = null,
            EmailVerified = false
        });

        var act = () => providerKey switch
        {
            "epic" => CreateSut().EpicCallbackSignInAsync(new EpicCallbackSignInRequest { Code = "code", State = "state", RedirectUri = "https://web.example.com/cb" }),
            "facebook" => CreateSut().FacebookCallbackSignInAsync(new FacebookCallbackSignInRequest { Code = "code", State = "state", RedirectUri = "https://web.example.com/cb" }),
            "microsoft" => CreateSut().MicrosoftCallbackSignInAsync(new MicrosoftCallbackSignInRequest { Code = "code", State = "state", RedirectUri = "https://web.example.com/cb" }),
            _ => throw new InvalidOperationException($"Unknown provider case {providerKey}.")
        };

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Email not available*");
        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

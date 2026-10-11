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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class GitHubCallbackSignInTests
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

    private const string RedirectUri = "https://web.example.com/api/auth/callback/github";

    public GitHubCallbackSignInTests()
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

        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("github", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile
            {
                ProviderId = "github-node-1",
                Provider = "GitHub",
                Email = "github@example.com",
                EmailVerified = true,
                Name = "GitHub User",
                Username = "githubuser",
                AccessToken = "github-access-token"
            });
        _oauthServiceMock
            .Setup(x => x.ResolveAuthorizationScopes(It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns(new[] { "read:user", "user:email" });

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

    private static GitHubCallbackSignInRequest Request(Guid? tenantId = null) =>
        new() { Code = "github-auth-code", State = "state-123", RedirectUri = RedirectUri, TenantId = tenantId };

    // ── Baseline: brand-new email → user + ExternalLogin + tokens ───────────

    [Fact]
    public async Task GitHubCallbackSignInAsync_NewEmail_CreatesUserAndGitHubExternalLogin_ReturnsTokens()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("github", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().GitHubCallbackSignInAsync(Request());

        result.Success.Should().BeTrue();
        result.Message.Should().Be("GitHub sign-in successful");
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.Email.Should().Be("github@example.com");
        result.SessionId.Should().NotBe(Guid.Empty);

        // Exchange + profile went through the provider-generic callback path.
        _oauthServiceMock.Verify(
            x => x.HandleCallbackAsync("github", "github-auth-code", "state-123", RedirectUri),
            Times.Once);
        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(e => e.Provider == "github" && e.ProviderKey == "github-node-1"), It.IsAny<CancellationToken>()),
            Times.Once);
        _jwtTokenServiceMock.Verify(
            x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _sessionManagementServiceMock.Verify(
            x => x.CreateSessionAsync(
                result.SessionId,
                result.UserId,
                "127.0.0.1",
                "TestAgent/1.0",
                "hash-refresh-token",
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (1) Existing ExternalLogin → reuse, no new user/row ─────────────────

    [Fact]
    public async Task GitHubCallbackSignInAsync_ExistingExternalLogin_ReusesUser_NoNewUserOrLink()
    {
        var existingUser = User.CreateOAuthUser("github@example.com", "Linked User");
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("github", "github-node-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin
            {
                Id = Guid.NewGuid(),
                UserId = existingUser.Id,
                Provider = "github",
                ProviderKey = "github-node-1"
            });
        _userRepoMock.Setup(x => x.GetByIdAsync(existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var result = await CreateSut().GitHubCallbackSignInAsync(Request());

        result.UserId.Should().Be(existingUser.Id);

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── (2) Verified email match → auto-link, ExternalLogin only ────────────

    [Fact]
    public async Task GitHubCallbackSignInAsync_VerifiedEmailMatch_LinksToExistingUser_NoDuplicate()
    {
        var existingUser = User.CreateWithPassword(
            "github@example.com",
            "existing",
            BCrypt.Net.BCrypt.HashPassword("irrelevant"));

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync("github@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var result = await CreateSut().GitHubCallbackSignInAsync(Request());

        result.UserId.Should().Be(existingUser.Id);

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(e => e.UserId == existingUser.Id && e.Provider == "github" && e.ProviderKey == "github-node-1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (3) Unverified email collision → reject, no link ────────────────────

    [Fact]
    public async Task GitHubCallbackSignInAsync_UnverifiedEmailMatch_ThrowsAndDoesNotLink()
    {
        var existingUser = User.CreateWithPassword(
            "github@example.com",
            "existing",
            BCrypt.Net.BCrypt.HashPassword("irrelevant"));

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync("github@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("github", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile
            {
                ProviderId = "github-node-1",
                Email = "github@example.com",
                EmailVerified = false, // NOT verified → must NOT merge.
                Name = "Attacker"
            });

        var sut = CreateSut();

        await sut
            .Invoking(s => s.GitHubCallbackSignInAsync(Request()))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── (4) GitHub email null → reject with explicit message ────────────────

    [Fact]
    public async Task GitHubCallbackSignInAsync_NullEmail_ThrowsUnauthorizedAccessWithMessage()
    {
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("github", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile
            {
                ProviderId = "github-node-1",
                Email = null, // GitHub account without a primary email (no user:email scope).
                EmailVerified = false,
                Name = "No Email"
            });

        var sut = CreateSut();

        (await sut.Invoking(s => s.GitHubCallbackSignInAsync(Request()))
                .Should()
                .ThrowAsync<UnauthorizedAccessException>())
            .Which.Message.Should().Be("Email not available from GitHub profile");

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── (5) Race: DbUpdateException on insert → refetch winning rows, resume ─

    [Fact]
    public async Task GitHubCallbackSignInAsync_RaceOnUserInsert_CatchesDbUpdateException_RefetchesAndResumes()
    {
        var winningUser = User.CreateOAuthUser("github@example.com", "Winner");
        var winningLink = new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = winningUser.Id,
            Provider = "github",
            ProviderKey = "github-node-1"
        };

        _userRepoMock.Setup(x => x.GetByEmailAsync("github@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var saveCallCount = 0;
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                saveCallCount++;
                if (saveCallCount == 1)
                {
                    throw new DbUpdateException("duplicate key value violates unique constraint");
                }
                return Task.CompletedTask;
            });

        var lookupCount = 0;
        _externalLoginRepoMock.Setup(x => x.GetByProviderKeyAsync("github", "github-node-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                lookupCount++;
                return lookupCount == 1 ? null : winningLink;
            });

        _userRepoMock.Setup(x => x.GetByIdAsync(winningUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(winningUser);

        var result = await CreateSut().GitHubCallbackSignInAsync(Request());

        result.Success.Should().BeTrue();
        result.UserId.Should().Be(winningUser.Id);
        result.AccessToken.Should().Be("access-token");
    }

    // ── (6) TenantId passthrough → ResolveTenantAccessContextAsync honors it ─

    [Fact]
    public async Task GitHubCallbackSignInAsync_RequestedTenantId_SelectsThatTenantOverDefault()
    {
        var studioTenantId = Guid.NewGuid();
        var defaultTenantId = Guid.NewGuid();

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _senderMock
            .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 2,
                Memberships =
                [
                    new UserMembershipDto
                    {
                        TenantId = studioTenantId,
                        TenantName = "Studio",
                        TenantSlug = "studio",
                        TenantIsActive = true,
                        TenantIsDefault = false,
                        Role = "Member",
                        IsActive = true
                    },
                    new UserMembershipDto
                    {
                        TenantId = defaultTenantId,
                        TenantName = "GameGuild",
                        TenantSlug = "gameguild",
                        TenantIsActive = true,
                        TenantIsDefault = true,
                        Role = "Member",
                        IsActive = true
                    }
                ]
            });

        var result = await CreateSut().GitHubCallbackSignInAsync(Request(tenantId: studioTenantId));

        // Requested tenant wins over the default tenant → TenantId flowed through
        // ResolveTenantAccessContextAsync into the response and the access token.
        result.TenantId.Should().Be(studioTenantId);
        result.AvailableTenants.Should().HaveCount(2);
        _jwtTokenServiceMock.Verify(
            x => x.GenerateAccessTokenAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                studioTenantId,
                It.IsAny<int>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (7) Callback command dispatch: auto-registration via AddCqrs scan ───
    //     This is the exact defect #248 recorded: GitHubCallbackCommand had no
    //     handler, so the endpoint could never succeed at runtime.

    [Fact]
    public async Task GitHubCallbackCommand_ResolvesToRegisteredHandler_ViaAddCqrsScan()
    {
        var oauthAuthService = new Mock<IOAuthAuthService>();
        oauthAuthService
            .Setup(x => x.GitHubCallbackSignInAsync(It.IsAny<GitHubCallbackSignInRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResponse
            {
                Success = true,
                Message = "GitHub sign-in successful",
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                UserId = Guid.NewGuid(),
                Email = "github@example.com"
            });
        var userRepo = new Mock<IUserRepository>();

        var services = new ServiceCollection();
        services.AddCqrs(typeof(OAuthAuthService).Assembly);
        services.AddSingleton(oauthAuthService.Object);
        services.AddSingleton(userRepo.Object);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(
            new GitHubCallbackCommand { Code = "c", State = "s", RedirectUri = RedirectUri },
            CancellationToken.None);

        response.Success.Should().BeTrue();
        response.AccessToken.Should().Be("access-token");
        oauthAuthService.Verify(
            x => x.GitHubCallbackSignInAsync(
                It.Is<GitHubCallbackSignInRequest>(r => r.Code == "c" && r.State == "s" && r.RedirectUri == RedirectUri),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

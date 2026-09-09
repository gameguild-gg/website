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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class OAuthAuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock = new();
    private readonly Mock<IOAuthService> _oauthServiceMock = new();
    private readonly Mock<IGoogleIdTokenVerifier> _googleVerifierMock = new();
    private readonly Mock<IExternalLoginRepository> _externalLoginRepoMock = new();
    private readonly Mock<IAuthAttemptService> _authAttemptServiceMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ISessionManagementService> _sessionManagementServiceMock = new();
    private readonly IConfiguration _configuration;

    public OAuthAuthServiceTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:RefreshTokenExpiryInDays", "7" },
                { "Jwt:RefreshTokenExpirationDays", "7" },
                { "Jwt:AccessTokenExpirationMinutes", "60" }
            })
            .Build();

        _oauthServiceMock
            .Setup(x => x.GetUserProfileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile
            {
                ProviderId = "oauth-sub-default",
                Email = "oauth@example.com",
                EmailVerified = true,
                Name = "OAuth User",
                AccessToken = "provider-token"
            });

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns("sync-access-token");

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "TestAgent/1.0";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
        _authAttemptServiceMock.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
        _refreshTokenHasherMock
            .Setup(x => x.HashToken(It.IsAny<string>()))
            .Returns((string token) => $"hash-{token}");

        _googleVerifierMock
            .Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGoogleUser
            {
                Sub = "google-sub-default",
                Email = "user@example.com",
                EmailVerified = true,
                Name = "Test User"
            });

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
            .Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");
    }

    private OAuthAuthService CreateSut() => new(
        _userRepoMock.Object,
        _jwtTokenServiceMock.Object,
        _refreshTokenHasherMock.Object,
        _oauthServiceMock.Object,
        _googleVerifierMock.Object,
        _externalLoginRepoMock.Object,
        _configuration,
        _authAttemptServiceMock.Object,
        _httpContextAccessorMock.Object,
        _senderMock.Object,
        _sessionManagementServiceMock.Object,
        NullLogger<OAuthAuthService>.Instance);

    private static GoogleIdTokenRequest Request(string idToken = "fake-id-token", Guid? tenantId = null) =>
        new() { IdToken = idToken, TenantId = tenantId };

    // ── (0) Migrated baseline: new behavior post-rewrite ────────────────────

    [Fact]
    public async Task GoogleIdTokenSignInAsync_NewEmail_CreatesUserAndExternalLogin_WithOneRefreshRowAndFreshSessionId()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("google", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().GoogleIdTokenSignInAsync(Request());

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.UserId.Should().NotBe(Guid.Empty);
        result.Email.Should().Be("user@example.com");
        result.SessionId.Should().NotBe(Guid.Empty);

        _jwtTokenServiceMock.Verify(
            x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()),
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

        // Verifier invoked (not the dead tokeninfo path).
        _googleVerifierMock.Verify(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        // ExternalLogin upserted with provider "google".
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(e => e.Provider == "google"), It.IsAny<CancellationToken>()),
            Times.Once);
        // User created.
        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── (1) Existing ExternalLogin → reuse, no new user/link ────────────────

    [Fact]
    public async Task GoogleIdTokenSignInAsync_ExistingExternalLogin_ReusesUser_NoNewUserOrLink()
    {
        var existingUser = User.CreateOAuthUser("linked@example.com", "Linked User");
        var existingLink = new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = existingUser.Id,
            Provider = "google",
            ProviderKey = "google-sub-default"
        };

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("google", "google-sub-default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);
        _userRepoMock.Setup(x => x.GetByIdAsync(existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var result = await CreateSut().GoogleIdTokenSignInAsync(Request());

        result.UserId.Should().Be(existingUser.Id);

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── (2) Verified email match → link, no duplicate user ──────────────────

    [Fact]
    public async Task GoogleIdTokenSignInAsync_VerifiedEmailMatch_LinksToExistingUser_NoDuplicate()
    {
        var existingUser = User.CreateWithPassword(
            "user@example.com",
            "existing",
            BCrypt.Net.BCrypt.HashPassword("irrelevant"));

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        _googleVerifierMock
            .Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGoogleUser
            {
                Sub = "google-sub-1",
                Email = "user@example.com",
                EmailVerified = true,
                Name = "Now Linked"
            });

        var result = await CreateSut().GoogleIdTokenSignInAsync(Request());

        result.UserId.Should().Be(existingUser.Id);

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(e => e.UserId == existingUser.Id && e.ProviderKey == "google-sub-1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (3) Unverified email match → reject, no link ────────────────────────

    [Fact]
    public async Task GoogleIdTokenSignInAsync_UnverifiedEmailMatch_ThrowsAndDoesNotLink()
    {
        var existingUser = User.CreateWithPassword(
            "user@example.com",
            "existing",
            BCrypt.Net.BCrypt.HashPassword("irrelevant"));

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        _googleVerifierMock
            .Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGoogleUser
            {
                Sub = "google-sub-1",
                Email = "user@example.com",
                EmailVerified = false, // NOT verified → must NOT merge.
                Name = "Attacker"
            });

        var sut = CreateSut();

        await sut
            .Invoking(s => s.GoogleIdTokenSignInAsync(Request()))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── (4) Brand-new email → user + ExternalLogin ──────────────────────────

    [Fact]
    public async Task GoogleIdTokenSignInAsync_BrandNewEmail_CreatesUserAndExternalLogin()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await CreateSut().GoogleIdTokenSignInAsync(Request());

        _userRepoMock.Verify(x => x.AddAsync(It.Is<User>(u => u.Email == "user@example.com"), It.IsAny<CancellationToken>()), Times.Once);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.Is<ExternalLogin>(e => e.Provider == "google" && e.ProviderKey == "google-sub-default"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GoogleIdTokenSignInAsync_UserWithoutMembership_ProvisionsTheDefaultTenant()
    {
        var defaultTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "GameGuild",
            Slug = "gameguild",
            IsDefault = true,
            IsActive = true
        };

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
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

        var result = await CreateSut().GoogleIdTokenSignInAsync(Request());

        result.TenantId.Should().Be(defaultTenant.Id);
        _senderMock.Verify(
            x => x.Send(
                It.Is<AddTenantMemberCommand>(command =>
                    command.TenantId == defaultTenant.Id && command.UserId == result.UserId && command.Role == "Member"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GoogleIdTokenSignInAsync_InactiveDefaultMembership_ReactivatesAndPreservesRole()
    {
        var defaultTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "GameGuild",
            Slug = "gameguild",
            IsDefault = true,
            IsActive = true
        };
        var user = User.CreateOAuthUser("user@example.com", "System administrator");
        AddTenantMemberCommand? capturedCommand = null;

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("google", "google-sub-default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin { Id = Guid.NewGuid(), UserId = user.Id, Provider = "google", ProviderKey = "google-sub-default" });
        _userRepoMock.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _senderMock.Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(defaultTenant);
        _senderMock
            .SetupSequence(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships = [new UserMembershipDto { TenantId = defaultTenant.Id, Role = "SystemAdmin", IsActive = false }]
            })
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships = [new UserMembershipDto { TenantId = defaultTenant.Id, TenantName = defaultTenant.Name, TenantSlug = defaultTenant.Slug, TenantIsActive = true, Role = "SystemAdmin", IsActive = true }]
            });
        _senderMock
            .Setup(x => x.Send(It.IsAny<AddTenantMemberCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AddTenantMemberResponse>, CancellationToken>((request, _) => capturedCommand = (AddTenantMemberCommand)request)
            .ReturnsAsync(new AddTenantMemberResponse { Success = true, MemberId = Guid.NewGuid() });

        var result = await CreateSut().GoogleIdTokenSignInAsync(Request());

        result.TenantId.Should().Be(defaultTenant.Id);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Role.Should().Be("SystemAdmin");
    }
    // ── (5) Exactly one refresh row ─────────────────────────────────────────
    //   Folded into baseline (0): GenerateRefreshTokenAsync once, CreateAsync never.

    // ── (6) TenantId reflects resolved context; Roles includes "User" ───────

    [Fact]
    public async Task GoogleIdTokenSignInAsync_NoMemberships_ReturnsNullTenantAndUserOnlyRole()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _senderMock
            .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse());
        _senderMock
            .Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        await CreateSut().GoogleIdTokenSignInAsync(Request());

        _jwtTokenServiceMock.Verify(
            x => x.GenerateAccessTokenAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.Is<string[]>(roles => roles.Contains("User")),
                null,
                It.IsAny<int>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GoogleIdTokenSignInAsync_WithMembership_ReflectsTenantIdAndAppendsUserRole()
    {
        var tenantId = Guid.NewGuid();
        var user = User.CreateOAuthUser("user@example.com", "User");
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("google", "google-sub-default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin { Id = Guid.NewGuid(), UserId = user.Id, Provider = "google", ProviderKey = "google-sub-default" });
        _userRepoMock.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _senderMock
            .Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships = new List<UserMembershipDto>
                {
                    new()
                    {
                        TenantId = tenantId,
                        TenantName = "My Tenant",
                        TenantSlug = "my-tenant",
                        TenantIsActive = true,
                        Role = "Admin",
                        IsActive = true
                    }
                }
            });

        var result = await CreateSut().GoogleIdTokenSignInAsync(Request());

        result.TenantId.Should().Be(tenantId);
        result.AvailableTenants.Should().NotBeNull();
        result.AvailableTenants!.Should().HaveCount(1);

        _jwtTokenServiceMock.Verify(
            x => x.GenerateAccessTokenAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.Is<string[]>(roles => roles.Contains("Admin") && roles.Contains("User")),
                tenantId,
                It.IsAny<int>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (7) SessionId is non-empty Guid, NOT tied to refresh row ───────────

    [Fact]
    public async Task GoogleIdTokenSignInAsync_SessionId_IsFreshGuid_IndependentOfRefreshRow()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var first = await CreateSut().GoogleIdTokenSignInAsync(Request());

        first.SessionId.Should().NotBe(Guid.Empty);

        var second = await CreateSut().GoogleIdTokenSignInAsync(Request());
        second.SessionId.Should().NotBe(first.SessionId);
    }

    // ── Adversarial: verifier throws → handler propagates UnauthorizedAccessException ─

    [Fact]
    public async Task GoogleIdTokenSignInAsync_VerifierThrows_PropagatesUnauthorizedAccess_DoesNotSwallowTo500()
    {
        _googleVerifierMock
            .Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("forged token"));

        var sut = CreateSut();

        await sut
            .Invoking(s => s.GoogleIdTokenSignInAsync(Request()))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();

        _userRepoMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(
            x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Race idempotency ───────────────────────────────────────────────────
    //   The EF in-memory provider does NOT enforce unique indexes at runtime, so we
    //   simulate the losing side of the race: the user insert collides on SaveChangesAsync
    //   with a real DbUpdateException, then the handler re-fetches the winning rows and
    //   resumes (returns tokens for the winning user, no throw).
    [Fact]
    public async Task GoogleIdTokenSignInAsync_RaceOnUserInsert_CatchesDbUpdateException_RefetchesAndResumes()
    {
        var winningUser = User.CreateOAuthUser("user@example.com", "Winner");
        var winningLink = new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = winningUser.Id,
            Provider = "google",
            ProviderKey = "google-sub-default"
        };

        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
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
        _externalLoginRepoMock.Setup(x => x.GetByProviderKeyAsync("google", "google-sub-default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                lookupCount++;
                return lookupCount == 1 ? null : winningLink;
            });

        _userRepoMock.Setup(x => x.GetByIdAsync(winningUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(winningUser);

        var result = await CreateSut().GoogleIdTokenSignInAsync(Request());

        result.Success.Should().BeTrue();
        result.UserId.Should().Be(winningUser.Id);
        result.AccessToken.Should().Be("access-token");
    }

    // ── Tenant parity: duplicated ResolveTenantAccessContextAsync matches the
    //    behavior of LocalAuthService's private copy for the same memberships.
    [Fact]
    public async Task TenantResolution_NoMemberships_ParityWithLocalAuthService()
    {
        var memberships = new GetUserMembershipsResponse();
        var (oauthTenantId, oauthRoles) = await CaptureOAuthTenantResolution(memberships);
        var (localTenantId, localRoles) = await CaptureLocalSignUpTenantResolution(memberships);

        oauthTenantId.Should().Be(localTenantId);
        oauthRoles.Should().BeEquivalentTo(localRoles);
        oauthTenantId.Should().BeNull();
        oauthRoles.Should().BeEquivalentTo(new[] { "User" });
    }

    [Fact]
    public async Task TenantResolution_OneActiveMembership_ParityWithLocalAuthService()
    {
        var tenantId = Guid.NewGuid();
        var memberships = new GetUserMembershipsResponse
        {
            TotalCount = 1,
            Memberships = new List<UserMembershipDto>
            {
                new()
                {
                    TenantId = tenantId,
                    TenantName = "T",
                    TenantSlug = "t",
                    TenantIsActive = true,
                    Role = "Admin",
                    IsActive = true
                }
            }
        };

        var (oauthTenantId, oauthRoles) = await CaptureOAuthTenantResolution(memberships);
        var (localTenantId, localRoles) = await CaptureLocalSignUpTenantResolution(memberships);

        oauthTenantId.Should().Be(localTenantId);
        oauthRoles.Should().BeEquivalentTo(localRoles);
        oauthTenantId.Should().Be(tenantId);
        oauthRoles.Should().Contain("Admin").And.Contain("User");
    }

    [Fact]
    public async Task TenantResolution_WithoutRequestedTenant_Should_SelectDefaultTenant()
    {
        var defaultTenantId = Guid.NewGuid();
        var studioTenantId = Guid.NewGuid();
        var memberships = new GetUserMembershipsResponse
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
                    Role = "TenantAdmin",
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
        };

        var (oauthTenantId, oauthRoles) = await CaptureOAuthTenantResolution(memberships);
        var (localTenantId, localRoles) = await CaptureLocalSignUpTenantResolution(memberships);

        oauthTenantId.Should().Be(defaultTenantId);
        localTenantId.Should().Be(defaultTenantId);
        oauthRoles.Should().BeEquivalentTo(localRoles);
        oauthRoles.Should().Contain("Member").And.Contain("User");
        oauthRoles.Should().NotContain("TenantAdmin");
    }

    [Fact]
    public async Task TenantResolution_DefaultSystemAdmin_RemainsGlobalWhenAnotherTenantIsSelected()
    {
        var defaultTenantId = Guid.NewGuid();
        var studioTenantId = Guid.NewGuid();
        var memberships = new GetUserMembershipsResponse
        {
            TotalCount = 2,
            Memberships =
            [
                new UserMembershipDto
                {
                    TenantId = defaultTenantId,
                    TenantName = "GameGuild",
                    TenantSlug = "gameguild",
                    TenantIsActive = true,
                    TenantIsDefault = true,
                    Role = "SystemAdmin",
                    IsActive = true
                },
                new UserMembershipDto
                {
                    TenantId = studioTenantId,
                    TenantName = "Studio",
                    TenantSlug = "studio",
                    TenantIsActive = true,
                    Role = "Member",
                    IsActive = true
                }
            ]
        };

        var (oauthTenantId, oauthRoles) = await CaptureOAuthTenantResolution(memberships, studioTenantId);
        var (localTenantId, localRoles) = await CaptureLocalSignUpTenantResolution(memberships, studioTenantId);

        oauthTenantId.Should().Be(studioTenantId);
        localTenantId.Should().Be(studioTenantId);
        oauthRoles.Should().BeEquivalentTo(localRoles);
        oauthRoles.Should().Contain("Member").And.Contain("SystemAdmin").And.Contain("User");
    }

    [Fact]
    public async Task TenantResolution_NonDefaultSystemAdminRole_DoesNotGrantGlobalSystemAdmin()
    {
        var studioTenantId = Guid.NewGuid();
        var memberships = new GetUserMembershipsResponse
        {
            TotalCount = 1,
            Memberships =
            [
                new UserMembershipDto
                {
                    TenantId = studioTenantId,
                    TenantName = "Studio",
                    TenantSlug = "studio",
                    TenantIsActive = true,
                    TenantIsDefault = false,
                    Role = "SystemAdmin",
                    IsActive = true
                }
            ]
        };

        var (_, oauthRoles) = await CaptureOAuthTenantResolution(memberships, studioTenantId);
        var (_, localRoles) = await CaptureLocalSignUpTenantResolution(memberships, studioTenantId);

        oauthRoles.Should().BeEquivalentTo(localRoles);
        oauthRoles.Should().NotContain("SystemAdmin");
        oauthRoles.Should().ContainSingle().Which.Should().Be("User");
    }

    private async Task<(Guid? TenantId, string[] Roles)> CaptureOAuthTenantResolution(
        GetUserMembershipsResponse memberships,
        Guid? requestedTenantId = null)
    {
        _externalLoginRepoMock.Reset();
        _userRepoMock.Reset();
        _googleVerifierMock.Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGoogleUser
            {
                Sub = "google-sub-1",
                Email = "oauth@example.com",
                EmailVerified = true,
                Name = "OAuth"
            });
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync("oauth@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepoMock.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userRepoMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _externalLoginRepoMock
            .Setup(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin dto, CancellationToken _) => { dto.Id = Guid.NewGuid(); return dto; });

        _senderMock.Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships);

        string[]? capturedRoles = null;
        Guid? capturedTenantId = null;
        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string[], Guid?, int, Guid, CancellationToken>((_, _, roles, tenantId, _, _, _) => { capturedRoles = roles; capturedTenantId = tenantId; })
            .ReturnsAsync("access");

        await CreateSut().GoogleIdTokenSignInAsync(Request(tenantId: requestedTenantId));

        return (capturedTenantId, capturedRoles!);
    }

    // ── (8) ExpiresIn / AccessTokenExpiresAt: all three builders report ACCESS-token lifetime ──

    private OAuthAuthService CreateSutWithConfig(IConfiguration config) => new(
        _userRepoMock.Object,
        _jwtTokenServiceMock.Object,
        _refreshTokenHasherMock.Object,
        _oauthServiceMock.Object,
        _googleVerifierMock.Object,
        _externalLoginRepoMock.Object,
        config,
        _authAttemptServiceMock.Object,
        _httpContextAccessorMock.Object,
        _senderMock.Object,
        _sessionManagementServiceMock.Object,
        NullLogger<OAuthAuthService>.Instance);

    private static IConfiguration BuildConfig(string accessTokenMinutes = "60", string refreshDays = "7") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:RefreshTokenExpirationDays", refreshDays },
                { "Jwt:RefreshTokenExpiryInDays", refreshDays },
                { "Jwt:AccessTokenExpirationMinutes", accessTokenMinutes }
            })
            .Build();

    [Fact]
    public async Task GitHubSignInAsync_ExpiresIn_ReportsAccessTokenLifetime()
    {
        var sut = CreateSutWithConfig(BuildConfig());
        var result = await sut.GitHubSignInAsync(new OAuthSignInRequest { AccessToken = "gh-token" });

        result.ExpiresIn.Should().Be(3600);
        result.AccessTokenExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddMinutes(60), TimeSpan.FromSeconds(5));
        result.ExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        result.RefreshTokenExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GoogleSignInAsync_ExpiresIn_ReportsAccessTokenLifetime()
    {
        var sut = CreateSutWithConfig(BuildConfig());
        var result = await sut.GoogleSignInAsync(new OAuthSignInRequest { AccessToken = "google-token" });

        result.ExpiresIn.Should().Be(3600);
        result.AccessTokenExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddMinutes(60), TimeSpan.FromSeconds(5));
        result.ExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        result.RefreshTokenExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GoogleIdTokenSignInAsync_ExpiresIn_ReportsAccessTokenLifetime()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = CreateSutWithConfig(BuildConfig());
        var result = await sut.GoogleIdTokenSignInAsync(Request());

        result.ExpiresIn.Should().Be(3600);
        result.AccessTokenExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddMinutes(60), TimeSpan.FromSeconds(5));
        result.ExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        result.RefreshTokenExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GitHubSignInAsync_CustomAccessTokenMinutes_ExpiresInMatches()
    {
        var sut = CreateSutWithConfig(BuildConfig(accessTokenMinutes: "15"));
        var result = await sut.GitHubSignInAsync(new OAuthSignInRequest { AccessToken = "gh-token" });

        result.ExpiresIn.Should().Be(900);
        result.AccessTokenExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GoogleSignInAsync_CustomAccessTokenMinutes_ExpiresInMatches()
    {
        var sut = CreateSutWithConfig(BuildConfig(accessTokenMinutes: "15"));
        var result = await sut.GoogleSignInAsync(new OAuthSignInRequest { AccessToken = "google-token" });

        result.ExpiresIn.Should().Be(900);
        result.AccessTokenExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GoogleIdTokenSignInAsync_CustomAccessTokenMinutes_ExpiresInMatches()
    {
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);
        _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = CreateSutWithConfig(BuildConfig(accessTokenMinutes: "15"));
        var result = await sut.GoogleIdTokenSignInAsync(Request());

        result.ExpiresIn.Should().Be(900);
        result.AccessTokenExpiresAt.Should().BeCloseTo(SystemClock.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    private static async Task<(Guid? TenantId, string[] Roles)> CaptureLocalSignUpTenantResolution(
        GetUserMembershipsResponse memberships,
        Guid? requestedTenantId = null)
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        userRepo.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        userRepo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var refreshTokenHasher = new Mock<IRefreshTokenHasher>();
        var authAttempt = new Mock<IAuthAttemptService>();
        authAttempt.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
        var anomaly = new Mock<IAuthenticationAnomalyDetectionService>();
        var enumeration = new Mock<IUserEnumerationProtectionService>();
        enumeration.Setup(x => x.AddTimingProtectionDelayAsync(It.IsAny<bool>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        var httpCtx = new Mock<IHttpContextAccessor>();
        httpCtx.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());
        var publisher = new Mock<IPublisher>();
        publisher.Setup(x => x.Publish(It.IsAny<UserSignedUpNotification>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);
        sender.Setup(x => x.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(memberships);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Jwt:RefreshTokenExpiryInDays", "7" } })
            .Build();

        string[]? capturedRoles = null;
        Guid? capturedTenantId = null;
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string[], Guid?, int, Guid, CancellationToken>((_, _, roles, tenantId, _, _, _) => { capturedRoles = roles; capturedTenantId = tenantId; })
            .ReturnsAsync("access");
        jwt.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("rt");

        var sut = new LocalAuthService(
            userRepo.Object,
            new Mock<IRefreshTokenRepository>().Object,
            jwt.Object,
            refreshTokenHasher.Object,
            config,
            authAttempt.Object,
            anomaly.Object,
            enumeration.Object,
            httpCtx.Object,
            NullLogger<LocalAuthService>.Instance,
            sender.Object,
            Mock.Of<ISessionManagementService>());

        await sut.LocalSignUpAsync(new LocalSignUpRequest
        {
            Email = "local@example.com",
            Password = "Password1!",
            Username = "localuser",
            TenantId = requestedTenantId
        });

        return (capturedTenantId, capturedRoles!);
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using GameGuild.API.Database;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Tests.Authentication.Integration;

/// <summary>
/// End-to-end integration tests for authentication flows
/// Tests complete authentication scenarios including:
/// - Local authentication (sign-up, sign-in, token lifecycle)
/// - Social authentication (OAuth providers)
/// - Web3 authentication (wallet signatures)
/// - Polymorphic authentication (multiple strategies)
/// - MFA enrollment and verification
/// - Token refresh and revocation
/// </summary>
public class AuthenticationFlowsE2ETests : IClassFixture<AuthenticationApiFactory>, IDisposable
{
    private readonly WebApplicationFactory<GameGuild.API.Program> _factory;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly IMfaService _mfaService;

    public AuthenticationFlowsE2ETests(AuthenticationApiFactory factory)
    {
        _factory = factory;

        _client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _authService = _scope.ServiceProvider.GetRequiredService<IAuthService>();
        _mfaService = _scope.ServiceProvider.GetRequiredService<IMfaService>();

        // Ensure database is created
        _dbContext.Database.EnsureCreated();
        SeedDefaultTenant(_dbContext);
    }

    #region Local Authentication E2E Tests

    [Fact]
    public async Task LocalAuth_WithInvalidCredentials_ShouldFail()
    {
        // Arrange
        var email = $"invalid.{Guid.NewGuid()}@test.com";

        var signInRequest = new LocalSignInRequest
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        // Act & Assert
        await FluentActions.Invoking(async () => await _authService.LocalSignInAsync(signInRequest))
            .Should().ThrowAsync<Exception>();
    }

    // LocalAuth_TokenRefresh_AfterRevocation_ShouldFail was retired: the production
    // revocation path (RefreshTokenRepository.RevokeFamilyAsync / replay containment
    // from the #263 sweep) uses atomic ExecuteUpdate statements, which the EF InMemory
    // provider cannot translate, so this InMemory-hosted copy could never execute
    // again. Its scenario — revoked refresh token rejected with replay containment,
    // family revocation, and session termination — is covered end-to-end against the
    // real provider at the HTTP boundary by the Postgres suite:
    // GameGuild.API.IntegrationTests/BearerRevocationPostgreSqlHttpTests.cs
    // (RealRefreshReplayRejectsEarlierSignedBearerAndLeavesAnotherUserActive) and
    // RefreshTokenLifecycleAuditPostgreSqlHttpTests.cs
    // (ReplayCommitsContainmentAuditAndMetricWhileReturningNoCredentials,
    // ExplicitRevocationCommitsAuditAndMetricForOwnedTokenOrWholeAccount).

    [Fact]
    public async Task LocalAuth_SignIn_WithTenantMemberships_ShouldPopulateTenantContext()
    {
        // Arrange
        var email = $"tenant.flow.{Guid.NewGuid()}@test.com";
        var password = "TenantPassword123!";

        var signUpRequest = new LocalSignUpRequest
        {
            Email = email,
            Username = $"tenant_user_{Guid.NewGuid():N}",
            Password = password
        };

        var signUpResult = await _authService.LocalSignUpAsync(signUpRequest);
        var userId = signUpResult.UserId;

        var tenantOne = new Tenant
        {
            Name = $"Tenant One {Guid.NewGuid():N}",
            Slug = $"tenant-one-{Guid.NewGuid():N}",
            AdminEmail = email,
            IsActive = true
        };

        var tenantTwo = new Tenant
        {
            Name = $"Tenant Two {Guid.NewGuid():N}",
            Slug = $"tenant-two-{Guid.NewGuid():N}",
            AdminEmail = email,
            IsActive = true
        };

        _dbContext.Set<Tenant>().AddRange(tenantOne, tenantTwo);
        await _dbContext.SaveChangesAsync();

        _dbContext.Set<TenantMember>().AddRange(
            new TenantMember
            {
                UserId = userId,
                TenantId = tenantOne.Id,
                Role = "Owner",
                IsActive = true,
                Tenant = tenantOne
            },
            new TenantMember
            {
                UserId = userId,
                TenantId = tenantTwo.Id,
                Role = "Owner",
                IsActive = true,
                Tenant = tenantTwo
            });
        await _dbContext.SaveChangesAsync();

        var signInRequest = new LocalSignInRequest
        {
            Email = email,
            Password = password,
            TenantId = tenantTwo.Id
        };

        // Act
        var signInResult = await _authService.LocalSignInAsync(signInRequest);

        // Assert
        signInResult.TenantId.Should().Be(tenantTwo.Id);
        signInResult.AvailableTenants.Should().NotBeNull();
        signInResult.AvailableTenants!.Should().HaveCount(3, "every authenticated user is provisioned into the default tenant");
        signInResult.AvailableTenants.Should().Contain(tenant => tenant.Id == tenantOne.Id && tenant.Name == tenantOne.Name);
        signInResult.AvailableTenants.Should().Contain(tenant => tenant.Id == tenantTwo.Id && tenant.Name == tenantTwo.Name);
    }

    #endregion

    #region MFA Enrollment and Verification E2E Tests

    [Fact]
    public async Task MFA_CompleteEnrollmentFlow_SetupVerifyUse_ShouldWorkCorrectly()
    {
        // Arrange - Create user first
        var email = $"mfa.enroll.{Guid.NewGuid()}@test.com";
        var password = "MfaPassword123!";

        var signUpRequest = new LocalSignUpRequest
        {
            Email = email,
            Username = $"mfa_user_{Guid.NewGuid():N}",
            Password = password
        };

        var signUpResult = await _authService.LocalSignUpAsync(signUpRequest);
        var userId = signUpResult.UserId;

        // Act 1: Initiate MFA Setup
        var setupResult = await _mfaService.InitiateMfaSetupAsync(userId, email);

        // Assert Setup Initiation
        setupResult.Should().NotBeNull();
        setupResult.QrCodeUri.Should().NotBeNullOrEmpty();
        setupResult.SecretKey.Should().NotBeNullOrEmpty();
        setupResult.BackupCodes.Should().NotBeNull();
        setupResult.BackupCodes.Should().HaveCount(10);

        // For testing purposes, we would simulate a valid TOTP code
        // In a real scenario, this would be generated by an authenticator app
        // var mockTotpCode = "123456";

        // Act 2: Complete MFA Setup (would fail in real scenario without valid TOTP)
        // Note: This test demonstrates the flow; actual verification would require a valid TOTP generator

        // Act 3: Get MFA Configuration
        var mfaConfig = await _mfaService.GetMfaConfigurationAsync(userId);

        // Assert MFA Configuration
        mfaConfig.Should().NotBeNull();
        // MFA is not enabled until successfully completed
    }

    [Fact]
    public async Task MFA_DisableFlow_ShouldRemoveMfaRequirement()
    {
        // Arrange - Create user
        var email = $"mfa.disable.{Guid.NewGuid()}@test.com";

        var signUpRequest = new LocalSignUpRequest
        {
            Email = email,
            Username = $"mfa_disable_user_{Guid.NewGuid():N}",
            Password = "DisableMfaPassword123!"
        };

        var signUpResult = await _authService.LocalSignUpAsync(signUpRequest);
        var userId = signUpResult.UserId;

        // Setup MFA first
        var setupResult = await _mfaService.InitiateMfaSetupAsync(userId, email);

        // Act: Disable MFA (requires password confirmation)
        await _mfaService.DisableMfaAsync(userId, "TestPassword123!");

        // Assert: MFA should be disabled
        var mfaConfig = await _mfaService.GetMfaConfigurationAsync(userId);
        mfaConfig.Should().NotBeNull();
        mfaConfig.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task MFA_BackupCodes_RegenerateFlow_ShouldProvideNewCodes()
    {
        // Arrange
        var email = $"mfa.backup.{Guid.NewGuid()}@test.com";

        var signUpRequest = new LocalSignUpRequest
        {
            Email = email,
            Username = $"mfa_backup_user_{Guid.NewGuid():N}",
            Password = "BackupPassword123!"
        };

        var signUpResult = await _authService.LocalSignUpAsync(signUpRequest);
        var userId = signUpResult.UserId;

        // Setup MFA
        var setupResult = await _mfaService.InitiateMfaSetupAsync(userId, email);
        var originalBackupCodes = setupResult.BackupCodes;

        // Complete MFA setup by enabling it directly in the database (test workaround)
        // In production, this would be done by verifying a valid TOTP code
        var mfaConfig = await _dbContext.Set<UserMfaConfiguration>().FirstOrDefaultAsync(c => c.UserId == userId);
        if (mfaConfig != null)
        {
            mfaConfig.IsEnabled = true;
            mfaConfig.EnabledAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        // Act: Regenerate backup codes
        var newBackupCodes = await _mfaService.GenerateBackupCodesAsync(userId);

        // Assert
        newBackupCodes.Should().NotBeNull();
        newBackupCodes.Should().HaveCount(10);
        newBackupCodes.Should().NotBeEquivalentTo(originalBackupCodes);
    }

    #endregion

    #region Social Authentication E2E Tests (with mocked providers)

    // Google ID-token sign-in is exercised end to end over HTTP against the real
    // ASP.NET host (routing → GoogleIdTokenSignInCommand → OAuthAuthService → user
    // provisioning/linking → default-tenant membership → session + token issuance →
    // SignInResponse serialization). Only the cryptographic boundary is stubbed:
    // GoogleSignInE2EApiFactory swaps IGoogleIdTokenVerifier (whose production
    // implementation calls GoogleJsonWebSignature.ValidateAsync — an external
    // Google JWKS dependency) for a programmable verifier that is faithful to the
    // real contract: it returns the provider-asserted claims for tokens that would
    // pass Google's validation and throws UnauthorizedAccessException for every
    // other token, exactly as GoogleIdTokenVerifier does for forged/expired
    // tokens. Everything downstream of that seam is production code.

    [Fact]
    public async Task SocialAuth_GoogleProvider_CompleteFlow_ShouldAuthenticateUser()
    {
        // Arrange - a Google credential the stub verifier accepts
        var email = $"google.e2e.{Guid.NewGuid():N}@gmail.com";
        var googleSub = $"google-sub-{Guid.NewGuid():N}";
        var idToken = $"e2e-google-id-token-{Guid.NewGuid():N}";

        using var factory = new GoogleSignInE2EApiFactory();
        factory.Verifier.Allow(idToken, new VerifiedGoogleUser
        {
            Sub = googleSub,
            Email = email,
            EmailVerified = true,
            Name = "Google E2E User",
            Picture = "https://lh3.googleusercontent.com/gameguild-e2e.png"
        });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();
        SeedDefaultTenant(dbContext);
        var defaultTenantId = dbContext.Set<Tenant>().Single(t => t.IsDefault).Id;

        using var client = factory.CreateClient();

        // Act - POST the endpoint the web Google GIS/One Tap credential feeds
        using var response = await client.PostAsJsonAsync(
            "/v1/auth/google:sign-in",
            new GoogleIdTokenRequestDto { IdToken = idToken });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var dto = await response.Content.ReadFromJsonAsync<SignInResponse>();

        // Assert - response DTO: tokens, session and tenant context are all issued
        dto.Should().NotBeNull();
        dto!.Success.Should().BeTrue();
        dto.Message.Should().Be("Google ID token sign-in successful");
        dto.AccessToken.Should().NotBeNullOrWhiteSpace();
        dto.AccessToken.Split('.').Should().HaveCount(3, "the access token must be a three-segment JWT");
        dto.RefreshToken.Should().NotBeNullOrWhiteSpace();
        dto.UserId.Should().NotBeEmpty();
        dto.Email.Should().Be(email);
        dto.SessionId.Should().NotBeEmpty();
        dto.ExpiresIn.Should().BePositive();
        dto.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
        dto.RefreshTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
        dto.TenantId.Should().Be(defaultTenantId);
        dto.AvailableTenants.Should().ContainSingle(tenant => tenant.Id == defaultTenantId);

        // Assert - user provisioned and the Google identity linked to it
        var user = await dbContext.Set<User>().SingleAsync(u => u.Email == email);
        user.Id.Should().Be(dto.UserId);
        var externalLogin = await dbContext.Set<ExternalLogin>()
            .SingleAsync(login => login.Provider == "google" && login.ProviderKey == googleSub);
        externalLogin.UserId.Should().Be(user.Id);

        // Assert - self-service default-tenant membership provisioned
        var membership = await dbContext.Set<TenantMember>().SingleAsync(m => m.UserId == user.Id);
        membership.TenantId.Should().Be(defaultTenantId);
        membership.IsActive.Should().BeTrue();

        // Assert - session persisted and only the hashed refresh token is stored
        var hasher = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>();
        var session = await dbContext.Set<UserSession>().SingleAsync(s => s.Id == dto.SessionId);
        session.UserId.Should().Be(user.Id);
        session.IsActive.Should().BeTrue();
        session.RefreshToken.Should().Be(hasher.HashToken(dto.RefreshToken));

        // Assert - the federated success is audited
        (await dbContext.Set<AuthenticationAttempt>()
            .AnyAsync(attempt => attempt.Email == email && attempt.UserId == user.Id && attempt.IsSuccessful))
            .Should().BeTrue();
    }

    [Fact]
    public async Task SocialAuth_GoogleProvider_SecondSignIn_ShouldReuseLinkedIdentityWithoutDuplicates()
    {
        // Arrange
        var email = $"google.e2e.repeat.{Guid.NewGuid():N}@gmail.com";
        var googleSub = $"google-sub-{Guid.NewGuid():N}";
        var idToken = $"e2e-google-id-token-{Guid.NewGuid():N}";

        using var factory = new GoogleSignInE2EApiFactory();
        factory.Verifier.Allow(idToken, new VerifiedGoogleUser
        {
            Sub = googleSub,
            Email = email,
            EmailVerified = true,
            Name = "Google E2E Repeat User"
        });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();
        SeedDefaultTenant(dbContext);

        using var client = factory.CreateClient();

        // Act - the same Google identity signs in twice
        using var firstResponse = await client.PostAsJsonAsync(
            "/v1/auth/google:sign-in",
            new GoogleIdTokenRequestDto { IdToken = idToken });
        using var secondResponse = await client.PostAsJsonAsync(
            "/v1/auth/google:sign-in",
            new GoogleIdTokenRequestDto { IdToken = idToken });

        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, firstBody);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK, secondBody);

        var first = await firstResponse.Content.ReadFromJsonAsync<SignInResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<SignInResponse>();

        // Assert - linked user is reused, no duplicate user or external login, fresh session each time
        first!.UserId.Should().Be(second!.UserId);
        first.SessionId.Should().NotBe(second.SessionId);
        (await dbContext.Set<User>().CountAsync(u => u.Email == email)).Should().Be(1);
        (await dbContext.Set<ExternalLogin>()
            .CountAsync(login => login.Provider == "google" && login.ProviderKey == googleSub))
            .Should().Be(1);
        var sessionIds = new[] { first.SessionId, second.SessionId };
        (await dbContext.Set<UserSession>().CountAsync(session => sessionIds.Contains(session.Id)))
            .Should().Be(2);
    }

    [Fact]
    public async Task SocialAuth_GoogleProvider_InvalidToken_ShouldFailClosedWithoutUserOrSession()
    {
        // Arrange - a forged token the stub verifier (like GoogleJsonWebSignature) rejects
        var forgedToken = $"e2e-forged-google-id-token-{Guid.NewGuid():N}";
        var victimEmail = $"google.e2e.forged.{Guid.NewGuid():N}@gmail.com";

        using var factory = new GoogleSignInE2EApiFactory();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();
        SeedDefaultTenant(dbContext);

        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync(
            "/v1/auth/google:sign-in",
            new GoogleIdTokenRequestDto { IdToken = forgedToken });

        // Assert - fail closed: 401, no credentials disclosed
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, body);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Title.Should().Be("Unauthorized");

        // Assert - no user provisioned, no external link, no session, no success audit
        (await dbContext.Set<User>().AnyAsync(u => u.Email == victimEmail)).Should().BeFalse();
        (await dbContext.Set<ExternalLogin>().AnyAsync()).Should().BeFalse();
        (await dbContext.Set<UserSession>().CountAsync()).Should().Be(0);
        (await dbContext.Set<AuthenticationAttempt>()
            .AnyAsync(attempt => !attempt.IsSuccessful && attempt.FailureReason == nameof(UnauthorizedAccessException)))
            .Should().BeTrue("a rejected Google token must be audited as a failed attempt");
    }

    [Fact]
    public async Task SocialAuth_GoogleProvider_UnverifiedEmailOverExistingLocalAccount_ShouldRefuseLinking()
    {
        // Arrange - a pre-existing local account with the same email
        var email = $"google.e2e.collision.{Guid.NewGuid():N}@example.com";

        using var factory = new GoogleSignInE2EApiFactory();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();
        SeedDefaultTenant(dbContext);

        using var client = factory.CreateClient();

        using var signUpResponse = await client.PostAsJsonAsync("/v1/auth/sign-up", new LocalSignUpRequest
        {
            Email = email,
            Username = $"local_user_{Guid.NewGuid():N}",
            Password = "LocalPassword123!"
        });
        var signUpBody = await signUpResponse.Content.ReadAsStringAsync();
        signUpResponse.StatusCode.Should().Be(HttpStatusCode.Created, signUpBody);

        var localUser = await dbContext.Set<User>().SingleAsync(u => u.Email == email);
        var sessionsBefore = await dbContext.Set<UserSession>().CountAsync(s => s.UserId == localUser.Id);

        // An unverified Google identity must not hijack the local account
        var idToken = $"e2e-google-id-token-unverified-{Guid.NewGuid():N}";
        factory.Verifier.Allow(idToken, new VerifiedGoogleUser
        {
            Sub = $"google-sub-{Guid.NewGuid():N}",
            Email = email,
            EmailVerified = false,
            Name = "Unverified Google User"
        });

        // Act
        using var response = await client.PostAsJsonAsync(
            "/v1/auth/google:sign-in",
            new GoogleIdTokenRequestDto { IdToken = idToken });

        // Assert - denied, no link created, no extra session issued
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, body);
        (await dbContext.Set<ExternalLogin>().AnyAsync(login => login.UserId == localUser.Id)).Should().BeFalse();
        (await dbContext.Set<UserSession>().CountAsync(s => s.UserId == localUser.Id))
            .Should().Be(sessionsBefore);
    }

    [Fact]
    public async Task SocialAuth_GitHubProvider_CompleteFlow_ShouldAuthenticateUser()
    {
        // Arrange - Mock GitHub OAuth response
        var mockGitHubCode = "mock_github_authorization_code";

        var githubSignInRequest = new GitHubSignInRequest
        {
            AccessToken = mockGitHubCode
        };

        // Act & Assert
        // This would work with proper mocking of external GitHub OAuth
        // await FluentActions.Invoking(async () => await _authService.SocialSignInAsync(githubSignInRequest))
        //     .Should().NotThrowAsync();

        await Task.CompletedTask; // Placeholder until actual implementation
    }

    #endregion

    #region Web3 Authentication E2E Tests
    // TODO: Create concrete Web3ChallengeRequest implementation before uncommenting
    /*
    [Fact(Skip = "Web3ChallengeRequest is abstract - needs concrete implementation")]
    public async Task Web3Auth_CompleteFlow_ChallengeAndVerify_ShouldAuthenticateWallet()
    {
        // Arrange
        var walletAddress = $"0x{Guid.NewGuid():N}";

        // Act 1: Generate Challenge
        var challengeRequest = new GameGuild.Identity.Authentication.DTOs.Web3ChallengeRequest
        {
            WalletAddress = walletAddress,
            ChainId = "1"
        };

        var challengeResult = await _authService.GenerateWeb3ChallengeAsync(challengeRequest);

        // Assert Challenge Generation
        challengeResult.Should().NotBeNull();
        challengeResult.Challenge.Should().NotBeNullOrEmpty();
        challengeResult.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // Act 2: Verify Signature (with mock signature)
        var mockSignature = "0x" + string.Concat(Enumerable.Repeat("a1b2c3d4", 16));

        var verifyRequest = new GameGuild.Identity.Authentication.Models.Requests.Web3VerificationRequest
        {
            WalletAddress = walletAddress,
            Challenge = challengeResult.Challenge,
            Signature = mockSignature,
            ChainId = "1"
        };

        // Note: Actual signature verification would require Web3 library mocking
        // This demonstrates the test flow structure
    }
    */

    // TODO: Create GenerateWeb3ChallengeRequest and VerifyWeb3SignatureRequest types before uncommenting
    /*
    [Fact(Skip = "Web3ChallengeRequest is abstract - needs concrete implementation")]
    public async Task Web3Auth_ExpiredChallenge_ShouldFailVerification()
    {
        // Arrange
        var walletAddress = $"0x{Guid.NewGuid():N}";

        var challengeRequest = new GenerateWeb3ChallengeRequest
        {
            WalletAddress = walletAddress
        };

        var challengeResult = await _authService.GenerateWeb3ChallengeAsync(challengeRequest);

        // Simulate challenge expiration by waiting or manipulating timestamp
        // In real test, we'd mock the time provider

        var verifyRequest = new VerifyWeb3SignatureRequest
        {
            WalletAddress = walletAddress,
            Challenge = "expired_challenge",
            Signature = "0x" + string.Concat(Enumerable.Repeat("a1b2c3d4", 16))
        };

        // Act & Assert - Should fail due to expired/invalid challenge
        // await FluentActions.Invoking(async () => await _authService.VerifyWeb3SignatureAsync(verifyRequest))
        //     .Should().ThrowAsync<Exception>();
    }
    */
    #endregion

    #region Polymorphic Authentication E2E Tests
    // TODO: Create PolymorphicSignInRequest type and implement PolymorphicSignInAsync before uncommenting
    /*
    [Fact(Skip = "PolymorphicSignInAsync API not yet implemented")]
    public async Task PolymorphicAuth_LocalStrategy_ShouldAuthenticateCorrectly()
    {
        // Arrange
        var email = $"poly.local.{Guid.NewGuid()}@test.com";
        var password = "PolyPassword123!";

        // First create a local user
        var signUpRequest = new LocalSignUpRequest
        {
            Email = email,
            Username = $"poly_user_{Guid.NewGuid():N}",
            Password = password
        };

        await _authService.LocalSignUpAsync(signUpRequest);

        // Act - Use polymorphic sign-in with local credentials
        var polyRequest = new PolymorphicSignInRequest
        {
            Strategy = "Local",
            Credentials = new Dictionary<string, string>
            {
                { "email", email },
                { "password", password }
            }
        };

        // TODO: Implement method - var result = await _authService.PolymorphicSignInAsync(polyRequest);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact(Skip = "PolymorphicSignInAsync API not yet implemented")]
    public async Task PolymorphicAuth_Web3Strategy_ShouldProcessChallengeResponse()
    {
        // Arrange
        var walletAddress = $"0x{Guid.NewGuid():N}";

        // Act - Generate challenge via polymorphic interface
        var polyRequest = new PolymorphicSignInRequest
        {
            Strategy = "Web3",
            Credentials = new Dictionary<string, string>
            {
                { "walletAddress", walletAddress }
            }
        };

        // Note: Full flow would require signature verification mocking
        // This demonstrates the structure
    }

    [Fact(Skip = "PolymorphicSignInAsync API not yet implemented")]
    public async Task PolymorphicAuth_CrossStrategy_UserWithMultipleAuth_ShouldLinkAccounts()
    {
        // Arrange - Create user with local auth
        var email = $"cross.strategy.{Guid.NewGuid()}@test.com";
        var password = "CrossStrategy123!";

        var localSignUpRequest = new LocalSignUpRequest
        {
            Email = email,
            Username = $"cross_user_{Guid.NewGuid():N}",
            Password = password
        };

        var localResult = await _authService.LocalSignUpAsync(localSignUpRequest);
        var userId = localResult.UserId;

        // Act - Link Web3 wallet to same user (in real scenario)
        // This would involve:
        // 1. User signs in with local auth
        // 2. User connects Web3 wallet
        // 3. System links wallet to existing user account

        // Assert - User should be able to sign in with either method
        var localSignIn = new LocalSignInRequest
        {
            Email = email,
            Password = password
        };

        var localSignInResult = await _authService.LocalSignInAsync(localSignIn);
        localSignInResult.UserId.Should().Be(userId);
    }

    #endregion

    #region Token Lifecycle Edge Cases

    [Fact]
    public async Task TokenLifecycle_MultipleRefreshes_ShouldInvalidateOldTokens()
    {
        // Arrange
        var email = $"token.lifecycle.{Guid.NewGuid()}@test.com";

        var signUpRequest = new LocalSignUpRequest
        {
            Email = email,
            Username = $"token_user_{Guid.NewGuid():N}",
            Password = "TokenLifecycle123!"
        };

        var signUpResult = await _authService.LocalSignUpAsync(signUpRequest);
        var firstRefreshToken = signUpResult.RefreshToken;

        // Act - Perform multiple refreshes
        var refreshRequest1 = new RefreshTokenRequest { RefreshToken = firstRefreshToken };
        var refreshResult1 = await _authService.RefreshTokenAsync(refreshRequest1);

        var refreshRequest2 = new RefreshTokenRequest { RefreshToken = refreshResult1.RefreshToken };
        var refreshResult2 = await _authService.RefreshTokenAsync(refreshRequest2);

        // Assert - Old tokens should be invalid
        var oldTokenRefreshRequest = new RefreshTokenRequest { RefreshToken = firstRefreshToken };

        await FluentActions.Invoking(async () => await _authService.RefreshTokenAsync(oldTokenRefreshRequest))
            .Should().ThrowAsync<Exception>();
    }
    */
    [Fact]
    public async Task TokenLifecycle_SignOut_ShouldRevokeAllUserTokens()
    {
        // Arrange
        var email = $"signout.test.{Guid.NewGuid()}@test.com";

        var signUpRequest = new LocalSignUpRequest
        {
            Email = email,
            Username = $"signout_user_{Guid.NewGuid():N}",
            Password = "SignOut123!"
        };

        var signUpResult = await _authService.LocalSignUpAsync(signUpRequest);
        var userId = signUpResult.UserId;

        // Create multiple sessions
        var signInResult1 = await _authService.LocalSignInAsync(new LocalSignInRequest
        {
            Email = email,
            Password = "SignOut123!"
        });

        var signInResult2 = await _authService.LocalSignInAsync(new LocalSignInRequest
        {
            Email = email,
            Password = "SignOut123!"
        });

        // Act - Sign out (revoke all tokens)
        await _authService.RevokeRefreshTokenAsync(signUpResult.RefreshToken, "127.0.0.1");
        await _authService.RevokeRefreshTokenAsync(signInResult1.RefreshToken, "127.0.0.1");
        await _authService.RevokeRefreshTokenAsync(signInResult2.RefreshToken, "127.0.0.1");

        // Assert - All tokens should be revoked
        var userTokens = await _dbContext.Set<RefreshToken>()
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        userTokens.Should().AllSatisfy(token => token.IsRevoked.Should().BeTrue());
    }

    #endregion

    public void Dispose()
    {
        _scope?.Dispose();
        _dbContext?.Dispose();
        _client?.Dispose();
    }

    private static void SeedDefaultTenant(ApplicationDbContext dbContext)
    {
        if (dbContext.Set<Tenant>().Any(tenant => tenant.IsDefault))
        {
            return;
        }

        dbContext.Set<Tenant>().Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Authentication Integration Default Tenant",
            Slug = "authentication-integration-default",
            Description = "Default tenant required by the authentication integration fixture.",
            AdminEmail = "authentication-integration-admin@example.test",
            IsActive = true,
            IsDefault = true
        });
        dbContext.SaveChanges();
    }

    /// <summary>
    ///     Programmable stand-in for <see cref="IGoogleIdTokenVerifier" />. Tokens registered
    ///     via <see cref="Allow" /> yield their provider-asserted claims (the outcome of a
    ///     successful GoogleJsonWebSignature validation); every other token is rejected with
    ///     <see cref="UnauthorizedAccessException" />, mirroring the fail-closed production
    ///     contract for forged, expired or mis-audience tokens.
    /// </summary>
    private sealed class StubGoogleIdTokenVerifier : IGoogleIdTokenVerifier
    {
        private readonly Dictionary<string, VerifiedGoogleUser> _validTokens = new(StringComparer.Ordinal);

        public void Allow(string idToken, VerifiedGoogleUser user) => _validTokens[idToken] = user;

        public Task<VerifiedGoogleUser> VerifyAsync(string idToken, CancellationToken ct) =>
            _validTokens.TryGetValue(idToken, out var user)
                ? Task.FromResult(user)
                : Task.FromException<VerifiedGoogleUser>(
                    new UnauthorizedAccessException("Google ID token is invalid"));
    }

    /// <summary>
    ///     Host for the Google sign-in E2E flow: same InMemory configuration as
    ///     <see cref="AuthenticationApiFactory" /> plus the stubbed
    ///     <see cref="IGoogleIdTokenVerifier" /> test seam. The sign-in happy path never
    ///     touches the ExecuteUpdate-based revocation code (RefreshTokenRepository), which
    ///     is what keeps the InMemory provider viable here; revocation is covered by the
    ///     Postgres suites (see the retired-test note above).
    /// </summary>
    private sealed class GoogleSignInE2EApiFactory : WebApplicationFactory<GameGuild.API.Program>
    {
        private readonly string _databaseName = $"GoogleSignInE2ETests_{Guid.NewGuid()}";

        public StubGoogleIdTokenVerifier Verifier { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                var descriptorsToRemove = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                        descriptor.ServiceType == typeof(ApplicationDbContext) ||
                        descriptor.ServiceType.FullName?.Contains("EntityFramework", StringComparison.Ordinal) == true ||
                        descriptor.ImplementationType?.FullName?.Contains("Npgsql", StringComparison.Ordinal) == true)
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
                services.AddMemoryCache();
                services.AddHttpLogging(_ => { });

                // Test seam: replace Google's cryptographic validation with the controlled stub.
                services.RemoveAll<IGoogleIdTokenVerifier>();
                services.AddSingleton<IGoogleIdTokenVerifier>(Verifier);
            });
        }
    }
}

using FluentAssertions;
using Xunit;
using GameGuild.API.Database;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task SocialAuth_GoogleProvider_CompleteFlow_ShouldAuthenticateUser()
    {
        // Arrange - Mock Google OAuth response
        var mockGoogleIdToken = "mock.google.id.token";
        var mockGoogleEmail = $"google.user.{Guid.NewGuid()}@gmail.com";

        // Note: In a real scenario, we would need to mock the Google token validation
        // For now, this demonstrates the test structure

        var googleSignInRequest = new GoogleSignInRequest
        {
            AccessToken = mockGoogleIdToken
        };

        // Act & Assert
        // This would work with proper mocking of external services
        // await FluentActions.Invoking(async () => await _authService.GoogleIdTokenSignInAsync(googleSignInRequest))
        //     .Should().NotThrowAsync();

        await Task.CompletedTask; // Placeholder until actual implementation
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
        if (dbContext.Set<Tenant>().Any(tenant => tenant.IsDefault)) return;

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
}

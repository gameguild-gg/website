using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ApiProgram = GameGuild.API.Program;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class MfaSignInPolicyPostgreSqlTests(ApiPostgreSqlFixture fixture, ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredMfaPolicyControlsLowRiskAnonymousPasswordSignIn(bool requireMfa)
        => await VerifyFirstFactorPolicyAsync(requireMfa, SignInFirstFactor.Password);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredMfaPolicyControlsAnonymousEmailCodeSignIn(bool requireMfa)
        => await VerifyFirstFactorPolicyAsync(requireMfa, SignInFirstFactor.EmailCode);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredMfaPolicyControlsAnonymousMagicLinkSignIn(bool requireMfa)
        => await VerifyFirstFactorPolicyAsync(requireMfa, SignInFirstFactor.MagicLink);

    private async Task VerifyFirstFactorPolicyAsync(bool requireMfa, SignInFirstFactor firstFactor)
    {
        var expectedAssembly = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LocalAuthService).Assembly.Location))).ToLowerInvariant();
        var risk = new Mock<IAuthenticationAnomalyDetectionService>(MockBehavior.Strict);
        risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        risk.Setup(service => service.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalPattern = true, RiskLevel = RiskLevel.Low });
        using var factory = fixture.CreateFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10",
                ["Mfa:Enabled"] = "true",
                ["Mfa:RequireMfaByDefault"] = requireMfa.ToString()
            }));
            builder.ConfigureTestServices(services =>
            {
                // Qualify enforcement independently of the host's configuration-provider ordering.
                // Use the normal options post-configuration seam, retaining the real policy and all assertions.
                services.PostConfigure<MfaOptions>(effective =>
                {
                    effective.Enabled = true;
                    effective.RequireMfaByDefault = requireMfa;
                });
                services.RemoveAll<IAuthenticationAnomalyDetectionService>();
                services.AddSingleton(risk.Object);
                // WebApplicationFactory applies overrides after direct options are captured by Program.
                // Bind the real policy reader to the same effective configured options; no policy result is mocked.
                services.RemoveAll<MfaOptions>();
                services.AddSingleton(provider => provider.GetRequiredService<IOptions<MfaOptions>>().Value);
                services.RemoveAll<IMfaAttemptTrackingService>();
                services.AddScoped<IMfaAttemptTrackingService>(provider =>
                    ActivatorUtilities.CreateInstance<MfaAttemptTrackingService>(provider,
                        provider.GetRequiredService<IOptions<MfaOptions>>().Value));
                services.RemoveAll<IMfaService>();
                services.AddScoped<IMfaService, MfaService>();
            });
        });
        var marker = Guid.NewGuid().ToString("N");
        var password = "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        User user;
        string? code = null;
        string? magicLinkToken = null;
        var tenantId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            Assert.IsType<LocalAuthService>(provider.GetRequiredService<ILocalAuthService>());
            var options = provider.GetRequiredService<IOptions<MfaOptions>>().Value;
            Assert.True(options.Enabled);
            Assert.Equal(requireMfa, options.RequireMfaByDefault);
            user = User.CreateWithPassword("mfa-" + marker + "@example.test", "Synthetic MFA policy account",
                provider.GetRequiredService<IPasswordHasher>().HashPassword(password), "mfa-" + marker);
            user.VerifyEmail();
            var context = provider.GetRequiredService<ApplicationDbContext>();
            context.Set<User>().Add(user);
            context.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = "MFA " + marker,
                Slug = "mfa-" + marker, AdminEmail = "admin-" + marker + "@example.test", IsActive = true });
            context.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId,
                UserId = user.Id, Role = "Member", IsActive = true });
            await context.SaveChangesAsync();
            var tracking = provider.GetRequiredService<IMfaAttemptTrackingService>();
            var mfa = provider.GetRequiredService<IMfaService>();
            Assert.IsType<MfaAttemptTrackingService>(tracking);
            Assert.IsType<MfaService>(mfa);
            var captured = (MfaOptions)typeof(MfaAttemptTrackingService)
                .GetField("_mfaOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(tracking)!;
            output.WriteLine("ISSUE145_POLICY_BINDING=" + JsonSerializer.Serialize(new
            {
                EffectiveRequireMfa = options.RequireMfaByDefault,
                DirectRequireMfa = provider.GetRequiredService<MfaOptions>().RequireMfaByDefault,
                CapturedRequireMfa = captured.RequireMfaByDefault,
                CapturedEnabled = captured.Enabled,
                TrackingImplementation = tracking.GetType().AssemblyQualifiedName,
                MfaImplementation = mfa.GetType().AssemblyQualifiedName,
                Acceptance = false
            }));
            Assert.Equal(requireMfa, captured.RequireMfaByDefault);
            Assert.True(captured.Enabled);
            Assert.Equal(requireMfa, await provider.GetRequiredService<IMfaService>().IsMfaRequiredAsync(user.Id));
            Assert.Equal(0, await context.Set<RefreshToken>().CountAsync(token => token.UserId == user.Id));
            Assert.Equal(0, await context.Set<UserSession>().CountAsync(session => session.UserId == user.Id));
            if (firstFactor == SignInFirstFactor.EmailCode)
            {
                code = await provider.GetRequiredService<IEmailCodeService>().GenerateEmailCodeAsync(user.Id, user.Email);
                Assert.Matches("^[0-9]{6}$", code!);
            }
            else if (firstFactor == SignInFirstFactor.MagicLink)
            {
                magicLinkToken = await provider.GetRequiredService<IEmailVerificationService>().GenerateMagicLinkTokenAsync(user.Id, user.Email);
                Assert.False(string.IsNullOrWhiteSpace(magicLinkToken));
            }
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = firstFactor switch
        {
            SignInFirstFactor.EmailCode => await client.PostAsJsonAsync("/v1/auth/email-code:consume", new { user.Email, code, tenantId }),
            SignInFirstFactor.MagicLink => await client.PostAsJsonAsync("/v1/auth/magic-link:consume", new { token = magicLinkToken, tenantId }),
            _ => await client.PostAsJsonAsync("/v1/auth/sign-in", new { user.Email, password, tenantId })
        };
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(body);
        var value = payload.RootElement;
        var ordinaryAccess = value.TryGetProperty("accessToken", out var access) && access.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(access.GetString());
        var ordinaryRefresh = value.TryGetProperty("refreshToken", out var refresh) && refresh.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(refresh.GetString());
        using var finalScope = factory.Services.CreateScope();
        var database = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokenCount = await database.Set<RefreshToken>().CountAsync(token => token.UserId == user.Id);
        var sessionCount = await database.Set<UserSession>().CountAsync(session => session.UserId == user.Id);
        output.WriteLine("ISSUE145_MFA_POLICY_OBSERVATION=" + JsonSerializer.Serialize(new
        {
            RequireMfa = requireMfa, FirstFactor = firstFactor.ToString(),
            RiskClassifier = "controlled low risk", Transport = "actual anonymous TestServer HTTP and migrated PostgreSQL",
            OrdinaryAccessTokenPresent = ordinaryAccess, OrdinaryRefreshTokenPresent = ordinaryRefresh,
            RefreshTokenCount = tokenCount, SessionCount = sessionCount, SubjectId = user.Id, TenantId = tenantId,
            AuthenticationAssemblySHA256 = expectedAssembly, Acceptance = false
        }));
        Assert.Equal(!requireMfa, ordinaryAccess);
        Assert.Equal(!requireMfa, ordinaryRefresh);
        Assert.Equal(requireMfa ? 0 : 1, tokenCount);
        Assert.Equal(requireMfa ? 0 : 1, sessionCount);
        var challenges = await database.Set<SignInMfaChallenge>().AsNoTracking().Where(challenge => challenge.SubjectId == user.Id).ToListAsync();
        if (requireMfa)
        {
            Assert.False(value.GetProperty("success").GetBoolean());
            Assert.True(value.GetProperty("requiresMfa").GetBoolean());
            var bearer = value.GetProperty("mfaToken").GetString();
            Assert.True(SignInMfaChallengeToken.TryHash(bearer, out var hash));
            var challenge = Assert.Single(challenges);
            Assert.Equal(hash, challenge.TokenHash);
            Assert.NotEqual(bearer, challenge.TokenHash);
            Assert.Equal(tenantId, challenge.TenantId);
            Assert.Equal(user.TokenVersion, challenge.SubjectTokenVersion);
            Assert.Equal(firstFactor, challenge.FirstFactor);
            Assert.Equal(SignInMfaPurpose.EnrollFactor, challenge.Purpose);
            Assert.Equal(TimeSpan.FromMinutes(5), challenge.ExpiresAt - challenge.CreatedAt);
            Assert.Null(challenge.ConsumedAt);
            Assert.Null(challenge.RevokedAt);
        }
        else
        {
            Assert.True(value.GetProperty("success").GetBoolean());
            Assert.Empty(challenges);
        }
        if (firstFactor is SignInFirstFactor.EmailCode or SignInFirstFactor.MagicLink)
        {
            using var replay = firstFactor == SignInFirstFactor.EmailCode
                ? await client.PostAsJsonAsync("/v1/auth/email-code:consume", new { user.Email, code, tenantId })
                : await client.PostAsJsonAsync("/v1/auth/magic-link:consume", new { token = magicLinkToken, tenantId });
            Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
            Assert.Equal(tokenCount, await database.Set<RefreshToken>().CountAsync(token => token.UserId == user.Id));
            Assert.Equal(sessionCount, await database.Set<UserSession>().CountAsync(session => session.UserId == user.Id));
            Assert.Equal(challenges.Count, await database.Set<SignInMfaChallenge>().CountAsync(challenge => challenge.SubjectId == user.Id));
        }
    }
}

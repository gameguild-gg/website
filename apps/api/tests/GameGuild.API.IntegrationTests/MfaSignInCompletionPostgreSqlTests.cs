using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class MfaSignInCompletionPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task EnrolledPasswordChallengeCanCompleteWithBackupCodeAndPreserveMfaOnRefresh()
    {
        var risk = new Mock<IAuthenticationAnomalyDetectionService>(MockBehavior.Strict);
        risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        risk.Setup(service => service.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalPattern = true, RiskLevel = RiskLevel.Low });
        using var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<MfaOptions>(options => { options.Enabled = true; options.RequireMfaByDefault = true; });
            services.RemoveAll<MfaOptions>();
            services.AddSingleton(provider => provider.GetRequiredService<IOptions<MfaOptions>>().Value);
            services.RemoveAll<IMfaAttemptTrackingService>();
            services.AddScoped<IMfaAttemptTrackingService>(provider => ActivatorUtilities.CreateInstance<MfaAttemptTrackingService>(provider,
                provider.GetRequiredService<IOptions<MfaOptions>>().Value));
            services.RemoveAll<IMfaService>();
            services.AddScoped<IMfaService, MfaService>();
            services.RemoveAll<IAuthenticationAnomalyDetectionService>();
            services.AddSingleton(risk.Object);
        }));
        var marker = Guid.NewGuid().ToString("N");
        var password = "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        var backupCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
        var tenantId = Guid.NewGuid();
        User user;
        using (var scope = factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            Assert.IsType<LocalAuthService>(provider.GetRequiredService<ILocalAuthService>());
            Assert.IsType<SignInMfaService>(provider.GetRequiredService<ISignInMfaService>());
            user = User.CreateWithPassword("mfa-complete-" + marker + "@example.test", "Synthetic MFA completion account",
                provider.GetRequiredService<IPasswordHasher>().HashPassword(password), "mfa-complete-" + marker);
            user.VerifyEmail();
            var context = provider.GetRequiredService<ApplicationDbContext>();
            context.Set<User>().Add(user);
            context.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = "MFA " + marker, Slug = "mfa-complete-" + marker,
                AdminEmail = "admin-" + marker + "@example.test", IsActive = true });
            context.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId,
                UserId = user.Id, Role = "Member", IsActive = true });
            context.Set<UserMfaConfiguration>().Add(new UserMfaConfiguration { Id = Guid.NewGuid(), UserId = user.Id,
                IsEnabled = true, IsSetupComplete = true,
                BackupCodes = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(backupCode))) });
            await context.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var pendingResponse = await client.PostAsJsonAsync("/v1/auth/sign-in", new { user.Email, password, tenantId });
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        using var pendingJson = JsonDocument.Parse(await pendingResponse.Content.ReadAsStringAsync());
        var pending = pendingJson.RootElement;
        Assert.False(pending.GetProperty("success").GetBoolean());
        Assert.True(pending.GetProperty("requiresMfa").GetBoolean());
        Assert.True(string.IsNullOrEmpty(pending.GetProperty("accessToken").GetString()));
        Assert.True(string.IsNullOrEmpty(pending.GetProperty("refreshToken").GetString()));
        var bearer = pending.GetProperty("mfaToken").GetString();
        Assert.True(SignInMfaChallengeToken.TryHash(bearer, out _));

        using var completion = await client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete", new { mfaToken = bearer, code = backupCode, method = "BackupCode" });
        Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        using var completedJson = JsonDocument.Parse(await completion.Content.ReadAsStringAsync());
        var completed = completedJson.RootElement;
        Assert.True(completed.GetProperty("success").GetBoolean());
        Assert.False(completed.GetProperty("requiresMfa").GetBoolean());
        Assert.Equal(user.Id, completed.GetProperty("userId").GetGuid());
        Assert.Equal(tenantId, completed.GetProperty("tenantId").GetGuid());
        var access = new JwtSecurityTokenHandler().ReadJwtToken(completed.GetProperty("accessToken").GetString());
        Assert.Contains(access.Claims, claim => claim.Type == "amr" && claim.Value == "mfa");
        Assert.Contains(access.Claims, claim => claim.Type == "mfa_verified" && claim.Value == "true");
        var verifiedAt = Assert.Single(access.Claims, claim => claim.Type == "mfa_time").Value;
        var authenticatedAt = Assert.Single(access.Claims, claim => claim.Type == "auth_time").Value;

        using var replay = await client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete", new { mfaToken = bearer, code = backupCode, method = "BackupCode" });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using var refreshResponse = await client.PostAsJsonAsync("/v1/auth/tokens:refresh", new
        {
            refreshToken = completed.GetProperty("refreshToken").GetString(), tenantId
        });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        using var refreshJson = JsonDocument.Parse(await refreshResponse.Content.ReadAsStringAsync());
        Assert.True(refreshJson.RootElement.GetProperty("success").GetBoolean());
        var renewed = new JwtSecurityTokenHandler().ReadJwtToken(refreshJson.RootElement.GetProperty("accessToken").GetString());
        Assert.Contains(renewed.Claims, claim => claim.Type == "amr" && claim.Value == "mfa");
        Assert.Equal(verifiedAt, Assert.Single(renewed.Claims, claim => claim.Type == "mfa_time").Value);
        Assert.Equal(authenticatedAt, Assert.Single(renewed.Claims, claim => claim.Type == "auth_time").Value);
        Assert.Equal(completed.GetProperty("sessionId").GetGuid(), refreshJson.RootElement.GetProperty("sessionId").GetGuid());
        using var finalScope = factory.Services.CreateScope();
        var database = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await database.Set<UserSession>().CountAsync(session => session.UserId == user.Id));
        Assert.Equal(2, await database.Set<RefreshToken>().CountAsync(token => token.UserId == user.Id));
        var challenge = await database.Set<SignInMfaChallenge>().AsNoTracking().SingleAsync(value => value.SubjectId == user.Id);
        Assert.NotNull(challenge.ConsumedAt);
        Assert.Equal(MfaMethod.BackupCode, challenge.VerificationMethod);
    }
}

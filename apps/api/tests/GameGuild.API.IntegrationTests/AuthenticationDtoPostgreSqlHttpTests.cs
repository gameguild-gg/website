using System.Net;
using System.Net.Http.Json;
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
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AuthenticationDtoPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task SignupReturnsCompleteNameAndPersistedIdentity()
    {
        using var client = fixture.Factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N");
        var name = $"Ana Maria {marker}";
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-up", new
        {
            email = $"dto-signup-{marker}@example.test", password = CreateSyntheticPassword(), username = name
        });
        using var payload = await ReadResponseAsync(response, HttpStatusCode.Created);
        var result = payload.RootElement;
        var id = result.GetProperty("userId").GetGuid();
        Assert.Equal("Ana", result.GetProperty("user").GetProperty("firstName").GetString());
        Assert.Equal($"Maria {marker}", result.GetProperty("user").GetProperty("lastName").GetString());
        Assert.Equal(id, result.GetProperty("user").GetProperty("id").GetGuid());
        await AssertCompletedTokensAsync(result, fixture.Factory);
        using var scope = fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await context.Set<User>().SingleAsync(user => user.Id == id);
        Assert.Equal(persisted.Email, result.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal(persisted.Username, result.GetProperty("user").GetProperty("username").GetString());
        // PostgreSQL persists microseconds; the freshly tracked signup entity can retain sub-microsecond ticks.
        Assert.InRange(Math.Abs((persisted.CreatedAt - result.GetProperty("user").GetProperty("createdAt").GetDateTime()).Ticks), 0, 9);
        Assert.Equal(name, persisted.Name);
    }

    [Fact]
    public async Task ActualLoginAndRefreshPreserveRepositoryProfileAndTokenMetadata()
    {
        using var factory = WithRisk(RiskLevel.Low);
        var account = await CreateAccountAsync(factory);
        using var client = factory.CreateClient();
        using var loginResponse = await client.PostAsJsonAsync("/v1/auth/sign-in", new { account.Email, account.Password });
        using var login = await ReadResponseAsync(loginResponse, HttpStatusCode.OK);
        AssertProfile(login.RootElement, account.User);
        await AssertCompletedTokensAsync(login.RootElement, factory);
        var firstRefresh = login.RootElement.GetProperty("refreshToken").GetString();
        using var refreshResponse = await client.PostAsJsonAsync("/v1/auth/tokens:refresh", new { refreshToken = firstRefresh });
        using var refresh = await ReadResponseAsync(refreshResponse, HttpStatusCode.OK);
        AssertProfile(refresh.RootElement, account.User);
        await AssertCompletedTokensAsync(refresh.RootElement, factory);
        Assert.NotEqual(firstRefresh, refresh.RootElement.GetProperty("refreshToken").GetString());
        Assert.Equal(login.RootElement.GetProperty("tenantId").GetRawText(), refresh.RootElement.GetProperty("tenantId").GetRawText());
        Assert.Equal(login.RootElement.GetProperty("sessionId").GetGuid(), refresh.RootElement.GetProperty("sessionId").GetGuid());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await context.Set<User>().SingleAsync(user => user.Id == account.User.Id);
        Assert.Equal(account.User.Name, persisted.Name);
        Assert.Equal(account.User.PhoneNumber, persisted.PhoneNumber);
        Assert.Equal(account.User.Username, persisted.Username);
    }

    [Fact]
    public async Task HighRiskLoginKeepsChallengeMetadataAndWithholdsPhone()
    {
        using var factory = WithRisk(RiskLevel.High);
        var account = await CreateAccountAsync(factory);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-in", new { account.Email, account.Password });
        using var payload = await ReadResponseAsync(response, HttpStatusCode.OK);
        var result = payload.RootElement;
        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("requiresStepUp").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("stepUpToken").GetString()));
        Assert.True(result.GetProperty("stepUpExpiresAt").GetDateTime() > DateTime.UtcNow);
        Assert.Contains("synthetic-risk", result.GetProperty("riskFactors").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("TOTP", result.GetProperty("availableMethods").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(account.User.Id, result.GetProperty("userId").GetGuid());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("user").GetProperty("phoneNumber").ValueKind);
        Assert.False(result.GetProperty("user").GetProperty("phoneNumberVerified").GetBoolean());
        Assert.True(string.IsNullOrEmpty(result.GetProperty("accessToken").GetString()));
        Assert.True(string.IsNullOrEmpty(result.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task InvalidPasswordDoesNotReturnProfileOrTokens()
    {
        using var factory = WithRisk(RiskLevel.Low);
        var account = await CreateAccountAsync(factory);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-in", new { account.Email, password = CreateSyntheticPassword() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(account.User.PhoneNumber!, payload, StringComparison.Ordinal);
        Assert.DoesNotContain(account.User.Username!, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnjoinedTenantRemainsForbiddenWithoutProfileOrTokens()
    {
        using var factory = WithRisk(RiskLevel.Low);
        var account = await CreateAccountAsync(factory);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-in", new
        {
            account.Email, account.Password, tenantId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(account.User.PhoneNumber!, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", payload, StringComparison.OrdinalIgnoreCase);
    }

    private WebApplicationFactory<Program> WithRisk(RiskLevel level)
    {
        // Only risk classification is controlled; CQRS, validators, auth, hashing, JWT, sessions and PostgreSQL are real.
        var risk = new Mock<IAuthenticationAnomalyDetectionService>(MockBehavior.Strict);
        risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = level, IsSuspicious = level == RiskLevel.High, DetectedAnomalies = ["synthetic-risk"] });
        risk.Setup(service => service.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalPattern = true, RiskLevel = RiskLevel.Low });
        return fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthenticationAnomalyDetectionService>();
            services.AddSingleton(risk.Object);
        }));
    }

    private static async Task<(User User, string Email, string Password)> CreateAccountAsync(WebApplicationFactory<Program> factory)
    {
        var marker = Guid.NewGuid().ToString("N");
        var email = $"dto-login-{marker}@example.test";
        var password = CreateSyntheticPassword();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-up", new { email, password, username = $"dto-{marker}" });
        using var payload = await ReadResponseAsync(response, HttpStatusCode.Created);
        var id = payload.RootElement.GetProperty("userId").GetGuid();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Set<User>().SingleAsync(value => value.Id == id);
        user.UpdateName("Ana Maria Silva");
        user.UpdatePhoneNumber("+15550001000");
        user.VerifyEmail();
        // Real successful authentication requires an active tenant membership; the fixture disables startup seeds.
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Name = $"DTO {marker}", Slug = $"dto-{marker}",
            AdminEmail = $"admin-{marker}@example.test", IsActive = true
        };
        context.Set<Tenant>().Add(tenant);
        context.Set<TenantMember>().Add(new TenantMember
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, UserId = user.Id, Role = "Member", IsActive = true
        });
        await context.SaveChangesAsync();
        return (user, email, password);
    }

    private static void AssertProfile(JsonElement result, User user)
    {
        var profile = result.GetProperty("user");
        Assert.Equal(user.Id, result.GetProperty("userId").GetGuid());
        Assert.Equal(user.Id, profile.GetProperty("id").GetGuid());
        Assert.Equal(user.Email, profile.GetProperty("email").GetString());
        Assert.Equal(user.Username, profile.GetProperty("username").GetString());
        Assert.Equal("Ana", profile.GetProperty("firstName").GetString());
        Assert.Equal("Maria Silva", profile.GetProperty("lastName").GetString());
        Assert.Equal(user.PhoneNumber, profile.GetProperty("phoneNumber").GetString());
        Assert.True(profile.GetProperty("emailVerified").GetBoolean());
        Assert.False(profile.GetProperty("phoneNumberVerified").GetBoolean());
        Assert.Equal(user.CreatedAt, profile.GetProperty("createdAt").GetDateTime());
    }

    private static async Task AssertCompletedTokensAsync(JsonElement result, WebApplicationFactory<Program> factory)
    {
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("refreshToken").GetString()));
        Assert.NotEqual(Guid.Empty, result.GetProperty("sessionId").GetGuid());
        var accessExpiry = result.GetProperty("accessTokenExpiresAt").GetDateTime();
        var refreshExpiry = result.GetProperty("refreshTokenExpiresAt").GetDateTime();
        Assert.Equal(refreshExpiry, result.GetProperty("expiresAt").GetDateTime());
        Assert.True(accessExpiry > DateTime.UtcNow);
        Assert.True(refreshExpiry > DateTime.UtcNow);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sessionId = result.GetProperty("sessionId").GetGuid();
        var session = await db.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == sessionId);
        var hash = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>()
            .HashToken(result.GetProperty("refreshToken").GetString()!);
        var token = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Token == hash);
        Assert.Equal(result.GetProperty("userId").GetGuid(), session.UserId);
        Assert.Equal(sessionId, token.SessionId);
        Assert.Equal(session.ExpiresAt, token.ExpiresAt);
        Assert.InRange(Math.Abs((refreshExpiry - token.ExpiresAt).Ticks), 0, 9);
        Assert.True(refreshExpiry <= session.CreatedAt.AddMinutes(
            scope.ServiceProvider.GetRequiredService<SessionOptions>().AbsoluteTimeoutMinutes).AddTicks(9));
        Assert.True(result.GetProperty("expiresIn").GetInt32() > 0);
    }

    private static async Task<JsonDocument> ReadResponseAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {payload}");
        return JsonDocument.Parse(payload);
    }

    private static string CreateSyntheticPassword() => $"Synthetic1!{Guid.NewGuid():N}";
}

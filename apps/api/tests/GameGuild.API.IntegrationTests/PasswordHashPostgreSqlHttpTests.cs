using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class PasswordHashPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    // Resolve public route metadata from the tested controller instead of duplicating endpoint strings.
    private static readonly string ChangeEndpoint = AuthEndpoint(nameof(AuthController.ChangePassword));
    private static readonly string ResetEndpoint = AuthEndpoint(nameof(AuthController.ResetPassword));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignupPersistsRandomSaltAndConfiguredWholePasswordHash(bool longPassword)
    {
        using var factory = CreateFactory();
        var password = SyntheticPassword(longPassword);
        var first = await CreateAccountAsync(factory, password);
        var second = await CreateAccountAsync(factory, password);
        var firstHash = await ReadHashAsync(factory, first.UserId);
        var secondHash = await ReadHashAsync(factory, second.UserId);
        Assert.NotEqual(firstHash, secondHash);
        Assert.NotEqual(password, firstHash);
        if (longPassword) { Assert.StartsWith("pbkdf2-sha256$600000$", firstHash); }
        else { Assert.Equal("10", firstHash.Split('$')[2]); }
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Assert.True(hasher.VerifyPassword(firstHash, password));
        Assert.False(hasher.VerifyPassword(firstHash, SyntheticPassword(longPassword)));
    }

    [Fact]
    public async Task LoginRejectsDifferentSuffixAfter72BytesAndAcceptsTheEntireCorrectPassword()
    {
        using var factory = CreateFactory();
        var prefix = "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(34));
        var password = prefix + Guid.NewGuid().ToString("N");
        var account = await CreateAccountAsync(factory, password);
        using var client = factory.CreateClient();
        using var incorrect = await client.PostAsJsonAsync("/v1/auth/sign-in", new { account.Email, password = prefix + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.Unauthorized, incorrect.StatusCode);
        using var correct = await client.PostAsJsonAsync("/v1/auth/sign-in", new { account.Email, password });
        await AssertSuccessAsync(correct);
    }

    [Theory]
    [InlineData("correct")]
    [InlineData("incorrect")]
    [InlineData("malformed")]
    public async Task LoginUsesStoredLegacyHashAndRejectsIncorrectOrMalformedCases(string scenario)
    {
        using var factory = CreateFactory();
        var password = SyntheticPassword();
        var account = await CreateAccountAsync(factory, password);
        var hash = scenario == "malformed" ? "$2b$12$not-a-hash" : BCrypt.Net.BCrypt.HashPassword(password, 10);
        await ReplaceStoredHashAsync(factory, account.UserId, hash);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-in", new { account.Email, password = scenario == "incorrect" ? SyntheticPassword() : password });
        Assert.Equal(scenario == "correct" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
        if (scenario == "correct") { await AssertSuccessAsync(response); }
        else { Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase); }
    }

    [Fact]
    public async Task AuthenticatedChangePersistsFullLengthHashAndRejectsAnIncorrectCurrentPassword()
    {
        using var factory = CreateFactory();
        var account = await CreateAccountAsync(factory, SyntheticPassword());
        using var client = CreateAuthenticatedClient(factory, account);
        var newPassword = SyntheticPassword(true);
        var originalHash = await ReadHashAsync(factory, account.UserId);
        using var incorrect = await client.PostAsJsonAsync(ChangeEndpoint, new
        {
            currentPassword = SyntheticPassword(), newPassword, confirmPassword = newPassword, revokeOtherSessions = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, incorrect.StatusCode);
        Assert.Equal(originalHash, await ReadHashAsync(factory, account.UserId));
        using var changed = await client.PostAsJsonAsync(ChangeEndpoint, new
        {
            currentPassword = account.Password, newPassword, confirmPassword = newPassword, revokeOtherSessions = false
        });
        await AssertSuccessAsync(changed);
        var hash = await ReadHashAsync(factory, account.UserId);
        Assert.StartsWith("pbkdf2-sha256$600000$", hash);
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Assert.True(hasher.VerifyPassword(hash, newPassword));
        Assert.False(hasher.VerifyPassword(hash, account.Password));
        using var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Set<User>().SingleAsync(value => value.Id == account.UserId);
        Assert.Contains(originalHash, user.GetPasswordHistoryHashes());
    }

    [Fact]
    public async Task ResetUsesRealTokenValidationPersistsFullLengthHashAndRejectsTokenReplay()
    {
        using var factory = CreateFactory();
        var account = await CreateAccountAsync(factory, SyntheticPassword());
        var newPassword = SyntheticPassword(true);
        var token = await GenerateResetTokenAsync(factory, account);
        using var client = factory.CreateClient();
        using var reset = await client.PostAsJsonAsync(ResetEndpoint, new { token, newPassword, confirmPassword = newPassword });
        await AssertSuccessAsync(reset);
        var hash = await ReadHashAsync(factory, account.UserId);
        Assert.StartsWith("pbkdf2-sha256$600000$", hash);
        using var scope = factory.Services.CreateScope();
        Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().VerifyPassword(hash, newPassword));
        var otherPassword = SyntheticPassword(true);
        using var replay = await client.PostAsJsonAsync(ResetEndpoint, new { token, newPassword = otherPassword, confirmPassword = otherPassword });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(hash, await ReadHashAsync(factory, account.UserId));
    }

    [Fact]
    public async Task AmbiguousLongLegacyPasswordRequiresRecoveryAndCannotEvadeHistoryByChangingItsSuffix()
    {
        using var factory = CreateFactory();
        var password = SyntheticPassword(true);
        var account = await CreateAccountAsync(factory, password);
        var legacyHash = BCrypt.Net.BCrypt.HashPassword(password, 10);
        await ReplaceStoredHashAsync(factory, account.UserId, legacyHash);
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/v1/auth/sign-in", new { account.Email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var samePrefix = password[..72] + Guid.NewGuid().ToString("N");
        var rejectedToken = await GenerateResetTokenAsync(factory, account);
        using var reuse = await client.PostAsJsonAsync(ResetEndpoint, new { token = rejectedToken, newPassword = samePrefix, confirmPassword = samePrefix });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Contains("reuse", await reuse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(legacyHash, await ReadHashAsync(factory, account.UserId));
        var token = await GenerateResetTokenAsync(factory, account);
        var newPassword = SyntheticPassword(true);
        using var reset = await client.PostAsJsonAsync(ResetEndpoint, new { token, newPassword, confirmPassword = newPassword });
        await AssertSuccessAsync(reset);
        using var after = await client.PostAsJsonAsync("/v1/auth/sign-in", new { account.Email, password = newPassword });
        await AssertSuccessAsync(after);
    }

    [Theory]
    [InlineData("signup")]
    [InlineData("change")]
    [InlineData("reset")]
    public async Task EveryPasswordWriterRejectsWeakInputWithoutChangingStoredPasswords(string writer)
    {
        using var factory = CreateFactory();
        var weakPassword = RandomLowercase(8);
        var account = await CreateAccountAsync(factory, SyntheticPassword());
        var originalHash = await ReadHashAsync(factory, account.UserId);
        using var client = writer == "change" ? CreateAuthenticatedClient(factory, account) : factory.CreateClient();
        HttpResponseMessage response;
        if (writer == "signup")
        {
            response = await client.PostAsJsonAsync("/v1/auth/sign-up", new { email = $"weak-{Guid.NewGuid():N}@example.test", password = weakPassword, username = $"weak-{Guid.NewGuid():N}" });
        }
        else if (writer == "change")
        {
            response = await client.PostAsJsonAsync(ChangeEndpoint, new { currentPassword = account.Password, newPassword = weakPassword, confirmPassword = weakPassword, revokeOtherSessions = false });
        }
        else
        {
            var token = await GenerateResetTokenAsync(factory, account);
            response = await client.PostAsJsonAsync(ResetEndpoint, new { token, newPassword = weakPassword, confirmPassword = weakPassword });
            using var scope = factory.Services.CreateScope();
            // Strength rejection precedes token consumption; the real token remains valid.
            Assert.True((await scope.ServiceProvider.GetRequiredService<IEmailVerificationService>().VerifyPasswordResetTokenAsync(token)).Success);
        }
        using (response) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(originalHash, await ReadHashAsync(factory, account.UserId));
    }

    [Fact]
    public async Task ConfiguredLengthAndDisabledCompositionApplyAtActualSignup()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["MinPasswordLength"] = "14",
            ["RequireUppercase"] = "false", ["RequireLowercase"] = "false",
            ["RequireDigit"] = "false", ["RequireSpecialChar"] = "false"
        });
        using var client = factory.CreateClient();
        using var tooShort = await client.PostAsJsonAsync("/v1/auth/sign-up", new { email = $"length-{Guid.NewGuid():N}@example.test", password = RandomLowercase(13), username = $"length-{Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        var account = await CreateAccountAsync(factory, RandomLowercase(14));
        using var scope = factory.Services.CreateScope();
        var policy = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        Assert.Equal("14", policy["PresentationLayer:Authentication:PasswordPolicy:MinPasswordLength"]);
        Assert.Equal("false", policy["PresentationLayer:Authentication:PasswordPolicy:RequireSpecialChar"]);
        Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().VerifyPassword(await ReadHashAsync(factory, account.UserId), account.Password));
    }

    private WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?>? passwordPolicy = null)
    {
        // Only risk classification is controlled. CQRS, policy, hashing, token storage, repositories and PostgreSQL are real.
        var risk = new Mock<IAuthenticationAnomalyDetectionService>(MockBehavior.Strict);
        risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        risk.Setup(service => service.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalPattern = true, RiskLevel = RiskLevel.Low });
        var values = new Dictionary<string, string?> { ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10" };
        foreach (var pair in passwordPolicy ?? []) { values[$"PresentationLayer:Authentication:PasswordPolicy:{pair.Key}"] = pair.Value; }
        return fixture.CreateFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthenticationAnomalyDetectionService>();
                services.AddSingleton(risk.Object);
            });
        });
    }

    private static async Task<Account> CreateAccountAsync(WebApplicationFactory<Program> factory, string password)
    {
        var marker = Guid.NewGuid().ToString("N");
        var email = $"hash-{marker}@example.test";
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-up", new { email, password, username = $"hash-{marker}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = payload.RootElement.GetProperty("userId").GetGuid();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Set<User>().SingleAsync(value => value.Id == id);
        user.VerifyEmail();
        var tenantId = Guid.NewGuid();
        context.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = $"Hash {marker}", Slug = $"hash-{marker}", AdminEmail = $"admin-{marker}@example.test", IsActive = true });
        context.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId, UserId = id, Role = "Member", IsActive = true });
        await context.SaveChangesAsync();
        return new Account(id, tenantId, email, password);
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, Account account)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(ApiPostgreSqlTestAuthHandler.SchemeName, "authenticated");
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.UserIdHeader, account.UserId.ToString());
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.TenantIdHeader, account.TenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Tenant-Id", account.TenantId.ToString());
        return client;
    }

    private static async Task<string> ReadHashAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<User>().SingleAsync(user => user.Id == userId)).PasswordHash!;
    }

    private static async Task ReplaceStoredHashAsync(WebApplicationFactory<Program> factory, Guid userId, string hash)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Set<User>().SingleAsync(value => value.Id == userId);
        user.PasswordHash = hash;
        await context.SaveChangesAsync();
    }

    private static async Task<string> GenerateResetTokenAsync(WebApplicationFactory<Program> factory, Account account)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IEmailVerificationService>().GeneratePasswordResetTokenAsync(account.UserId, account.Email);
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());
    }

    private static string AuthEndpoint(string action)
    {
        var route = typeof(AuthController).GetMethod(action)?.GetCustomAttribute<HttpPostAttribute>()?.Template
            ?? throw new InvalidOperationException($"No POST route for auth action {action}.");
        return "/" + route.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);
    }

    private static string SyntheticPassword(bool longPassword = false) => "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(longPassword ? 50 : 20));

    private static string RandomLowercase(int length) => new(Enumerable.Range(0, length).Select(_ => (char)RandomNumberGenerator.GetInt32('a', 'z' + 1)).ToArray());

    private sealed record Account(Guid UserId, Guid TenantId, string Email, string Password);
}

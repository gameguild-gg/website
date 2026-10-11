using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Controllers;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class BearerRevocationPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private static readonly string SessionsEndpoint = "/" + typeof(SessionController).GetMethod(nameof(SessionController.GetSessions))!
        .GetCustomAttribute<HttpGetAttribute>()!.Template!.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);
    private static readonly string RefreshEndpoint = "/" + typeof(AuthController).GetMethod(nameof(AuthController.RefreshToken))!
        .GetCustomAttribute<HttpPostAttribute>()!.Template!.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);
    private static readonly string LiveEndpoint = typeof(HealthController).GetMethod(nameof(HealthController.GetLiveness))!
        .GetCustomAttributes<HttpGetAttribute>().First().Template!;

    [Theory]
    [InlineData("revoked")]
    [InlineData("replaced")]
    public async Task RealRefreshReplayRejectsEarlierSignedBearerAndLeavesAnotherUserActive(string scenario)
    {
        using var factory = CreateBearerFactory();
        var account = await SeedAsync(factory);
        var other = await SeedAsync(factory);
        var oldAccess = await AccessAsync(factory, account);
        var otherAccess = await AccessAsync(factory, other);
        using var client = factory.CreateClient();
        await AssertBearerStatusAsync(client, oldAccess, HttpStatusCode.OK);
        await AssertBearerStatusAsync(client, otherAccess, HttpStatusCode.OK);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var presented = await db.Set<RefreshToken>().SingleAsync(value => value.Id == account.RefreshToken.Id);
            presented.IsRevoked = true;
            presented.RevokedAt = DateTime.UtcNow;
            if (scenario == "replaced") { presented.ReplacedByToken = account.Session.RefreshToken; }
            await db.SaveChangesAsync();
        }
        using (var anonymous = factory.CreateClient())
        using (var denied = await anonymous.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawRefreshToken }))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(account.User.TokenVersion + 1, (await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == account.User.Id)).TokenVersion);
            Assert.False((await db.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == account.Session.Id)).IsActive);
            Assert.True((await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == account.Survivor.Id)).IsRevoked);
        }
        await AssertBearerStatusAsync(client, oldAccess, HttpStatusCode.Unauthorized);
        await AssertBearerStatusAsync(client, otherAccess, HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("stale-version")]
    [InlineData("revoked-jti")]
    [InlineData("all-user")]
    public async Task ActualHostRejectsPreviouslySignedTokenAfterRevocation(string scenario)
    {
        using var factory = CreateBearerFactory();
        var account = await SeedAsync(factory);
        var token = await AccessAsync(factory, account);
        using var client = factory.CreateClient();
        await AssertBearerStatusAsync(client, token, HttpStatusCode.OK);
        await RevokeAsync(factory, account, token, scenario);
        await AssertBearerStatusAsync(client, token, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("stale-version")]
    [InlineData("revoked-jti")]
    [InlineData("all-user")]
    public async Task ExplicitAnonymousEndpointRemainsAvailableWhenClientPresentsRevokedBearer(string scenario)
    {
        using var factory = CreateBearerFactory();
        var account = await SeedAsync(factory);
        var token = await AccessAsync(factory, account);
        await RevokeAsync(factory, account, token, scenario);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
        using var response = await client.GetAsync(LiveEndpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(token, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("current")]
    [InlineData("other-user")]
    public async Task CurrentSignedBearerRemainsAcceptedAcrossIndependentStoredVersionChanges(string scenario)
    {
        using var factory = CreateBearerFactory();
        var account = await SeedAsync(factory);
        var changed = scenario == "current" ? account : await SeedAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<User>().SingleAsync(value => value.Id == changed.User.Id)).IncrementTokenVersion();
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        await AssertBearerStatusAsync(client, await AccessAsync(factory, account, scenario == "current" ? account.User.TokenVersion + 1 : account.User.TokenVersion), HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedSessionEndpointStillRejectsAnonymousRequests()
    {
        using var factory = CreateBearerFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(SessionsEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateBearerFactory() => fixture.CreateFactory(builder =>
        builder.ConfigureTestServices(services => services.PostConfigure<AuthenticationOptions>(options =>
        {
            // Only select the production handler. Its crypto/events/pipeline and every repository remain real.
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })));

    private static async Task<Account> SeedAsync(WebApplicationFactory<Program> factory)
    {
        var marker = Guid.NewGuid().ToString("N");
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var now = DateTime.UtcNow;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>();
        var user = User.Create($"bearer-{marker}@example.test", "Synthetic bearer account");
        user.Username = $"bearer-{marker}";
        var tenantId = Guid.NewGuid();
        var presented = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, Token = hasher.HashToken(raw), CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddHours(1) };
        var survivor = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, Token = hasher.HashToken(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))), CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddHours(1) };
        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, RefreshToken = survivor.Token, IpAddress = "127.0.0.1", CreatedAt = now, UpdatedAt = now, LastUsedAt = now, ExpiresAt = now.AddHours(1), IsActive = true };
        db.Set<User>().Add(user);
        db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = $"Bearer {marker}", Slug = $"bearer-{marker}", AdminEmail = $"admin-{marker}@example.test", IsActive = true });
        db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id, Role = "Member", IsActive = true });
        db.Set<RefreshToken>().AddRange(presented, survivor);
        db.Set<UserSession>().Add(session);
        await db.SaveChangesAsync();
        return new Account(user, tenantId, presented, survivor, session, raw);
    }

    private static async Task<string> AccessAsync(WebApplicationFactory<Program> factory, Account account, int? version = null)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
            account.User.Id, account.User.Email, ["Member"], account.TenantId, version ?? account.User.TokenVersion, account.Session.Id);
    }

    private static async Task RevokeAsync(WebApplicationFactory<Program> factory, Account account, string token, string scenario)
    {
        using var scope = factory.Services.CreateScope();
        if (scenario == "stale-version")
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<User>().SingleAsync(value => value.Id == account.User.Id)).IncrementTokenVersion();
            await db.SaveChangesAsync();
            return;
        }
        var store = scope.ServiceProvider.GetRequiredService<ITokenRevocationService>();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        if (scenario == "revoked-jti") { await store.RevokeTokenAsync(jwt.Id, jwt.ValidTo, "Synthetic acceptance"); }
        else { await store.RevokeAllUserTokensAsync(account.User.Id, "Synthetic acceptance"); }
    }

    private static async Task AssertBearerStatusAsync(HttpClient client, string encoded, HttpStatusCode expected)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, encoded);
        using var response = await client.GetAsync(SessionsEndpoint);
        Assert.Equal(expected, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(encoded, body, StringComparison.Ordinal);
        if (expected == HttpStatusCode.Unauthorized)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == JwtBearerDefaults.AuthenticationScheme);
            using var problem = JsonDocument.Parse(body);
            Assert.Equal(401, problem.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Invalid access token", problem.RootElement.GetProperty("detail").GetString());
        }
    }

    private sealed record Account(User User, Guid TenantId, RefreshToken RefreshToken, RefreshToken Survivor, UserSession Session, string RawRefreshToken);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RefreshTokenReplayScopePostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("Family", false)]
    [InlineData("Account", true)]
    public async Task ReplayContainsSelectedScopeAndPreservesOtherAccounts(string policy, bool revokeAccount)
    {
        using var factory = CreateFactory(policy);
        var owner = await SeedAccountAsync(factory);
        var unrelated = await SeedAccountAsync(factory);
        var siblingAccess = await CreateBearerAsync(factory, owner, owner.Second);
        var unrelatedAccess = await CreateBearerAsync(factory, unrelated, unrelated.First);
        await AssertBearerAsync(factory, siblingAccess, HttpStatusCode.OK);
        using var client = factory.CreateClient();
        using var rotated = await client.PostAsJsonAsync("/v1/auth/tokens:refresh",
            new { refreshToken = owner.First.Raw, owner.TenantId });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        using var replacement = JsonDocument.Parse(await rotated.Content.ReadAsStringAsync());
        var affectedAccess = replacement.RootElement.GetProperty("accessToken").GetString()!;
        await AssertBearerAsync(factory, affectedAccess, HttpStatusCode.OK);
        using var replay = await client.PostAsJsonAsync("/v1/auth/tokens:refresh",
            new { refreshToken = owner.First.Raw, owner.TenantId });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        await AssertBearerAsync(factory, affectedAccess, HttpStatusCode.Unauthorized);
        await AssertBearerAsync(factory, siblingAccess, revokeAccount ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
        await AssertBearerAsync(factory, unrelatedAccess, HttpStatusCode.OK);
        using (var verification = factory.Services.CreateScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var affected = await db.Set<RefreshToken>().AsNoTracking()
                .Where(value => value.UserId == owner.User.Id && value.SessionId == owner.First.Session.Id).ToListAsync();
            Assert.Equal(2, affected.Count);
            Assert.All(affected, value => Assert.True(value.IsRevoked));
            var sessions = await db.Set<UserSession>().AsNoTracking().Where(value => value.UserId == owner.User.Id).ToListAsync();
            Assert.False(sessions.Single(value => value.Id == owner.First.Session.Id).IsActive);
            Assert.Equal(!revokeAccount, sessions.Single(value => value.Id == owner.Second.Session.Id).IsActive);
            Assert.Equal(revokeAccount,
                (await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == owner.Second.Token.Id)).IsRevoked);
            Assert.Equal(owner.User.TokenVersion + (revokeAccount ? 1 : 0),
                (await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == owner.User.Id)).TokenVersion);
        }
        using var siblingRefresh = await client.PostAsJsonAsync("/v1/auth/tokens:refresh",
            new { refreshToken = owner.Second.Raw, owner.TenantId });
        Assert.Equal(revokeAccount ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, siblingRefresh.StatusCode);
        using var unaffectedRefresh = await client.PostAsJsonAsync("/v1/auth/tokens:refresh",
            new { refreshToken = unrelated.First.Raw, unrelated.TenantId });
        Assert.Equal(HttpStatusCode.OK, unaffectedRefresh.StatusCode);
    }

    [Theory]
    [InlineData("Family")]
    [InlineData("Account")]
    public async Task LegacyReplayWithoutProvableFamilyFallsBackToAccountContainment(string policy)
    {
        using var factory = CreateFactory(policy);
        var owner = await SeedAccountAsync(factory);
        var bearer = await CreateBearerAsync(factory, owner, owner.Second);
        using (var mutation = factory.Services.CreateScope())
        {
            var db = mutation.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var root = await db.Set<RefreshToken>().SingleAsync(value => value.Id == owner.First.Token.Id);
            root.SessionId = null;
            root.IsRevoked = true;
            root.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/tokens:refresh",
            new { refreshToken = owner.First.Raw, owner.TenantId });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertBearerAsync(factory, bearer, HttpStatusCode.Unauthorized);
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.All(await context.Set<RefreshToken>().AsNoTracking().Where(value => value.UserId == owner.User.Id).ToListAsync(),
            value => Assert.True(value.IsRevoked));
        Assert.All(await context.Set<UserSession>().AsNoTracking().Where(value => value.UserId == owner.User.Id).ToListAsync(),
            value => Assert.False(value.IsActive));
        Assert.Equal(owner.User.TokenVersion + 1,
            (await context.Set<User>().AsNoTracking().SingleAsync(value => value.Id == owner.User.Id)).TokenVersion);
    }

    [Fact]
    public async Task StaleActiveSessionUpdateCannotResurrectContainedFamily()
    {
        using var factory = CreateFactory("Family");
        var owner = await SeedAccountAsync(factory);
        var bearer = await CreateBearerAsync(factory, owner, owner.First);
        using var staleScope = factory.Services.CreateScope();
        var staleDb = staleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staleSession = await staleDb.Set<UserSession>().SingleAsync(value => value.Id == owner.First.Session.Id);
        Assert.True(staleSession.IsActive);
        using (var mutation = factory.Services.CreateScope())
        {
            var db = mutation.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var root = await db.Set<RefreshToken>().SingleAsync(value => value.Id == owner.First.Token.Id);
            root.IsRevoked = true;
            root.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/tokens:refresh",
            new { refreshToken = owner.First.Raw, owner.TenantId });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        staleSession.LastUsedAt = DateTime.UtcNow;
        await staleScope.ServiceProvider.GetRequiredService<IUserSessionRepository>().UpdateAsync(staleSession);
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await context.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == staleSession.Id);
        Assert.False(persisted.IsActive);
        Assert.NotNull(persisted.TerminatedAt);
        Assert.Equal(SessionTerminationReason.SecurityViolation.ToString(), persisted.TerminationReason);
        await AssertBearerAsync(factory, bearer, HttpStatusCode.Unauthorized);
    }

    private WebApplicationFactory<Program> CreateFactory(string policy) => fixture.CreateFactory(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Jwt:RefreshTokenReplayContainmentScope"] = policy }));
        builder.ConfigureTestServices(services => services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }));
    });

    private static async Task<string> CreateBearerAsync(WebApplicationFactory<Program> factory, Account account, Credential credential)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
            account.User.Id, account.User.Email, ["Member"], account.TenantId, account.User.TokenVersion, credential.Session.Id);
    }

    private static async Task AssertBearerAsync(WebApplicationFactory<Program> factory, string token, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
        using var response = await client.GetAsync("/v1/auth/sessions");
        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task<Account> SeedAccountAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create($"replay-scope-{marker}@example.test", "Synthetic policy owner");
        user.Username = $"replay-scope-{marker}";
        var tenantId = Guid.NewGuid();
        db.Set<User>().Add(user);
        db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = $"Replay scope {marker}", Slug = $"replay-scope-{marker}",
            AdminEmail = $"scope-admin-{marker}@example.test", IsActive = true });
        db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id,
            Role = "Member", IsActive = true });
        var first = CreateCredential();
        var second = CreateCredential();
        db.Set<UserSession>().AddRange(first.Session, second.Session);
        db.Set<RefreshToken>().AddRange(first.Token, second.Token);
        await db.SaveChangesAsync();
        return new Account(user, tenantId, first, second);

        Credential CreateCredential()
        {
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var hash = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(raw);
            var now = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
            var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, RefreshToken = hash,
                CreatedAt = now.AddMinutes(-10), UpdatedAt = now, LastUsedAt = now, ExpiresAt = now.AddHours(6),
                IpAddress = "127.0.0.1", IsActive = true };
            var token = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, Token = hash, SessionId = session.Id,
                CreatedAt = session.CreatedAt, UpdatedAt = now, ExpiresAt = session.ExpiresAt, CreatedByIp = "127.0.0.1" };
            return new Credential(raw, token, session);
        }
    }

    private sealed record Credential(string Raw, RefreshToken Token, UserSession Session);
    private sealed record Account(User User, Guid TenantId, Credential First, Credential Second);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
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
public sealed class RefreshTokenOwnershipPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private static readonly string RevokeEndpoint = "/" + typeof(AuthController).GetMethod(nameof(AuthController.RevokeToken))!
        .GetCustomAttribute<HttpPostAttribute>()!.Template!.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);
    private static readonly string SessionsEndpoint = "/" + typeof(SessionController).GetMethod(nameof(SessionController.GetSessions))!
        .GetCustomAttribute<HttpGetAttribute>()!.Template!.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);

    [Theory]
    [InlineData("owner", HttpStatusCode.NoContent)]
    [InlineData("other-user-same-tenant", HttpStatusCode.Forbidden)]
    [InlineData("other-user-different-tenant", HttpStatusCode.Forbidden)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    public async Task ActualRevocationRequiresAuthenticatedTokenOwner(string callerKind, HttpStatusCode expected)
    {
        using var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })));
        var owner = await SeedAsync(factory);
        var other = await SeedAsync(factory, callerKind == "other-user-same-tenant" ? owner.TenantId : null);
        using var client = factory.CreateClient();
        if (callerKind != "anonymous")
        {
            var caller = callerKind == "owner" ? owner : other;
            using var signing = factory.Services.CreateScope();
            var access = await signing.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
                caller.User.Id, caller.User.Email, ["Member"], caller.TenantId, caller.User.TokenVersion, caller.Session.Id);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, access);
        }
        using var response = await client.PostAsJsonAsync(RevokeEndpoint,
            new { token = owner.RawToken, ipAddress = "198.51.100.123", userId = owner.User.Id });
        Assert.Equal(expected, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(owner.RawToken, body, StringComparison.Ordinal);
        Assert.DoesNotContain(owner.User.Id.ToString(), body, StringComparison.Ordinal);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == owner.Token.Id);
        Assert.Equal(callerKind == "owner", stored.IsRevoked);
        Assert.Null(stored.ReplacedByToken);
        if (callerKind != "owner")
        {
            Assert.Null(stored.RevokedAt);
            Assert.Null(stored.RevokedByIp);
        }
        else
        {
            Assert.NotNull(stored.RevokedAt);
            Assert.False(string.IsNullOrWhiteSpace(stored.RevokedByIp));
            Assert.NotEqual("198.51.100.123", stored.RevokedByIp);
        }
        Assert.False((await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == other.Token.Id)).IsRevoked);
        Assert.Equal(1, await db.Set<RefreshToken>().CountAsync(value => value.UserId == owner.User.Id));
        Assert.Equal(owner.User.TokenVersion,
            (await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == owner.User.Id)).TokenVersion);
        Assert.Equal(callerKind != "owner",
            (await db.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == owner.Session.Id)).IsActive);
        Assert.True((await db.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == other.Session.Id)).IsActive);
        using var protectedResponse = await client.GetAsync(SessionsEndpoint);
        Assert.Equal(callerKind is "owner" or "anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK,
            protectedResponse.StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("other-user")]
    [InlineData("empty")]
    [InlineData("terminated")]
    public async Task InvalidSignedSessionIsDeniedWithoutMutatingTokens(string scenario)
    {
        using var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })));
        var owner = await SeedAsync(factory);
        var other = await SeedAsync(factory);
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sessionId = scenario switch
        {
            "missing" => Guid.NewGuid(), "other-user" => other.Session.Id, "empty" => Guid.Empty, _ => owner.Session.Id
        };
        if (scenario is "expired" or "terminated")
        {
            var session = await db.Set<UserSession>().SingleAsync(value => value.Id == owner.Session.Id);
            if (scenario == "expired") { session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); }
            else { session.TerminatedAt = DateTime.UtcNow; }
            await db.SaveChangesAsync();
        }
        var access = await setup.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
            owner.User.Id, owner.User.Email, ["Member"], owner.TenantId, owner.User.TokenVersion, sessionId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, access);

        using var response = await client.GetAsync(SessionsEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(owner.User.Id.ToString(), body, StringComparison.Ordinal);
        Assert.DoesNotContain(sessionId.ToString(), body, StringComparison.Ordinal);
        using var verify = factory.Services.CreateScope();
        var stored = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False((await stored.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == owner.Token.Id)).IsRevoked);
        Assert.False((await stored.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == other.Token.Id)).IsRevoked);
        Assert.True((await stored.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == other.Session.Id)).IsActive);
        Assert.Equal(owner.User.TokenVersion,
            (await stored.Set<User>().AsNoTracking().SingleAsync(value => value.Id == owner.User.Id)).TokenVersion);
    }

    private static async Task<Account> SeedAsync(WebApplicationFactory<Program> factory, Guid? existingTenant = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create($"ownership-{marker}@example.test", "Synthetic token owner");
        user.Username = $"ownership-{marker}";
        var tenant = existingTenant ?? Guid.NewGuid();
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var now = DateTime.UtcNow;
        var token = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id,
            Token = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(raw),
            CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddHours(6), CreatedByIp = "127.0.0.1" };
        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, RefreshToken = token.Token,
            CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddHours(6), LastUsedAt = now,
            IsActive = true, IpAddress = "127.0.0.1" };
        db.Set<User>().Add(user);
        if (!existingTenant.HasValue)
        {
            db.Set<Tenant>().Add(new Tenant { Id = tenant, Name = $"Ownership {marker}", Slug = $"ownership-{marker}",
                AdminEmail = $"admin-{marker}@example.test", IsActive = true });
        }
        db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenant,
            IsActive = true, Role = "Member" });
        db.Set<RefreshToken>().Add(token);
        db.Set<UserSession>().Add(session);
        await db.SaveChangesAsync();
        return new Account(user, tenant, token, session, raw);
    }

    private sealed record Account(User User, Guid TenantId, RefreshToken Token, UserSession Session, string RawToken);
}

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class SameSecondReloginPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task ActualCredentialReloginCurrentVersionBearerIsAcceptedAtTheLogoutSecondWhilePriorBearerIsDenied()
    {
        using var factory = fixture.CreateFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Authentication:TokenRevocation:UseDistributedCache"] = "true", ["Redis:Enabled"] = "false" }));
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                });
                services.RemoveAll<ITokenRevocationService>();
                services.AddSingleton<ITokenRevocationService, DistributedCacheTokenRevocationService>();
                services.RemoveAll<IDistributedCache>();
                services.AddDistributedMemoryCache();
            });
        });
        Assert.IsType<DistributedCacheTokenRevocationService>(factory.Services.GetRequiredService<ITokenRevocationService>());
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
        var marker = Guid.NewGuid().ToString("N");
        User user;
        var tenantId = Guid.NewGuid();
        string oldBearer;
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            user = User.CreateWithPassword($"relogin-{marker}@example.test", "Synthetic re-login account",
                seed.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword(password), $"relogin-{marker}");
            var now = DateTime.UtcNow;
            var hash = seed.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
            var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, RefreshToken = hash,
                CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddHours(6), LastUsedAt = now, IsActive = true, IpAddress = "127.0.0.1" };
            db.Set<User>().Add(user);
            db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = $"Relogin {marker}", Slug = $"relogin-{marker}",
                AdminEmail = $"admin-{marker}@example.test", IsActive = true });
            db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenantId, IsActive = true, Role = "Member" });
            db.Set<UserSession>().Add(session);
            db.Set<RefreshToken>().Add(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, Token = hash,
                CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddDays(7), CreatedByIp = "127.0.0.1" });
            await db.SaveChangesAsync();
            oldBearer = await seed.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
                user.Id, user.Email, ["Member"], tenantId, user.TokenVersion, session.Id);
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldBearer);
        using var logout = await client.PostAsJsonAsync("/v1/auth/sessions:terminate-all", new { });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        using var anonymous = factory.CreateClient();
        using var login = await anonymous.PostAsJsonAsync("/v1/auth/polymorphic", new { credential = user.Email, password, tenantId });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var payload = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var access = payload.RootElement.GetProperty("accessToken").GetString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        using var verification = factory.Services.CreateScope();
        var storedVersion = (await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<User>().AsNoTracking()
            .SingleAsync(value => value.Id == user.Id)).TokenVersion;
        Assert.Equal(user.TokenVersion + 1, storedVersion);
        Assert.Equal(storedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), jwt.Claims.Single(value => value.Type == "token_version").Value);
        var cutoffPayload = await factory.Services.GetRequiredService<IDistributedCache>()
            .GetStringAsync("auth:user-token-revoked-at:" + user.Id.ToString("N"));
        Assert.NotNull(cutoffPayload);
        using var cutoff = JsonDocument.Parse(cutoffPayload);
        var cutoffTime = cutoff.RootElement.GetProperty("revokedAt").GetDateTime();
        Assert.Equal(storedVersion, cutoff.RootElement.GetProperty("minimumTokenVersion").GetInt32());
        var logoutSecond = new DateTimeOffset(cutoffTime).ToUnixTimeSeconds();
        var claims = jwt.Claims.Where(value => value.Type != JwtRegisteredClaimNames.Iat).Append(
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Iat,
                logoutSecond.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Security.Claims.ClaimValueTypes.Integer64));
        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        // Keep actual credential sign-in identity/session/version. Change only signed iat to the exact logout second.
        var boundary = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(jwt.Issuer, jwt.Audiences.Single(), claims,
            jwt.ValidFrom, jwt.ValidTo, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey)), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", boundary);
        using var response = await client.GetAsync("/v1/auth/sessions");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldBearer);
        using var oldResponse = await client.GetAsync("/v1/auth/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, oldResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var actualSigninBearer = await client.GetAsync("/v1/auth/sessions");
        Assert.Equal(HttpStatusCode.OK, actualSigninBearer.StatusCode);
    }
}

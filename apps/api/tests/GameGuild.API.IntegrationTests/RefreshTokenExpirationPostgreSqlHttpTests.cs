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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GameGuild.API.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RefreshTokenExpirationPostgreSqlCollection : ICollectionFixture<ApiPostgreSqlFixture>
{
    internal const string Name = "Refresh-token expiration PostgreSQL";
}

[Collection(RefreshTokenExpirationPostgreSqlCollection.Name)]
public sealed class RefreshTokenExpirationPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(true, 1440)]
    [InlineData(false, 1440)]
    [InlineData(true, 43200)]
    [InlineData(false, 43200)]
    public async Task RotationPersistsAndReportsTheConfiguredLifetime(bool sliding, int absoluteMinutes)
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Jwt:RefreshTokenSlidingExpiration"] = sliding.ToString() }));
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                });
                services.PostConfigure<JwtOptions>(options => options.RefreshTokenExpirationDays = 7);
                services.RemoveAll<SessionOptions>();
                services.AddSingleton(new SessionOptions { AbsoluteTimeoutMinutes = absoluteMinutes });
                services.RemoveAll<IHostedService>();
            });
        });
        var marker = Guid.NewGuid().ToString("N");
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;
        var user = User.Create($"expiration-{marker}@example.test", "Synthetic expiration owner");
        user.Username = "expiration-" + marker;
        var tenantId = Guid.NewGuid();
        var originalExpiry = now.AddMinutes(15);
        var sessionId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hash = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(raw);
            db.Set<User>().Add(user);
            db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = "Expiration " + marker,
                Slug = "expiration-" + marker, AdminEmail = "admin-" + marker + "@example.test", IsActive = true });
            db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id,
                TenantId = tenantId, Role = "Member", IsActive = true });
            db.Set<RefreshToken>().Add(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id,
                Token = hash, SessionId = sessionId, CreatedAt = now.AddHours(-23),
                UpdatedAt = now, ExpiresAt = originalExpiry, CreatedByIp = "127.0.0.1" });
            db.Set<UserSession>().Add(new UserSession { Id = sessionId, UserId = user.Id,
                RefreshToken = hash, CreatedAt = now.AddHours(-23), UpdatedAt = now,
                LastUsedAt = now, ExpiresAt = originalExpiry, IsActive = true, IpAddress = "127.0.0.1" });
            await db.SaveChangesAsync();
        }
        var before = DateTime.UtcNow;
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/tokens:refresh", new { refreshToken = raw, tenantId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var after = DateTime.UtcNow;
        var replacement = payload.RootElement.GetProperty("refreshToken").GetString()!;
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var replacementHash = verification.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(replacement);
        var token = await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Token == replacementHash);
        var session = await context.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == sessionId);
        var reported = payload.RootElement.GetProperty("refreshTokenExpiresAt").GetDateTime();
        Assert.Equal(sessionId, token.SessionId);
        Assert.Equal(session.ExpiresAt, token.ExpiresAt);
        Assert.InRange(Math.Abs((reported - token.ExpiresAt).TotalMilliseconds), 0, 0.001);
        Assert.Equal(reported, payload.RootElement.GetProperty("expiresAt").GetDateTime());
        Assert.True(token.ExpiresAt <= now.AddHours(-23).AddMinutes(absoluteMinutes));
        if (!sliding)
        {
            Assert.Equal(originalExpiry, token.ExpiresAt);
        }
        else if (absoluteMinutes == 1440)
        {
            Assert.Equal(now.AddHours(1), token.ExpiresAt);
        }
        else
        {
            Assert.InRange(token.ExpiresAt, before.AddDays(7).AddMilliseconds(-0.001), after.AddDays(7));
        }
        Assert.Single(await context.Set<RefreshToken>().Where(value => value.UserId == user.Id && value.ParentTokenId != null).ToListAsync());
    }
}

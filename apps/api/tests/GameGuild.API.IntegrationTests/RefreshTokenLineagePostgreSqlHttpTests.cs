using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RefreshTokenLineagePostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task ActualCredentialLoginAndTwoRotationsPersistParentAndSessionMetadata()
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
        var marker = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        User user;
        using (var seed = fixture.Factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            user = User.CreateWithPassword($"lineage-{marker}@example.test", "Synthetic lineage account",
                seed.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword(password), $"lineage-{marker}");
            db.Set<User>().Add(user);
            db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = $"Lineage {marker}", Slug = $"lineage-{marker}",
                AdminEmail = $"admin-{marker}@example.test", IsActive = true });
            db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id,
                TenantId = tenantId, IsActive = true, Role = "Member" });
            await db.SaveChangesAsync();
        }
        using var client = fixture.Factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/v1/auth/polymorphic", new { credential = user.Email, password, tenantId });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var rootPayload = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var rootRaw = rootPayload.RootElement.GetProperty("refreshToken").GetString()!;
        var rootSession = rootPayload.RootElement.GetProperty("sessionId").GetGuid();
        using var first = await client.PostAsJsonAsync("/v1/auth/tokens:refresh", new { refreshToken = rootRaw, tenantId });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var childPayload = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var childRaw = childPayload.RootElement.GetProperty("refreshToken").GetString()!;
        var childSession = childPayload.RootElement.GetProperty("sessionId").GetGuid();
        using var second = await client.PostAsJsonAsync("/v1/auth/tokens:refresh", new { refreshToken = childRaw, tenantId });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var leafPayload = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        var leafRaw = leafPayload.RootElement.GetProperty("refreshToken").GetString()!;
        var leafSession = leafPayload.RootElement.GetProperty("sessionId").GetGuid();
        using var verification = fixture.Factory.Services.CreateScope();
        var persisted = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = verification.ServiceProvider.GetRequiredService<IRefreshTokenHasher>();
        var rootHash = hasher.HashToken(rootRaw);
        var childHash = hasher.HashToken(childRaw);
        var leafHash = hasher.HashToken(leafRaw);
        var tokens = await persisted.Set<RefreshToken>().AsNoTracking().Where(value => value.UserId == user.Id).ToListAsync();
        var root = tokens.Single(value => value.Token == rootHash);
        var child = tokens.Single(value => value.Token == childHash);
        var leaf = tokens.Single(value => value.Token == leafHash);
        var sessions = await persisted.Set<UserSession>().AsNoTracking().Where(value => value.UserId == user.Id).ToListAsync();
        Assert.Equal(3, tokens.Count);
        Assert.Equal(2, tokens.Count(value => value.IsRevoked));
        Assert.Single(tokens, value => value.IsActive);
        Assert.Equal(childHash, root.ReplacedByToken);
        Assert.Equal(leafHash, child.ReplacedByToken);
        Assert.Equal(rootSession, childSession);
        Assert.Equal(rootSession, leafSession);
        var session = Assert.Single(sessions);
        Assert.Equal(rootSession, session.Id);
        Assert.Equal(leafHash, session.RefreshToken);
        Assert.Null(root.ParentTokenId);
        Assert.Equal(root.Id, child.ParentTokenId);
        Assert.Equal(child.Id, leaf.ParentTokenId);
        Assert.All(tokens, token => Assert.Equal(rootSession, token.SessionId));
        Assert.DoesNotContain(rootRaw, tokens.Select(value => value.Token));
        Assert.DoesNotContain(childRaw, tokens.Select(value => value.Token));
        Assert.DoesNotContain(leafRaw, tokens.Select(value => value.Token));
    }
}

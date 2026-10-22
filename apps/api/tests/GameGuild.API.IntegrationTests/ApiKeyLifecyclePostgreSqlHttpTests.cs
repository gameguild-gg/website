using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

public sealed partial class ApiKeyPolicyPostgreSqlHttpTests
{
    [Fact]
    public async Task TenantIsResolvedFromTheAuthenticatedKeyWithoutAClientOverride()
    {
        using var factory = CreateFactory();
        var owner = await Seed(factory.Services, "valid", ApiKeyScopes.ManageApiKeys);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", owner.Plaintext);
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(body.RootElement.EnumerateArray(), row => row.GetProperty("id").GetGuid() == owner.KeyId);
    }

    [Theory]
    [InlineData("foreign-tenant")]
    [InlineData("inactive-membership")]
    public async Task ValidKeyCannotBypassTenantMembershipOrItsTenantClaim(string boundary)
    {
        using var factory = CreateFactory();
        var owner = await Seed(factory.Services, "valid", ApiKeyScopes.ManageApiKeys);
        var foreign = await Seed(factory.Services, "valid", ApiKeyScopes.ManageApiKeys);
        if (boundary == "inactive-membership")
        {
            await using var write = factory.Services.CreateAsyncScope();
            var db = write.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var membership = await db.Set<TenantMember>().SingleAsync(row =>
                row.UserId == owner.OwnerId && row.TenantId == owner.TenantId);
            membership.IsActive = false;
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", owner.Plaintext);
        client.DefaultRequestHeaders.Add("X-Tenant-Id",
            (boundary == "foreign-tenant" ? foreign.TenantId : owner.TenantId).ToString());
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ActualHttpLifecyclePreservesOwnershipHashingRevocationAndSafeAudit()
    {
        using var factory = CreateFactory();
        var owner = await Seed(factory.Services, "valid", ApiKeyScopes.ManageApiKeys);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", owner.Plaintext);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", owner.TenantId.ToString());
        using var creation = await client.PostAsJsonAsync(Route, new
        {
            name = "Synthetic HTTP lifecycle",
            scopes = new[] { ApiKeyScopes.ManageApiKeys },
            userId = Guid.NewGuid(),
            tenantId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, creation.StatusCode);
        var created = (await creation.Content.ReadFromJsonAsync<CreateApiKeyResponse>())!;
        Assert.NotNull(created);
        using var listing = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        var listed = await listing.Content.ReadAsStringAsync();
        Assert.DoesNotContain(created.ApiKey, listed, StringComparison.Ordinal);
        Assert.DoesNotContain("\"keyHash\"", listed, StringComparison.OrdinalIgnoreCase);

        using var rotation = await client.PostAsJsonAsync(Route + "/" + created.Id + ":rotate",
            new { gracePeriodMinutes = 0 });
        Assert.Equal(HttpStatusCode.OK, rotation.StatusCode);
        var rotated = (await rotation.Content.ReadFromJsonAsync<RotateApiKeyResponse>())!;
        Assert.True(rotated.OldKeyRevoked);
        Assert.Null(rotated.OldKeyGraceEndsAt);
        Assert.Equal(created.Id, rotated.OldKeyId);

        using var oldClient = factory.CreateClient();
        oldClient.DefaultRequestHeaders.Add("X-API-Key", created.ApiKey);
        oldClient.DefaultRequestHeaders.Add("X-Tenant-Id", owner.TenantId.ToString());
        using var oldResponse = await oldClient.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, oldResponse.StatusCode);
        using var replacementClient = factory.CreateClient();
        replacementClient.DefaultRequestHeaders.Add("X-API-Key", rotated.NewKey.ApiKey);
        replacementClient.DefaultRequestHeaders.Add("X-Tenant-Id", owner.TenantId.ToString());
        using var replacementResponse = await replacementClient.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, replacementResponse.StatusCode);
        using var revocation = await client.PostAsJsonAsync(Route + "/" + rotated.NewKey.Id + ":revoke",
            new { reason = "Synthetic HTTP revocation" });
        Assert.Equal(HttpStatusCode.OK, revocation.StatusCode);
        using var rejectedReplacement = await replacementClient.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedReplacement.StatusCode);

        await using var read = factory.Services.CreateAsyncScope();
        var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var old = await db.Set<ApiKey>().AsNoTracking().SingleAsync(row => row.Id == created.Id);
        var replacement = await db.Set<ApiKey>().AsNoTracking().SingleAsync(row => row.Id == rotated.NewKey.Id);
        Assert.Equal(owner.OwnerId, old.UserId);
        Assert.Equal(owner.TenantId, old.TenantId);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(created.ApiKey))).ToLowerInvariant(), old.KeyHash);
        Assert.Equal(created.Id, replacement.ReplacesKeyId);
        Assert.Equal(owner.OwnerId, replacement.UserId);
        Assert.Equal(owner.TenantId, replacement.TenantId);
        Assert.False(old.IsActive);
        Assert.NotNull(old.RevokedAt);
        Assert.False(replacement.IsActive);
        Assert.NotNull(replacement.RevokedAt);
        Assert.True(replacement.UsageCount > 0);
        var audit = await db.Set<AuditLog>().AsNoTracking().Where(row =>
            row.UserId == owner.OwnerId && row.ResourceType == "ApiKey" &&
            (row.ActionType == ApiKeyAuditActions.Created || row.ActionType == ApiKeyAuditActions.Rotated ||
             row.ActionType == ApiKeyAuditActions.Revoked)).ToListAsync();
        Assert.Single(audit, row => row.ActionType == ApiKeyAuditActions.Created && row.ResourceId == created.Id.ToString());
        Assert.Single(audit, row => row.ActionType == ApiKeyAuditActions.Rotated && row.ResourceId == rotated.NewKey.Id.ToString());
        Assert.Single(audit, row => row.ActionType == ApiKeyAuditActions.Revoked && row.ResourceId == rotated.NewKey.Id.ToString());
        Assert.All(audit, row => { Assert.True(row.Success); Assert.Equal(owner.TenantId, row.TenantId); });
        var auditJson = JsonSerializer.Serialize(audit);
        Assert.DoesNotContain(created.ApiKey, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(rotated.NewKey.ApiKey, auditJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActualManagementRouteCannotListRotateOrRevokeAnotherOwnersCredential()
    {
        using var factory = CreateFactory();
        var owner = await Seed(factory.Services, "valid", ApiKeyScopes.ManageApiKeys);
        var foreign = await Seed(factory.Services, "valid", ApiKeyScopes.ManageApiKeys);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", owner.Plaintext);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", owner.TenantId.ToString());
        using var listing = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        using var listed = JsonDocument.Parse(await listing.Content.ReadAsStringAsync());
        Assert.DoesNotContain(listed.RootElement.EnumerateArray(), row => row.GetProperty("id").GetGuid() == foreign.KeyId);
        foreach (var operation in new[] { "rotate", "revoke" })
        {
            using var denied = await client.PostAsJsonAsync(Route + "/" + foreign.KeyId + ":" + operation, new { });
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
            Assert.Contains("ApiKey.NotFound", await denied.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        await using var read = factory.Services.CreateAsyncScope();
        var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var key = await db.Set<ApiKey>().AsNoTracking().SingleAsync(row => row.Id == foreign.KeyId);
        Assert.True(key.IsActive);
        Assert.Null(key.RevokedAt);
        Assert.Null(key.RotationGraceEndsAt);
        Assert.False(await db.Set<ApiKey>().AnyAsync(row => row.ReplacesKeyId == foreign.KeyId));
    }
}

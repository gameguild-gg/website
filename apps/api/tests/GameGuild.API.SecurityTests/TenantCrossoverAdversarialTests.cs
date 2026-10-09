using System.Net;
using System.Text;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.SecurityTests.Infrastructure;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.SecurityTests;

/// <summary>
///     Adversarial scenario family (b): tenant crossover (issue #327).
///     A legitimately authenticated tenant-A member holding a valid grant attempts to
///     reach tenant-B data through every carriage available to an HTTP client: route
///     parameters, query strings, the X-Tenant-Id header and request bodies. Tenant
///     isolation must hold everywhere, and the tenant-A view must never leak tenant-B rows.
/// </summary>
[Collection(AdversarialSecurityCollection.Name)]
public sealed class TenantCrossoverAdversarialTests(AdversarialSecurityFixture fixture)
{
    private const string FeaturesProbe = "/v1/features";
    private const string PermissionsByTenantTemplate = "/api/v1/authorization/tenants/{0}/permissions";

    [Fact]
    public async Task TenantBResourcesAreUnreachableFromTenantAToken()
    {
        var accountA = await fixture.SeedAccountAsync(grantedPermissions: ["features:read"]);
        var accountB = await fixture.SeedAccountAsync(grantedPermissions: ["features:read"]);
        using var client = await fixture.CreateBearerClientAsync(accountA);

        // Positive control: the caller can exercise their granted permission and read
        // their own tenant-scoped permission inventory.
        using var granted = await client.GetAsync(FeaturesProbe);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);

        using var ownInventory = await client.GetAsync(string.Format(PermissionsByTenantTemplate, accountA.TenantId));
        Assert.Equal(HttpStatusCode.OK, ownInventory.StatusCode);
        var ownBody = await ownInventory.Content.ReadAsStringAsync();
        Assert.Contains(accountA.User.Id.ToString(), ownBody, StringComparison.Ordinal);
        Assert.DoesNotContain(accountB.User.Id.ToString(), ownBody, StringComparison.Ordinal);
        Assert.DoesNotContain(accountB.TenantId.ToString(), ownBody, StringComparison.Ordinal);

        // Route-parameter crossover: ask for tenant B's permission inventory explicitly.
        using var routeCrossover = await client.GetAsync(
            string.Format(PermissionsByTenantTemplate, accountB.TenantId) + $"?userId={accountB.User.Id}");
        Assert.True(
            routeCrossover.StatusCode is HttpStatusCode.Forbidden
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.InternalServerError,
            $"tenant-B route parameter must be denied, got {(int)routeCrossover.StatusCode}");
        var routeBody = await routeCrossover.Content.ReadAsStringAsync();
        Assert.DoesNotContain(accountB.User.Id.ToString(), routeBody, StringComparison.Ordinal);

        // Header crossover: present a tenant-B header while holding a tenant-A token.
        // TenantMiddleware must reject members of A reaching into B.
        client.DefaultRequestHeaders.Add("X-Tenant-Id", accountB.TenantId.ToString());
        HttpResponseMessage headerCrossover;
        try
        {
            headerCrossover = await client.GetAsync(FeaturesProbe);
        }
        finally
        {
            client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        }

        using (headerCrossover)
        {
            Assert.True(
                headerCrossover.StatusCode is HttpStatusCode.Forbidden
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.InternalServerError,
                $"tenant-B header with tenant-A token must be denied, got {(int)headerCrossover.StatusCode}");
        }

        // Query-string crossover.
        using var queryCrossover = await client.GetAsync($"{FeaturesProbe}?tenantId={accountB.TenantId}");
        Assert.True(
            queryCrossover.StatusCode is HttpStatusCode.Forbidden
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.InternalServerError,
            $"tenant-B query parameter must be denied, got {(int)queryCrossover.StatusCode}");

        // Body crossover: attempt to grant permissions inside tenant B with a tenant-A token.
        var grantBody = JsonSerializer.Serialize(new
        {
            tenantId = accountB.TenantId,
            userId = accountA.User.Id,
            permissions = new[] { "features:read" },
            grantedBy = accountA.User.Id,
            reason = "adversarial cross-tenant grant attempt",
        });
        using var bodyCrossover = await client.PostAsync(
            "/api/v1/authorization/tenants/grant",
            new StringContent(grantBody, Encoding.UTF8, "application/json"));
        Assert.True(
            bodyCrossover.StatusCode is HttpStatusCode.Forbidden
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.InternalServerError,
            $"cross-tenant grant must be denied, got {(int)bodyCrossover.StatusCode}");

        // Database-level assertion: no grant row may exist placing the tenant-A user in tenant B.
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var crossTenantGrant = await db.Set<TenantPermission>().AsNoTracking()
                .AnyAsync(grant => grant.UserId == accountA.User.Id && grant.TenantId == accountB.TenantId);
            Assert.False(crossTenantGrant, "a cross-tenant grant row must never be created for the attacker");
        }

        // Re-verify the own-tenant view is still intact and still free of tenant-B data.
        using var ownAgain = await client.GetAsync(string.Format(PermissionsByTenantTemplate, accountA.TenantId));
        Assert.Equal(HttpStatusCode.OK, ownAgain.StatusCode);
        var ownAgainBody = await ownAgain.Content.ReadAsStringAsync();
        Assert.DoesNotContain(accountB.User.Id.ToString(), ownAgainBody, StringComparison.Ordinal);
    }
}

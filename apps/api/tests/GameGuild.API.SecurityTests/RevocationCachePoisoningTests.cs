using System.Net;
using GameGuild.API.Database;
using GameGuild.API.SecurityTests.Infrastructure;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.SecurityTests;

/// <summary>
///     Adversarial scenario family (f): post-revocation cache poisoning / stale-allow
///     attempts (issue #327). Builds on the #746 stale-guard service tests by driving the
///     same attack through the full HTTP host: warm the authorization caches with an
///     allowed request, then revoke/deny/expire the grant through the production
///     permission-mutation paths and immediately re-request. A TTL window that keeps
///     answering 200 after the mutation would be a stale-allow vulnerability.
/// </summary>
[Collection(AdversarialSecurityCollection.Name)]
public sealed class RevocationCachePoisoningTests(AdversarialSecurityFixture fixture)
{
    private const int Iterations = 5;
    private const string FeaturesProbe = "/v1/features";

    [Fact]
    public async Task WarmedCacheYieldsNoStaleAllowAfterRepeatedGrantRevokeCycles()
    {
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var account = await fixture.SeedAccountAsync(grantedPermissions: ["features:read"]);
            using var client = await fixture.CreateBearerClientAsync(account);

            // Warm every caching layer the request path consults.
            for (var warmup = 0; warmup < 3; warmup++)
            {
                using var allowed = await client.GetAsync(FeaturesProbe);
                Assert.True(allowed.StatusCode == HttpStatusCode.OK,
                    $"iteration {iteration}: granted request must be allowed, got {(int)allowed.StatusCode}");
            }

            using (var scope = fixture.Factory.Services.CreateScope())
            {
                var grantService = scope.ServiceProvider.GetRequiredService<IPermissionGrantService>();
                var revoked = await grantService.RevokeTenantPermissionAsync(
                    account.User.Id, account.TenantId, ["features:read"]);
                Assert.True(revoked, $"iteration {iteration}: revoke must succeed");
            }

            // Immediate re-request on the same connection and every subsequent one must
            // observe the revocation — the tenant security version bump invalidates the
            // warmed decision caches (#746 stale guard) at HTTP level too.
            for (var probe = 0; probe < 3; probe++)
            {
                using var denied = await client.GetAsync(FeaturesProbe);
                Assert.True(denied.StatusCode == HttpStatusCode.Forbidden,
                    $"iteration {iteration} probe {probe}: stale-allow detected — warmed cache answered {(int)denied.StatusCode} after revocation");
            }
        }
    }

    [Fact]
    public async Task DenyRuleWinsImmediatelyOverAWarmedAllow()
    {
        var account = await fixture.SeedAccountAsync(grantedPermissions: ["features:read"]);
        using var client = await fixture.CreateBearerClientAsync(account);

        using var warm = await client.GetAsync(FeaturesProbe);
        Assert.Equal(HttpStatusCode.OK, warm.StatusCode);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var grantService = scope.ServiceProvider.GetRequiredService<IPermissionGrantService>();
            await grantService.DenyTenantPermissionAsync(account.User.Id, account.TenantId, ["features:read"]);
        }

        using var denied = await client.GetAsync(FeaturesProbe);
        Assert.True(denied.StatusCode == HttpStatusCode.Forbidden,
            $"deny-wins must hold immediately after a warmed allow, got {(int)denied.StatusCode}");
    }

    [Fact]
    public async Task ExpiredAndInactiveGrantsNeverAuthorizeOverHttp()
    {
        // Grant lifecycle states that must contribute nothing anywhere (resolver drops
        // expired/inactive layers on every evaluation) — asserted here over HTTP. The
        // grants are seeded already expired/already inactive, so no earlier allow can be
        // cached: any 200 would be a real finding.
        var expired = await fixture.SeedAccountAsync(grantedPermissions: ["features:read"]);
        var inactive = await fixture.SeedAccountAsync(grantedPermissions: ["features:read"]);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var expiredRow = await db.Set<TenantPermission>()
                .SingleAsync(grant => grant.UserId == expired.User.Id && grant.TenantId == expired.TenantId);
            expiredRow.ExpiresAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();

            var inactiveRow = await db.Set<TenantPermission>()
                .SingleAsync(grant => grant.UserId == inactive.User.Id && grant.TenantId == inactive.TenantId);
            inactiveRow.IsActive = false;
            await db.SaveChangesAsync();
        }

        using var expiredClient = await fixture.CreateBearerClientAsync(expired);
        using var expiredResponse = await expiredClient.GetAsync(FeaturesProbe);
        Assert.True(expiredResponse.StatusCode == HttpStatusCode.Forbidden,
            $"expired grant must not authorize, got {(int)expiredResponse.StatusCode}");

        using var inactiveClient = await fixture.CreateBearerClientAsync(inactive);
        using var inactiveResponse = await inactiveClient.GetAsync(FeaturesProbe);
        Assert.True(inactiveResponse.StatusCode == HttpStatusCode.Forbidden,
            $"inactive grant must not authorize, got {(int)inactiveResponse.StatusCode}");
    }
}

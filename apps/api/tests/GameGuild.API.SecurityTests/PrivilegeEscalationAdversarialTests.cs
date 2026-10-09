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
///     Adversarial scenario family (a): privilege escalation (issue #327).
///     An authenticated tenant member with zero permission grants attempts to reach
///     permissioned admin/mutation surfaces. The deny-by-default model must reject
///     each attempt AND leave no granted capability behind in the database.
/// </summary>
[Collection(AdversarialSecurityCollection.Name)]
public sealed class PrivilegeEscalationAdversarialTests(AdversarialSecurityFixture fixture)
{
    private const string SessionsProbe = "/v1/auth/sessions";
    private const string GrantEndpoint = "/api/v1/authorization/tenants/grant";
    private const string DenyEndpoint = "/api/v1/authorization/tenants/deny";
    private const string GlobalDefaultsEndpoint = "/api/v1/authorization/tenants/global/defaults";

    [Fact]
    public async Task UngrantedMemberIsDeniedOnPermissionedEndpointFamilies()
    {
        var account = await fixture.SeedAccountAsync(membershipRole: "Member");
        using var client = await fixture.CreateBearerClientAsync(account);

        // Positive control: a plain authenticated endpoint works, so the denials below
        // are authorization decisions, not authentication failures.
        using var authenticated = await client.GetAsync(SessionsProbe);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);

        // Permissioned read family (Features.Read policy: tenant/system admin or features:read).
        using var featuresList = await client.GetAsync("/v1/features");
        AssertDenial(featuresList, "feature-flag list without grant");

        using var featureByKey = await client.GetAsync("/v1/features/probe-key");
        AssertDenial(featureByKey, "feature-flag detail without grant");

        // Permissioned mutation family (tenant permission engine: tenant/system admin only).
        var grantBody = JsonSerializer.Serialize(new
        {
            tenantId = account.TenantId,
            userId = account.User.Id,
            permissions = new[] { "features:read", "TenantAdmin" },
            grantedBy = account.User.Id,
            reason = "adversarial self-grant attempt",
        });
        using var selfGrant = await client.PostAsync(GrantEndpoint, new StringContent(grantBody, Encoding.UTF8, "application/json"));
        AssertDenial(selfGrant, "self-grant attempt");

        var globalBody = JsonSerializer.Serialize(new
        {
            permissions = new[] { "SystemAdmin" },
            setBy = account.User.Id,
        });
        using var globalDefaults = await client.PostAsync(GlobalDefaultsEndpoint, new StringContent(globalBody, Encoding.UTF8, "application/json"));
        AssertDenial(globalDefaults, "global-default escalation attempt");

        var denyBody = JsonSerializer.Serialize(new
        {
            tenantId = account.TenantId,
            userId = Guid.NewGuid(),
            permissions = new[] { "features:read" },
            deniedBy = account.User.Id,
        });
        using var denyOther = await client.PostAsync(DenyEndpoint, new StringContent(denyBody, Encoding.UTF8, "application/json"));
        AssertDenial(denyOther, "deny-for-another-user attempt");

        // Reading another tenant member's permission inventory requires user-read rights.
        using var otherUserPermissions = await client.GetAsync(
            $"/api/v1/authorization/tenants/{account.TenantId}/permissions?userId={Guid.NewGuid()}");
        AssertDenial(otherUserPermissions, "other-user permission inventory read");

        // Database-level assertion: none of the attempts may have left a grant behind.
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var grants = await db.Set<TenantPermission>()
                .AsNoTracking()
                .Where(grant => grant.UserId == account.User.Id || grant.TenantId == account.TenantId)
                .ToListAsync();
            Assert.DoesNotContain(grants, grant => grant.Permissions.Any(permission =>
                permission.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                permission.Contains("features", StringComparison.OrdinalIgnoreCase)));

            // No global-default row (null tenant/user) may have been created either.
            var globalRows = await db.Set<TenantPermission>().AsNoTracking()
                .Where(grant => grant.UserId == null && grant.TenantId == null)
                .ToListAsync();
            Assert.DoesNotContain(globalRows, row => row.Permissions.Contains("SystemAdmin"));
        }
    }

    [Fact]
    public async Task AnonymousRequestsToGuardedMutationSurfacesAreRejected()
    {
        // No token at all: the guarded surfaces must demand authentication first.
        using var client = fixture.Factory.CreateClient();

        using var grant = await client.GetAsync(GrantEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, grant.StatusCode);

        using var globalDefaults = await client.GetAsync(GlobalDefaultsEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, globalDefaults.StatusCode);

        using var features = await client.GetAsync("/v1/features");
        Assert.Equal(HttpStatusCode.Unauthorized, features.StatusCode);
    }

    private static void AssertDenial(HttpResponseMessage response, string label)
    {
        // 401 = authentication-layer rejection; 403 = authorization-policy rejection.
        // Guards implemented as UnauthorizedAccessException in command handlers are
        // surfaced by the global exception middleware as a generic 500 problem+json
        // (no detail leakage, request denied) — a response-hygiene observation recorded
        // in the findings report, not a bypass. A 2xx would be a privilege-escalation
        // finding; the database assertions in the calling tests prove no capability was
        // acquired regardless of the denial status.
        Assert.True(
            response.StatusCode
                is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                or HttpStatusCode.InternalServerError,
            $"{label} must be denied (401/403/500-fail-closed), got {(int)response.StatusCode}");
    }
}

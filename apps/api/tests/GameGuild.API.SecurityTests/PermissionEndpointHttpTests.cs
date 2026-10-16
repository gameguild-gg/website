using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.SecurityTests.Infrastructure;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.API.SecurityTests;

/// <summary>
///     HTTP-level endpoint coverage for the permission evaluation and grant surfaces
/// (issue #298): tenant:check, the paginated tenant permission lists (both route
/// families), the global permission validation, and resource-grants grant/revoke —
/// closing the integration-test gaps named in the family sweep.
/// </summary>
[Collection(AdversarialSecurityCollection.Name)]
public sealed class PermissionEndpointHttpTests(AdversarialSecurityFixture fixture)
{
    private const string TenantCheckEndpoint = "/v1/permissions/tenant:check";
    private const string TenantListEndpoint = "/v1/permissions/tenant";
    private const string ResourceCheckEndpoint = "/v1/permissions/resource:check";
    private const string ResourceGrantEndpoint = "/v1/permissions/resource-grants";
    private const string ResourceRevokeEndpoint = "/v1/permissions/resource-grants:revoke";

    [Fact]
    public async Task TenantCheck_ReturnsTrueForGrantedAndFalseForUngrantedPermission()
    {
        var account = await fixture.SeedAccountAsync(
            membershipRole: "TenantAdmin",
            grantedPermissions: ["Read"]);
        using var client = await fixture.CreateBearerClientAsync(account);

        using var granted = await client.PostAsync(
            TenantCheckEndpoint,
            JsonContent(new { userId = account.User.Id, tenantId = account.TenantId, permission = "Read" }));
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        Assert.Equal("true", await granted.Content.ReadAsStringAsync());

        using var ungranted = await client.PostAsync(
            TenantCheckEndpoint,
            JsonContent(new { userId = account.User.Id, tenantId = account.TenantId, permission = "Delete" }));
        Assert.Equal(HttpStatusCode.OK, ungranted.StatusCode);
        Assert.Equal("false", await ungranted.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TenantCheck_FailClosedOnInvalidContext()
    {
        var account = await fixture.SeedAccountAsync(membershipRole: "TenantAdmin");
        using var client = await fixture.CreateBearerClientAsync(account);

        using var response = await client.PostAsync(
            TenantCheckEndpoint,
            JsonContent(new { userId = Guid.Empty, tenantId = account.TenantId, permission = "Read" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("false", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TenantList_PlatformRoute_ReturnsPaginatedPermissions()
    {
        var account = await fixture.SeedAccountAsync(
            membershipRole: "TenantAdmin",
            grantedPermissions: ["Read", "Write", "Comment", "Share", "Create"]);
        using var client = await fixture.CreateBearerClientAsync(account);

        using var response = await client.GetAsync(
            $"{TenantListEndpoint}?userId={account.User.Id}&tenantId={account.TenantId}&includeEffective=false&page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.Equal(account.User.Id, payload.GetProperty("userId").GetGuid());
        Assert.Equal(2, payload.GetProperty("page").GetInt32());
        Assert.Equal(2, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, payload.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, payload.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, payload.GetProperty("permissions").GetArrayLength());
    }

    [Fact]
    public async Task TenantList_ApiRoute_ReturnsPaginatedPermissions()
    {
        var account = await fixture.SeedAccountAsync(
            membershipRole: "TenantAdmin",
            grantedPermissions: ["Read", "Write", "Comment"]);
        using var client = await fixture.CreateBearerClientAsync(account);

        using var response = await client.GetAsync(
            $"/api/v1/authorization/tenants/{account.TenantId}/permissions?userId={account.User.Id}&includeEffective=false&page=1&pageSize=2&sortDescending=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.Equal(3, payload.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, payload.GetProperty("totalPages").GetInt32());
        var page = payload.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Equal(2, page.Count);
        Assert.Equal("Write", page[0], StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Read", page[1], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TenantList_MemberCannotReadAnotherTenantsPermissions()
    {
        var account = await fixture.SeedAccountAsync(membershipRole: "Member");
        var foreignTenant = Guid.NewGuid();
        using var client = await fixture.CreateBearerClientAsync(account);

        using var response = await client.GetAsync(
            $"{TenantListEndpoint}?userId={account.User.Id}&tenantId={foreignTenant}&includeEffective=false");

        // Fail-closed guard: cross-tenant permission reads are rejected (401/403/500
        // fail-closed family — never 2xx).
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.InternalServerError,
            $"cross-tenant permission read must be denied, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task GlobalPermissionCheck_ReportsBaselineMembership()
    {
        var admin = await fixture.SeedAccountAsync(membershipRole: "SystemAdmin");
        using var client = await fixture.CreateBearerClientAsync(admin);

        using var response = await client.GetAsync(
            "/api/v1/authorization/permissions/global:check?permission=features:read");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.Equal("features:read", payload.GetProperty("permission").GetString());
        Assert.Equal("GlobalDefault", payload.GetProperty("source").GetString());
    }

    [Fact]
    public async Task GlobalPermissionCheck_UnprivilegedMemberIsDenied()
    {
        var member = await fixture.SeedAccountAsync(membershipRole: "Member");
        using var client = await fixture.CreateBearerClientAsync(member);

        using var response = await client.GetAsync(
            "/api/v1/authorization/permissions/global:check?permission=features:read");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.InternalServerError,
            $"global-permission validation must be admin-guarded, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ResourceGrants_GrantThenRevoke_RoundTripsThroughHttp()
    {
        var account = await fixture.SeedAccountAsync(membershipRole: "TenantAdmin");
        using var client = await fixture.CreateBearerClientAsync(account);
        var resourceId = Guid.NewGuid();

        var grantBody = JsonSerializer.Serialize(new
        {
            userId = account.User.Id,
            tenantId = account.TenantId,
            resourceId,
            resourceType = "Probe",
            permissions = new[] { "Read" },
            grantedBy = account.User.Id.ToString(),
            reason = "endpoint coverage"
        });
        using var grant = await client.PostAsync(ResourceGrantEndpoint, new StringContent(grantBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);

        using var checkGranted = await client.PostAsync(
            ResourceCheckEndpoint,
            JsonContent(new { userId = account.User.Id, tenantId = account.TenantId, resourceId, permission = "Read" }));
        Assert.Equal(HttpStatusCode.OK, checkGranted.StatusCode);
        Assert.Equal("true", await checkGranted.Content.ReadAsStringAsync());

        var revokeBody = JsonSerializer.Serialize(new
        {
            userId = account.User.Id,
            tenantId = account.TenantId,
            resourceId,
            permissions = new[] { "Read" },
            revokedBy = account.User.Id.ToString(),
            reason = "endpoint coverage"
        });
        using var revoke = await client.PostAsync(ResourceRevokeEndpoint, new StringContent(revokeBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var checkRevoked = await client.PostAsync(
            ResourceCheckEndpoint,
            JsonContent(new { userId = account.User.Id, tenantId = account.TenantId, resourceId, permission = "Read" }));
        Assert.Equal(HttpStatusCode.OK, checkRevoked.StatusCode);
        Assert.Equal("false", await checkRevoked.Content.ReadAsStringAsync());
    }

    private static StringContent JsonContent(object payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static async Task<System.Text.Json.JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
}

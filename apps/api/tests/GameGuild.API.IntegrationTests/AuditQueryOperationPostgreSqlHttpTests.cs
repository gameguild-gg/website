using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

// Real JWT authentication, persisted grants and migrated PostgreSQL. No permission/service replacements.
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AuditQueryOperationPostgreSqlHttpTests(ApiPostgreSqlFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PolicyDefinitionSeeder>().SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("GET", "/v1/admin/audit-logs")]
    [InlineData("GET", "/v1/admin/audit-logs/search/by-date-range?period=last24h")]
    [InlineData("POST", "/v1/admin/audit-logs/search-filter")]
    [InlineData("POST", "/api/audit/search-filter")]
    [InlineData("GET", "/v1/admin/audit-logs/search/by-action-type")]
    [InlineData("GET", "/v1/admin/audit-logs/search/by-action-type/taxonomy")]
    [InlineData("GET", "/v1/admin/audit-logs/statistics")]
    [InlineData("GET", "/v1/admin/security-audit")]
    [InlineData("GET", "/v1/admin/security-audit/permissions")]
    [InlineData("GET", "/v1/admin/security-audit/dashboard")]
    public async Task ReadWithoutTheOperationGrantIsDeniedBeforeReturningData(string method, string route)
    {
        var scenario = await SeedAsync([]);
        using var client = await SignedClientAsync(scenario);
        using var response = method == "POST"
            ? await client.PostAsJsonAsync(route, Conditions(scenario.Marker))
            : await client.GetAsync(route);
        await AssertDeniedAsync(response, scenario);
    }

    [Theory]
    [InlineData("List")]
    [InlineData("Search")]
    [InlineData("Query")]
    public async Task ExactOperationGrantReturnsOnlyTheAuthenticatedTenantsRows(string operation)
    {
        var scenario = await SeedAsync([Key(operation)]);
        using var client = await SignedClientAsync(scenario);
        using var response = await OperationAsync(client, operation, scenario);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("own-" + scenario.Marker, body);
        Assert.DoesNotContain("foreign-" + scenario.Marker, body);
        Assert.DoesNotContain("unscoped-" + scenario.Marker, body);
    }

    [Theory]
    [InlineData("List")]
    [InlineData("Search")]
    [InlineData("Query")]
    public async Task OtherOperationGrantsDoNotSubstituteForTheRequestedOperation(string operation)
    {
        var others = new[] { "List", "Search", "Query" }.Where(value => value != operation).Select(Key).ToArray();
        var scenario = await SeedAsync(others);
        using var client = await SignedClientAsync(scenario);
        using var response = await OperationAsync(client, operation, scenario);
        await AssertDeniedAsync(response, scenario);
    }

    [Theory]
    [InlineData("List")]
    [InlineData("Search")]
    [InlineData("Query")]
    public async Task GrantForAnotherTenantDoesNotAuthorizeTheAuthenticatedTenant(string operation)
    {
        var scenario = await SeedAsync([Key(operation)], foreignGrant: true);
        using var client = await SignedClientAsync(scenario);
        using var response = await OperationAsync(client, operation, scenario);
        await AssertDeniedAsync(response, scenario);
    }

    [Theory]
    [InlineData("List")]
    [InlineData("Search")]
    [InlineData("Query")]
    public async Task OperationGrantDoesNotPermitARequestedForeignTenant(string operation)
    {
        var scenario = await SeedAsync([Key(operation)]);
        using var client = await SignedClientAsync(scenario);
        using var response = await OperationAsync(client, operation, scenario, scenario.ForeignTenantId);
        await AssertDeniedAsync(response, scenario);
    }

    [Theory]
    [InlineData("List")]
    [InlineData("Search")]
    [InlineData("Query")]
    public async Task ExplicitDenyWinsOverTheSamePersistedOperationGrant(string operation)
    {
        var scenario = await SeedAsync([Key(operation)], deny: Key(operation));
        using var client = await SignedClientAsync(scenario);
        using var response = await OperationAsync(client, operation, scenario);
        await AssertDeniedAsync(response, scenario);
    }

    [Theory]
    [InlineData("/v1/admin/audit-logs")]
    [InlineData("/v1/admin/security-audit")]
    public async Task TextDiscoveryOnACollectionAlsoRequiresSearch(string route)
    {
        var scenario = await SeedAsync([QueryPermission.Keys.List]);
        using var client = await SignedClientAsync(scenario);
        using var response = await client.GetAsync(route + "?SearchText=" + scenario.Marker);
        await AssertDeniedAsync(response, scenario);
    }

    [Theory]
    [InlineData("/v1/admin/audit-logs")]
    [InlineData("/v1/admin/security-audit")]
    public async Task ListAndSearchTogetherPermitScopedTextDiscovery(string route)
    {
        var scenario = await SeedAsync([QueryPermission.Keys.List, QueryPermission.Keys.Search]);
        using var client = await SignedClientAsync(scenario);
        using var response = await client.GetAsync(route + "?SearchText=" + scenario.Marker);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("own-" + scenario.Marker, body);
        Assert.DoesNotContain("foreign-" + scenario.Marker, body);
        Assert.DoesNotContain("unscoped-" + scenario.Marker, body);
    }

    [Theory]
    [InlineData("/v1/admin/audit-logs")]
    [InlineData("/v1/admin/security-audit")]
    public async Task BlankSearchTextKeepsTheListPermissionRequirement(string route)
    {
        var scenario = await SeedAsync([QueryPermission.Keys.List]);
        using var client = await SignedClientAsync(scenario);
        using var response = await client.GetAsync(route + "?SearchText=%20");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("own-" + scenario.Marker, body);
        Assert.DoesNotContain("foreign-" + scenario.Marker, body);
        Assert.DoesNotContain("unscoped-" + scenario.Marker, body);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("dashboard")]
    [InlineData("export")]
    public async Task AuthenticationRowsAndAggregatesStayWithinTheRequestedTenant(string surface)
    {
        var scenario = await SeedAsync([QueryPermission.Keys.List, QueryPermission.Keys.Search, QueryPermission.Keys.Query, "audit:export"]);
        using var client = await SignedClientAsync(scenario);
        using var response = surface switch
        {
            "list" => await client.GetAsync("/v1/admin/security-audit"),
            "dashboard" => await client.GetAsync("/v1/admin/security-audit/dashboard"),
            _ => await client.PostAsJsonAsync("/v1/admin/security-audit/:export", new UnifiedSecurityAuditRequest())
        };
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains(scenario.Email, body);
        Assert.DoesNotContain(scenario.ForeignEmail, body);
        Assert.DoesNotContain(scenario.UnscopedEmail, body);
        Assert.DoesNotContain("203.0.113.21", body);
        Assert.DoesNotContain("203.0.113.22", body);
        if (surface == "dashboard")
        {
            using var json = JsonDocument.Parse(body);
            Assert.Equal(1, json.RootElement.GetProperty("totalAuthenticationAttempts").GetInt32());
        }
    }

    private static string Key(string operation) => operation switch
    {
        "List" => QueryPermission.Keys.List,
        "Search" => QueryPermission.Keys.Search,
        "Query" => QueryPermission.Keys.Query,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static AuditConditionSearchRequest Conditions(string marker, Guid? tenantId = null) => new()
    {
        TenantId = tenantId,
        Condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Field,
            Field = AuditFilterField.ActionType,
            Comparison = AuditFilterComparison.Equals,
            Value = marker
        }
    };

    private static Task<HttpResponseMessage> OperationAsync(HttpClient client, string operation, Scenario scenario, Guid? tenantId = null)
    {
        var tenantQuery = tenantId.HasValue ? "&TenantId=" + tenantId.Value : string.Empty;
        return operation switch
        {
            "List" => client.GetAsync("/v1/admin/audit-logs?ActionType=" + scenario.Marker + tenantQuery),
            "Search" => client.GetAsync("/v1/admin/audit-logs/search/by-date-range?period=last24h&ActionType=" + scenario.Marker + tenantQuery),
            "Query" => client.PostAsJsonAsync("/v1/admin/audit-logs/search-filter", Conditions(scenario.Marker, tenantId)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    private static async Task AssertDeniedAsync(HttpResponseMessage response, Scenario scenario)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, body);
        Assert.DoesNotContain(scenario.Marker, body);
        Assert.DoesNotContain(scenario.Email, body);
        Assert.DoesNotContain(scenario.ForeignEmail, body);
        Assert.DoesNotContain(scenario.ForeignTenantId.ToString(), body);
    }

    private async Task<HttpClient> SignedClientAsync(Scenario scenario)
    {
        await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.Equal(typeof(JwtBearerHandler), (await schemes.GetSchemeAsync("Bearer"))!.HandlerType);
        Assert.Equal("Bearer", (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name);
        var token = await scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
            scenario.UserId, scenario.Email, [], scenario.TenantId, scenario.TokenVersion, DateTimeOffset.UtcNow);
        var client = fixture.RealAuthenticationFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", scenario.TenantId.ToString());
        return client;
    }

    private async Task<Scenario> SeedAsync(string[] permissions, bool foreignGrant = false, string? deny = null)
    {
        await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        var owner = User.Create("owner-" + marker + "@example.test", "Synthetic audit reader");
        owner.Username = "owner-" + marker;
        var foreign = User.Create("foreign-" + marker + "@example.test", "Synthetic foreign reader");
        foreign.Username = "foreign-" + marker;
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Audit " + marker, Slug = "audit-" + marker, AdminEmail = owner.Email, IsActive = true };
        var otherTenant = new Tenant { Id = Guid.NewGuid(), Name = "Other " + marker, Slug = "other-" + marker, AdminEmail = foreign.Email, IsActive = true };
        db.AddRange(owner, foreign, tenant, otherTenant);
        db.AddRange(
            new TenantMember { Id = Guid.NewGuid(), UserId = owner.Id, TenantId = tenant.Id, Role = "Member", IsActive = true },
            new TenantMember { Id = Guid.NewGuid(), UserId = foreign.Id, TenantId = otherTenant.Id, Role = "Member", IsActive = true });
        db.Add(new TenantPermission
        {
            UserId = owner.Id, TenantId = tenant.Id,
            Permissions = foreignGrant ? ["audit:read"] : ["audit:read", .. permissions],
            DenyPermissions = deny is null ? [] : [deny]
        });
        if (foreignGrant)
            db.Add(new TenantPermission { UserId = owner.Id, TenantId = otherTenant.Id, Permissions = permissions });
        var now = DateTime.UtcNow.AddMinutes(-2);
        db.AddRange(
            new AuditLog { TenantId = tenant.Id, UserId = owner.Id, ResourceType = "Synthetic", ActionType = marker, Description = "own-" + marker, CreatedAt = now },
            new AuditLog { TenantId = otherTenant.Id, UserId = foreign.Id, ResourceType = "Synthetic", ActionType = marker, Description = "foreign-" + marker, CreatedAt = now },
            new AuditLog { TenantId = null, UserId = foreign.Id, ResourceType = "Synthetic", ActionType = marker, Description = "unscoped-" + marker, CreatedAt = now });
        var unscopedEmail = "unscoped-" + marker + "@example.test";
        db.AddRange(
            new AuthenticationAttempt { Id = Guid.NewGuid(), TenantId = tenant.Id, UserId = owner.Id, Email = owner.Email, IpAddress = "203.0.113.20", AttemptedAt = now, IsSuccessful = true },
            new AuthenticationAttempt { Id = Guid.NewGuid(), TenantId = otherTenant.Id, UserId = foreign.Id, Email = foreign.Email, IpAddress = "203.0.113.21", AttemptedAt = now, IsSuccessful = true },
            new AuthenticationAttempt { Id = Guid.NewGuid(), TenantId = null, UserId = foreign.Id, Email = unscopedEmail, IpAddress = "203.0.113.22", AttemptedAt = now, IsSuccessful = false });
        await db.SaveChangesAsync();
        return new(owner.Id, owner.Email, owner.TokenVersion, tenant.Id, otherTenant.Id, foreign.Email, unscopedEmail, marker);
    }

    private sealed record Scenario(Guid UserId, string Email, int TokenVersion, Guid TenantId, Guid ForeignTenantId,
        string ForeignEmail, string UnscopedEmail, string Marker);
}

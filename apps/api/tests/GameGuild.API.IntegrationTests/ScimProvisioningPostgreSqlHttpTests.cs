using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Provisioning;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

/// <summary>
///     RFC 7643/7644 conformance-style HTTP tests for the SCIM provisioning surface
/// (issue #269): discovery documents, Users CRUD/PATCH/filter/pagination with
/// idempotent externalId, deprovision revocation, cross-tenant isolation, Groups with
/// membership, Bulk with bulkId resolution, provisioning-token lifecycle, and the
/// feature flag. Request payloads are recorded JSON fixtures.
/// </summary>
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class ScimProvisioningPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
    : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _scimEnabledFactory = fixture.Factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Scim:Enabled"] = "true"
            }));
    });

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _adminA = Guid.NewGuid();
    private readonly Guid _adminB = Guid.NewGuid();

    /// <summary>
    ///     The PostgreSQL collection shares one database across every test method with no
    ///     per-test reset, while <c>Users.Email</c>/<c>Users.Username</c> and
    ///     <c>Tenants.Name</c> are globally unique. Each test instance (xUnit constructs
    ///     one per method) therefore stamps its fixture user names/emails with this marker
    ///     and names its tenants uniquely, exactly like the sibling marker-based suites.
    /// </summary>
    private readonly string _marker = Guid.NewGuid().ToString("N")[..8];

    private string _tokenA = string.Empty;
    private string _tokenB = string.Empty;

    private string Fixture(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Scim", name))
            .Replace("scim.", $"scim.{_marker}.", StringComparison.Ordinal);

    public async Task InitializeAsync()
    {
        using (var scope = _scimEnabledFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Set<Tenant>().AddRange(
                new Tenant { Id = _tenantA, Name = $"SCIM Tenant A {_marker}", Slug = $"scim-a-{Guid.NewGuid():N}", AdminEmail = $"admin-{_tenantA:N}@scim.test", IsActive = true },
                new Tenant { Id = _tenantB, Name = $"SCIM Tenant B {_marker}", Slug = $"scim-b-{Guid.NewGuid():N}", AdminEmail = $"admin-{_tenantB:N}@scim.test", IsActive = true });
            await context.SaveChangesAsync();

            // The admin surface is policy-guarded; policies resolve from the store and
            // fail closed when the definition is missing, so seed the defaults once.
            await scope.ServiceProvider.GetRequiredService<PolicyDefinitionSeeder>().SeedAsync();
        }

        _tokenA = await IssueTokenAsync(fixture.CreateAuthenticatedClient(_adminA, _tenantA, isSystemAdmin: true));
        _tokenB = await IssueTokenAsync(fixture.CreateAuthenticatedClient(_adminB, _tenantB, isSystemAdmin: true));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CreateScimClient(string token)
    {
        var client = _scimEnabledFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> IssueTokenAsync(HttpClient adminClient)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/v1/auth/scim-provisioning-tokens",
            new { Name = "conformance", Scopes = new[] { "scim:read", "scim:write" } });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a tenant system administrator can issue provisioning tokens: {0}",
            await response.Content.ReadAsStringAsync());

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return payload["token"]!.GetValue<string>();
    }

    // ── Discovery ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ServiceProviderConfig_AdvertisesExactlyTheImplementedSurface()
    {
        using var client = CreateScimClient(_tokenA);

        var response = await client.GetAsync("/scim/v2/ServiceProviderConfig");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["schemas"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .Should().Contain("urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig");
        body["patch"]!["supported"]!.GetValue<bool>().Should().BeTrue();
        body["filter"]!["supported"]!.GetValue<bool>().Should().BeTrue();
        body["bulk"]!["supported"]!.GetValue<bool>().Should().BeTrue();
        body["sort"]!["supported"]!.GetValue<bool>().Should().BeFalse();
        body["etag"]!["supported"]!.GetValue<bool>().Should().BeFalse();
        body["changeLog"]!["supported"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Schemas_And_ResourceTypes_ExposeUserAndGroup()
    {
        using var client = CreateScimClient(_tokenA);

        var schemas = JsonNode.Parse(await (
            await client.GetAsync("/scim/v2/Schemas")).Content.ReadAsStringAsync())!;
        schemas["Resources"]!.AsArray().Should().HaveCount(2);

        var userSchema = JsonNode.Parse(await (
            await client.GetAsync("/scim/v2/Schemas/urn:ietf:params:scim:schemas:core:2.0:User")).Content.ReadAsStringAsync())!;
        userSchema["id"]!.GetValue<string>().Should().Be("urn:ietf:params:scim:schemas:core:2.0:User");

        var resourceTypes = JsonNode.Parse(await (
            await client.GetAsync("/scim/v2/ResourceTypes")).Content.ReadAsStringAsync())!;
        resourceTypes["totalResults"]!.GetValue<int>().Should().Be(2);
    }

    // ── Users lifecycle ───────────────────────────────────────────────────────

    [Fact]
    public async Task UserLifecycle_CreateGetPatchReplaceDelete_FromFixtures()
    {
        using var client = CreateScimClient(_tokenA);

        var created = await client.PostAsync("/scim/v2/Users", FixtureContent("create-user.json"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdBody = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var userId = createdBody["id"]!.GetValue<string>();
        createdBody["externalId"]!.GetValue<string>().Should().Be("00u5pljx8TkcQBZo2PxB");
        createdBody["active"]!.GetValue<bool>().Should().BeTrue();
        createdBody["meta"]!["resourceType"]!.GetValue<string>().Should().Be("User");

        var fetched = await client.GetAsync($"/scim/v2/Users/{userId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        (JsonNode.Parse(await fetched.Content.ReadAsStringAsync())!["id"]!.GetValue<string>())
            .Should().Be(userId);

        var patched = await client.PatchAsync(
            $"/scim/v2/Users/{userId}",
            FixtureContent("patch-user-active.json"));
        patched.StatusCode.Should().Be(HttpStatusCode.OK);
        var patchedBody = JsonNode.Parse(await patched.Content.ReadAsStringAsync())!;
        patchedBody["active"]!.GetValue<bool>().Should().BeFalse();
        patchedBody["displayName"]!.GetValue<string>().Should().Be("Barbara J. Jensen");

        var replaced = await client.PutAsync(
            $"/scim/v2/Users/{userId}",
            new StringContent(Fixture("create-user.json"), MediaTypeHeaderValue.Parse("application/json")));
        replaced.StatusCode.Should().Be(HttpStatusCode.OK);
        var replacedBody = JsonNode.Parse(await replaced.Content.ReadAsStringAsync())!;
        replacedBody["active"]!.GetValue<bool>().Should().BeTrue("PUT restores the fixture state");

        var deleted = await client.DeleteAsync($"/scim/v2/Users/{userId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/scim/v2/Users/{userId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateUser_IsIdempotentOnExternalId()
    {
        using var client = CreateScimClient(_tokenA);

        var first = await client.PostAsync("/scim/v2/Users", FixtureContent("create-user-inactive.json"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = JsonNode.Parse(await first.Content.ReadAsStringAsync())!;
        firstBody["active"]!.GetValue<bool>().Should().BeFalse();

        var second = await client.PostAsync("/scim/v2/Users", FixtureContent("create-user-inactive.json"));
        second.StatusCode.Should().Be(HttpStatusCode.OK,
            "RFC 7644 idempotent create returns the existing resource, not a duplicate");
        var secondBody = JsonNode.Parse(await second.Content.ReadAsStringAsync())!;
        secondBody["id"]!.GetValue<string>().Should().Be(firstBody["id"]!.GetValue<string>());

        var list = await ListUsersAsync(client, "externalId eq \"00u5pljx8TkcQBZo2PxC\"");
        list["totalResults"]!.GetValue<int>().Should().Be(1, "no duplicate user was created");
    }

    [Fact]
    public async Task ListUsers_FiltersAndPaginates_OneBased()
    {
        using var client = CreateScimClient(_tokenA);
        await CreateThreePagedUsersAsync(client);

        var all = await ListUsersAsync(client, "userName sw \"scim.page\"");
        all["totalResults"]!.GetValue<int>().Should().Be(3);

        // RFC 7644 §3.4.2.4: startIndex is the 1-based index of the FIRST result on
        // the page, so startIndex=2&count=2 over three matches returns results 2 and 3
        // (not a single trailing item). The server's ordering key is an implementation
        // detail, so pages are asserted as a sliding window rather than fixed users.
        static List<string> PageIds(JsonNode body)
            => body["Resources"]!.AsArray().Select(resource => resource!["id"]!.GetValue<string>()).ToList();

        var firstPageBody = JsonNode.Parse(await (
            await client.GetAsync("/scim/v2/Users?filter=" + Uri.EscapeDataString("userName sw \"scim.page\"") + "&startIndex=1&count=2")
            ).Content.ReadAsStringAsync())!;
        firstPageBody["startIndex"]!.GetValue<int>().Should().Be(1);
        firstPageBody["itemsPerPage"]!.GetValue<int>().Should().Be(2);
        firstPageBody["totalResults"]!.GetValue<int>().Should().Be(3);
        var firstPageIds = PageIds(firstPageBody);
        firstPageIds.Should().HaveCount(2);

        var page = await client.GetAsync("/scim/v2/Users?filter=" + Uri.EscapeDataString("userName sw \"scim.page\"") + "&startIndex=2&count=2");
        var pageBody = JsonNode.Parse(await page.Content.ReadAsStringAsync())!;
        pageBody["startIndex"]!.GetValue<int>().Should().Be(2);
        pageBody["itemsPerPage"]!.GetValue<int>().Should().Be(2);
        pageBody["totalResults"]!.GetValue<int>().Should().Be(3);
        var secondPageIds = PageIds(pageBody);
        secondPageIds.Should().HaveCount(2, "startIndex=2&count=2 returns the second and third of the three matches");
        secondPageIds[0].Should().Be(firstPageIds[1], "the second page starts at the second result of the first page");
        secondPageIds.Should().NotContain(firstPageIds[0], "the first page's leading result is not repeated");

        var tailBody = JsonNode.Parse(await (
            await client.GetAsync("/scim/v2/Users?filter=" + Uri.EscapeDataString("userName sw \"scim.page\"") + "&startIndex=4&count=2")
            ).Content.ReadAsStringAsync())!;
        tailBody["totalResults"]!.GetValue<int>().Should().Be(3);
        PageIds(tailBody).Should().BeEmpty("a start index past the last result yields an empty page, not an error");
    }

    [Fact]
    public async Task UnsupportedFilter_Returns_400_WithInvalidFilterScimType()
    {
        using var client = CreateScimClient(_tokenA);

        var response = await client.GetAsync("/scim/v2/Users?filter=" + Uri.EscapeDataString("userName gt \"a\""));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["schemas"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .Should().Contain("urn:ietf:params:scim:api:messages:2.0:Error");
        body["scimType"]!.GetValue<string>().Should().Be("invalidFilter");
        body["status"]!.GetValue<int>().Should().Be(400);
    }

    [Fact]
    public async Task UnknownAttributeFilter_Returns_400_WithInvalidFilterScimType()
    {
        using var client = CreateScimClient(_tokenA);

        var response = await client.GetAsync("/scim/v2/Users?filter=" + Uri.EscapeDataString("nickName eq \"babs\""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["scimType"]!.GetValue<string>()
            .Should().Be("invalidFilter");
    }

    // ── Tenant isolation & auth ───────────────────────────────────────────────

    [Fact]
    public async Task CrossTenantAccess_IsIsolated()
    {
        using var clientA = CreateScimClient(_tokenA);
        using var clientB = CreateScimClient(_tokenB);

        var created = await clientA.PostAsync("/scim/v2/Users", FixtureContent("create-user.json"));
        var userId = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        (await clientB.GetAsync($"/scim/v2/Users/{userId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var tenantBList = await ListUsersAsync(clientB, "userName co \"scim\"");
        tenantBList["totalResults"]!.GetValue<int>().Should().Be(0, "tenant B never sees tenant A users");
    }

    [Fact]
    public async Task MissingOrInvalidToken_IsRejected_401()
    {
        using var anonymous = _scimEnabledFactory.CreateClient();
        (await anonymous.GetAsync("/scim/v2/Users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var invalid = _scimEnabledFactory.CreateClient();
        invalid.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "gg_scim_invalid_invalid_invalid_invalid00");
        (await invalid.GetAsync("/scim/v2/Users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FeatureFlag_WhenDisabled_Returns404()
    {
        // The shared fixture never enables Scim, so its factory serves the disabled default.
        using var client = fixture.Factory.CreateClient();

        (await client.GetAsync("/scim/v2/ServiceProviderConfig")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/scim/v2/Users")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deprovision_RevokesSessionsAndTokens()
    {
        using var client = CreateScimClient(_tokenA);
        var created = await client.PostAsync("/scim/v2/Users", FixtureContent("create-user.json"));
        var createdBody = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var userId = Guid.Parse(createdBody["id"]!.GetValue<string>());

        int tokenVersionBefore;
        using (var scope = _scimEnabledFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            tokenVersionBefore = (await context.Set<User>().SingleAsync(user => user.Id == userId)).TokenVersion;
        }

        (await client.DeleteAsync($"/scim/v2/Users/{userId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _scimEnabledFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await context.Set<User>().AsNoTracking().SingleAsync(u => u.Id == userId);
            user.IsDeleted.Should().BeTrue("deprovision soft-deletes the user");
            user.TokenVersion.Should().BeGreaterThan(tokenVersionBefore,
                "the token version bump invalidates every issued access token");
            (await context.Set<RefreshToken>()
                    .CountAsync(token => token.UserId == userId && token.RevokedAt == null))
                .Should().Be(0, "all refresh tokens are revoked on deprovision");
        }
    }

    // ── Groups ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GroupLifecycle_MembershipPatch_BumpsTenantSecurityVersion()
    {
        long versionBefore;
        using (var scope = _scimEnabledFactory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ITenantSecurityVersionStore>();
            versionBefore = await store.GetVersionAsync(_tenantA.ToString());
        }

        using var client = CreateScimClient(_tokenA);
        var memberId = await CreateMemberUserAsync(client);

        var created = await client.PostAsync("/scim/v2/Groups", FixtureContent("create-group.json"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdBody = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var groupId = createdBody["id"]!.GetValue<string>();
        createdBody["displayName"]!.GetValue<string>().Should().Be("SCIM Engineering");
        createdBody["members"]!.AsArray().Should().BeEmpty();

        // Idempotent group create on externalId.
        var repeated = await client.PostAsync("/scim/v2/Groups", FixtureContent("create-group.json"));
        repeated.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonNode.Parse(await repeated.Content.ReadAsStringAsync())!["id"]!.GetValue<string>().Should().Be(groupId);

        // Add a member.
        var addMember = await client.PatchAsync(
            $"/scim/v2/Groups/{groupId}",
            JsonContent(new
            {
                schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:PatchOp" },
                Operations = new object[]
                {
                    new { op = "add", path = "members", value = new[] { new { value = memberId } } }
                }
            }));
        addMember.StatusCode.Should().Be(HttpStatusCode.OK);
        var withMember = JsonNode.Parse(await addMember.Content.ReadAsStringAsync())!;
        withMember["members"]!.AsArray().Should().HaveCount(1);

        // Remove the member via value filter.
        var removeMember = await client.PatchAsync(
            $"/scim/v2/Groups/{groupId}",
            JsonContent(new
            {
                schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:PatchOp" },
                Operations = new object[]
                {
                    new { op = "remove", path = $"members[value eq \"{memberId}\"]" }
                }
            }));
        removeMember.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonNode.Parse(await removeMember.Content.ReadAsStringAsync())!["members"]!.AsArray().Should().BeEmpty();

        // Filter groups by displayName.
        var listed = await client.GetAsync("/scim/v2/Groups?filter=" + Uri.EscapeDataString("displayName sw \"SCIM Eng\""));
        var listBody = JsonNode.Parse(await listed.Content.ReadAsStringAsync())!;
        listBody["totalResults"]!.GetValue<int>().Should().Be(1);

        (await client.DeleteAsync($"/scim/v2/Groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/scim/v2/Groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        long versionAfter;
        using (var scope = _scimEnabledFactory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ITenantSecurityVersionStore>();
            versionAfter = await store.GetVersionAsync(_tenantA.ToString());
        }
        versionAfter.Should().BeGreaterThan(versionBefore,
            "membership mutations bump the tenant security version so permission caches invalidate");
    }

    // ── Bulk ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bulk_CreatesUsersAndGroup_ResolvingBulkIdReferences()
    {
        using var client = CreateScimClient(_tokenA);

        var response = await client.PostAsync("/scim/v2/Bulk", FixtureContent("bulk-users-and-group.json"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var operations = body["Operations"]!.AsArray();
        operations.Should().HaveCount(3);
        operations.Select(operation => operation!["status"]!["code"]!.GetValue<int>())
            .Should().OnlyContain(code => code == 201);

        var groupLocation = operations.Last()!["location"]!.GetValue<string>();
        var groupResponse = await client.GetAsync(groupLocation);
        groupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var group = JsonNode.Parse(await groupResponse.Content.ReadAsStringAsync())!;
        group["members"]!.AsArray().Should().HaveCount(2,
            "bulkId member references resolve to the created users");
    }

    [Fact]
    public async Task Bulk_HonorsFailOnErrors_AndReportsPerOperationStatus()
    {
        using var client = CreateScimClient(_tokenA);

        var response = await client.PostAsync("/scim/v2/Bulk", JsonContent(new
        {
            schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:BulkRequest" },
            failOnErrors = 1,
            Operations = new object[]
            {
                new { method = "DELETE", path = $"/Users/{Guid.NewGuid()}" },
                new { method = "POST", path = "/Users", bulkId = "never-reached", data = new { } }
            }
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var operations = body["Operations"]!.AsArray();
        operations.Should().HaveCount(1, "processing stops once failOnErrors is reached");
        operations[0]!["status"]!["code"]!.GetValue<int>().Should().Be(404);
    }

    // ── Token administration ──────────────────────────────────────────────────

    [Fact]
    public async Task TokenAdministration_RequiresThePolicyPermission()
    {
        using var regularAdmin = fixture.CreateAuthenticatedClient(Guid.NewGuid(), _tenantA, isSystemAdmin: false);

        var response = await regularAdmin.PostAsJsonAsync(
            "/v1/auth/scim-provisioning-tokens",
            new { Name = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the Provisioning.ManageTokens policy fails closed for non-system administrators without the permission");
    }

    [Fact]
    public async Task RotatedToken_WorksDuringGrace_AndDiesAfterRevoke()
    {
        var adminClient = fixture.CreateAuthenticatedClient(_adminA, _tenantA, isSystemAdmin: true);

        var tokenId = await TokenIdForAdminAsync(adminClient);
        var rotateResponse = await adminClient.PostAsJsonAsync(
            $"/v1/auth/scim-provisioning-tokens/{tokenId}:rotate",
            new { GracePeriodMinutes = 10 });
        rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = JsonNode.Parse(await rotateResponse.Content.ReadAsStringAsync())!;
        var newToken = rotated["token"]!.GetValue<string>();
        var newTokenId = Guid.Parse(rotated["id"]!.GetValue<string>());

        // Old token still valid inside the grace window.
        using var oldClient = CreateScimClient(_tokenA);
        (await oldClient.GetAsync("/scim/v2/ResourceTypes")).StatusCode.Should().Be(HttpStatusCode.OK);

        using var newClient = CreateScimClient(newToken);
        (await newClient.GetAsync("/scim/v2/ResourceTypes")).StatusCode.Should().Be(HttpStatusCode.OK);

        var revokeResponse = await adminClient.PostAsJsonAsync(
            $"/v1/auth/scim-provisioning-tokens/{newTokenId}:revoke",
            new { Reason = "test" });
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var revokedClient = CreateScimClient(newToken);
        (await revokedClient.GetAsync("/scim/v2/ResourceTypes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ScopedToken_CannotMutate()
    {
        using var scope = _scimEnabledFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (entity, plaintext) = ScimProvisioningToken.Create(_tenantA, _adminA, "read-only", ["scim:read"]);
        context.Set<ScimProvisioningToken>().Add(entity);
        await context.SaveChangesAsync();

        using var readOnly = CreateScimClient(plaintext);
        (await readOnly.GetAsync("/scim/v2/Users")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await readOnly.PostAsync("/scim/v2/Users", FixtureContent("create-user.json")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "a scim:read-only token must not be able to mutate resources");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpContent FixtureContent(string name)
        => new StringContent(Fixture(name), MediaTypeHeaderValue.Parse("application/json"));

    private static HttpContent JsonContent(object payload)
        => System.Net.Http.Json.JsonContent.Create(payload);

    private static async Task<JsonNode> ListUsersAsync(HttpClient client, string filter)
    {
        var response = await client.GetAsync("/scim/v2/Users?filter=" + Uri.EscapeDataString(filter));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private async Task CreateThreePagedUsersAsync(HttpClient client)
    {
        for (var index = 1; index <= 3; index++)
        {
            var response = await client.PostAsync("/scim/v2/Users", JsonContent(new
            {
                schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
                externalId = $"00u_page_{index}",
                userName = $"scim.page{index}{_marker}",
                displayName = $"Page User {index}",
                emails = new[] { new { value = $"scim.page{index}{_marker}@example.com", primary = true } }
            }));
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    private async Task<string> CreateMemberUserAsync(HttpClient client)
    {
        var response = await client.PostAsync("/scim/v2/Users", JsonContent(new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
            externalId = "00u_group_member",
            userName = $"scim.group.member{_marker}",
            displayName = "Group Member",
            emails = new[] { new { value = $"scim.group.member{_marker}@example.com", primary = true } }
        }));
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();
    }

    private static async Task<Guid> TokenIdForAdminAsync(HttpClient adminClient)
    {
        var listResponse = await adminClient.GetAsync("/v1/auth/scim-provisioning-tokens");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = JsonNode.Parse(await listResponse.Content.ReadAsStringAsync())!.AsArray();
        tokens.Should().NotBeEmpty();
        return Guid.Parse(tokens.OrderByDescending(token => token!["createdAt"]!.GetValue<string>()).First()!["id"]!.GetValue<string>());
    }
}

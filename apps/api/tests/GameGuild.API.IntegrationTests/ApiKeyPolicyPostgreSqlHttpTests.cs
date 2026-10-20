using System.Net;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

/// <summary>Real API-key scheme, tenant/actor/authorization pipeline and PostgreSQL repository.
/// Explicit supported policy configuration selects ApiKey; no synthetic identity authenticates these requests.</summary>
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class ApiKeyPolicyPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string Route = "/v1/auth/api-keys";

    [Fact]
    public async Task ConfiguredApiKeyPolicyCarriesItsAuthenticatedOwnerIntoTheActualHandler()
    {
        using var factory = CreateFactory();
        var seeded = await Seed(factory.Services, "valid", ApiKeyScopes.ManageApiKeys);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", seeded.Plaintext);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", seeded.TenantId.ToString());
        using var response = await client.GetAsync(Route);
        var body = await response.Content.ReadAsStringAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Set<ApiKey>().AsNoTracking().SingleAsync(key => key.Id == seeded.KeyId);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Configured real ApiKey policy returned {(int)response.StatusCode}; persisted credential usage={persisted.UsageCount}: {body}");
        using var document = JsonDocument.Parse(body);
        Assert.Contains(document.RootElement.EnumerateArray(), row => row.GetProperty("id").GetGuid() == seeded.KeyId);
        Assert.DoesNotContain(seeded.Plaintext, body, StringComparison.Ordinal);
        Assert.Equal(seeded.OwnerId, persisted.UserId);
        Assert.True(persisted.UsageCount > 0);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("expired")]
    [InlineData("revoked")]
    public async Task ConfiguredPolicyRejectsUnavailableCredentials(string credential)
    {
        using var factory = CreateFactory();
        var seeded = await Seed(factory.Services, credential, ApiKeyScopes.ManageApiKeys);
        using var client = factory.CreateClient();
        if (credential != "missing")
        {
            client.DefaultRequestHeaders.Add("X-API-Key", credential == "unknown" ? "never-issued-integration-key" : seeded.Plaintext);
        }
        client.DefaultRequestHeaders.Add("X-Tenant-Id", seeded.TenantId.ToString());
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, (await db.Set<ApiKey>().AsNoTracking().SingleAsync(key => key.Id == seeded.KeyId)).UsageCount);
    }

    [Fact]
    public async Task ConfiguredPolicyDoesNotGrantTheManageScopeToAnUnrelatedScope()
    {
        using var factory = CreateFactory();
        var seeded = await Seed(factory.Services, "valid", "reports:read");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", seeded.Plaintext);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", seeded.TenantId.ToString());
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateFactory() => fixture.CreateFactory(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:EnableApiKeyAuthentication"] = "true",
            ["Authorization:DefaultPolicy"] = "ApiKeyIntegrationAcceptance",
            ["Authorization:Policies:ApiKeyIntegrationAcceptance:RequireAuthenticatedUser"] = "true",
            ["Authorization:Policies:ApiKeyIntegrationAcceptance:AuthenticationSchemes:0"] = ApiKeyAuthenticationOptions.SchemeName
        }));
        builder.ConfigureTestServices(services => services.PostConfigure<AuthenticationOptions>(options =>
        {
            // Remove the common fixture's synthetic default, retaining the real host's Bearer default.
            // ApiKey selection must come from the actual configured authorization policy above.
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }));
    });

    private static async Task<SeededKey> Seed(IServiceProvider services, string credential, string grantedScope)
    {
        // Fail clearly when the host did not bind the intended supported configuration.
        // Merely receiving 401 for absent/invalid keys cannot prove that ApiKey ran.
        var schemes = services.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemes.GetSchemeAsync(ApiKeyAuthenticationOptions.SchemeName);
        Assert.NotNull(scheme);
        Assert.Equal(typeof(ApiKeyAuthenticationHandler), scheme.HandlerType);
        var policyProvider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();
        Assert.Contains(ApiKeyAuthenticationOptions.SchemeName, defaultPolicy.AuthenticationSchemes);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        var owner = User.Create(marker + "@example.test", "Synthetic API-key integration account");
        owner.Username = marker;
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Synthetic API-key tenant " + marker, Slug = "key-" + marker,
            AdminEmail = marker + "@example.test", IsActive = true };
        var (key, plaintext) = ApiKey.Create(owner.Id, tenant.Id, "Synthetic integration key", [grantedScope]);
        if (credential == "expired") { key.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); }
        if (credential == "revoked") { key.Revoke("Synthetic integration revocation"); }
        db.AddRange(owner, tenant, new TenantMember { Id = Guid.NewGuid(), UserId = owner.Id, TenantId = tenant.Id,
            Role = "Member", IsActive = true }, key);
        await db.SaveChangesAsync();
        return new(owner.Id, tenant.Id, key.Id, plaintext);
    }

    private sealed record SeededKey(Guid OwnerId, Guid TenantId, Guid KeyId, string Plaintext);
}

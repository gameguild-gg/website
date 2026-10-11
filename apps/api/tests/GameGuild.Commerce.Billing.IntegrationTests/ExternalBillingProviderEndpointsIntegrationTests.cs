using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Commerce.Billing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Billing.IntegrationTests;

/// <summary>
///     Integration tests for the external billing provider management endpoints
///     (issue #397): authorization over the real pipeline (anonymous 401, non-admin
///     403, admin 200), the enable/disable round-trip through the real registry and
///     persistence, fail-closed behavior for unknown provider keys, and the
///     report-only migration dry-run.
/// </summary>
public class ExternalBillingProviderEndpointsIntegrationTests
    : IClassFixture<WebApplicationFactory<GameGuild.API.Program>>, IDisposable
{
    private readonly WebApplicationFactory<GameGuild.API.Program> _factory;
    private readonly HttpClient _client;
    private static readonly string DatabaseName = $"ExternalProviderTestDb_{Guid.NewGuid()}";

    public ExternalBillingProviderEndpointsIntegrationTests(WebApplicationFactory<GameGuild.API.Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                // In-memory EF store (same harness as the webhook endpoint tests).
                var descriptorsToRemove = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                        descriptor.ServiceType == typeof(ApplicationDbContext) ||
                        descriptor.ServiceType.FullName?.Contains("EntityFramework") == true ||
                        descriptor.ImplementationType?.FullName?.Contains("Npgsql") == true)
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(DatabaseName));
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

                // Test authentication: presence of the header authenticates, the
                // X-Test-Role header drives the role claims.
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = ProviderTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = ProviderTestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, ProviderTestAuthHandler>(ProviderTestAuthHandler.SchemeName, _ => { });

                // Deterministic policy resolution for the test host: the SystemAdmin
                // policy is a role requirement instead of the database-seeded definition.
                services.AddAuthorization(options => options.AddPolicy(
                    GameGuild.Identity.Authorization.Policies.SystemAdmin,
                    policy => policy.RequireRole("SystemAdmin")));
                services.RemoveAll<IAuthorizationPolicyProvider>();
                services.TryAddSingleton<IAuthorizationPolicyProvider>(provider =>
                    new DefaultAuthorizationPolicyProvider(provider.GetRequiredService<IOptions<AuthorizationOptions>>()));
            });
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private HttpClient CreateClient(string? role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            ProviderTestAuthHandler.SchemeName,
            "provider-test-user");
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        return client;
    }

    [Fact]
    public async Task ListProviders_WithoutAuthentication_ShouldBeChallengedWith401()
    {
        var response = await _client.GetAsync("/api/v1/billing/external-providers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListProviders_WithNonAdminAuthentication_ShouldBeForbidden()
    {
        using var client = CreateClient(role: "User");

        var response = await client.GetAsync("/api/v1/billing/external-providers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListProviders_WithSystemAdmin_ShouldReturnEverySupportedProvider()
    {
        using var client = CreateClient(role: "SystemAdmin");

        var response = await client.GetAsync("/api/v1/billing/external-providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var providerKeys = body.EnumerateArray()
            .Select(provider => provider.GetProperty("providerKey").GetString())
            .ToList();
        providerKeys.Should().BeEquivalentTo(PaymentProviders.All);
    }

    [Fact]
    public async Task EnableDisableRoundTrip_ShouldPersistAcrossRequests()
    {
        using var admin = CreateClient(role: "SystemAdmin");

        var disableResponse = await admin.PostAsync("/api/v1/billing/external-providers/stripe:disable", null);
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DisableResponseEnabled(disableResponse)).Should().BeFalse();

        var listAfterDisable = await admin.GetFromJsonAsync<JsonElement>("/api/v1/billing/external-providers");
        listAfterDisable.EnumerateArray()
            .Single(provider => provider.GetProperty("providerKey").GetString() == PaymentProviders.Stripe)
            .GetProperty("enabled").GetBoolean().Should().BeFalse();

        var enableResponse = await admin.PostAsync("/api/v1/billing/external-providers/stripe:enable", null);
        enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DisableResponseEnabled(enableResponse)).Should().BeTrue();

        var listAfterEnable = await admin.GetFromJsonAsync<JsonElement>("/api/v1/billing/external-providers");
        listAfterEnable.EnumerateArray()
            .Single(provider => provider.GetProperty("providerKey").GetString() == PaymentProviders.Stripe)
            .GetProperty("enabled").GetBoolean().Should().BeTrue();

        static async Task<bool> DisableResponseEnabled(HttpResponseMessage message)
        {
            var body = await message.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("enabled").GetBoolean();
        }
    }

    [Fact]
    public async Task DisableProvider_WithoutAdmin_ShouldBeForbidden()
    {
        using var client = CreateClient(role: "User");

        var response = await client.PostAsync("/api/v1/billing/external-providers/stripe:disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EnableProvider_WithUnknownProviderKey_ShouldFailClosed()
    {
        using var client = CreateClient(role: "SystemAdmin");

        var response = await client.PostAsync("/api/v1/billing/external-providers/not-a-provider:enable", null);

        // Fail-closed: the unknown key is rejected (400 from the command validator /
        // 404 from the registry) and never materializes provider state.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProvider_WithUnknownProviderKey_ShouldBe404()
    {
        using var client = CreateClient(role: "SystemAdmin");

        var response = await client.GetAsync("/api/v1/billing/external-providers/not-a-provider");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MigrationDryRun_WithSystemAdmin_ShouldReturnReportWithoutMutation()
    {
        using var client = CreateClient(role: "SystemAdmin");
        var content = new StringContent(
            """{"sourceProvider":"stripe","targetProvider":"paypal"}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/v1/billing/external-providers/migration:dry-run", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("dryRun").GetBoolean().Should().BeTrue();
        report.GetProperty("mutationApplied").GetBoolean().Should().BeFalse();
        report.GetProperty("sourceProvider").GetString().Should().Be(PaymentProviders.Stripe);
        report.GetProperty("targetProvider").GetString().Should().Be(PaymentProviders.PayPal);
        report.GetProperty("totalSubscriptionsScanned").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task MigrationDryRun_WithUnknownProviderKey_ShouldFailClosedWith400()
    {
        using var client = CreateClient(role: "SystemAdmin");
        var content = new StringContent(
            """{"sourceProvider":"chargebee","targetProvider":"paypal"}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/v1/billing/external-providers/migration:dry-run", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MigrationDryRun_WithoutAdmin_ShouldBeForbidden()
    {
        using var client = CreateClient(role: "User");
        var content = new StringContent(
            """{"sourceProvider":"stripe","targetProvider":"paypal"}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/v1/billing/external-providers/migration:dry-run", content);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

/// <summary>
///     Test authentication handler for the external provider management endpoints:
///     authenticates any request carrying the TestScheme authorization header and maps
///     the X-Test-Role header to role claims.
/// </summary>
internal sealed class ProviderTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ProviderTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!authorizationValues.ToString().StartsWith($"{SchemeName} ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "provider-management-test-user"),
            new(ClaimTypes.Name, "Provider Management Integration Test User")
        };

        claims.AddRange(Request.Headers
            .Where(header => header.Key.Equals("X-Test-Role", StringComparison.OrdinalIgnoreCase))
            .SelectMany(header => header.Value)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => new Claim(ClaimTypes.Role, role!)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

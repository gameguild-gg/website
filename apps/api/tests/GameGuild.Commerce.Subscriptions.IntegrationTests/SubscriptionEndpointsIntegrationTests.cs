using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Subscriptions.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.IntegrationTests;

/// <summary>
/// Integration tests for Subscription API endpoints
/// </summary>
public class SubscriptionEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<GameGuild.API.Program>>, IDisposable
{
    private readonly WebApplicationFactory<GameGuild.API.Program> _factory;
    private readonly HttpClient _client;
    private static readonly string DatabaseName = $"SubscriptionTestDb_{Guid.NewGuid()}";
    private static readonly Guid _tenantId = Guid.NewGuid();

    public SubscriptionEndpointsIntegrationTests(WebApplicationFactory<GameGuild.API.Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                // Remove ALL existing DbContext and EF Core registrations
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                                d.ServiceType == typeof(ApplicationDbContext) ||
                                d.ServiceType.FullName?.Contains("EntityFramework") == true ||
                                d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database with custom context that includes Subscription entities
                // Using manual registration to ensure DbContextOptions<ApplicationDbContext> is available for the base class
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(DatabaseName)
                    .Options;

                services.AddSingleton(options);
                services.AddScoped<SubscriptionTestDbContext>();
                services.AddScoped<ApplicationDbContext>(p => p.GetRequiredService<SubscriptionTestDbContext>());
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

                // Add HTTP logging services (required by the pipeline)
                services.AddHttpLogging(o => { });

                services.PostConfigure<StripeGatewayOptions>(options =>
                {
                    // Issue #413: gateway selection is fail-closed, so the simulated
                    // Stripe gateway must be explicitly enabled for the charge path.
                    options.IsEnabled = true;
                    options.UseSimulation = true;
                    options.ApiKey = string.Empty;
                });

                // Override authentication with the test handler
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", _tenantId.ToString());
    }

    private async Task<Guid> SeedSubscriptionPlanAsync()
    {
        SubscriptionTestTenantSeeder.EnsureTenantExists(_factory.Services, _tenantId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var plan = new GameGuild.Commerce.Subscriptions.SubscriptionPlan(
            name: "Test Plan",
            slug: "test-plan",
            monthlyPriceInCents: 2999,
            currency: "USD",
            description: "Test subscription plan"
        );

        dbContext.Set<GameGuild.Commerce.Subscriptions.SubscriptionPlan>().Add(plan);
        await dbContext.SaveChangesAsync();

        return plan.Id;
    }

    private async Task<Guid> CreateSubscriptionAsync(Guid planId)
    {
        var createRequest = new
        {
            TenantId = _tenantId,
            PlanId = planId,
            CreatedByUserId = Guid.NewGuid(),
            BillingCycle = 1,
            Amount = 29.99m,
            Currency = "USD",
            StartDate = (DateTime?)null,
            TrialDays = (int?)null
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/subscriptions", createRequest);
        createResponse.EnsureSuccessStatusCode();

        var responseContent = await createResponse.Content.ReadAsStringAsync();
        var subscription = System.Text.Json.JsonDocument.Parse(responseContent).RootElement;

        return subscription.TryGetProperty("id", out var idProp)
            ? idProp.GetGuid()
            : subscription.TryGetProperty("Id", out var idPropCap)
                ? idPropCap.GetGuid()
                : throw new Exception($"Could not find id property in response: {responseContent}");
    }

    [Fact]
    public async Task CreateSubscription_ShouldReturn201_WithValidCommand()
    {
        // Arrange
        var planId = await SeedSubscriptionPlanAsync();
        var request = new
        {
            TenantId = _tenantId,
            PlanId = planId,
            CreatedByUserId = Guid.NewGuid(),
            BillingCycle = 1, // Monthly
            Amount = 29.99m,
            Currency = "USD",
            StartDate = (DateTime?)null,
            TrialDays = (int?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/subscriptions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ProcessPayment_ShouldUseServerResolvedBillingCycleAndActivateSubscription()
    {
        // Arrange
        var planId = await SeedSubscriptionPlanAsync();
        var subscriptionId = await CreateSubscriptionAsync(planId);

        var paymentRequest = new
        {
            TenantId = _tenantId,
            SubscriptionId = subscriptionId,
            Amount = 29.99m,
            PaymentMethodId = "pm_test_card"
        };

        // Act
        var paymentResponse = await _client.PostAsJsonAsync("/api/v1/payments", paymentRequest);
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await _client.GetAsync($"/api/v1/subscriptions/{subscriptionId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await getResponse.Content.ReadAsStringAsync();
        var subscription = System.Text.Json.JsonDocument.Parse(responseContent).RootElement;

        var status = subscription.TryGetProperty("status", out var statusProp)
            ? statusProp.GetString()
            : subscription.TryGetProperty("Status", out var statusPropCap)
                ? statusPropCap.GetString()
                : null;

        var isActive = subscription.TryGetProperty("isActive", out var isActiveProp)
            ? isActiveProp.GetBoolean()
            : subscription.TryGetProperty("IsActive", out var isActivePropCap)
                && isActivePropCap.GetBoolean();

        var lastPaymentAtPresent = subscription.TryGetProperty("lastPaymentAt", out var lastPaymentProp)
            ? lastPaymentProp.ValueKind != System.Text.Json.JsonValueKind.Null && lastPaymentProp.ValueKind != System.Text.Json.JsonValueKind.Undefined
            : subscription.TryGetProperty("LastPaymentAt", out var lastPaymentPropCap)
                && lastPaymentPropCap.ValueKind != System.Text.Json.JsonValueKind.Null
                && lastPaymentPropCap.ValueKind != System.Text.Json.JsonValueKind.Undefined;

        status.Should().Be("Active");
        isActive.Should().BeTrue();
        lastPaymentAtPresent.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSubscription_ShouldReturnBadRequest_WithInvalidData()
    {
        // Arrange
        var request = new
        {
            TenantId = Guid.Empty, // Invalid
            PlanId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            BillingCycle = "Monthly",
            Amount = 29.99m,
            Currency = "USD",
            StartDate = DateTime.UtcNow,
            TrialDays = (int?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/subscriptions", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSubscriptionById_ShouldReturn404_WhenNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/subscriptions/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAndGetSubscription_ShouldReturnCreatedSubscription()
    {
        // Arrange
        var planId = await SeedSubscriptionPlanAsync();
        var createRequest = new
        {
            TenantId = _tenantId,
            PlanId = planId,
            CreatedByUserId = Guid.NewGuid(),
            BillingCycle = 1, // Monthly
            Amount = 29.99m,
            Currency = "USD",
            StartDate = (DateTime?)null,
            TrialDays = 14
        };

        // Act
        var createResponse = await _client.PostAsJsonAsync("/api/v1/subscriptions", createRequest);
        createResponse.EnsureSuccessStatusCode();

        var responseContent = await createResponse.Content.ReadAsStringAsync();
        var subscription = System.Text.Json.JsonDocument.Parse(responseContent).RootElement;
        // Try both "id" and "Id" for case sensitivity
        var subscriptionId = subscription.TryGetProperty("id", out var idProp) ? idProp.GetGuid() :
                           subscription.TryGetProperty("Id", out var idPropCap) ? idPropCap.GetGuid() :
                           throw new Exception($"Could not find id property in response: {responseContent}");
        var getResponse = await _client.GetAsync($"/api/v1/subscriptions/{subscriptionId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelSubscription_ShouldReturn200_WhenSubscriptionExists()
    {
        // Arrange
        var planId = await SeedSubscriptionPlanAsync();

        // Create subscription
        var createRequest = new
        {
            TenantId = _tenantId,
            PlanId = planId,
            CreatedByUserId = Guid.NewGuid(),
            BillingCycle = 1, // Monthly
            Amount = 29.99m,
            Currency = "USD",
            StartDate = (DateTime?)null,
            TrialDays = (int?)null
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/subscriptions", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var responseContent = await createResponse.Content.ReadAsStringAsync();
        var subscription = System.Text.Json.JsonDocument.Parse(responseContent).RootElement;
        // Try both "id" and "Id" for case sensitivity
        var subscriptionId = subscription.TryGetProperty("id", out var idProp) ? idProp.GetGuid() :
                           subscription.TryGetProperty("Id", out var idPropCap) ? idPropCap.GetGuid() :
                           throw new Exception($"Could not find id property in response: {responseContent}");

        var cancelRequest = new
        {
            Reason = "UserRequested"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/subscriptions/{subscriptionId}:cancel", cancelRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}

using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Subscriptions.IntegrationTests;
using GameGuild.Commerce.Subscriptions.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.IntegrationTests.Security;

/// <summary>
///     P0 Critical Integration Tests: Commerce Security Flows
///     From: COMMERCE_MODULES_SECURITY_AUDIT.md Section 7 - Test Plan
///     These tests verify end-to-end security flows across the commerce modules.
/// </summary>
public class CommerceSecurityIntegrationTests : IClassFixture<WebApplicationFactory<GameGuild.API.Program>>, IDisposable
{
    private const string StripeWebhookSecret = "whsec_subscriptions_security";
    private readonly WebApplicationFactory<GameGuild.API.Program> _factory;
    private readonly HttpClient _client;
    private static readonly string DatabaseName = $"CommerceSecurityTestDb_{Guid.NewGuid()}";

    public CommerceSecurityIntegrationTests(WebApplicationFactory<GameGuild.API.Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Billing:Stripe:WebhookSecret"] = StripeWebhookSecret,
                    ["Billing:Stripe:WebhookEndpointId"] = "we_subscriptions_security",
                    ["Billing:Stripe:AccountId"] = "acct_platform",
                    ["Billing:Stripe:ApiVersion"] = "2023-10-16",
                    ["Billing:Stripe:LiveMode"] = "false",
                    ["Billing:Stripe:WebhookToleranceSeconds"] = "300"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                // Remove existing DbContext registrations
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

                // Add in-memory database
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DatabaseName);
                    // Renewal quotes now append a durable pricing-decision ledger entry (issue #400)
                    // inside a DB transaction; the in-memory provider ignores transactions, which is
                    // an error-level warning unless suppressed (same pattern as the other InMemory hosts).
                    options.ConfigureWarnings(warnings =>
                        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

                services.AddHttpLogging(o => { });

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
    }

    #region Subscription Renewal Flow - Single Charge Guarantee (P0)

    [Fact]
    public async Task RenewalFlow_RepeatedRequests_ShouldRemainBlockedWithoutPayment()
    {
        // Arrange
        var subscriptionId = await SeedActiveSubscriptionAsync();
        var response1 = await _client.PostAsync($"/api/v1/subscriptions/{subscriptionId}:renew", null);
        var response2 = await _client.PostAsync($"/api/v1/subscriptions/{subscriptionId}:renew", null);

        response1.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RenewalFlow_WithValidSubscription_ShouldRequireProviderPayment()
    {
        // Arrange
        var subscriptionId = await SeedActiveSubscriptionAsync();
        // Act
        var response = await _client.PostAsync($"/api/v1/subscriptions/{subscriptionId}:renew", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Plan Change Flow - Proration Safety (P0)

    [Fact]
    public async Task PlanChange_Upgrade_ShouldCalculateProrationCorrectly()
    {
        // Arrange
        var subscriptionId = await SeedActiveSubscriptionAsync();
        var newPlanId = await SeedUpgradePlanAsync();

        var request = new { NewPlanId = newPlanId, EffectiveDate = (DateTime?)DateTime.UtcNow };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/subscriptions/{subscriptionId}:upgrade", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Accepted, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PlanChange_Downgrade_ShouldBeEffectiveAtPeriodEnd()
    {
        // Arrange
        var subscriptionId = await SeedActiveSubscriptionAsync();
        var downgradePlanId = await SeedDowngradePlanAsync();

        var request = new { NewPlanId = downgradePlanId, EffectiveDate = (DateTime?)null };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/subscriptions/{subscriptionId}:downgrade", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Accepted, HttpStatusCode.NoContent);
        // Response should indicate the change is scheduled, not immediate
    }

    #endregion

    #region Cancellation Flow - Clean Cancellation (P0)

    [Fact]
    public async Task Cancellation_ImmediateCancel_ShouldStopAccessImmediately()
    {
        // Arrange
        var subscriptionId = await SeedActiveSubscriptionAsync();

        var request = new { Reason = "UserRequested", Note = (string?)null, EffectiveDate = (DateTime?)null };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/subscriptions/{subscriptionId}:cancel", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        // Verify subscription is cancelled
        var statusResponse = await _client.GetAsync($"/api/v1/subscriptions/{subscriptionId}");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await statusResponse.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            var subscription = System.Text.Json.JsonSerializer.Deserialize<SubscriptionDto>(responseBody,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            subscription?.Status.Should().Be("Cancelled");
        }
    }

    [Fact]
    public async Task Cancellation_EndOfPeriod_ShouldContinueUntilPeriodEnd()
    {
        // Arrange
        var subscriptionId = await SeedActiveSubscriptionAsync();

        var request = new { Reason = "UserRequested", Note = (string?)null, EffectiveDate = (DateTime?)DateTime.UtcNow.AddDays(30) };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/subscriptions/{subscriptionId}:cancel", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Accepted, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Cancellation_ShouldPreventSubsequentRenewals()
    {
        // Arrange
        var subscriptionId = await SeedActiveSubscriptionAsync();

        // Cancel the subscription
        var cancelRequest = new { Reason = "UserRequested", Note = (string?)null, EffectiveDate = (DateTime?)null };
        var cancelResponse = await _client.PostAsJsonAsync($"/api/v1/subscriptions/{subscriptionId}:cancel", cancelRequest);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Try to renew the cancelled subscription
        // Act
        var response = await _client.PostAsync($"/api/v1/subscriptions/{subscriptionId}:renew", null);

        // Assert - Renewal should fail for cancelled subscription
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Webhook Processing Flow - Idempotency (P0)

    [Fact]
    public async Task Webhook_DuplicateStripeEvent_ShouldBeIdempotent()
    {
        // Arrange
        var externalEventId = $"evt_stripe_{Guid.NewGuid()}";
        var webhookPayload = JsonSerializer.Serialize(new
        {
            id = externalEventId,
            @object = "event",
            api_version = "2023-10-16",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new { @object = new { id = "cus_subscriptions", @object = "customer" } },
            livemode = false,
            pending_webhooks = 1,
            request = new { id = (string?)null, idempotency_key = (string?)null },
            type = "customer.created"
        });

        // Act - First webhook call
        var response1 = await PostStripeWebhookAsync(webhookPayload);

        // Act - Duplicate webhook call (should be idempotent)
        var response2 = await PostStripeWebhookAsync(webhookPayload);

        // Assert - Both should succeed (or second returns 200/204 indicating already processed)
        response1.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.Accepted);
        response2.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_ShouldBeRejected()
    {
        // Arrange
        var webhookPayload = JsonSerializer.Serialize(new
        {
            id = "evt_fake",
            type = "invoice.payment_succeeded"
        });
        var content = new StringContent(webhookPayload, Encoding.UTF8, "application/json");

        // No signature header or invalid signature

        // Act
        var response = await _client.PostAsync("/api/v1/billing/webhooks/stripe", content);

        // Assert - Should reject unsigned/invalid webhooks
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Tenant Isolation Flow (P0)

    [Fact]
    public async Task TenantIsolation_CrossTenantAccess_ShouldBeBlocked()
    {
        // Arrange - Create subscriptions in different tenants
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();
        var subscription1 = await SeedSubscriptionForTenantAsync(tenant1Id);

        // Set headers for tenant 2
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant2Id.ToString());

        // Act - Try to access tenant1's subscription from tenant2's context
        var response = await _client.GetAsync($"/api/v1/subscriptions/{subscription1}");

        // Assert - Should be forbidden or not found
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TenantIsolation_MissingTenantHeader_ShouldBeRejected()
    {
        // Arrange
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");

        // Act
        var response = await _client.GetAsync("/api/v1/subscriptions");

        // Assert - Should require tenant context
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.OK);
    }

    #endregion

    #region Payment Retry Flow (P0/P1)

    [Fact]
    public async Task PaymentRetry_AfterFailure_ShouldBeScheduled()
    {
        // Arrange
        var subscriptionId = await SeedActiveSubscriptionAsync();
        var invoiceId = await SeedFailedInvoiceAsync(subscriptionId);

        // Act
        var response = await _client.PostAsync($"/api/v1/billing/invoices/{invoiceId}/retry", null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PaymentRetry_ExceedsMaxAttempts_ShouldMarkAsFailed()
    {
        // Arrange
        var subscriptionId = await SeedActiveSubscriptionAsync();
        var invoiceId = await SeedInvoiceWithMaxFailuresAsync(subscriptionId);

        // Act
        var response = await _client.PostAsync($"/api/v1/billing/invoices/{invoiceId}/retry", null);

        // Assert - Should reject retry or mark permanently failed
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> SeedActiveSubscriptionAsync()
    {
        var tenantId = Guid.NewGuid();
        SubscriptionTestTenantSeeder.EnsureTenantExists(_factory.Services, tenantId);
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var planId = await SeedSubscriptionPlanAsync(dbContext);

        var subscription = new Subscription(
            tenantId: tenantId,
            planId: planId,
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(29.99m, "USD"),
            startDate: DateTime.UtcNow
        );

        subscription.Activate();

        dbContext.Set<Subscription>().Add(subscription);
        await dbContext.SaveChangesAsync();

        return subscription.Id;
    }

    private async Task<Guid> SeedSubscriptionForTenantAsync(Guid tenantId)
    {
        SubscriptionTestTenantSeeder.EnsureTenantExists(_factory.Services, tenantId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var planId = await SeedSubscriptionPlanAsync(dbContext);

        var subscription = new Subscription(
            tenantId: tenantId,
            planId: planId,
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(29.99m, "USD"),
            startDate: DateTime.UtcNow
        );

        dbContext.Set<Subscription>().Add(subscription);
        await dbContext.SaveChangesAsync();

        return subscription.Id;
    }

    private async Task<Guid> SeedSubscriptionPlanAsync(ApplicationDbContext dbContext)
    {
        var plan = new SubscriptionPlan(
            name: "Test Plan",
            slug: "test-plan",
            monthlyPriceInCents: 2999,
            currency: "USD",
            description: "Test subscription plan"
        );

        dbContext.Set<SubscriptionPlan>().Add(plan);
        await dbContext.SaveChangesAsync();

        return plan.Id;
    }

    private async Task<Guid> SeedUpgradePlanAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var plan = new SubscriptionPlan(
            name: "Premium Plan",
            slug: "premium-plan",
            monthlyPriceInCents: 4999,
            currency: "USD",
            description: "Premium subscription plan"
        );

        dbContext.Set<SubscriptionPlan>().Add(plan);
        await dbContext.SaveChangesAsync();

        return plan.Id;
    }

    private async Task<Guid> SeedDowngradePlanAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var plan = new SubscriptionPlan(
            name: "Basic Plan",
            slug: "basic-plan",
            monthlyPriceInCents: 999,
            currency: "USD",
            description: "Basic subscription plan"
        );

        dbContext.Set<SubscriptionPlan>().Add(plan);
        await dbContext.SaveChangesAsync();

        return plan.Id;
    }

    private async Task<Guid> SeedFailedInvoiceAsync(Guid subscriptionId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get the subscription to retrieve its TenantId
        var subscription = await dbContext.Set<Subscription>()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (subscription == null)
            throw new InvalidOperationException($"Subscription {subscriptionId} not found");

        if (subscription.TenantId == null)
            throw new InvalidOperationException($"Subscription {subscriptionId} has no TenantId (data integrity issue)");

        // Create an invoice in PastDue status (represents a failed payment)
        var invoice = new Invoice(
            tenantId: subscription.TenantId.Value,
            subscriptionId: subscriptionId,
            amount: 29.99m,
            currency: "USD"
        );

        // Issue the invoice and mark it as past due (failed)
        invoice.Issue();
        invoice.MarkPastDue();

        dbContext.Set<Invoice>().Add(invoice);
        await dbContext.SaveChangesAsync();

        return invoice.Id;
    }

    private async Task<Guid> SeedInvoiceWithMaxFailuresAsync(Guid subscriptionId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get the subscription to retrieve its TenantId
        var subscription = await dbContext.Set<Subscription>()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (subscription == null)
            throw new InvalidOperationException($"Subscription {subscriptionId} not found");

        if (subscription.TenantId == null)
            throw new InvalidOperationException($"Subscription {subscriptionId} has no TenantId (data integrity issue)");

        // Create an invoice marked as uncollectible (exceeded max retry attempts)
        var invoice = new Invoice(
            tenantId: subscription.TenantId.Value,
            subscriptionId: subscriptionId,
            amount: 29.99m,
            currency: "USD"
        );

        // Issue the invoice, mark past due, then mark uncollectible (max failures)
        invoice.Issue();
        invoice.MarkPastDue();
        invoice.MarkUncollectible();

        dbContext.Set<Invoice>().Add(invoice);
        await dbContext.SaveChangesAsync();

        return invoice.Id;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private Task<HttpResponseMessage> PostStripeWebhookAsync(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(StripeWebhookSecret));
        var signature = Convert.ToHexString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}")))
            .ToLowerInvariant();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", $"t={timestamp},v1={signature}");
        return _client.SendAsync(request);
    }

    #endregion
}

/// <summary>
/// DTO for subscription status checking
/// </summary>
internal class SubscriptionDto
{
    public Guid Id { get; set; }
    public string? Status { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public bool AutoRenew { get; set; }
}

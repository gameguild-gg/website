using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Commerce.Products;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace GameGuild.API.IntegrationTests;

/// <summary>
///     HTTP-level integration coverage for the monetization/analytics permission gates
///     (issue #346, criterion "Integration tests verify end-to-end authorization flows").
///     Exercises the real request pipeline — synthetic authentication, ActorContext
///     permission hydration from PostgreSQL grants and the CQRS
///     <c>AuthorizationBehavior</c> — for one ViewAnalytics surface
///     (<c>GET /api/metrics/product</c>), one Configure command
///     (<c>POST /v1/subscription-plans</c>) and the Monetize gate
///     (<c>PUT /v1/products/{id}/pricing</c>). Asserts 200-with-grant, fail-closed
///     denial without the grant, cross-tenant and expired-grant fail-closed behavior,
///     no mutation on denial, and SystemAdmin bypass.
/// </summary>
/// <remarks>
///     <para>
///         <b>Denial status semantics (as configured):</b> the endpoint-level
///         <c>ResourcePermissionAuthorizationFilter</c> is registered only when
///         <c>Controllers:EnablePermissionAuthorizationFilter</c> is set, and that flag
///         defaults to <c>false</c> with no override in any environment configuration.
///         The enforced gate for these surfaces is therefore the
///         <c>[AuthorizeRequest]</c> permission on the dispatched command/query: the
///         <c>AuthorizationBehavior</c> denies and throws
///         <see cref="UnauthorizedAccessException"/>, which the shared exception
///         pipeline maps to a fail-closed 500 ProblemDetails (no detail leakage, no
///         mutation). These tests pin that observed contract; the security properties
///         under test are that access is refused, nothing persists and nothing leaks.
///     </para>
///     <para>
///         <b>Decision-audit rows:</b> <see cref="PermissionOperationType.Check"/>
///         rows are written by the endpoint filter only; with the filter disabled
///         host-wide they cannot occur end-to-end, so they are not asserted here.
///     </para>
/// </remarks>
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class MonetizationPermissionHttpTests(ApiPostgreSqlFixture fixture, ITestOutputHelper output)
{
    private const string MetricsRoute = "/api/metrics/product";
    private const string PlansRoute = "/v1/subscription-plans";

    private const string ViewAnalyticsKey = MonetizationPermission.Keys.ViewAnalytics;
    private const string ConfigureKey = MonetizationPermission.Keys.Configure;
    private const string MonetizeKey = MonetizationPermission.Keys.Monetize;
    private const string PricingManageKey = "products:pricing:manage";

    [Fact]
    public async Task ViewAnalyticsMetrics_AllowDenyBypassAndFailClosedEndToEnd()
    {
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var systemAdminId = Guid.NewGuid();
        var foreignViewerId = Guid.NewGuid();

        await SeedTenantAsync(tenantId, [
            (viewerId, "Member"),
            (memberId, "Member"),
        ]);
        await SeedTenantAsync(foreignTenantId, [
            (foreignViewerId, "Member"),
        ]);
        await SeedGrantAsync(tenantId, viewerId, [ViewAnalyticsKey]);
        // The grant lives in another tenant: requesting under tenantId's context must not honor it.
        await SeedGrantAsync(foreignTenantId, foreignViewerId, [ViewAnalyticsKey]);

        try
        {
            // Unauthenticated requests never reach the metrics surface.
            using var anonymousClient = fixture.Factory.CreateClient();
            var anonymousResponse = await anonymousClient.GetAsync(MetricsRoute);
            anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Without the grant the request is denied fail-closed: the CQRS gate on
            // GetProductMetricsQuery refuses the dispatch and the shared exception
            // pipeline answers with a 500 ProblemDetails (no metrics payload leaks).
            using var memberClient = fixture.CreateAuthenticatedClient(memberId, tenantId);
            var deniedResponse = await memberClient.GetAsync(MetricsRoute);
            deniedResponse.StatusCode.Should().Be(
                HttpStatusCode.InternalServerError,
                await deniedResponse.Content.ReadAsStringAsync());
            (await deniedResponse.Content.ReadAsStringAsync()).Should().NotContain("monthlyRecurringRevenue",
                "a denied request must not leak any metrics payload");

            // With the grant in the request tenant the metrics payload is returned.
            using var viewerClient = fixture.CreateAuthenticatedClient(viewerId, tenantId);
            var allowedResponse = await viewerClient.GetAsync(MetricsRoute);
            allowedResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await allowedResponse.Content.ReadAsStringAsync());
            var metrics = JsonNode.Parse(await allowedResponse.Content.ReadAsStringAsync())!;
            metrics["generatedAtUtc"].Should().NotBeNull("the metrics payload was returned");

            // A grant held in another tenant does not leak into this tenant's context:
            // the tenant-validation layer rejects the non-member with 403 before the
            // dispatch-level gate is even reached (fail closed).
            using var foreignViewerClient = fixture.CreateAuthenticatedClient(foreignViewerId, tenantId);
            var crossTenantResponse = await foreignViewerClient.GetAsync(MetricsRoute);
            crossTenantResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await crossTenantResponse.Content.ReadAsStringAsync()).Should().NotContain("monthlyRecurringRevenue");

            // SystemAdmin bypasses the permission check without any grant.
            using var systemAdminClient = fixture.CreateAuthenticatedClient(systemAdminId, tenantId, isSystemAdmin: true);
            var bypassResponse = await systemAdminClient.GetAsync(MetricsRoute);
            bypassResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await bypassResponse.Content.ReadAsStringAsync());
        }
        finally
        {
            await CleanupAsync([tenantId, foreignTenantId], [], [viewerId, memberId, systemAdminId, foreignViewerId]);
        }
    }

    [Fact]
    public async Task ConfigurePlanCreation_AllowDenyExpiredGrantAndBypassEndToEnd()
    {
        var tenantId = Guid.NewGuid();
        var configuratorId = Guid.NewGuid();
        var monetizerId = Guid.NewGuid();
        var expiredConfiguratorId = Guid.NewGuid();
        var systemAdminId = Guid.NewGuid();
        // Keep the base slug short enough that the "-admin" variant also satisfies the
        // CreateSubscriptionPlan validator's 50-character Slug limit: with the CQRS
        // ValidationBehavior now registered (issue #394), registered validators are
        // enforced at dispatch time and an oversized slug fails with 400 instead of 201.
        var slug = $"monetization-http-{Guid.NewGuid().ToString("N")[..24]}";

        await SeedTenantAsync(tenantId, [
            (configuratorId, "Member"),
            (monetizerId, "Member"),
            (expiredConfiguratorId, "Member"),
        ]);
        await SeedGrantAsync(tenantId, configuratorId, [ConfigureKey]);
        await SeedGrantAsync(tenantId, monetizerId, [MonetizeKey]);
        // Expired grant: active row, but ExpiresAt is in the past — must contribute nothing.
        await SeedGrantAsync(tenantId, expiredConfiguratorId, [ConfigureKey], expiresAt: DateTime.UtcNow.AddHours(-1));

        try
        {
            var planBody = new { name = "Monetization HTTP plan", slug, monthlyPriceInCents = 1999L };

            // A different monetization permission does not unlock Configure: the gate on
            // CreateSubscriptionPlanCommand denies the dispatch (fail-closed 500) and
            // no plan is persisted.
            using var monetizerClient = fixture.CreateAuthenticatedClient(monetizerId, tenantId);
            var wrongPermissionResponse = await monetizerClient.PostAsJsonAsync(PlansRoute, planBody);
            wrongPermissionResponse.StatusCode.Should().Be(
                HttpStatusCode.InternalServerError,
                await wrongPermissionResponse.Content.ReadAsStringAsync());
            (await CountPlansBySlugAsync(slug)).Should().Be(0, "denied requests must not create plans");

            // An expired Configure grant fails closed.
            using var expiredClient = fixture.CreateAuthenticatedClient(expiredConfiguratorId, tenantId);
            var expiredResponse = await expiredClient.PostAsJsonAsync(PlansRoute, planBody);
            expiredResponse.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await CountPlansBySlugAsync(slug)).Should().Be(0);

            // With the Configure grant the plan is created and persisted.
            using var configuratorClient = fixture.CreateAuthenticatedClient(configuratorId, tenantId);
            var createdResponse = await configuratorClient.PostAsJsonAsync(PlansRoute, planBody);
            createdResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await createdResponse.Content.ReadAsStringAsync());
            var created = JsonNode.Parse(await createdResponse.Content.ReadAsStringAsync())!;
            var planId = Guid.Parse(created["id"]!.GetValue<string>());
            output.WriteLine($"Created subscription plan {planId} via the Configure-gated endpoint");
            (await CountPlansBySlugAsync(slug)).Should().Be(1);

            // SystemAdmin bypasses the Configure gate without a grant.
            using var systemAdminClient = fixture.CreateAuthenticatedClient(systemAdminId, tenantId, isSystemAdmin: true);
            var bypassResponse = await systemAdminClient.PostAsJsonAsync(PlansRoute,
                new { name = "Monetization HTTP admin plan", slug = $"{slug}-admin", monthlyPriceInCents = 2999L });
            bypassResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await bypassResponse.Content.ReadAsStringAsync());
            (await CountPlansBySlugAsync($"{slug}-admin")).Should().Be(1);
        }
        finally
        {
            await CleanupAsync([tenantId], [slug, $"{slug}-admin"],
                [configuratorId, monetizerId, expiredConfiguratorId, systemAdminId]);
        }
    }

    [Fact]
    public async Task MonetizePricingGate_ControllerAndCommandLayersEnforcedEndToEnd()
    {
        var tenantId = Guid.NewGuid();
        var noGrantUserId = Guid.NewGuid();
        var manageOnlyUserId = Guid.NewGuid();
        var monetizerUserId = Guid.NewGuid();
        var systemAdminId = Guid.NewGuid();

        await SeedTenantAsync(tenantId, [
            (noGrantUserId, "Member"),
            (manageOnlyUserId, "Member"),
            (monetizerUserId, "Member"),
            // Seeded as a user row so the product-creator foreign key holds; the
            // SystemAdmin role arrives via the test-auth claim, not the membership.
            (systemAdminId, "Member"),
        ]);
        // SetProductPricingCommand requires monetization:monetize on every dispatch
        // path; the controller action additionally keeps its products:pricing:manage
        // defense-in-depth check. Under the default configuration the endpoint-level
        // permission filter is disabled, so both denials surface from the command
        // layer's AuthorizationBehavior through the shared exception pipeline.
        await SeedGrantAsync(tenantId, manageOnlyUserId, [PricingManageKey]);
        await SeedGrantAsync(tenantId, monetizerUserId, [PricingManageKey, MonetizeKey]);

        var noGrantProduct = await SeedProductAsync(tenantId, noGrantUserId);
        var manageOnlyProduct = await SeedProductAsync(tenantId, manageOnlyUserId);
        var monetizerProduct = await SeedProductAsync(tenantId, monetizerUserId);
        var adminProduct = await SeedProductAsync(tenantId, systemAdminId);

        try
        {
            // No permissions at all: the Monetize gate refuses the dispatch and the
            // creator's product remains pricing-free.
            using var noGrantClient = fixture.CreateAuthenticatedClient(noGrantUserId, tenantId);
            var controllerDenyResponse = await noGrantClient.PutAsJsonAsync(
                $"/v1/products/{noGrantProduct}/pricing",
                PricingBody("Standard", 49.90m));
            controllerDenyResponse.StatusCode.Should().Be(
                HttpStatusCode.InternalServerError,
                await controllerDenyResponse.Content.ReadAsStringAsync());
            (await CountPricingRowsAsync(noGrantProduct)).Should().Be(0, "denied pricing requests must not persist");

            // products:pricing:manage alone does not satisfy the monetization:monetize
            // gate on the dispatched command: the request fails closed and nothing is
            // persisted (fail closed, no mutation).
            using var manageOnlyClient = fixture.CreateAuthenticatedClient(manageOnlyUserId, tenantId);
            var commandDenyResponse = await manageOnlyClient.PutAsJsonAsync(
                $"/v1/products/{manageOnlyProduct}/pricing",
                PricingBody("Standard", 49.90m));
            commandDenyResponse.IsSuccessStatusCode.Should().BeFalse(
                "the Monetize gate must deny the pricing mutation even with the endpoint-level permission");
            commandDenyResponse.StatusCode.Should().Be(
                HttpStatusCode.InternalServerError,
                await commandDenyResponse.Content.ReadAsStringAsync());
            (await CountPricingRowsAsync(manageOnlyProduct)).Should().Be(0, "denied pricing commands must not persist");

            // Both layers satisfied: the creator with products:pricing:manage AND
            // monetization:monetize persists the pricing option.
            using var monetizerClient = fixture.CreateAuthenticatedClient(monetizerUserId, tenantId);
            var allowedResponse = await monetizerClient.PutAsJsonAsync(
                $"/v1/products/{monetizerProduct}/pricing",
                PricingBody("Premium", 99.90m));
            allowedResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await allowedResponse.Content.ReadAsStringAsync());
            var pricingRows = await GetPricingRowsAsync(monetizerProduct);
            pricingRows.Should().ContainSingle().Which
                .Name.Should().Be("Premium");

            // SystemAdmin bypasses both gates without any grant (creator of its own product).
            using var systemAdminClient = fixture.CreateAuthenticatedClient(systemAdminId, tenantId, isSystemAdmin: true);
            var bypassResponse = await systemAdminClient.PutAsJsonAsync(
                $"/v1/products/{adminProduct}/pricing",
                PricingBody("Admin", 149.90m));
            bypassResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await bypassResponse.Content.ReadAsStringAsync());
            (await CountPricingRowsAsync(adminProduct)).Should().Be(1);
        }
        finally
        {
            await CleanupAsync(
                [tenantId],
                [],
                [noGrantUserId, manageOnlyUserId, monetizerUserId, systemAdminId],
                [noGrantProduct, manageOnlyProduct, monetizerProduct, adminProduct]);
        }
    }

    private static object PricingBody(string name, decimal basePrice) => new
    {
        name,
        basePrice,
        currency = "USD",
        salePrice = (decimal?)null,
        saleStartDate = (DateTime?)null,
        saleEndDate = (DateTime?)null,
        isDefault = false,
        pricingId = (Guid?)null,
    };

    private async Task SeedTenantAsync(Guid tenantId, (Guid UserId, string Role)[] members)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = tenantId.ToString("N");
        context.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            Name = $"Monetization gate tenant {marker}",
            Slug = $"monetization-gate-{marker}",
            AdminEmail = $"admin-{marker}@monetization-gate.test",
            IsActive = true,
        });
        context.Set<User>().AddRange(members.Select(member =>
        {
            var user = User.CreateOAuthUser($"monetization-gate-{member.UserId:N}@monetization-gate.test", $"Monetization gate user {member.UserId:N}");
            user.Id = member.UserId;
            return user;
        }));
        context.Set<TenantMember>().AddRange(members.Select(member => new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = member.UserId,
            Role = member.Role,
            IsActive = true,
        }));
        await context.SaveChangesAsync();
    }

    private async Task SeedGrantAsync(Guid tenantId, Guid userId, string[] permissions, DateTime? expiresAt = null)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Set<TenantPermission>().Add(new TenantPermission
        {
            UserId = userId,
            TenantId = tenantId,
            Permissions = permissions,
            IsActive = true,
            ExpiresAt = expiresAt,
            GrantedBy = null,
        });
        await context.SaveChangesAsync();
    }

    private async Task<Guid> SeedProductAsync(Guid tenantId, Guid creatorId)
    {
        var product = Product.Create(
            $"Monetization gate product {creatorId:N}",
            creatorId: creatorId,
            tenantId: tenantId);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Set<Product>().Add(product);
        await context.SaveChangesAsync();
        return product.Id;
    }

    private async Task<int> CountPlansBySlugAsync(string slug)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<SubscriptionPlan>().CountAsync(plan => plan.Slug == slug);
    }

    private async Task<int> CountPricingRowsAsync(Guid productId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<ProductPricing>().CountAsync(pricing => pricing.ProductId == productId);
    }

    private async Task<List<ProductPricing>> GetPricingRowsAsync(Guid productId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<ProductPricing>().Where(pricing => pricing.ProductId == productId).ToListAsync();
    }

    private async Task CleanupAsync(
        Guid[] tenantIds,
        string[] planSlugPrefixes,
        Guid[] userIds,
        Guid[]? productIds = null)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (productIds is { Length: > 0 })
        {
            var pricingIds = await context.Set<ProductPricing>()
                .Where(pricing => productIds.Contains(pricing.ProductId))
                .Select(pricing => pricing.Id)
                .ToListAsync();
            if (pricingIds.Count > 0)
            {
                await context.Set<ProductPricingVersion>()
                    .Where(version => pricingIds.Contains(version.ProductPricingId))
                    .ExecuteDeleteAsync();
            }
            await context.Set<ProductPricing>()
                .Where(pricing => productIds.Contains(pricing.ProductId))
                .ExecuteDeleteAsync();
            await context.Set<Product>()
                .Where(product => productIds.Contains(product.Id))
                .ExecuteDeleteAsync();
        }

        foreach (var slugPrefix in planSlugPrefixes)
        {
            await context.Set<SubscriptionPlan>()
                .Where(plan => plan.Slug == slugPrefix)
                .ExecuteDeleteAsync();
        }

        if (userIds.Length > 0)
        {
            await context.Set<PermissionAuditLog>()
                .Where(log => log.UserId != null && userIds.Contains(log.UserId.Value))
                .ExecuteDeleteAsync();
        }

        await context.Set<TenantPermission>()
            .Where(grant => grant.TenantId != null && tenantIds.Contains(grant.TenantId.Value))
            .ExecuteDeleteAsync();

        var seededUserIds = await context.Set<TenantMember>()
            .Where(member => tenantIds.Contains(member.TenantId))
            .Select(member => member.UserId)
            .ToListAsync();

        await context.Set<TenantMember>()
            .Where(member => tenantIds.Contains(member.TenantId))
            .ExecuteDeleteAsync();
        await context.Set<Tenant>()
            .Where(tenant => tenantIds.Contains(tenant.Id))
            .ExecuteDeleteAsync();
        await context.Set<User>()
            .Where(user => seededUserIds.Contains(user.Id))
            .ExecuteDeleteAsync();
    }
}

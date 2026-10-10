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
///     permission hydration from PostgreSQL grants, the endpoint-level
///     <see cref="ResourcePermissionAuthorizationFilter"/> and the CQRS
///     <c>AuthorizationBehavior</c> — for one ViewAnalytics surface
///     (<c>GET /api/metrics/product</c>), one Configure command
///     (<c>POST /v1/subscription-plans</c>) and the Monetize gate
///     (<c>PUT /v1/products/{id}/pricing</c>). Asserts 200-with-grant, 403-without,
///     fail-closed semantics (cross-tenant and expired grants, no mutation on denial),
///     SystemAdmin bypass, and <see cref="PermissionOperationType.Check"/> decision-audit rows.
/// </summary>
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
    public async Task ViewAnalyticsMetrics_AllowDenyBypassAndDecisionAuditEndToEnd()
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

            // Without the grant: 403 plus a durable denied Check row.
            using var memberClient = fixture.CreateAuthenticatedClient(memberId, tenantId);
            var deniedResponse = await memberClient.GetAsync(MetricsRoute);
            deniedResponse.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                await deniedResponse.Content.ReadAsStringAsync());

            var deniedAudit = await GetLatestCheckRowAsync(memberId, ViewAnalyticsKey);
            deniedAudit.Should().NotBeNull("denied endpoint permission checks must be audited");
            deniedAudit!.Success.Should().BeFalse();
            deniedAudit.ErrorMessage.Should().Contain(ViewAnalyticsKey);
            deniedAudit.UserId.Should().Be(memberId);

            // With the grant in the request tenant: 200 plus a granted Check row.
            using var viewerClient = fixture.CreateAuthenticatedClient(viewerId, tenantId);
            var allowedResponse = await viewerClient.GetAsync(MetricsRoute);
            allowedResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await allowedResponse.Content.ReadAsStringAsync());
            var metrics = JsonNode.Parse(await allowedResponse.Content.ReadAsStringAsync())!;
            metrics["generatedAtUtc"].Should().NotBeNull("the metrics payload was returned");

            var grantedAudit = await GetLatestCheckRowAsync(viewerId, ViewAnalyticsKey);
            grantedAudit.Should().NotBeNull("granted endpoint permission checks must be audited");
            grantedAudit!.Success.Should().BeTrue();
            grantedAudit.ErrorMessage.Should().BeNull();

            // A grant held in another tenant does not leak into this tenant's context (fail closed).
            using var foreignViewerClient = fixture.CreateAuthenticatedClient(foreignViewerId, tenantId);
            var crossTenantResponse = await foreignViewerClient.GetAsync(MetricsRoute);
            crossTenantResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

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
        var slug = $"monetization-http-{Guid.NewGuid():N}";

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

            // A different monetization permission does not unlock Configure.
            using var monetizerClient = fixture.CreateAuthenticatedClient(monetizerId, tenantId);
            var wrongPermissionResponse = await monetizerClient.PostAsJsonAsync(PlansRoute, planBody);
            wrongPermissionResponse.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                await wrongPermissionResponse.Content.ReadAsStringAsync());
            (await CountPlansBySlugAsync(slug)).Should().Be(0, "denied requests must not create plans");

            var wrongKeyAudit = await GetLatestCheckRowAsync(monetizerId, ConfigureKey);
            wrongKeyAudit.Should().NotBeNull();
            wrongKeyAudit!.Success.Should().BeFalse();

            // An expired Configure grant fails closed.
            using var expiredClient = fixture.CreateAuthenticatedClient(expiredConfiguratorId, tenantId);
            var expiredResponse = await expiredClient.PostAsJsonAsync(PlansRoute, planBody);
            expiredResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

            var grantedAudit = await GetLatestCheckRowAsync(configuratorId, ConfigureKey);
            grantedAudit.Should().NotBeNull();
            grantedAudit!.Success.Should().BeTrue();

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
        ]);
        // The pricing mutation passes the controller only with products:pricing:manage,
        // and the SetProductPricingCommand additionally requires monetization:monetize
        // on every dispatch path (two-layer gate).
        await SeedGrantAsync(tenantId, manageOnlyUserId, [PricingManageKey]);
        await SeedGrantAsync(tenantId, monetizerUserId, [PricingManageKey, MonetizeKey]);

        var noGrantProduct = await SeedProductAsync(tenantId, noGrantUserId);
        var manageOnlyProduct = await SeedProductAsync(tenantId, manageOnlyUserId);
        var monetizerProduct = await SeedProductAsync(tenantId, monetizerUserId);
        var adminProduct = await SeedProductAsync(tenantId, systemAdminId);

        try
        {
            // Layer 1 (endpoint): no products:pricing:manage => 403 before the command runs.
            using var noGrantClient = fixture.CreateAuthenticatedClient(noGrantUserId, tenantId);
            var controllerDenyResponse = await noGrantClient.PutAsJsonAsync(
                $"/v1/products/{noGrantProduct}/pricing",
                PricingBody("Standard", 49.90m));
            controllerDenyResponse.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                await controllerDenyResponse.Content.ReadAsStringAsync());
            (await CountPricingRowsAsync(noGrantProduct)).Should().Be(0);

            var controllerDenyAudit = await GetLatestCheckRowAsync(noGrantUserId, PricingManageKey);
            controllerDenyAudit.Should().NotBeNull();
            controllerDenyAudit!.Success.Should().BeFalse();

            // Layer 2 (command): products:pricing:manage alone does not satisfy the
            // monetization:monetize gate on the dispatched command. The request fails
            // closed (non-success; the denial surfaces through the global exception
            // pipeline) and nothing is persisted.
            using var manageOnlyClient = fixture.CreateAuthenticatedClient(manageOnlyUserId, tenantId);
            var commandDenyResponse = await manageOnlyClient.PutAsJsonAsync(
                $"/v1/products/{manageOnlyProduct}/pricing",
                PricingBody("Standard", 49.90m));
            commandDenyResponse.IsSuccessStatusCode.Should().BeFalse(
                "the Monetize gate must deny the pricing mutation even with the controller-level permission");
            commandDenyResponse.StatusCode.Should().BeOneOf(
                [HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError],
                because: $"observed: {commandDenyResponse.StatusCode} - {await commandDenyResponse.Content.ReadAsStringAsync()}");
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

    private async Task<PermissionAuditLog?> GetLatestCheckRowAsync(Guid userId, string permissionKey)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<PermissionAuditLog>()
            .Where(log => log.UserId == userId
                && log.PermissionType == permissionKey
                && log.OperationType == PermissionOperationType.Check)
            .OrderByDescending(log => log.Timestamp)
            .FirstOrDefaultAsync();
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

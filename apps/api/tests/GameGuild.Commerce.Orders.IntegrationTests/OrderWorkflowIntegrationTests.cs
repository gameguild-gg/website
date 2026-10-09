using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Commerce;
using GameGuild.Commerce.Products;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GameGuild.Commerce.Orders.IntegrationTests;

/// <summary>
/// Integration tests for complete order workflows.
/// Tests end-to-end order processing with real infrastructure.
/// </summary>
public class OrderWorkflowIntegrationTests : IClassFixture<WebApplicationFactory<GameGuild.API.Program>>, IDisposable
{
    private readonly WebApplicationFactory<GameGuild.API.Program> _factory;
    private readonly HttpClient _client;
    private static readonly string DatabaseName = $"OrdersTestDb_{Guid.NewGuid()}";
    internal static readonly Guid TestUserIdValue = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid TestTenantIdValue = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public OrderWorkflowIntegrationTests(WebApplicationFactory<GameGuild.API.Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                // Remove all EF Core and Npgsql service registrations
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

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DatabaseName);
                });
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = OrdersTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = OrdersTestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, OrdersTestAuthHandler>(
                    OrdersTestAuthHandler.SchemeName,
                    _ => { });

                services.RemoveAll<IAuthorizationPermissionService>();
                services.RemoveAll<IPermissionQueryService>();
                services.RemoveAll<IOrderPaymentProcessor>();
                services.AddSingleton<IAuthorizationPermissionService, AllowAllOrderPermissions>();
                services.AddSingleton<IPermissionQueryService, AllowAllOrderPermissions>();
                services.AddSingleton<IOrderPaymentProcessor, SuccessfulOrderPaymentProcessor>();
            });
        });

        SeedAuthenticatedTenantMembership(_factory);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SeedAuthenticatedTenantMembership(WebApplicationFactory<GameGuild.API.Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!context.Set<Tenant>().Any(tenant => tenant.Id == TestTenantIdValue))
        {
            context.Set<Tenant>().Add(new Tenant
            {
                Id = TestTenantIdValue,
                Name = "Orders Integration Test Tenant",
                Slug = "orders-integration-test",
                AdminEmail = "orders-integration-admin@example.test",
                IsActive = true
            });
        }

        if (!context.Set<TenantMember>().Any(member =>
                member.UserId == TestUserIdValue && member.TenantId == TestTenantIdValue))
        {
            context.Set<TenantMember>().Add(new TenantMember
            {
                UserId = TestUserIdValue,
                TenantId = TestTenantIdValue,
                Role = "Member",
                IsActive = true
            });
        }

        context.SaveChanges();
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnUnauthorized_WithoutAuthentication()
    {
        // Arrange
        var request = new
        {
            UserId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid().ToString(),
            Currency = "USD"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrder_ShouldReturnUnauthorized_WithoutAuthentication()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/v1/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteOrder_ShouldReturnUnauthorized_WithoutAuthentication()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new
        {
            PaymentId = Guid.NewGuid().ToString(),
            PaymentMethod = "card"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/orders/{orderId}:complete", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelOrder_ShouldReturnNotFound_WhenRouteIsOutsideMinimumSurface()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new { Reason = "Test cancellation" };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/orders/{orderId}:cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RefundOrder_ShouldReturnNotFound_WhenRouteIsOutsideMinimumSurface()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new
        {
            Amount = 100.00m,
            Reason = "Test refund"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/orders/{orderId}:refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddItemToOrder_ShouldReturnUnauthorized_WithoutAuthentication()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new
        {
            ProductId = Guid.NewGuid(),
            Quantity = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/orders/{orderId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CaptureOrder_ShouldUseAuthenticatedAuthoritativeHttpPath()
    {
        var product = Product.Create("Authoritative product", tenantId: TestTenantIdValue);
        var order = Order.Create(TestUserIdValue, $"integration-{Guid.NewGuid():N}", TestTenantIdValue);
        order.AddLineItem(
            product.Id,
            product.Name,
            new OrderLineItemPricingSnapshot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                45m,
                null,
                45m,
                "USD"));
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Set<Product>().Add(product);
            dbContext.Set<Order>().Add(order);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "orders-integration");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantIdValue.ToString());

        var response = await client.PostAsJsonAsync(
            $"/v1/orders/{order.Id}:capture",
            new CaptureOrderRequest("pm_test"));

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        using var captured = JsonDocument.Parse(responseBody);
        captured.RootElement.GetProperty("status").GetString().Should().Be(nameof(OrderStatus.Paid));
        captured.RootElement.GetProperty("total").GetDecimal().Should().Be(45m);
    }

    [Fact]
    public async Task OrderVersion_ShouldRejectConcurrentMutationAfterPaymentReservation()
    {
        var order = Order.Create(TestUserIdValue, $"concurrency-{Guid.NewGuid():N}", TestTenantIdValue);
        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seedContext.Set<Order>().Add(order);
            await seedContext.SaveChangesAsync();
        }

        using var reservationScope = _factory.Services.CreateScope();
        using var mutationScope = _factory.Services.CreateScope();
        var reservationContext = reservationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mutationContext = mutationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservationRepository = new OrderRepository(reservationContext);
        var mutationRepository = new OrderRepository(mutationContext);
        var reservedOrder = await reservationRepository.GetWithLineItemsAsync(order.Id);
        var staleOrder = await mutationRepository.GetWithLineItemsAsync(order.Id);

        reservedOrder!.StartPaymentProcessing();
        await reservationRepository.UpdateAsync(reservedOrder);
        await reservationRepository.SaveChangesAsync();

        staleOrder!.Cancel("stale concurrent cancellation");
        await mutationRepository.UpdateAsync(staleOrder);
        var staleSave = () => mutationRepository.SaveChangesAsync();

        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await verificationContext.Set<Order>().SingleAsync(item => item.Id == order.Id);
        persisted.Status.Should().Be(OrderStatus.Processing);
        persisted.Version.Should().Be(2);
    }
}

internal sealed class OrdersTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "OrdersTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.Authorization.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim("sub", OrderWorkflowIntegrationTests.TestUserIdValue.ToString()),
            new Claim(ClaimTypes.NameIdentifier, OrderWorkflowIntegrationTests.TestUserIdValue.ToString()),
            new Claim("tenant_id", OrderWorkflowIntegrationTests.TestTenantIdValue.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

internal sealed class SuccessfulOrderPaymentProcessor : IOrderPaymentProcessor
{
    public string? GetPaymentMethodValidationError(string paymentMethodId) => null;

    public Task<OrderChargeResult> ProcessAsync(
        AuthoritativeOrderCharge charge,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OrderChargeResult.Succeeded(Guid.NewGuid(), "pi_orders_integration"));
}

internal sealed class AllowAllOrderPermissions : IAuthorizationPermissionService, IPermissionQueryService
{
    public Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([OrdersPermission.Keys.Create, OrdersPermission.Keys.Read]);

    public Task<PermissionCheckResult> HasAllPermissionsAsync(Guid userId, Guid tenantId, IEnumerable<string> permissions, CancellationToken cancellationToken = default) =>
        Task.FromResult(PermissionCheckResult.AllPresent(permissions));

    public Task<PermissionCheckResult> HasAnyPermissionAsync(Guid userId, Guid tenantId, IEnumerable<string> permissions, CancellationToken cancellationToken = default) =>
        Task.FromResult(PermissionCheckResult.AllPresent(permissions));

    public Task<bool> HasTenantPermissionAsync(Guid? userId, Guid? tenantId, string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<List<string>> GetTenantPermissionsAsync(Guid? userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<string> { OrdersPermission.Keys.Create, OrdersPermission.Keys.Read });

    public Task<List<string>> GetEffectivePermissionsAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        GetTenantPermissionsAsync(userId, tenantId, cancellationToken);

    public Task<List<string>> GetGlobalDefaultPermissionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<string>());

    public Task<List<string>> GetTenantDefaultPermissionsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<string>());

    public Task<bool> IsUserInTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}

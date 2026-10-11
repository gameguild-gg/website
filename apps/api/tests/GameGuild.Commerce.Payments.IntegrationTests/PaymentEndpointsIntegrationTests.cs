using FluentAssertions;
using GameGuild.API.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace GameGuild.Commerce.Payments.IntegrationTests;

/// <summary>
/// Integration tests for Payment API endpoints
/// </summary>
public class PaymentEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<GameGuild.API.Program>>, IDisposable
{
    private readonly WebApplicationFactory<GameGuild.API.Program> _factory;
    private readonly HttpClient _client;
    private static readonly string DatabaseName = $"PaymentsTestDb_{Guid.NewGuid()}";

    public PaymentEndpointsIntegrationTests(WebApplicationFactory<GameGuild.API.Program> factory)
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
                services.AddDefaultTenantMembership();

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        });

        TestTenantMembershipServices.SeedDefaultTenant(_factory.Services);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestAuthHandler.DefaultTenantId.ToString());
    }

    [Fact]
    public async Task GetAllPayments_ShouldReturn200_WithEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/payments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAllPayments_ShouldSupportPagination()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/payments?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllPayments_ShouldSupportFiltering_ByStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/payments?status=completed");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllPayments_ShouldSupportFiltering_ByDateRange()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("O");
        var endDate = DateTime.UtcNow.ToString("O");

        // Act
        var response = await _client.GetAsync($"/api/v1/payments?startDate={startDate}&endDate={endDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllPayments_ShouldSupportFiltering_ByAuthenticatedTenantId()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/payments?tenantId={TestAuthHandler.DefaultTenantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllPayments_ShouldRejectFiltering_ByAnotherTenantId()
    {
        var response = await _client.GetAsync($"/api/v1/payments?tenantId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProcessPayment_ShouldReturnBadRequest_WithInvalidData()
    {
        // Arrange
        var request = new
        {
            TenantId = Guid.Empty,
            SubscriptionId = Guid.NewGuid(),
            Amount = -100m,
            PaymentMethodId = "invalid"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/payments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCanceledPayments_ShouldReturn200()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/payments?status=cancelled");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPaymentById_ShouldReturn404_WhenNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/payments/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRetryAttempts_ShouldReturn404_WhenPaymentDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}:retry-attempts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRetryAttempts_ShouldReturn200_WithSeededHistory()
    {
        // Arrange: one succeeded payment for the test tenant with a persisted retry attempt.
        Guid paymentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenantId = TestAuthHandler.DefaultTenantId;
            var payment = Payment.Create(
                tenantId,
                25m,
                "USD",
                $"idem-integration-{Guid.NewGuid():N}",
                subscriptionId: Guid.NewGuid());
            payment.MarkAsProcessing();
            payment.MarkAsFailed("insufficient funds", "insufficient_funds");
            payment.PrepareForRetry();
            payment.MarkAsProcessing();
            payment.MarkAsSucceeded("pi_recovered", "ch_recovered");
            context.Set<Payment>().Add(payment);
            context.Set<PaymentRetryAttempt>().Add(PaymentRetryAttempt.Create(
                payment.Id,
                attemptNumber: 1,
                scheduledForUtc: SystemClock.UtcNow.AddHours(-1),
                executedAtUtc: SystemClock.UtcNow,
                outcome: PaymentStatus.Succeeded,
                gateway: "stripe",
                gatewayTransactionId: "ch_recovered"));
            await context.SaveChangesAsync();
            paymentId = payment.Id;
        }

        // Act
        var response = await _client.GetAsync($"/api/v1/payments/{paymentId}:retry-attempts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var attempts = await response.Content.ReadFromJsonAsync<List<PaymentRetryAttemptDto>>();
        attempts.Should().ContainSingle().Which.AttemptNumber.Should().Be(1);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}

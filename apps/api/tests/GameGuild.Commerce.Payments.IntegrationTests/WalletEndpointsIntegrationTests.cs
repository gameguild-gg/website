using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;

namespace GameGuild.Commerce.Payments.IntegrationTests;

/// <summary>
/// Integration tests for Wallet API endpoints
/// </summary>
public class WalletEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<GameGuild.API.Program>>, IDisposable
{
    private readonly WebApplicationFactory<GameGuild.API.Program> _factory;
    private readonly HttpClient _client;
    private static readonly string DatabaseName = $"PaymentsWalletTestDb_{Guid.NewGuid()}";

    public WalletEndpointsIntegrationTests(WebApplicationFactory<GameGuild.API.Program> factory)
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

                // Authorization matrix tests intentionally exercise permission claims from
                // the test token instead of loading grants from the application database.
                services.RemoveAll<IAuthorizationPermissionService>();
                services.AddScoped<IAuthorizationPermissionService, TestAuthorizationPermissionService>();
                services.RemoveAll<IAuthorizationTenantResolver>();
                services.AddScoped<IAuthorizationTenantResolver, TestAuthorizationTenantResolver>();
                services.RemoveAll<IRevenueAuditService>();
                services.AddSingleton<TestRevenueAuditService>();
                services.AddSingleton<IRevenueAuditService>(provider => provider.GetRequiredService<TestRevenueAuditService>());

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        });

        TestTenantMembershipServices.SeedDefaultTenant(_factory.Services);
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateWallet_ShouldDeriveTheOwnerFromTheAuthenticatedActor()
    {
        var subjectId = Guid.NewGuid();
        using var request = CreateRequest(HttpMethod.Post, "/api/v1/wallet", subjectId);
        request.Content = JsonContent.Create(new { Currency = "USD" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(subjectId.ToString());
    }

    [Fact]
    public async Task GetWallet_ShouldReturnOnlyTheAuthenticatedActorsWallet()
    {
        var subjectId = Guid.NewGuid();
        await SeedWalletAsync(subjectId);
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/wallet", subjectId);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var wallet = await response.Content.ReadFromJsonAsync<UserWallet>();
        wallet!.UserId.Should().Be(subjectId);
    }

    [Fact]
    public async Task LegacyUserRoute_ShouldNotExposeAnotherUsersWallet()
    {
        var subjectId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await SeedWalletAsync(otherUserId);
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/users/{otherUserId}/wallet", subjectId);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TenantAdmin_ShouldNotGainCrossUserWalletAccess()
    {
        var tenantAdminId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await SeedWalletAsync(otherUserId);
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/users/{otherUserId}/wallet",
            tenantAdminId,
            roles: "TenantAdmin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PlatformAdminEndpoint_ShouldRejectAnActorWithoutWalletAdminPermission()
    {
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/wallets/{wallet.Id}", Guid.NewGuid());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformAdminEndpoint_ShouldAllowAnActorWithWalletAdminPermission()
    {
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/wallets/{wallet.Id}",
            Guid.NewGuid(),
            permissions: "wallets:admin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SelfServiceWallet_ShouldRejectMissingTenantContext()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/wallet", Guid.NewGuid(), omitTenant: true);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SelfServiceWallet_ShouldRejectAnUnauthenticatedRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/wallet");
        request.Headers.Add("X-Test-Unauthenticated", "true");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ArbitraryWalletCreationRoute_ShouldNotBePublic()
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/v1/wallets", Guid.NewGuid());
        request.Content = JsonContent.Create(new { UserId = Guid.NewGuid(), Currency = "USD" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("add-funds")]
    [InlineData("deduct-funds")]
    [InlineData("transfer")]
    public async Task ArbitraryValueMutationRoutes_ShouldNotBePublic(string operation)
    {
        var subjectId = Guid.NewGuid();
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/users/{subjectId}/wallet:{operation}",
            subjectId);
        request.Content = JsonContent.Create(new
        {
            Amount = 10m,
            Description = "untrusted mutation",
            ToUserId = Guid.NewGuid()
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task ListWallets_ShouldRequireWalletAdminPermission()
    {
        using var deniedRequest = CreateRequest(HttpMethod.Get, "/api/v1/wallets", Guid.NewGuid(), roles: "TenantAdmin");
        using var allowedRequest = CreateRequest(
            HttpMethod.Get,
            "/api/v1/wallets",
            Guid.NewGuid(),
            permissions: "wallets:admin");

        var deniedResponse = await _client.SendAsync(deniedRequest);
        var allowedResponse = await _client.SendAsync(allowedRequest);

        deniedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        allowedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FreezeWallet_ShouldPersistThePrivilegedActionAuditEvent()
    {
        var administratorId = Guid.NewGuid();
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/wallets/{wallet.Id}:freeze",
            administratorId,
            roles: "Admin",
            permissions: "wallets:admin");
        request.Content = JsonContent.Create(new { Reason = "manual fraud review" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var auditService = _factory.Services.GetRequiredService<TestRevenueAuditService>();
        var audit = auditService.Entries.SingleOrDefault(entry => entry.EntityId == wallet.Id);
        audit.Should().NotBeNull();
        audit!.ChangedBy.Should().Be(administratorId);
        audit.Reason.Should().Be("manual fraud review");
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        Guid subjectId,
        string? roles = null,
        string? permissions = null,
        bool omitTenant = false)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-Subject", subjectId.ToString());
        if (!string.IsNullOrWhiteSpace(roles))
        {
            request.Headers.Add("X-Test-Roles", roles);
        }

        if (!string.IsNullOrWhiteSpace(permissions))
        {
            request.Headers.Add("X-Test-Permissions", permissions);
        }

        if (!omitTenant)
        {
            request.Headers.Add("X-Tenant-Id", TestAuthHandler.DefaultTenantId.ToString());
        }

        if (omitTenant)
        {
            request.Headers.Add("X-Test-No-Tenant", "true");
        }

        return request;
    }

    private async Task<UserWallet> SeedWalletAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = new UserWallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Currency = "USD",
            Balance = 0m,
            IsActive = true
        };
        dbContext.Set<UserWallet>().Add(wallet);
        await dbContext.SaveChangesAsync();
        return wallet;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}

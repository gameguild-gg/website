using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Resources.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.Resources.IntegrationTests.Security;

/// <summary>
/// Integration tests verifying authentication and authorization on Resources module endpoints.
/// These tests ensure that:
/// - Unauthenticated requests receive 401 Unauthorized
/// - Cross-tenant access attempts receive 403 Forbidden
/// - User ownership validation works correctly
/// 
/// Uses Testcontainers to spin up a real PostgreSQL database for realistic integration testing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Security", "Authorization")]
[Trait("Infrastructure", "Database")]
[Collection("PostgreSql")]
public class ResourcesAuthorizationIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlTestFixture _fixture;
    private PostgreSqlWebApplicationFactory? _factory;
    private HttpClient? _anonymousClient;

    // Test tenant and user IDs
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public ResourcesAuthorizationIntegrationTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new PostgreSqlWebApplicationFactory(_fixture.ConnectionString);
        _anonymousClient = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private HttpClient CreateAuthenticatedClient(Guid userId, Guid tenantId, bool isSystemAdmin = false)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "TestScheme", 
            $"{userId}|{tenantId}|{isSystemAdmin}");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    [Fact]
    public void Factory_PreservesDatabaseAliasesAndIsolatedConnection()
    {
        using var scope = _factory!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        scope.ServiceProvider.GetRequiredService<DbContext>().Should().BeSameAs(context);
        scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().Should().BeSameAs(context);
        context.Database.GetConnectionString().Should().Be(_fixture.ConnectionString);
    }

    #region Tenant Quotas Controller Tests

    [Fact]
    public async Task TenantQuotas_Anonymous_Returns401()
    {
        // Act
        var response = await _anonymousClient!.GetAsync($"/v1/tenants/{TenantA}/quotas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TenantQuotas_AuthenticatedWrongTenant_Returns403()
    {
        // Arrange - User from TenantA trying to access TenantB
        using var client = CreateAuthenticatedClient(UserA, TenantA);

        // Act
        var response = await client.GetAsync($"/v1/tenants/{TenantB}/quotas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantQuotas_AuthenticatedCorrectTenant_Succeeds()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(UserA, TenantA);

        // Act
        var response = await client.GetAsync($"/v1/tenants/{TenantA}/quotas");

        // Assert - Should succeed (200 or 204, not 401/403)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Tenant Resources Controller Tests

    [Fact]
    public async Task TenantResources_Anonymous_Returns401()
    {
        // Act
        var response = await _anonymousClient!.GetAsync($"/v1/tenants/{TenantA}/resources/usage-records");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TenantResources_CrossTenantAccess_Returns403()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(UserA, TenantA);

        // Act
        var response = await client.GetAsync($"/v1/tenants/{TenantB}/resources/usage-records");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region User Quotas Controller Tests

    [Fact]
    public async Task UserQuotas_Anonymous_Returns401()
    {
        // Act
        var response = await _anonymousClient!.GetAsync($"/v1/users/{UserA}/quotas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserQuotas_OtherUserAccess_Returns403()
    {
        // Arrange - UserA trying to access UserB's quotas
        using var client = CreateAuthenticatedClient(UserA, TenantA);

        // Act
        var response = await client.GetAsync($"/v1/users/{UserB}/quotas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserQuotas_OwnAccess_Succeeds()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(UserA, TenantA);

        // Act
        var response = await client.GetAsync($"/v1/users/{UserA}/quotas");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Admin Controller Tests

    [Fact]
    public async Task ResourcesAdmin_Anonymous_Returns401()
    {
        // Act - Admin endpoints require authentication
        var response = await _anonymousClient!.GetAsync("/v1/resources/usage?type=0&startDate=2024-01-01&endDate=2024-12-31");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResourcesAdmin_NonAdmin_Returns403()
    {
        // Arrange - Regular user (not admin)
        using var client = CreateAuthenticatedClient(UserA, TenantA, isSystemAdmin: false);

        // Act
        var response = await client.GetAsync("/v1/resources/usage?type=0&startDate=2024-01-01&endDate=2024-12-31");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResourcesAdmin_SystemAdmin_Succeeds()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(UserA, TenantA, isSystemAdmin: true);

        // Act
        var response = await client.GetAsync("/v1/resources/usage?type=0&startDate=2024-01-01&endDate=2024-12-31");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region System Admin Bypass Tests

    [Fact]
    public async Task SystemAdmin_CanAccessAnyTenant()
    {
        // Arrange - System admin from TenantA accessing TenantB
        using var client = CreateAuthenticatedClient(UserA, TenantA, isSystemAdmin: true);

        // Act
        var response = await client.GetAsync($"/v1/tenants/{TenantB}/quotas");

        // Assert - System admin should bypass tenant validation
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SystemAdmin_CanAccessAnyUser()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(UserA, TenantA, isSystemAdmin: true);

        // Act
        var response = await client.GetAsync($"/v1/users/{UserB}/quotas");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    #endregion

    public void Dispose()
    {
        _anonymousClient?.Dispose();
        _factory?.Dispose();
    }
}

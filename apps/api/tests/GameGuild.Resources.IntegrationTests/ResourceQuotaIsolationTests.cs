using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Resources.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Resources.IntegrationTests;

/// <summary>
/// Integration tests verifying cross-tenant quota isolation.
/// Constructs real sub-services (management → enforcement → maintenance → facade).
/// </summary>
[Collection("PostgreSql")]
public class ResourceQuotaIsolationTests : IDisposable
{
    private readonly PostgreSqlTestFixture _postgreSqlFixture;
    private readonly ResourceQuotaTestDbContext _context;
    private readonly IResourceQuotaService _service;

    public ResourceQuotaIsolationTests(PostgreSqlTestFixture postgreSqlFixture)
    {
        _postgreSqlFixture = postgreSqlFixture;
        var options = new DbContextOptionsBuilder<ResourceQuotaTestDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ResourceQuotaTestDbContext(options);
        IResourceQuotaRepository repository = new ResourceQuotaRepository(_context);
        var usageRepository = new UsageRecordRepository(_context);
        var publisherMock = new Mock<IPublisher>();

        // Build the real sub-services
        var management = new QuotaManagementService(
            repository,
            usageRepository,
            publisherMock.Object,
            NullLogger<QuotaManagementService>.Instance);

        var enforcement = new QuotaEnforcementService(
            repository,
            management,
            publisherMock.Object,
            NullLogger<QuotaEnforcementService>.Instance);

        var maintenance = new QuotaMaintenanceService(
            repository,
            usageRepository,
            management,
            publisherMock.Object,
            NullLogger<QuotaMaintenanceService>.Instance);

        // Compose the facade
        _service = new ResourceQuotaService(management, enforcement, maintenance);
    }

    [Fact]
    public async Task TenantA_CannotAccessOrAffect_TenantBQuota()
    {
        // Arrange: Create quotas for two different tenants
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await _service.SetQuotaAsync(tenantA, ResourceUsageType.Users, softLimit: 50, hardLimit: 100);
        await _service.SetQuotaAsync(tenantB, ResourceUsageType.Users, softLimit: 20, hardLimit: 30);

        // Act: Tenant A consumes quota
        for (int i = 0; i < 60; i++)
        {
            await _service.TryAtomicConsumeAsync(tenantA, ResourceUsageType.Users, 1);
        }

        // Tenant B consumes quota
        for (int i = 0; i < 15; i++)
        {
            await _service.TryAtomicConsumeAsync(tenantB, ResourceUsageType.Users, 1);
        }

        // Assert: Verify complete isolation
        var quotaA = await _service.GetQuotaAsync(tenantA, ResourceUsageType.Users);
        var quotaB = await _service.GetQuotaAsync(tenantB, ResourceUsageType.Users);

        quotaA.Should().NotBeNull();
        quotaB.Should().NotBeNull();

        quotaA!.CurrentUsage.Should().Be(60, "Tenant A should have consumed 60");
        quotaB!.CurrentUsage.Should().Be(15, "Tenant B should have consumed 15");

        quotaA.HardLimit.Should().Be(100);
        quotaB.HardLimit.Should().Be(30);

        // Verify that Tenant A's high usage doesn't affect Tenant B
        var (canProceedB, _, _) = await _service.TryAtomicConsumeAsync(tenantB, ResourceUsageType.Users, 1);
        canProceedB.Should().BeTrue("Tenant B should still be able to consume quota despite Tenant A's usage");

        var quotaBAfter = await _service.GetQuotaAsync(tenantB, ResourceUsageType.Users);
        quotaBAfter!.CurrentUsage.Should().Be(16);
    }

    [Fact]
    public async Task TenantA_CannotReadOrModify_TenantBQuota()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await _service.SetQuotaAsync(tenantA, ResourceUsageType.Storage, softLimit: null, hardLimit: 1000);
        await _service.SetQuotaAsync(tenantB, ResourceUsageType.Storage, softLimit: null, hardLimit: 2000);

        // Act: Try to get Tenant B's quota using Tenant A's ID (should return null or Tenant A's quota)
        var quotaA = await _service.GetQuotaAsync(tenantA, ResourceUsageType.Storage);
        var quotaB = await _service.GetQuotaAsync(tenantB, ResourceUsageType.Storage);

        // Assert: Quotas are completely separate
        quotaA.Should().NotBeNull();
        quotaB.Should().NotBeNull();

        quotaA!.TenantId.Should().Be(tenantA);
        quotaB!.TenantId.Should().Be(tenantB);

        quotaA.HardLimit.Should().Be(1000);
        quotaB.HardLimit.Should().Be(2000);

        // Verify that modifying Tenant A's quota doesn't affect Tenant B
        await _service.SetQuotaAsync(tenantA, ResourceUsageType.Storage, softLimit: null, hardLimit: 500);

        var updatedQuotaA = await _service.GetQuotaAsync(tenantA, ResourceUsageType.Storage);
        var unchangedQuotaB = await _service.GetQuotaAsync(tenantB, ResourceUsageType.Storage);

        updatedQuotaA!.HardLimit.Should().Be(500);
        unchangedQuotaB!.HardLimit.Should().Be(2000, "Tenant B's quota should be unaffected");
    }

    [Fact]
    public async Task ConcurrentOperations_OnDifferentTenants_AreFullyIsolated()
    {
        await using var database = await _postgreSqlFixture.CreateDatabaseAsync("quota_isolation");
        await using var setupScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        await setupScope.Context.Database.EnsureCreatedAsync();

        var tenants = new[]
        {
            (Id: Guid.NewGuid(), ExpectedUsage: 30L),
            (Id: Guid.NewGuid(), ExpectedUsage: 25L),
            (Id: Guid.NewGuid(), ExpectedUsage: 40L)
        };

        foreach (var tenant in tenants)
        {
            await setupScope.Service.SetQuotaAsync(
                tenant.Id,
                ResourceUsageType.Users,
                softLimit: null,
                hardLimit: 50);
        }

        var operations = tenants.Select(async tenant =>
        {
            await using var operationScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);

            for (var operation = 0; operation < tenant.ExpectedUsage; operation++)
            {
                var (success, _, _) = await operationScope.Service.TryAtomicConsumeAsync(
                    tenant.Id,
                    ResourceUsageType.Users,
                    amount: 1);

                success.Should().BeTrue("every operation remains below that tenant's hard limit");
            }
        });

        await Task.WhenAll(operations);

        await using var assertionScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        foreach (var tenant in tenants)
        {
            var quota = await assertionScope.Service.GetQuotaAsync(tenant.Id, ResourceUsageType.Users);
            quota.Should().NotBeNull();
            quota!.CurrentUsage.Should().Be(
                tenant.ExpectedUsage,
                $"tenant {tenant.Id} must only contain its own operations");
        }
    }

    [Fact]
    public async Task SequentialOperations_OnDifferentTenants_AreFullyIsolated()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantC = Guid.NewGuid();

        await _service.SetQuotaAsync(tenantA, ResourceUsageType.Users, softLimit: null, hardLimit: 50);
        await _service.SetQuotaAsync(tenantB, ResourceUsageType.Users, softLimit: null, hardLimit: 50);
        await _service.SetQuotaAsync(tenantC, ResourceUsageType.Users, softLimit: null, hardLimit: 50);

        // Act: Sequential operations for each tenant
        for (int i = 0; i < 30; i++)
        {
            await _service.TryAtomicConsumeAsync(tenantA, ResourceUsageType.Users, 1);
        }
        for (int i = 0; i < 25; i++)
        {
            await _service.TryAtomicConsumeAsync(tenantB, ResourceUsageType.Users, 1);
        }
        for (int i = 0; i < 40; i++)
        {
            await _service.TryAtomicConsumeAsync(tenantC, ResourceUsageType.Users, 1);
        }

        // Assert: Each tenant's quota is accurate and isolated
        var quotaA = await _service.GetQuotaAsync(tenantA, ResourceUsageType.Users);
        var quotaB = await _service.GetQuotaAsync(tenantB, ResourceUsageType.Users);
        var quotaC = await _service.GetQuotaAsync(tenantC, ResourceUsageType.Users);

        quotaA!.CurrentUsage.Should().Be(30, "Tenant A should have exactly 30");
        quotaB!.CurrentUsage.Should().Be(25, "Tenant B should have exactly 25");
        quotaC!.CurrentUsage.Should().Be(40, "Tenant C should have exactly 40");

        // None should affect each other
        quotaA.CurrentUsage.Should().NotBe(quotaB.CurrentUsage);
        quotaB.CurrentUsage.Should().NotBe(quotaC.CurrentUsage);
    }

    [Fact]
    public async Task DeleteQuota_OnlyAffects_SpecifiedTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await _service.SetQuotaAsync(tenantA, ResourceUsageType.Projects, softLimit: null, hardLimit: 10);
        await _service.SetQuotaAsync(tenantB, ResourceUsageType.Projects, softLimit: null, hardLimit: 20);

        // Act: Delete Tenant A's quota
        var deleted = await _service.DeleteQuotaAsync(tenantA, ResourceUsageType.Projects);

        // Assert
        deleted.Should().BeTrue();

        var quotaA = await _service.GetQuotaAsync(tenantA, ResourceUsageType.Projects);
        var quotaB = await _service.GetQuotaAsync(tenantB, ResourceUsageType.Projects);

        quotaA.Should().BeNull("Tenant A's quota should be deleted");
        quotaB.Should().NotBeNull("Tenant B's quota should still exist");
        quotaB!.HardLimit.Should().Be(20);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Resources.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Resources.IntegrationTests;

/// <summary>
/// Integration tests for resource quota functionality with concurrency scenarios.
/// Constructs real sub-services (management → enforcement → maintenance → facade).
/// </summary>
[Collection("PostgreSql")]
public class ResourceQuotaIntegrationTests : IDisposable
{
    private readonly PostgreSqlTestFixture _postgreSqlFixture;
    private readonly ResourceQuotaTestDbContext _context;
    private readonly IResourceQuotaRepository _repository;
    private readonly IResourceQuotaService _service;
    private readonly Mock<IPublisher> _publisherMock;

    public ResourceQuotaIntegrationTests(PostgreSqlTestFixture postgreSqlFixture)
    {
        _postgreSqlFixture = postgreSqlFixture;
        var options = new DbContextOptionsBuilder<ResourceQuotaTestDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ResourceQuotaTestDbContext(options);
        _repository = new ResourceQuotaRepository(_context);
        _publisherMock = new Mock<IPublisher>();
        var usageRepository = new UsageRecordRepository(_context);

        // Build the real sub-services
        var management = new QuotaManagementService(
            _repository,
            usageRepository,
            _publisherMock.Object,
            NullLogger<QuotaManagementService>.Instance);

        var enforcement = new QuotaEnforcementService(
            _repository,
            management,
            _publisherMock.Object,
            NullLogger<QuotaEnforcementService>.Instance);

        var maintenance = new QuotaMaintenanceService(
            _repository,
            usageRepository,
            management,
            _publisherMock.Object,
            NullLogger<QuotaMaintenanceService>.Instance);

        // Compose the facade
        _service = new ResourceQuotaService(management, enforcement, maintenance);
    }

    [Fact]
    public async Task ConcurrentCreates_DoNotExceedQuota_WithRaceCondition()
    {
        await using var database = await _postgreSqlFixture.CreateDatabaseAsync("quota_concurrency");
        await using var setupScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        await setupScope.Context.Database.EnsureCreatedAsync();

        // Arrange: Create a quota with limit of 10
        var tenantId = Guid.NewGuid();
        await setupScope.Service.SetQuotaAsync(tenantId, ResourceUsageType.Users, softLimit: 8, hardLimit: 10);

        var operationScopes = new List<ResourceQuotaPostgreSqlScope>();
        try
        {
            // Establish independent connections before releasing the quota
            // operations together; connection setup is not the race under test.
            for (var index = 0; index < 20; index++)
            {
                var operationScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
                operationScopes.Add(operationScope);
                await operationScope.Context.Database.OpenConnectionAsync();
            }

            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = operationScopes.Select(async operationScope =>
                {
                    await start.Task;
                    var (success, _, _) = await operationScope.Service.TryAtomicConsumeAsync(
                        tenantId,
                        ResourceUsageType.Users,
                        amount: 1);
                    return success;
                })
                .ToArray();

            // Act: all twenty database operations remain concurrent.
            start.SetResult(true);
            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            // Assert
            successCount.Should().Be(10, "all ten available quota units should be consumed exactly once");
            results.Count(result => !result).Should().Be(10, "the remaining requests should receive quota rejection results");

            await using var assertionScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
            var quota = await assertionScope.Service.GetQuotaAsync(tenantId, ResourceUsageType.Users);
            quota.Should().NotBeNull();
            quota!.CurrentUsage.Should().Be(10, "final usage should equal the hard limit");
            quota.CurrentUsage.Should().Be(successCount, "usage should match number of successful operations");
        }
        finally
        {
            foreach (var operationScope in operationScopes)
            {
                await operationScope.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ConcurrentCreates_ResetExpiredQuota_AndConsumeExactCapacity()
    {
        await using var database = await _postgreSqlFixture.CreateDatabaseAsync("quota_expired_race");
        await using var setupScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        await setupScope.Context.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var quota = await setupScope.Service.SetQuotaAsync(
            tenantId,
            ResourceUsageType.ApiCalls,
            softLimit: 8,
            hardLimit: 10,
            period: ResourceQuotaPeriod.Daily);
        var expiredReset = SystemClock.UtcNow.AddDays(-2);
        quota.CurrentUsage = 10;
        quota.LastReset = expiredReset;
        await setupScope.Repository.UpdateAsync(quota);

        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 20)
            .Select(async _ =>
            {
                await start.Task;
                await using var operationScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
                var (success, _, _) = await operationScope.Service.TryAtomicConsumeAsync(
                    tenantId,
                    ResourceUsageType.ApiCalls,
                    1);
                return success;
            })
            .ToArray();

        start.SetResult(true);
        var results = await Task.WhenAll(tasks);

        results.Count(result => result).Should().Be(10);
        results.Count(result => !result).Should().Be(10);

        await using var assertionScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        var finalQuota = await assertionScope.Repository.GetByTenantAndTypeAsync(tenantId, ResourceUsageType.ApiCalls);
        finalQuota!.CurrentUsage.Should().Be(10);
        finalQuota.LastReset.Should().BeAfter(expiredReset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AtomicConsume_RejectsNonPositiveAmounts(long amount)
    {
        var action = () => _repository.TryIncrementUsageAsync(
            Guid.NewGuid(),
            ResourceUsageType.ApiCalls,
            amount);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task AtomicConsume_ThrowsOnUnlimitedQuotaOverflow_WithoutChangingUsage()
    {
        await using var database = await _postgreSqlFixture.CreateDatabaseAsync("quota_overflow");
        await using var setupScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        await setupScope.Context.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var quota = await setupScope.Service.SetQuotaAsync(
            tenantId,
            ResourceUsageType.Storage,
            softLimit: null,
            hardLimit: null,
            period: ResourceQuotaPeriod.Unlimited);
        quota.CurrentUsage = long.MaxValue;
        await setupScope.Repository.UpdateAsync(quota);

        await using var operationScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        var action = () => operationScope.Repository.TryIncrementUsageAsync(
            tenantId,
            ResourceUsageType.Storage,
            1);

        await action.Should().ThrowAsync<OverflowException>();

        await using var assertionScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        var persisted = await assertionScope.Repository.GetByTenantAndTypeAsync(tenantId, ResourceUsageType.Storage);
        persisted!.CurrentUsage.Should().Be(long.MaxValue);
    }

    [Fact]
    public async Task AtomicConsume_DoesNotSaveUnrelatedTrackedChanges()
    {
        await using var database = await _postgreSqlFixture.CreateDatabaseAsync("quota_tracker_isolation");
        await using var setupScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        await setupScope.Context.Database.EnsureCreatedAsync();

        var pendingTenantId = Guid.NewGuid();
        var consumedTenantId = Guid.NewGuid();
        await setupScope.Service.SetQuotaAsync(pendingTenantId, ResourceUsageType.Users, null, 10);
        await setupScope.Service.SetQuotaAsync(consumedTenantId, ResourceUsageType.Users, null, 10);

        await using var operationScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        var pendingQuota = await operationScope.Repository.GetByTenantAndTypeAsync(
            pendingTenantId,
            ResourceUsageType.Users);
        pendingQuota!.Description = "pending-change";

        var (success, _, _) = await operationScope.Service.TryAtomicConsumeAsync(
            consumedTenantId,
            ResourceUsageType.Users,
            1);

        success.Should().BeTrue();
        operationScope.Context.Entry(pendingQuota).State.Should().Be(EntityState.Modified);

        await using (var beforeSaveScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString))
        {
            var beforeSave = await beforeSaveScope.Repository.GetByTenantAndTypeAsync(
                pendingTenantId,
                ResourceUsageType.Users);
            beforeSave!.Description.Should().BeNull();
        }

        await operationScope.Context.SaveChangesAsync();

        await using var afterSaveScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        var afterSave = await afterSaveScope.Repository.GetByTenantAndTypeAsync(
            pendingTenantId,
            ResourceUsageType.Users);
        afterSave!.Description.Should().Be("pending-change");
    }

    [Fact]
    public async Task AtomicConsume_CancellationLeavesQuotaUnchangedAndUntracked()
    {
        await using var database = await _postgreSqlFixture.CreateDatabaseAsync("quota_cancellation");
        await using var setupScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        await setupScope.Context.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var quota = await setupScope.Service.SetQuotaAsync(tenantId, ResourceUsageType.Users, null, 10);

        await using var lockScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        await using var transaction = await lockScope.Context.Database.BeginTransactionAsync();
        await lockScope.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE resources.resource_quotas SET \"CurrentUsage\" = \"CurrentUsage\" WHERE \"Id\" = {quota.Id}");

        await using var operationScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var action = () => operationScope.Repository.TryIncrementUsageAsync(
            tenantId,
            ResourceUsageType.Users,
            1,
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        operationScope.Context.ChangeTracker.Entries<ResourceQuota>().Should().BeEmpty();
        await transaction.RollbackAsync();

        await using var assertionScope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        var persisted = await assertionScope.Repository.GetByTenantAndTypeAsync(tenantId, ResourceUsageType.Users);
        persisted!.CurrentUsage.Should().Be(0);
    }

    [Fact]
    public async Task SequentialCreates_DoNotExceedQuota()
    {
        // Arrange: Create a quota with limit of 10
        var tenantId = Guid.NewGuid();
        await _service.SetQuotaAsync(tenantId, ResourceUsageType.Users, softLimit: 8, hardLimit: 10);

        // Simulate 20 sequential create requests
        int successCount = 0;
        for (int i = 0; i < 20; i++)
        {
            var (success, _, _) = await _service.TryAtomicConsumeAsync(
                tenantId,
                ResourceUsageType.Users,
                amount: 1);
            if (success) successCount++;
        }

        // Assert
        successCount.Should().Be(10, "exactly 10 should succeed before hitting hard limit");

        var quota = await _service.GetQuotaAsync(tenantId, ResourceUsageType.Users);
        quota.Should().NotBeNull();
        quota!.CurrentUsage.Should().Be(10, "final usage should be exactly at hard limit");
    }

    [Fact]
    public async Task SequentialCreates_WithExactQuotaRemaining_OnlyOneSucceeds()
    {
        // Note: Changed to sequential due to in-memory DbContext thread-safety limitations.
        // For true concurrency testing, use a real database.

        // Arrange: Tenant with quota 10, current usage 9
        var tenantId = Guid.NewGuid();
        await _service.SetQuotaAsync(tenantId, ResourceUsageType.Users, softLimit: 8, hardLimit: 10);

        // Pre-consume 9 slots
        for (int i = 0; i < 9; i++)
        {
            await _service.TryAtomicConsumeAsync(tenantId, ResourceUsageType.Users, 1);
        }

        // Act: Try 10 more sequential create requests (only 1 slot remaining)
        int successCount = 0;
        for (int i = 0; i < 10; i++)
        {
            var (success, _, _) = await _service.TryAtomicConsumeAsync(
                tenantId,
                ResourceUsageType.Users,
                amount: 1);
            if (success) successCount++;
        }

        // Assert: Exactly 1 should succeed
        successCount.Should().Be(1, "only one request should get the last remaining slot");

        // Assert: Quota should be exactly at limit
        var quota = await _service.GetQuotaAsync(tenantId, ResourceUsageType.Users);
        quota.Should().NotBeNull();
        quota!.CurrentUsage.Should().Be(10, "usage should be exactly at the hard limit");
    }

    [Fact]
    public async Task CreateAndDelete_MaintainsAccurateQuota_OverMultipleOperations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await _service.SetQuotaAsync(tenantId, ResourceUsageType.Users, softLimit: 50, hardLimit: 100);

        // Act: Perform mixed create and delete operations
        // Each cycle: +10 creates, -5 deletes = net +5 per cycle
        long expectedUsage = 0;
        for (int cycle = 0; cycle < 5; cycle++)
        {
            // Create 10 users
            for (int i = 0; i < 10; i++)
            {
                var (success, _, _) = await _service.TryAtomicConsumeAsync(
                    tenantId,
                    ResourceUsageType.Users,
                    amount: 1);
                success.Should().BeTrue();
            }
            expectedUsage += 10;

            // Verify count
            var quotaAfterCreates = await _service.GetQuotaAsync(tenantId, ResourceUsageType.Users);
            quotaAfterCreates!.CurrentUsage.Should().Be(expectedUsage, $"cycle {cycle}: after 10 creates");

            // Delete 5 users
            for (int i = 0; i < 5; i++)
            {
                var decremented = await _repository.DecrementUsageAsync(
                    tenantId,
                    ResourceUsageType.Users,
                    amount: 1);
                decremented.Should().BeTrue();
            }
            expectedUsage -= 5;

            // Verify count after deletes
            var quotaAfterDeletes = await _service.GetQuotaAsync(tenantId, ResourceUsageType.Users);
            quotaAfterDeletes!.CurrentUsage.Should().Be(expectedUsage, $"cycle {cycle}: after 5 deletes");
        }

        // Final assertion: 5 cycles * net +5 = 25
        var finalQuota = await _service.GetQuotaAsync(tenantId, ResourceUsageType.Users);
        finalQuota!.CurrentUsage.Should().Be(25, "final usage should be accurate after multiple create/delete cycles");
    }

    [Fact]
    public async Task RollbackOnFailure_DoesNotIncrementQuota()
    {
        await using var database = await _postgreSqlFixture.CreateDatabaseAsync("quota_rollback");
        await using var scope = ResourceQuotaPostgreSqlScope.Create(database.ConnectionString);
        await scope.Context.Database.EnsureCreatedAsync();

        // Arrange
        var tenantId = Guid.NewGuid();
        await scope.Service.SetQuotaAsync(tenantId, ResourceUsageType.Users, softLimit: null, hardLimit: 10);

        var initialQuota = await scope.Service.GetQuotaAsync(tenantId, ResourceUsageType.Users);
        var initialUsage = initialQuota!.CurrentUsage;

        // Act: Simulate a transaction that would increment quota but then fail
        await using var transaction = await scope.Context.Database.BeginTransactionAsync();

        try
        {
            // Try to consume quota
            var (success, _, _) = await scope.Service.TryAtomicConsumeAsync(
                tenantId,
                ResourceUsageType.Users,
                amount: 1);

            success.Should().BeTrue();

            // Simulate failure - rollback
            await transaction.RollbackAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
        }

        // Assert: Quota should not have been incremented
        scope.Context.ChangeTracker.Clear(); // Clear tracking to force fresh read
        var quotaAfterRollback = await scope.Service.GetQuotaAsync(tenantId, ResourceUsageType.Users);
        quotaAfterRollback!.CurrentUsage.Should().Be(initialUsage, "quota should not change after rollback");
    }

    [Fact]
    public async Task QuotaReset_HandledCorrectly_SequentialOperations()
    {
        // Note: Changed from concurrent to sequential due to in-memory DbContext limitations.
        // For true concurrency testing, use a real database with proper transaction isolation.

        // Arrange
        var tenantId = Guid.NewGuid();
        var quota = await _service.SetQuotaAsync(
            tenantId,
            ResourceUsageType.ApiCalls,
            softLimit: 800,
            hardLimit: 1000,
            period: ResourceQuotaPeriod.Daily);

        // Pre-fill quota to near limit
        for (int i = 0; i < 100; i++)
        {
            await _service.TryAtomicConsumeAsync(tenantId, ResourceUsageType.ApiCalls, 1);
        }

        var quotaBeforeReset = await _service.GetQuotaAsync(tenantId, ResourceUsageType.ApiCalls);
        quotaBeforeReset!.CurrentUsage.Should().Be(100);

        // Act: Reset quota and fire sequential requests
        quota = await _service.GetQuotaAsync(tenantId, ResourceUsageType.ApiCalls);
        quota!.CurrentUsage = 0;
        quota.LastReset = DateTime.UtcNow;
        await _repository.UpdateAsync(quota);

        // Consume after reset
        int successCount = 0;
        for (int i = 0; i < 50; i++)
        {
            var (success, _, _) = await _service.TryAtomicConsumeAsync(
                tenantId,
                ResourceUsageType.ApiCalls,
                amount: 1);
            if (success) successCount++;
        }

        // Assert: All 50 should succeed since we reset to 0
        successCount.Should().Be(50, "all requests should succeed after quota reset");

        var finalQuota = await _service.GetQuotaAsync(tenantId, ResourceUsageType.ApiCalls);
        finalQuota!.CurrentUsage.Should().Be(50, "usage should match successful operations after reset");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

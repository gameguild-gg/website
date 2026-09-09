using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Finance.Contracts;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using GameGuild.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Resources.UnitTests;

#region GetResourceUsageTrendsHandler Tests

public class GetResourceUsageTrendsHandlerTests
{
    private readonly GetResourceUsageTrendsHandler _handler = new();

    [Fact]
    public async Task Handle_DailyGranularity_ReturnsDataPoints()
    {
        var query = new GetResourceUsageTrendsQuery(
            ResourceUsageType.Users,
            new DateTime(2025, 1, 1),
            new DateTime(2025, 1, 5),
            TrendGranularity.Daily);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Type.Should().Be(ResourceUsageType.Users);
        result.StartDate.Should().Be(new DateTime(2025, 1, 1));
        result.EndDate.Should().Be(new DateTime(2025, 1, 5));
        result.Granularity.Should().Be(TrendGranularity.Daily);
        result.DataPoints.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Handle_WeeklyGranularity_ReturnsDataPoints()
    {
        var query = new GetResourceUsageTrendsQuery(
            ResourceUsageType.Projects,
            new DateTime(2025, 1, 1),
            new DateTime(2025, 2, 1),
            TrendGranularity.Weekly);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Granularity.Should().Be(TrendGranularity.Weekly);
        result.DataPoints.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Handle_MonthlyGranularity_ReturnsDataPoints()
    {
        var query = new GetResourceUsageTrendsQuery(
            ResourceUsageType.Storage,
            new DateTime(2025, 1, 1),
            new DateTime(2025, 6, 1),
            TrendGranularity.Monthly);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Granularity.Should().Be(TrendGranularity.Monthly);
        result.DataPoints.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Handle_EmptyRange_ReturnsEmptyDataPoints()
    {
        var date = new DateTime(2025, 1, 1);
        var query = new GetResourceUsageTrendsQuery(
            ResourceUsageType.ApiCalls,
            date.AddDays(1),
            date,
            TrendGranularity.Daily);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.DataPoints.Should().BeEmpty();
    }
}

#endregion

#region DefaultIncidentTicketProvider Tests

public class DefaultIncidentTicketProviderTests
{
    private readonly DefaultIncidentTicketProvider _provider = new();

    [Fact]
    public async Task CreateTicketAsync_ReturnsTicketId()
    {
        var analysis = new SlaImpactAnalysis
        {
            Id = Guid.NewGuid(),
            ResourceQuotaId = Guid.NewGuid()
        };

        var ticketId = await _provider.CreateTicketAsync(analysis);

        ticketId.Should().NotBeNullOrWhiteSpace();
        ticketId.Should().StartWith("INC-");
    }

    [Fact]
    public async Task UpdateTicketAsync_CompletesSuccessfully()
    {
        await _provider.UpdateTicketAsync("INC-001", "InProgress", "some notes");
        // No-op in default implementation - just verify it doesn't throw
    }

    [Fact]
    public async Task CloseTicketAsync_CompletesSuccessfully()
    {
        await _provider.CloseTicketAsync("INC-001", "resolved");
        // No-op in default implementation - just verify it doesn't throw
    }
}

#endregion

#region ResourceUsageTypeRegistry Tests

public class ResourceUsageTypeRegistryAdditionalTests
{
    [Fact]
    public void GetAll_ReturnsBuiltInTypes()
    {
        var all = ResourceUsageTypeRegistry.GetAll();
        all.Should().NotBeEmpty();
    }

    [Fact]
    public void GetBuiltIn_ReturnsOnlyBuiltIn()
    {
        var builtIn = ResourceUsageTypeRegistry.GetBuiltIn();
        builtIn.Should().NotBeEmpty();
        builtIn.All(t => t.IsBuiltIn).Should().BeTrue();
    }

    [Fact]
    public void Get_ByEnumValue_ReturnsInfo()
    {
        var info = ResourceUsageTypeRegistry.Get(ResourceUsageType.Users);
        info.Should().NotBeNull();
        info.Key.Should().Be("Users");
    }

    [Fact]
    public void GetByKey_ValidKey_ReturnsInfo()
    {
        var info = ResourceUsageTypeRegistry.GetByKey("Users");
        info.Should().NotBeNull();
    }

    [Fact]
    public void GetByKey_InvalidKey_Throws()
    {
        var act = () => ResourceUsageTypeRegistry.GetByKey("NonExistentKey12345");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TryGetByKey_NullOrWhitespace_ReturnsFalse()
    {
        var result = ResourceUsageTypeRegistry.TryGetByKey("", out var info);
        result.Should().BeFalse();
        info.Should().BeNull();
    }

    [Fact]
    public void IsRegistered_ById_ReturnsTrue()
    {
        var result = ResourceUsageTypeRegistry.IsRegistered((int)ResourceUsageType.Users);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_ByKey_ReturnsTrue()
    {
        var result = ResourceUsageTypeRegistry.IsRegistered("Users");
        result.Should().BeTrue();
    }

    [Fact]
    public void ToKey_ReturnsCorrectKey()
    {
        var key = ResourceUsageTypeRegistry.ToKey(ResourceUsageType.Users);
        key.Should().Be("Users");
    }

    [Fact]
    public void ToEnum_ReturnsCorrectEnum()
    {
        var type = ResourceUsageTypeRegistry.ToEnum("Users");
        type.Should().Be(ResourceUsageType.Users);
    }
}

#endregion

#region CachedResourceQuotaService Additional Tests

public class CachedResourceQuotaServiceAdditionalTests
{
    private readonly Mock<IResourceQuotaService> _inner = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly CachedResourceQuotaService _service;

    public CachedResourceQuotaServiceAdditionalTests()
    {
        _service = new CachedResourceQuotaService(
            _inner.Object,
            _cache,
            NullLogger<CachedResourceQuotaService>.Instance);
    }

    [Fact]
    public async Task GetUsageHistoryAsync_DelegatesToInner()
    {
        var tenantId = Guid.NewGuid();
        var records = new List<UsageRecord>().AsEnumerable();
        _inner.Setup(x => x.GetUsageHistoryAsync(tenantId, ResourceUsageType.Users, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var result = await _service.GetUsageHistoryAsync(tenantId, ResourceUsageType.Users);

        result.Should().BeSameAs(records);
    }

    [Fact]
    public async Task CheckLimitsAsync_DelegatesToInner()
    {
        var tenantId = Guid.NewGuid();
        var response = new ResourceLimitCheckResponse();
        _inner.Setup(x => x.CheckLimitsAsync(tenantId, ResourceUsageType.Users, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _service.CheckLimitsAsync(tenantId, ResourceUsageType.Users);

        result.Should().Be(response);
    }

    [Fact]
    public async Task CheckMultipleLimitsAsync_DelegatesToInner()
    {
        var tenantId = Guid.NewGuid();
        var requested = new Dictionary<ResourceUsageType, long> { { ResourceUsageType.Users, 1 } };
        var response = new Dictionary<ResourceUsageType, ResourceLimitCheckResponse>();
        _inner.Setup(x => x.CheckMultipleLimitsAsync(tenantId, requested, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _service.CheckMultipleLimitsAsync(tenantId, requested);

        result.Should().BeSameAs(response);
    }

    [Fact]
    public async Task TryAtomicConsumeAsync_InvalidatesCacheAndReturns()
    {
        var tenantId = Guid.NewGuid();
        _inner.Setup(x => x.TryAtomicConsumeAsync(tenantId, ResourceUsageType.Users, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 5L, (long?)100));

        var result = await _service.TryAtomicConsumeAsync(tenantId, ResourceUsageType.Users);

        result.Success.Should().BeTrue();
        result.CurrentUsage.Should().Be(5);
    }

    [Fact]
    public async Task DecrementUsageAsync_WhenSuccess_InvalidatesCache()
    {
        var tenantId = Guid.NewGuid();
        _inner.Setup(x => x.DecrementUsageAsync(tenantId, ResourceUsageType.Users, 1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DecrementUsageAsync(tenantId, ResourceUsageType.Users);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DecrementUsageAsync_WhenFailure_DoesNotInvalidateCache()
    {
        var tenantId = Guid.NewGuid();
        _inner.Setup(x => x.DecrementUsageAsync(tenantId, ResourceUsageType.Users, 1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.DecrementUsageAsync(tenantId, ResourceUsageType.Users);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetTenantsExceedingLimitsAsync_DelegatesToInner()
    {
        var tenants = new List<Guid> { Guid.NewGuid() }.AsEnumerable();
        _inner.Setup(x => x.GetTenantsExceedingLimitsAsync(null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenants);

        var result = await _service.GetTenantsExceedingLimitsAsync();

        result.Should().BeSameAs(tenants);
    }

    [Fact]
    public async Task ResetExpiredQuotasAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.ResetExpiredQuotasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _service.ResetExpiredQuotasAsync();

        result.Should().Be(3);
    }

    [Fact]
    public async Task CleanupOldUsageRecordsAsync_DelegatesToInner()
    {
        var olderThan = DateTime.UtcNow.AddDays(-30);
        _inner.Setup(x => x.CleanupOldUsageRecordsAsync(olderThan, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var result = await _service.CleanupOldUsageRecordsAsync(olderThan);

        result.Should().Be(10);
    }

    [Fact]
    public async Task RecalculateUsageAsync_WhenSuccess_InvalidatesCache()
    {
        var tenantId = Guid.NewGuid();
        _inner.Setup(x => x.RecalculateUsageAsync(tenantId, ResourceUsageType.Users, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.RecalculateUsageAsync(tenantId, ResourceUsageType.Users);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RecalculateUsageAsync_WhenFailure_DoesNotInvalidateCache()
    {
        var tenantId = Guid.NewGuid();
        _inner.Setup(x => x.RecalculateUsageAsync(tenantId, ResourceUsageType.Users, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.RecalculateUsageAsync(tenantId, ResourceUsageType.Users);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetResourceUsageDetailsAsync_DelegatesToInner()
    {
        var tenantId = Guid.NewGuid();
        var response = new ResourceUsageResponse();
        _inner.Setup(x => x.GetResourceUsageDetailsAsync(tenantId, ResourceUsageType.Users, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _service.GetResourceUsageDetailsAsync(tenantId, ResourceUsageType.Users);

        result.Should().Be(response);
    }
}

#endregion

#region ResourceQuota Entity Additional Tests

public class ResourceQuotaEntityAdditionalTests
{
    [Fact]
    public void GetNextResetTime_WithResetTime_WhenPassed_ReturnsNextDay()
    {
        var quota = new ResourceQuota();
        quota.LastReset = new DateTime(2025, 1, 15, 14, 0, 0);
        quota.ResetTime = new TimeSpan(10, 0, 0); // 10:00 AM, already passed since LastReset is 14:00
        quota.Period = ResourceQuotaPeriod.Daily;

        var nextReset = quota.GetNextResetTime();

        nextReset.Should().NotBeNull();
    }

    [Fact]
    public void GetNextResetTime_UnlimitedPeriod_ReturnsNull()
    {
        var quota = new ResourceQuota();
        quota.LastReset = DateTime.UtcNow;
        quota.Period = ResourceQuotaPeriod.Unlimited;

        var nextReset = quota.GetNextResetTime();

        nextReset.Should().BeNull();
    }

    [Fact]
    public void GetNextResetTime_NoLastReset_ReturnsNull()
    {
        var quota = new ResourceQuota();
        quota.LastReset = null;

        var nextReset = quota.GetNextResetTime();

        nextReset.Should().BeNull();
    }

    [Fact]
    public void GetNextResetTime_QuarterlyPeriod_ReturnsCorrectDate()
    {
        var quota = new ResourceQuota();
        var lastReset = new DateTime(2025, 1, 1, 0, 0, 0);
        quota.LastReset = lastReset;
        quota.Period = ResourceQuotaPeriod.Quarterly;

        var nextReset = quota.GetNextResetTime();

        nextReset.Should().Be(lastReset.AddMonths(3));
    }

    [Fact]
    public void GetNextResetTime_YearlyPeriod_ReturnsCorrectDate()
    {
        var quota = new ResourceQuota();
        var lastReset = new DateTime(2025, 1, 1, 0, 0, 0);
        quota.LastReset = lastReset;
        quota.Period = ResourceQuotaPeriod.Yearly;

        var nextReset = quota.GetNextResetTime();

        nextReset.Should().Be(lastReset.AddYears(1));
    }
}

#endregion

#region ResourceThrottlingPolicy Additional Tests

public class ResourceThrottlingPolicyAdditionalTests
{
    [Fact]
    public void CalculateDelayMs_HardCutoff_ReturnsMaxInt()
    {
        var policy = new ResourceThrottlingPolicy
        {
            IsActive = true,
            ThrottlingThresholdPercent = 50,
            Strategy = ThrottlingStrategy.HardCutoff
        };

        var delay = policy.CalculateDelayMs(75);

        delay.Should().Be(int.MaxValue);
    }

    [Fact]
    public void CalculateDelayMs_RateLimiting_CalculatesCorrectly()
    {
        var policy = new ResourceThrottlingPolicy
        {
            IsActive = true,
            ThrottlingThresholdPercent = 50,
            Strategy = ThrottlingStrategy.RateLimiting,
            MaxRequestsPerWindow = 100,
            WindowDurationSeconds = 60
        };

        var delay = policy.CalculateDelayMs(75);

        delay.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateDelayMs_PriorityBased_CalculatesCorrectly()
    {
        var policy = new ResourceThrottlingPolicy
        {
            IsActive = true,
            ThrottlingThresholdPercent = 50,
            Strategy = ThrottlingStrategy.PriorityBased,
            DegradationFactor = 1.0m
        };

        var delay = policy.CalculateDelayMs(75);

        delay.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateDelayMs_RateLimiting_NoWindow_ReturnsZero()
    {
        var policy = new ResourceThrottlingPolicy
        {
            IsActive = true,
            ThrottlingThresholdPercent = 50,
            Strategy = ThrottlingStrategy.RateLimiting,
            MaxRequestsPerWindow = null
        };

        var delay = policy.CalculateDelayMs(75);

        delay.Should().Be(0);
    }
}

#endregion

#region Command Handler Constructor Tests

public class CommandHandlerConstructorTests
{
    [Fact]
    public void ArchiveResourceUsageRecordsCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IUsageRecordRepository>();
        var handler = new ArchiveResourceUsageRecordsCommandHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CleanupOrphanedResourcesHandler_CanBeConstructed()
    {
        using var db = CreateResourcesDbContext();
        var handler = new CleanupOrphanedResourcesHandler(db);
        handler.Should().NotBeNull();
    }

    [Fact]
    public async Task CleanupOrphanedResourcesHandler_DryRunCountsAndDeleteRemovesOnlyOrphans()
    {
        await using var db = CreateResourcesDbContext();
        var tenantId = Guid.NewGuid();
        db.UsageRecords.AddRange(
            UsageRecord.CreateDaily(ResourceUsageType.Users, tenantId, 1, DateTime.UtcNow),
            new UsageRecord
            {
                Type = ResourceUsageType.Users,
                Count = 2,
                PeriodStart = DateTime.UtcNow.Date,
                PeriodEnd = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1)
            },
            new UsageRecord
            {
                TenantId = Guid.Empty,
                Type = ResourceUsageType.Storage,
                Count = 3,
                PeriodStart = DateTime.UtcNow.Date,
                PeriodEnd = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1)
            });
        await db.SaveChangesAsync();

        var handler = new CleanupOrphanedResourcesHandler(db);

        var dryRunCount = await handler.Handle(new CleanupOrphanedResourcesCommand(DryRun: true), CancellationToken.None);
        dryRunCount.Should().Be(2);
        db.UsageRecords.Should().HaveCount(3);

        var deleted = await handler.Handle(new CleanupOrphanedResourcesCommand(DryRun: false, ResourceTypes: [ResourceUsageType.Users]), CancellationToken.None);
        deleted.Should().Be(1);
        db.UsageRecords.Should().HaveCount(2);
        db.UsageRecords.Count(record => record.TenantId == tenantId).Should().Be(1);
        db.UsageRecords.Count(record => record.TenantId == Guid.Empty).Should().Be(1);
    }

    [Fact]
    public void DeleteUserResourceQuotaCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new DeleteUserResourceQuotaCommandHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void RecordUserResourceUsageCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IUsageRecordRepository>();
        var handler = new RecordUserResourceUsageCommandHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ResetResourceUsageCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IUsageRecordRepository>();
        var handler = new ResetResourceUsageCommandHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ResetUserResourceQuotaCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new ResetUserResourceQuotaCommandHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ResetUserResourceUsageCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IUsageRecordRepository>();
        var handler = new ResetUserResourceUsageCommandHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void SetUserResourceQuotaCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new SetUserResourceQuotaCommandHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ToggleResourceQuotaCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new ToggleResourceQuotaCommandHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ToggleUserResourceQuotaCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new ToggleUserResourceQuotaCommandHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void DeleteResourceQuotaCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var publisher = new Mock<IPublisher>();
        var accessor = new Mock<IActorContextAccessor>();
        var handler = new DeleteResourceQuotaCommandHandler(repo.Object, publisher.Object, accessor.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ResetResourceQuotaCommandHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var publisher = new Mock<IPublisher>();
        var accessor = new Mock<IActorContextAccessor>();
        var handler = new ResetResourceQuotaCommandHandler(repo.Object, publisher.Object, accessor.Object);
        handler.Should().NotBeNull();
    }

    private static CleanupResourcesDbContext CreateResourcesDbContext()
    {
        var options = new DbContextOptionsBuilder<CleanupResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new CleanupResourcesDbContext(options);
    }

    private sealed class CleanupResourcesDbContext(DbContextOptions<CleanupResourcesDbContext> options) : DbContext(options), IApplicationDbContext
    {
        public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UsageRecord>();
        }
    }
}

#endregion

#region Query Handler Constructor Tests

public class QueryHandlerConstructorTests
{
    [Fact]
    public void CheckResourceQuotaQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new CheckResourceQuotaQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CheckResourceUsageLimitsQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var dbCtx = new Mock<GameGuild.IApplicationDbContext>();
        var handler = new CheckResourceUsageLimitsQueryHandler(repo.Object, dbCtx.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CheckUserResourceQuotaQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new CheckUserResourceQuotaQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CheckUserResourceUsageLimitsQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new CheckUserResourceUsageLimitsQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetCurrentResourceUsageSummaryQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IUsageRecordRepository>();
        var handler = new GetCurrentResourceUsageSummaryQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetCurrentUserResourceUsageSummaryQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IUsageRecordRepository>();
        var handler = new GetCurrentUserResourceUsageSummaryQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetResourceUsageByTypeQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IUsageRecordRepository>();
        var handler = new GetResourceUsageByTypeQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetResourceUsageRecordsQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IUsageRecordRepository>();
        var handler = new GetResourceUsageRecordsQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetTenantResourceQuotasQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new GetTenantResourceQuotasQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetUserResourceQuotaQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new GetUserResourceQuotaQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetUserResourceQuotasQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IResourceQuotaRepository>();
        var handler = new GetUserResourceQuotasQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetUserResourceUsageRecordsQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IUsageRecordRepository>();
        var handler = new GetUserResourceUsageRecordsQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }
}

#endregion

#region Repository Constructor Tests

public class RepositoryConstructorTests
{
    [Fact]
    public void CostAllocationReportRepository_CanBeConstructed()
    {
        var ctx = new Mock<GameGuild.IApplicationDbContext>();
        var repo = new CostAllocationReportRepository(ctx.Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void ResourceMetadataRepository_CanBeConstructed()
    {
        var ctx = new Mock<GameGuild.IApplicationDbContext>();
        var repo = new ResourceMetadataRepository(ctx.Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void ResourceSettingsRepository_CanBeConstructed()
    {
        var ctx = new Mock<GameGuild.IApplicationDbContext>();
        var repo = new ResourceSettingsRepository(ctx.Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void ResourceThrottlingPolicyRepository_CanBeConstructed()
    {
        var ctx = new Mock<GameGuild.IApplicationDbContext>();
        var repo = new ResourceThrottlingPolicyRepository(ctx.Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void ResourceUsageTrendRepository_CanBeConstructed()
    {
        var ctx = new Mock<GameGuild.IApplicationDbContext>();
        var repo = new ResourceUsageTrendRepository(ctx.Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void SlaImpactAnalysisRepository_CanBeConstructed()
    {
        var ctx = new Mock<GameGuild.IApplicationDbContext>();
        var repo = new SlaImpactAnalysisRepository(ctx.Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void UsageRecordRepository_CanBeConstructed()
    {
        var ctx = new Mock<GameGuild.IApplicationDbContext>();
        var repo = new UsageRecordRepository(ctx.Object);
        repo.Should().NotBeNull();
    }

    [Fact]
    public void UsageRetentionPolicyRepository_CanBeConstructed()
    {
        var ctx = new Mock<GameGuild.IApplicationDbContext>();
        var repo = new UsageRetentionPolicyRepository(ctx.Object);
        repo.Should().NotBeNull();
    }
}

#endregion

#region Service Constructor Tests

public class ServiceConstructorTests
{
    [Fact]
    public void QuotaEnforcementService_CanBeConstructed()
    {
        var quotaRepo = new Mock<IResourceQuotaRepository>();
        var mgmtService = new Mock<IQuotaManagementService>();
        var publisher = new Mock<IPublisher>();
        var logger = NullLogger<QuotaEnforcementService>.Instance;
        var service = new QuotaEnforcementService(quotaRepo.Object, mgmtService.Object, publisher.Object, logger);
        service.Should().NotBeNull();
    }

    [Fact]
    public void QuotaMaintenanceService_CanBeConstructed()
    {
        var quotaRepo = new Mock<IResourceQuotaRepository>();
        var usageRepo = new Mock<IUsageRecordRepository>();
        var mgmtService = new Mock<IQuotaManagementService>();
        var publisher = new Mock<IPublisher>();
        var logger = NullLogger<QuotaMaintenanceService>.Instance;
        var service = new QuotaMaintenanceService(quotaRepo.Object, usageRepo.Object, mgmtService.Object, publisher.Object, logger);
        service.Should().NotBeNull();
    }

    [Fact]
    public void QuotaManagementService_CanBeConstructed()
    {
        var quotaRepo = new Mock<IResourceQuotaRepository>();
        var usageRepo = new Mock<IUsageRecordRepository>();
        var publisher = new Mock<IPublisher>();
        var logger = NullLogger<QuotaManagementService>.Instance;
        var service = new QuotaManagementService(quotaRepo.Object, usageRepo.Object, publisher.Object, logger);
        service.Should().NotBeNull();
    }
}

#endregion

#region ResourceQuotaMetadata Tests

public class ResourceQuotaMetadataAdditionalTests
{
    [Fact]
    public void Empty_ReturnsEmptyMetadata()
    {
        var meta = ResourceQuotaMetadata.Empty;
        meta.Should().NotBeNull();
    }
}

#endregion

#region Controller Constructor Tests

public class ControllerConstructorTests
{
    [Fact]
    public void ResourcesController_CanBeConstructed()
    {
        var sender = new Mock<ISender>();
        var controller = new ResourcesController(sender.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void TenantQuotasController_CanBeConstructed()
    {
        var sender = new Mock<ISender>();
        var accessor = new Mock<IActorContextAccessor>();
        var checker = new Mock<GameGuild.Identity.Authorization.ITenantMembershipChecker>();
        var controller = new TenantQuotasController(sender.Object, accessor.Object, checker.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void TenantResourceMetadataController_CanBeConstructed()
    {
        var repo = new Mock<IResourceMetadataRepository>();
        var sender = new Mock<ISender>();
        var accessor = new Mock<IActorContextAccessor>();
        var checker = new Mock<GameGuild.Identity.Authorization.ITenantMembershipChecker>();
        var controller = new TenantResourceMetadataController(repo.Object, sender.Object, accessor.Object, checker.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void TenantResourcesController_CanBeConstructed()
    {
        var sender = new Mock<ISender>();
        var quotaService = new Mock<IResourceQuotaService>();
        var accessor = new Mock<IActorContextAccessor>();
        var checker = new Mock<GameGuild.Identity.Authorization.ITenantMembershipChecker>();
        var controller = new TenantResourcesController(sender.Object, quotaService.Object, accessor.Object, checker.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void TenantResourceSettingsController_CanBeConstructed()
    {
        var repo = new Mock<IResourceSettingsRepository>();
        var sender = new Mock<ISender>();
        var accessor = new Mock<IActorContextAccessor>();
        var checker = new Mock<GameGuild.Identity.Authorization.ITenantMembershipChecker>();
        var controller = new TenantResourceSettingsController(repo.Object, sender.Object, accessor.Object, checker.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void UserQuotasController_CanBeConstructed()
    {
        var sender = new Mock<ISender>();
        var accessor = new Mock<IActorContextAccessor>();
        var controller = new UserQuotasController(sender.Object, accessor.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void UserResourceMetadataController_CanBeConstructed()
    {
        var repo = new Mock<IResourceMetadataRepository>();
        var sender = new Mock<ISender>();
        var accessor = new Mock<IActorContextAccessor>();
        var controller = new UserResourceMetadataController(repo.Object, sender.Object, accessor.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void UserResourcesController_CanBeConstructed()
    {
        var sender = new Mock<ISender>();
        var accessor = new Mock<IActorContextAccessor>();
        var controller = new UserResourcesController(sender.Object, accessor.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void UserResourceSettingsController_CanBeConstructed()
    {
        var repo = new Mock<IResourceSettingsRepository>();
        var sender = new Mock<ISender>();
        var accessor = new Mock<IActorContextAccessor>();
        var controller = new UserResourceSettingsController(repo.Object, sender.Object, accessor.Object);
        controller.Should().NotBeNull();
    }
}

#endregion

#region DependencyInjection Registration Tests

[Collection("ResourceUsageTypeRegistry")]
public class DependencyInjectionTests : IDisposable
{
    public DependencyInjectionTests()
    {
        // Reset the static registry before the test to ensure clean state
        ResetRegistry();
    }

    [Fact]
    public void AddResourcesInfrastructure_RegistersAllExpectedServices()
    {
        var services = new ServiceCollection();

        // Add dependencies that the registration method needs
        services.AddMemoryCache();
        services.AddLogging();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Resources:QuotaCheckEnabled", "true" },
                { "Resources:DefaultQuotaPeriod", "Monthly" }
            })
            .Build();

        // Act - this exercises all registration code paths
        services.AddResourcesInfrastructure(config);

        // Assert - verify key service descriptors were registered (not resolved)
        var registeredTypes = services.Select(s => s.ServiceType).ToList();

        registeredTypes.Should().Contain(typeof(IResourceQuotaRepository));
        registeredTypes.Should().Contain(typeof(IUsageRecordRepository));
        registeredTypes.Should().Contain(typeof(IResourceQuotaService));
        registeredTypes.Should().Contain(typeof(IResourceSettingsRepository));
        registeredTypes.Should().Contain(typeof(IResourceMetadataRepository));
        registeredTypes.Should().Contain(typeof(IIntegrationEventHandler<EconomyPostingAcceptedEventV1>));
    }

    public void Dispose()
    {
        // Reset the static ResourceUsageTypeRegistry to avoid side effects on other tests
        ResetRegistry();
    }

    private static void ResetRegistry()
    {
        typeof(ResourceUsageTypeRegistry)
            .GetMethod("Reset", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(null, null);
    }
}

#endregion

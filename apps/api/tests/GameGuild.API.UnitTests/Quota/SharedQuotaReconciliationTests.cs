using Microsoft.EntityFrameworkCore;
using GameGuild.API.Core.Quotas;
using GameGuild.API.Database;
using GameGuild.Resources;

namespace GameGuild.API.UnitTests.Quota;

public sealed class SharedQuotaReconciliationTests
{
    [Fact]
    public async Task UnknownProductCounter_IsNotInterpretedAsZero()
    {
        await using var db = CreateContext();
        db.Add(new ResourceQuota { TenantId = Guid.NewGuid(), Type = ResourceUsageType.Properties, CurrentUsage = 7 });
        await db.SaveChangesAsync();
        Assert.Equal(0, await new QuotaReconciliationService(db).ReconcileAsync());
        Assert.Equal(7, (await db.Set<ResourceQuota>().SingleAsync()).CurrentUsage);
        Assert.Empty(await db.Set<QuotaLedgerEntry>().ToListAsync());
    }

    [Fact]
    public async Task ProductAdapter_RepairsTenantPoolOnce_WithoutChangingUserQuota()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        db.AddRange(
            new ResourceQuota { TenantId = tenantId, Type = ResourceUsageType.Properties, CurrentUsage = 0 },
            new ResourceQuota { TenantId = tenantId, UserId = Guid.NewGuid(), Type = ResourceUsageType.Properties, CurrentUsage = 9 });
        await db.SaveChangesAsync();
        var source = new ProductCounter(tenantId, 2);
        var service = new QuotaReconciliationService(db, [source]);

        Assert.Equal(1, await service.ReconcileAsync());
        Assert.Equal(0, await service.ReconcileAsync());

        Assert.Equal(2, (await db.Set<ResourceQuota>().SingleAsync(q => q.UserId == null)).CurrentUsage);
        Assert.Equal(9, (await db.Set<ResourceQuota>().SingleAsync(q => q.UserId != null)).CurrentUsage);
        var entry = Assert.Single(await db.Set<QuotaLedgerEntry>().ToListAsync());
        Assert.Equal(2, entry.Delta);
        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal("quota.reconciled", entry.OperationCode);
    }

    [Fact]
    public async Task DuplicateProductOwnership_IsRejected()
    {
        await using var db = CreateContext();
        var source = new ProductCounter(Guid.NewGuid(), 0);
        Assert.Throws<ArgumentException>(() => new QuotaReconciliationService(db, [source, source]));
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class ProductCounter(Guid expectedTenant, long count) : IQuotaAuthoritativeUsageSource
    {
        public ResourceUsageType ResourceType => ResourceUsageType.Properties;
        public Task<long> CountAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedTenant, tenantId);
            return Task.FromResult(count);
        }
    }
}

using GameGuild.API.Database;
using GameGuild.Features;
using GameGuild.Identity.Context.Actors;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using CommerceSubscription = GameGuild.Commerce.Subscriptions.Subscription;
using CommerceSubscriptionPlan = GameGuild.Commerce.Subscriptions.SubscriptionPlan;

namespace GameGuild.API.UnitTests.Database;

public sealed class CapabilityPlanSyncPostgreSqlTests(CapabilityPlanSyncPostgreSqlFixture fixture)
    : IClassFixture<CapabilityPlanSyncPostgreSqlFixture>
{
    private const string CapabilityKey = "lxp.discovery";
    private const int KnownCapabilityCount = 16;

    [Theory]
    [InlineData("override:admin", false)]
    [InlineData("override:admin", true)]
    [InlineData("trial:promotion", false)]
    [InlineData("promotional", false)]
    [InlineData(null, false)]
    [InlineData("OVERRIDE:admin", false)]
    public async Task Sync_PreservesExplicitRowsWithoutUniqueKeyCollision(string? source, bool expired)
    {
        await using var context = fixture.CreateContext();
        var tenantId = await SeedSubscription(context);
        var userId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(
            DateTimeOffset.UtcNow.AddDays(expired ? -1 : 1).ToUnixTimeMilliseconds());
        var row = new TenantCapability
        {
            TenantId = tenantId, CapabilityKey = CapabilityKey, IsEnabled = false, Source = source,
            Priority = 1000, ExpiresAt = expiresAt, Metadata = "{\"reason\":\"explicit entitlement\"}",
            ModifiedByUserId = Guid.NewGuid(), ModificationReason = "Preserve the explicit decision"
        };
        context.Add(row);
        await context.SaveChangesAsync();
        var originalId = row.Id;
        var originalActor = row.ModifiedByUserId;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(context, cache, tenantId, userId);
        Assert.Equal(expired, (await service.GetTenantCapabilitiesAsync(tenantId))[CapabilityKey]);

        await service.SyncCapabilitiesFromPlanAsync(tenantId);

        context.ChangeTracker.Clear();
        var stored = await context.Set<TenantCapability>().SingleAsync(value =>
            value.TenantId == tenantId && value.CapabilityKey == CapabilityKey);
        Assert.Equal(originalId, stored.Id);
        Assert.False(stored.IsEnabled);
        Assert.Equal(source, stored.Source);
        Assert.Equal(1000, stored.Priority);
        Assert.Equal(expiresAt, stored.ExpiresAt);
        Assert.Equal(row.Metadata, stored.Metadata);
        Assert.Equal(originalActor, stored.ModifiedByUserId);
        Assert.Equal(row.ModificationReason, stored.ModificationReason);
        Assert.Equal(KnownCapabilityCount, await context.Set<TenantCapability>().CountAsync(value => value.TenantId == tenantId));
        Assert.Empty(await context.Set<CapabilityAuditLog>().Where(value =>
            value.TenantId == tenantId && value.CapabilityKey == CapabilityKey).ToListAsync());
        Assert.Equal(expired, (await service.GetTenantCapabilitiesAsync(tenantId))[CapabilityKey]);
        Assert.Equal(expired, await service.IsCapabilityEnabledAsync(tenantId, CapabilityKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sync_AuditsEachNewPlanRowWithTheResolvedActor(bool systemActor)
    {
        await using var context = fixture.CreateContext();
        var tenantId = await SeedSubscription(context);
        var userId = systemActor ? (Guid?)null : Guid.NewGuid();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(context, cache, tenantId, userId);

        await service.SyncCapabilitiesFromPlanAsync(tenantId);

        context.ChangeTracker.Clear();
        var rows = await context.Set<TenantCapability>().Where(value => value.TenantId == tenantId).ToListAsync();
        var audits = await context.Set<CapabilityAuditLog>().Where(value => value.TenantId == tenantId).ToListAsync();
        Assert.Equal(KnownCapabilityCount, rows.Count);
        Assert.Equal(KnownCapabilityCount, audits.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal("plan:starter", row.Source);
            Assert.Equal(0, row.Priority);
            Assert.Equal(userId, row.ModifiedByUserId);
            var audit = Assert.Single(audits, value => value.CapabilityKey == row.CapabilityKey);
            Assert.Null(audit.OldValue);
            Assert.Null(audit.OldSource);
            Assert.Equal(row.IsEnabled, audit.NewValue);
            Assert.Equal(row.Source, audit.NewSource);
            Assert.Equal(userId, audit.ChangedByUserId);
            Assert.Equal(CapabilityChangeType.PlanChange, audit.ChangeType);
            Assert.False(string.IsNullOrWhiteSpace(audit.ChangeReason));
        });
    }

    [Theory]
    [InlineData("plan:free")]
    [InlineData("plan:enterprise")]
    [InlineData("PLAN:FREE")]
    public async Task Sync_UpdatesAndAuditsPlanSourceEvenWhenEnabledStateIsUnchanged(string previousSource)
    {
        await using var context = fixture.CreateContext();
        var tenantId = await SeedSubscription(context);
        var userId = Guid.NewGuid();
        var row = new TenantCapability
        {
            TenantId = tenantId, CapabilityKey = CapabilityKey, IsEnabled = true, Source = previousSource
        };
        context.Add(row);
        await context.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await CreateService(context, cache, tenantId, userId).SyncCapabilitiesFromPlanAsync(tenantId);

        context.ChangeTracker.Clear();
        var stored = await context.Set<TenantCapability>().SingleAsync(value => value.Id == row.Id);
        Assert.True(stored.IsEnabled);
        Assert.Equal("plan:starter", stored.Source);
        Assert.Equal(userId, stored.ModifiedByUserId);
        var audit = await context.Set<CapabilityAuditLog>().SingleAsync(value =>
            value.TenantId == tenantId && value.CapabilityKey == CapabilityKey);
        Assert.Equal(true, audit.OldValue);
        Assert.True(audit.NewValue);
        Assert.Equal(previousSource, audit.OldSource);
        Assert.Equal("plan:starter", audit.NewSource);
        Assert.Equal(userId, audit.ChangedByUserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sync_AuditsChangedPlanValueWithTheResolvedActor(bool systemActor)
    {
        await using var context = fixture.CreateContext();
        var tenantId = await SeedSubscription(context);
        var userId = systemActor ? (Guid?)null : Guid.NewGuid();
        context.Add(new TenantCapability
        {
            TenantId = tenantId, CapabilityKey = CapabilityKey, IsEnabled = false, Source = "plan:free"
        });
        await context.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await CreateService(context, cache, tenantId, userId).SyncCapabilitiesFromPlanAsync(tenantId);

        var audit = await context.Set<CapabilityAuditLog>().SingleAsync(value =>
            value.TenantId == tenantId && value.CapabilityKey == CapabilityKey);
        Assert.Equal(false, audit.OldValue);
        Assert.True(audit.NewValue);
        Assert.Equal(userId, audit.ChangedByUserId);
        Assert.Equal(CapabilityChangeType.PlanChange, audit.ChangeType);
    }

    [Fact]
    public async Task Sync_RepeatedUnchangedPlanDoesNotDuplicateRowsOrAuditEntries()
    {
        await using var context = fixture.CreateContext();
        var tenantId = await SeedSubscription(context);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(context, cache, tenantId, Guid.NewGuid());
        await service.SyncCapabilitiesFromPlanAsync(tenantId);
        var rowIds = await context.Set<TenantCapability>().Where(value => value.TenantId == tenantId)
            .OrderBy(value => value.CapabilityKey).Select(value => value.Id).ToListAsync();
        var auditIds = await context.Set<CapabilityAuditLog>().Where(value => value.TenantId == tenantId)
            .OrderBy(value => value.Id).Select(value => value.Id).ToListAsync();

        await service.SyncCapabilitiesFromPlanAsync(tenantId);

        Assert.Equal(rowIds, await context.Set<TenantCapability>().Where(value => value.TenantId == tenantId)
            .OrderBy(value => value.CapabilityKey).Select(value => value.Id).ToListAsync());
        Assert.Equal(auditIds, await context.Set<CapabilityAuditLog>().Where(value => value.TenantId == tenantId)
            .OrderBy(value => value.Id).Select(value => value.Id).ToListAsync());
    }

    [Fact]
    public async Task Sync_WithoutActiveSubscriptionDoesNotChangeRows()
    {
        await using var context = fixture.CreateContext();
        var tenantId = Guid.NewGuid();
        context.Add(new TenantCapability
        {
            TenantId = tenantId, CapabilityKey = CapabilityKey, IsEnabled = false, Source = "override:admin"
        });
        await context.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await CreateService(context, cache, tenantId, Guid.NewGuid()).SyncCapabilitiesFromPlanAsync(tenantId);

        Assert.Single(await context.Set<TenantCapability>().Where(value => value.TenantId == tenantId).ToListAsync());
        Assert.Empty(await context.Set<CapabilityAuditLog>().Where(value => value.TenantId == tenantId).ToListAsync());
    }

    [Fact]
    public async Task Sync_DoesNotModifyAnotherTenantsRows()
    {
        await using var context = fixture.CreateContext();
        var tenantId = await SeedSubscription(context);
        var otherTenantId = Guid.NewGuid();
        var row = new TenantCapability
        {
            TenantId = otherTenantId, CapabilityKey = CapabilityKey, IsEnabled = false, Source = "plan:free"
        };
        context.Add(row);
        await context.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await CreateService(context, cache, tenantId, Guid.NewGuid()).SyncCapabilitiesFromPlanAsync(tenantId);

        context.ChangeTracker.Clear();
        var stored = await context.Set<TenantCapability>().SingleAsync(value => value.Id == row.Id);
        Assert.False(stored.IsEnabled);
        Assert.Equal("plan:free", stored.Source);
        Assert.Empty(await context.Set<CapabilityAuditLog>().Where(value => value.TenantId == otherTenantId).ToListAsync());
    }

    private static CapabilityService CreateService(ApplicationDbContext context, IMemoryCache cache, Guid tenantId, Guid? userId)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(userId.HasValue
            ? ActorContextBuilder.ForUser(userId.Value).WithTenantId(tenantId).WithRole("TenantAdmin").Build()
            : ActorContextBuilder.ForSystem("capability-plan-sync").Build());
        return new CapabilityService(context, cache, NullLogger<CapabilityService>.Instance, accessor);
    }

    private static async Task<Guid> SeedSubscription(ApplicationDbContext context)
    {
        var tenantId = Guid.NewGuid();
        var marker = Guid.NewGuid().ToString("N");
        var plan = await context.Set<CommerceSubscriptionPlan>().SingleOrDefaultAsync(value => value.Slug == "starter");
        if (plan is null)
        {
            plan = new CommerceSubscriptionPlan($"Starter {marker}", "starter", 1000);
            context.Add(plan);
        }
        var subscription = new CommerceSubscription(tenantId, plan.Id, Guid.NewGuid(), BillingCycle.Monthly,
            new Money(10m), DateTime.UtcNow.AddDays(-1)) { Plan = plan };
        subscription.Activate();
        context.Add(subscription);
        await context.SaveChangesAsync();
        return tenantId;
    }
}

public sealed class CapabilityPlanSyncPostgreSqlFixture : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase? _database;

    public async Task InitializeAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("capability_plan_sync");
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(_database!.ConnectionString).Options);

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}

using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Subscription = GameGuild.Commerce.Subscriptions.Subscription;
using CommerceSubscriptionPlan = GameGuild.Commerce.Subscriptions.SubscriptionPlan;

namespace GameGuild.Features.UnitTests;

public sealed class CapabilityAuthorizationTests
{
    private static readonly Guid TenantId = Guid.Parse("148d59a5-c80d-4c0a-ad83-94e0a22355a3");
    private static readonly Guid UserId = Guid.Parse("6f7eeb7b-8244-4739-a6c1-fe72cc3a9e77");
    private static readonly Guid SpoofedId = Guid.Parse("1a80884a-244d-4867-ae7b-409bcd7cbd94");

    public static IEnumerable<object[]> DeniedActors()
    {
        foreach (var operation in new[] { "set", "remove", "sync", "audit" })
        {
            foreach (var actor in new[] { "member", "flag-manager", "other-admin", "no-tenant", "anonymous", "invalid-user", "empty-user", "unprivileged-system", "service-member" })
            {
                yield return [operation, actor];
            }
        }
    }

    public static IEnumerable<object[]> DeniedHandlerActors() => DeniedActors().Where(value => (string)value[0] != "audit");

    [Theory]
    [MemberData(nameof(DeniedActors))]
    public async Task Service_RejectsUnauthorizedCallsBeforePersistence(string operation, string actorName)
    {
        await using var db = CreateDatabase();
        await SeedAsync(db);
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(Actor(actorName));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(db, cache, accessor);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CallServiceAsync(service, operation));
        Assert.False((await db.Set<TenantCapability>().SingleAsync()).IsEnabled);
        Assert.Empty(await db.Set<CapabilityAuditLog>().ToListAsync());
        accessor.ClearActorContext();
    }

    [Theory]
    [MemberData(nameof(DeniedHandlerActors))]
    public async Task Handler_RejectsUnauthorizedMutationBeforeCallingService(string operation, string actorName)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(Actor(actorName));
        var service = new Mock<ICapabilityService>(MockBehavior.Strict);
        var handler = CreateHandler(service.Object, accessor);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CallHandlerAsync(handler, operation));
        service.VerifyNoOtherCalls();
        accessor.ClearActorContext();
    }

    [Theory]
    [InlineData("set")]
    [InlineData("remove")]
    public async Task Service_AuditsResolvedUserInsteadOfCallerSuppliedIdentity(string operation)
    {
        await using var db = CreateDatabase();
        await SeedAsync(db);
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(Actor("admin"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(db, cache, accessor);
        await CallServiceAsync(service, operation);
        var log = await db.Set<CapabilityAuditLog>().SingleAsync();
        Assert.Equal(UserId, log.ChangedByUserId);
        if (operation == "set")
        {
            Assert.Equal(UserId, (await db.Set<TenantCapability>().SingleAsync()).ModifiedByUserId);
        }

        accessor.ClearActorContext();
    }

    [Theory]
    [InlineData("set")]
    [InlineData("remove")]
    public async Task Handler_PropagatesResolvedUserInsteadOfCommandIdentity(string operation)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(Actor("admin"));
        var service = new Mock<ICapabilityService>();
        var handler = CreateHandler(service.Object, accessor);
        await CallHandlerAsync(handler, operation);
        if (operation == "set")
        {
            service.Verify(value => value.SetCapabilityOverrideAsync(TenantId, "branding.custom", true, "override:test", UserId, "native test", null, default), Times.Once);
        }
        else
        {
            service.Verify(value => value.RemoveCapabilityOverrideAsync(TenantId, "branding.custom", UserId, "native test", default), Times.Once);
        }

        accessor.ClearActorContext();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("trusted-system")]
    [InlineData("service-admin")]
    public async Task Service_PreservesAuthorizedMutationsAuditAndCacheInvalidation(string actorName)
    {
        await using var db = CreateDatabase();
        await SeedAsync(db);
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(Actor(actorName));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(db, cache, accessor);
        Assert.False((await service.GetTenantCapabilitiesAsync(TenantId))["branding.custom"]);
        await CallServiceAsync(service, "set");
        Assert.True((await service.GetTenantCapabilitiesAsync(TenantId))["branding.custom"]);
        Assert.True(await service.IsCapabilityEnabledAsync(TenantId, "branding.custom"));
        await CallServiceAsync(service, "remove");
        Assert.False((await service.GetTenantCapabilitiesAsync(TenantId))["branding.custom"]);
        await CallServiceAsync(service, "sync");
        Assert.Contains(await db.Set<TenantCapability>().ToListAsync(), row => row.Source == "plan:starter" && row.IsEnabled);
        var logs = (await service.GetAuditLogAsync(TenantId)).ToList();
        Assert.Equal(18, logs.Count);
        Assert.Equal(16, logs.Count(log => log.ChangeType == CapabilityChangeType.PlanChange));
        Assert.All(logs, log => Assert.Equal(actorName == "admin" ? UserId : (Guid?)null, log.ChangedByUserId));
        accessor.ClearActorContext();
    }

    [Theory]
    [InlineData("set")]
    [InlineData("remove")]
    [InlineData("sync")]
    [InlineData("audit")]
    public async Task Service_RejectsEmptyTenantEvenForTrustedSystem(string operation)
    {
        await using var db = CreateDatabase();
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(Actor("trusted-system"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(db, cache, accessor);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CallServiceAsync(service, operation, Guid.Empty));
        Assert.Empty(await db.Set<TenantCapability>().ToListAsync());
        Assert.Empty(await db.Set<CapabilityAuditLog>().ToListAsync());
        accessor.ClearActorContext();
    }

    private static ActorContext Actor(string name)
    {
        if (name == "anonymous")
        {
            return ActorContext.Anonymous;
        }

        if (name == "trusted-system")
        {
            return ActorContextBuilder.ForSystem("capability-sync").Build();
        }

        var builder = ActorContextBuilder.ForUser(UserId).WithTenantId(TenantId);
        return name switch
        {
            "admin" => builder.WithRole("TenantAdmin").Build(),
            "member" => builder.WithRole("Member").Build(),
            "flag-manager" => builder.WithPermission("features:manage").Build(),
            "other-admin" => builder.WithRole("TenantAdmin").WithTenantId(SpoofedId).Build(),
            "no-tenant" => builder.WithRole("TenantAdmin").WithTenantId(null).Build(),
            "invalid-user" => builder.WithRole("TenantAdmin").WithSubjectId("invalid").Build(),
            "empty-user" => builder.WithRole("TenantAdmin").WithSubjectId(Guid.Empty.ToString()).Build(),
            "unprivileged-system" => builder.WithActorKind(ActorKind.System).WithSubjectId("system").Build(),
            "service-member" => builder.WithActorKind(ActorKind.Service).WithSubjectId("service-client").Build(),
            "service-admin" => builder.WithActorKind(ActorKind.Service).WithSubjectId("service-client").WithRole("TenantAdmin").Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static CapabilityService CreateService(CapabilityDbContext db, IMemoryCache cache, IActorContextAccessor accessor)
        => new(db, cache, NullLogger<CapabilityService>.Instance, accessor);

    private static FeatureOperationCommandHandler CreateHandler(ICapabilityService service, IActorContextAccessor accessor)
        => new(Mock.Of<IFeatureFlagEvaluationService>(), service, NullLogger<FeatureOperationCommandHandler>.Instance, accessor);

    private static Task CallServiceAsync(ICapabilityService service, string operation) => CallServiceAsync(service, operation, TenantId);

    private static Task CallServiceAsync(ICapabilityService service, string operation, Guid target) => operation switch
    {
        "set" => service.SetCapabilityOverrideAsync(target, "branding.custom", true, "override:test", SpoofedId, "native test"),
        "remove" => service.RemoveCapabilityOverrideAsync(target, "branding.custom", SpoofedId, "native test"),
        "sync" => service.SyncCapabilitiesFromPlanAsync(target),
        "audit" => service.GetAuditLogAsync(target),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static Task<Unit> CallHandlerAsync(FeatureOperationCommandHandler handler, string operation) => operation switch
    {
        "set" => handler.Handle(new SetCapabilityOverrideCommand(TenantId, "branding.custom", true, "override:test", SpoofedId, "native test", null), default),
        "remove" => handler.Handle(new RemoveCapabilityOverrideCommand(TenantId, "branding.custom", SpoofedId, "native test"), default),
        "sync" => handler.Handle(new SyncCapabilitiesFromPlanCommand(TenantId), default),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static CapabilityDbContext CreateDatabase() => new(new DbContextOptionsBuilder<CapabilityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedAsync(CapabilityDbContext db)
    {
        var plan = new CommerceSubscriptionPlan("Starter", "starter", 1000) { Id = Guid.NewGuid() };
        db.Add(plan);
        var subscription = new Subscription(TenantId, plan.Id, Guid.NewGuid(), BillingCycle.Monthly,
            new Money(10m), DateTime.UtcNow.AddDays(-1)) { Id = Guid.NewGuid(), Plan = plan };
        subscription.Activate();
        db.Add(subscription);
        db.Add(new TenantCapability { TenantId = TenantId, CapabilityKey = "branding.custom", IsEnabled = false, Source = "override:test", Priority = 1000 });
        await db.SaveChangesAsync();
    }

    private sealed class CapabilityDbContext(DbContextOptions<CapabilityDbContext> options) : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantCapability>();
            modelBuilder.Entity<CapabilityAuditLog>();
            modelBuilder.Entity<CommerceSubscriptionPlan>();
            modelBuilder.Entity<Subscription>().OwnsOne(subscription => subscription.Amount);
            modelBuilder.Entity<Subscription>().HasOne(subscription => subscription.Plan).WithMany(plan => plan.Subscriptions).HasForeignKey("PlanId");
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The in-memory fixture does not support transactions.");
    }
}

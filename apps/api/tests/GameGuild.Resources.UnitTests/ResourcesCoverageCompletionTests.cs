using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using GameGuild;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Resources.Handlers;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace GameGuild.Resources.UnitTests;

public sealed class ResourceUserScopedControllerCoverageTests
{
    [Fact]
    public async Task UserQuotasController_CoversOwnershipBranches()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetUserResourceQuotasQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ResourceQuotaResponse>());

        await AssertForbidAsync(() => CreateQuotasController(sender.Object, null).GetUserQuotas(userId));
        await AssertForbidAsync(() => CreateQuotasController(sender.Object, Actor(userId, authenticated: false)).GetUserQuotas(userId));
        await AssertForbidAsync(() => CreateQuotasController(sender.Object, Actor("not-a-guid")).GetUserQuotas(userId));
        await AssertOkAsync(() => CreateQuotasController(sender.Object, Actor(Guid.NewGuid(), roles: "SystemAdmin")).GetUserQuotas(userId));
        await AssertOkAsync(() => CreateQuotasController(sender.Object, Actor(userId)).GetUserQuotas(userId));
        await AssertForbidAsync(() => CreateQuotasController(sender.Object, Actor(Guid.NewGuid())).GetUserQuotas(userId));
    }

    [Fact]
    public async Task UserResourcesController_CoversOwnershipBranches()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetCurrentUserResourceUsageSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ResourceUsageType, long>());

        await AssertForbidAsync(() => CreateResourcesController(sender.Object, null).GetCurrentUsageSummary(userId, CancellationToken.None));
        await AssertForbidAsync(() => CreateResourcesController(sender.Object, Actor(userId, authenticated: false)).GetCurrentUsageSummary(userId, CancellationToken.None));
        await AssertForbidAsync(() => CreateResourcesController(sender.Object, Actor("not-a-guid")).GetCurrentUsageSummary(userId, CancellationToken.None));
        await AssertOkAsync(() => CreateResourcesController(sender.Object, Actor(Guid.NewGuid(), roles: "SystemAdmin")).GetCurrentUsageSummary(userId, CancellationToken.None));
        await AssertOkAsync(() => CreateResourcesController(sender.Object, Actor(userId)).GetCurrentUsageSummary(userId, CancellationToken.None));
        await AssertForbidAsync(() => CreateResourcesController(sender.Object, Actor(Guid.NewGuid())).GetCurrentUsageSummary(userId, CancellationToken.None));
    }

    [Fact]
    public async Task UserMetadataController_CoversOwnershipBranches()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IResourceMetadataRepository>();
        repository
            .Setup(r => r.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ResourceMetadata>());

        await AssertForbidAsync(() => CreateMetadataController(repository.Object, null).GetUserMetadata(userId, CancellationToken.None));
        await AssertForbidAsync(() => CreateMetadataController(repository.Object, Actor(userId, authenticated: false)).GetUserMetadata(userId, CancellationToken.None));
        await AssertForbidAsync(() => CreateMetadataController(repository.Object, Actor("not-a-guid")).GetUserMetadata(userId, CancellationToken.None));
        await AssertOkAsync(() => CreateMetadataController(repository.Object, Actor(Guid.NewGuid(), roles: "SystemAdmin")).GetUserMetadata(userId, CancellationToken.None));
        await AssertOkAsync(() => CreateMetadataController(repository.Object, Actor(userId)).GetUserMetadata(userId, CancellationToken.None));
        await AssertForbidAsync(() => CreateMetadataController(repository.Object, Actor(Guid.NewGuid())).GetUserMetadata(userId, CancellationToken.None));
    }

    [Fact]
    public async Task UserSettingsController_CoversOwnershipBranches()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IResourceSettingsRepository>();
        repository
            .Setup(r => r.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ResourceSettings>());

        await AssertForbidAsync(() => CreateSettingsController(repository.Object, null).GetUserSettings(userId, CancellationToken.None));
        await AssertForbidAsync(() => CreateSettingsController(repository.Object, Actor(userId, authenticated: false)).GetUserSettings(userId, CancellationToken.None));
        await AssertForbidAsync(() => CreateSettingsController(repository.Object, Actor("not-a-guid")).GetUserSettings(userId, CancellationToken.None));
        await AssertOkAsync(() => CreateSettingsController(repository.Object, Actor(Guid.NewGuid(), roles: "SystemAdmin")).GetUserSettings(userId, CancellationToken.None));
        await AssertOkAsync(() => CreateSettingsController(repository.Object, Actor(userId)).GetUserSettings(userId, CancellationToken.None));
        await AssertForbidAsync(() => CreateSettingsController(repository.Object, Actor(Guid.NewGuid())).GetUserSettings(userId, CancellationToken.None));
    }

    private static UserQuotasController CreateQuotasController(ISender sender, ActorContext? actor)
        => new(sender, Accessor(actor));

    private static UserResourcesController CreateResourcesController(ISender sender, ActorContext? actor)
        => new(sender, Accessor(actor));

    private static UserResourceMetadataController CreateMetadataController(IResourceMetadataRepository repository, ActorContext? actor)
        => new(repository, Mock.Of<ISender>(), Accessor(actor));

    private static UserResourceSettingsController CreateSettingsController(IResourceSettingsRepository repository, ActorContext? actor)
        => new(repository, Mock.Of<ISender>(), Accessor(actor));

    private static IActorContextAccessor Accessor(ActorContext? actor)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor!);
        return accessor.Object;
    }

    private static ActorContext Actor(Guid subjectId, bool authenticated = true, params string[] roles)
        => Actor(subjectId.ToString(), authenticated, roles);

    private static ActorContext Actor(string? subjectId, bool authenticated = true, params string[] roles)
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = subjectId,
            TenantId = Guid.NewGuid(),
            Roles = roles.ToHashSet(StringComparer.Ordinal),
            Permissions = new HashSet<string>(),
            IsAuthenticated = authenticated
        };

    private static async Task AssertForbidAsync(Func<Task<IActionResult>> action)
        => (await action()).Should().BeOfType<ForbidResult>();

    private static async Task AssertOkAsync(Func<Task<IActionResult>> action)
        => (await action()).Should().BeOfType<OkObjectResult>();
}

public sealed class ResourceInfrastructureCoverageCompletionTests
{
    [Fact]
    public async Task RepositorySetAccessors_AreExercised()
    {
        var context = new ThrowingApplicationDbContext();

        await AssertSetAccessorAsync(() => new CostAllocationReportRepository(context).GetByIdAsync(Guid.NewGuid()));
        await AssertSetAccessorAsync(() => new ResourceMetadataRepository(context).GetByIdAsync(Guid.NewGuid()));
        await AssertSetAccessorAsync(() => new ResourceSettingsRepository(context).GetByIdAsync(Guid.NewGuid()));
        await AssertSetAccessorAsync(() => new ResourceThrottlingPolicyRepository(context).GetByIdAsync(Guid.NewGuid()));
        await AssertSetAccessorAsync(() => new ResourceUsageTrendRepository(context).GetByIdAsync(Guid.NewGuid()));
        await AssertSetAccessorAsync(() => new SlaImpactAnalysisRepository(context).GetByIdAsync(Guid.NewGuid()));
        await AssertSetAccessorAsync(() => new UsageRecordRepository(context).GetByIdAsync(Guid.NewGuid()));
        await AssertSetAccessorAsync(() => new UsageRetentionPolicyRepository(context).GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public void ResourcesInfrastructure_RegistersRedisLimiter_WhenRedisEnabled()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:Enabled"] = "true" })
            .Build();

        services.AddLogging();
        services.AddMemoryCache();
        services.AddResourcesInfrastructure(configuration);

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IDistributedRateLimiter)
            && descriptor.ImplementationType == typeof(RedisDistributedRateLimiter));
    }

    [Fact]
    public void ResourceOptions_Validate_RejectsNullCostConfiguration()
    {
        var options = new ResourcesOptions { CostPerUnit = null! };

        var act = options.Validate;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("CostPerUnit configuration cannot be empty");
    }

    [Fact]
    public void SealResourceUsageTypeRegistry_SealsAndReturnsServices()
    {
        ResetRegistry();
        try
        {
            var services = new ServiceCollection();

            services.SealResourceUsageTypeRegistry().Should().BeSameAs(services);
            services.SealResourceUsageTypeRegistry().Should().BeSameAs(services);

            var act = () => ResourceUsageTypeRegistry.Register(new ResourceUsageTypeInfo
            {
                Id = 1500,
                Key = "sealed_test",
                DisplayName = "Sealed Test"
            });

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ResetRegistry();
        }
    }

    private static async Task AssertSetAccessorAsync(Func<Task> action)
        => (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Set accessor exercised");

    private static void ResetRegistry()
    {
        typeof(ResourceUsageTypeRegistry)
            .GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, null);
    }
}

[Collection("ResourceUsageTypeRegistry")]
public sealed class ResourceRegistryCoverageCompletionTests : IDisposable
{
    public ResourceRegistryCoverageCompletionTests() => ResetRegistry();

    public void Dispose() => ResetRegistry();

    [Fact]
    public void Register_CoversValidationAndDuplicateBranches()
    {
        var nullAct = () => ResourceUsageTypeRegistry.Register(null!);
        nullAct.Should().Throw<ArgumentNullException>();

        var invalidIdAct = () => ResourceUsageTypeRegistry.Register(new ResourceUsageTypeInfo
        {
            Id = 999,
            Key = "bad_custom",
            DisplayName = "Bad Custom"
        });
        invalidIdAct.Should().Throw<ArgumentException>();

        var first = new ResourceUsageTypeInfo { Id = 1501, Key = "custom_one", DisplayName = "Custom One" };
        ResourceUsageTypeRegistry.Register(first);

        var duplicateIdAct = () => ResourceUsageTypeRegistry.Register(new ResourceUsageTypeInfo
        {
            Id = 1501,
            Key = "custom_two",
            DisplayName = "Custom Two"
        });
        duplicateIdAct.Should().Throw<InvalidOperationException>();

        var duplicateKeyAct = () => ResourceUsageTypeRegistry.Register(new ResourceUsageTypeInfo
        {
            Id = 1502,
            Key = "custom_one",
            DisplayName = "Custom One Again"
        });
        duplicateKeyAct.Should().Throw<InvalidOperationException>();

        ResourceUsageTypeRegistry.TryGetById(1502, out _).Should().BeFalse();
    }

    private static void ResetRegistry()
    {
        typeof(ResourceUsageTypeRegistry)
            .GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, null);
    }
}

public sealed class ResourceEntityAndServiceBranchCoverageTests
{
    [Fact]
    public void ResourceQuota_CoversResetSwitchFallbacks()
    {
        var unlimited = new ResourceQuota
        {
            LastReset = SystemClock.UtcNow,
            Period = ResourceQuotaPeriod.Unlimited
        };
        unlimited.GetNextResetTime().Should().BeNull();
        unlimited.ShouldReset().Should().BeFalse();

        var unknown = new ResourceQuota
        {
            LastReset = SystemClock.UtcNow,
            Period = (ResourceQuotaPeriod)999
        };
        unknown.GetNextResetTime().Should().NotBeNull();
    }

    [Fact]
    public void ResourceThrottlingPolicy_CoversUnknownStrategyFallback()
    {
        var policy = new ResourceThrottlingPolicy
        {
            IsActive = true,
            ThrottlingThresholdPercent = 50,
            Strategy = (ThrottlingStrategy)999
        };

        policy.CalculateDelayMs(90).Should().Be(0);
    }

    [Fact]
    public void SlaImpactAnalysisAndRetentionPolicy_CoverOpenEndedTimeBranches()
    {
        var analysis = new SlaImpactAnalysis
        {
            ViolationStartTime = SystemClock.UtcNow.AddHours(-2),
            ViolationEndTime = null
        };

        analysis.ExceedsDuration(30).Should().BeTrue();

        var policy = new UsageRetentionPolicy
        {
            LastExecutedAt = null,
            CompactionIntervalDays = 1
        };

        policy.CalculateNextCompaction().Should().BeAfter(SystemClock.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task UsageService_CoversValidationAndParsingFailures()
    {
        var service = new UsageService(
            Mock.Of<IUsageRecordRepository>(),
            Mock.Of<IResourceQuotaRepository>(),
            NullLogger<UsageService>.Instance);

        await service.Invoking(s => s.GetCurrentUsageAsync(Guid.Empty))
            .Should().ThrowAsync<ArgumentException>();

        await service.Invoking(s => s.TrackUsageAsync(Guid.NewGuid(), "missing-type", 1))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void QuotaServiceActivitySources_AreInitialized()
    {
        QuotaManagementService.ActivitySource.Name.Should().Be("GameGuild.Resources.QuotaManagement");
        QuotaEnforcementService.ActivitySource.Name.Should().Be("GameGuild.Resources.QuotaEnforcement");
        QuotaMaintenanceService.ActivitySource.Name.Should().Be("GameGuild.Resources.QuotaMaintenance");
    }

    [Fact]
    public void SlaImpactAnalysis_ExceedsDuration_UsesExplicitEndTime()
    {
        var analysis = new SlaImpactAnalysis
        {
            ViolationStartTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ViolationEndTime = new DateTime(2025, 1, 1, 0, 20, 0, DateTimeKind.Utc)
        };

        analysis.ExceedsDuration(10).Should().BeTrue();
    }

    [Fact]
    public async Task GetResourceUsageTrendsHandler_CoversDefaultGranularityBranch()
    {
        var handler = new GetResourceUsageTrendsHandler();
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await handler.Handle(
            new GetResourceUsageTrendsQuery(ResourceUsageType.ApiCalls, start, start.AddDays(2), (TrendGranularity)999),
            CancellationToken.None);

        result.DataPoints.Should().HaveCount(3);
    }

    [Fact]
    public async Task DistributedCacheRateLimiter_CoversAllowDenyAndResetPaths()
    {
        var cache = new DictionaryDistributedCache();
        var limiter = new DistributedCacheRateLimiter(cache, NullLogger<DistributedCacheRateLimiter>.Instance);
        var window = TimeSpan.FromMinutes(1);

        (await limiter.IsAllowedAsync("actor", 1, window)).Should().BeTrue();
        (await limiter.IsAllowedAsync("actor", 1, window)).Should().BeFalse();
        (await limiter.GetCurrentCountAsync("actor", window)).Should().Be(1);
        (await limiter.GetTimeUntilResetAsync("actor", window)).Should().NotBeNull();

        await limiter.ResetAsync("actor");
    }

    [Fact]
    public async Task RedisDistributedRateLimiter_CoversResetPath()
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        var limiter = new RedisDistributedRateLimiter(redis.Object, NullLogger<RedisDistributedRateLimiter>.Instance);

        await limiter.ResetAsync("actor");

        database.Verify(db => db.KeyDeleteAsync((RedisKey)"ratelimit:actor", It.IsAny<CommandFlags>()), Times.Once);
    }
}

public sealed class UsageTrendAnalysisBranchCompletionTests
{
    [Fact]
    public async Task ForecastUsageAsync_CoversSingleRecordAndFlatDateBranches()
    {
        var tenantId = Guid.NewGuid();
        var usageRepository = new Mock<IUsageRecordRepository>();
        var trendRepository = new Mock<IResourceUsageTrendRepository>();
        var service = new UsageTrendAnalysisService(
            trendRepository.Object,
            usageRepository.Object,
            NullLogger<UsageTrendAnalysisService>.Instance);

        var baseDate = SystemClock.UtcNow.AddDays(-1);
        usageRepository
            .SetupSequence(r => r.GetByTenantAsync(tenantId, ResourceUsageType.Storage, It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.Storage, UsageAmount = 25, PeriodStart = baseDate }
            })
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.Storage, UsageAmount = 10, PeriodStart = baseDate },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 30, PeriodStart = baseDate }
            });

        (await service.ForecastUsageAsync(tenantId, ResourceUsageType.Storage, baseDate.AddDays(1))).Should().Be(25);
        (await service.ForecastUsageAsync(tenantId, ResourceUsageType.Storage, baseDate.AddDays(1))).Should().Be(20);
    }

    [Fact]
    public async Task AnalyzeTrendAsync_CoversOneSidedGrowthAndPatternBranches()
    {
        var tenantId = Guid.NewGuid();
        var usageRepository = new Mock<IUsageRecordRepository>();
        var trendRepository = new Mock<IResourceUsageTrendRepository>();
        trendRepository
            .Setup(r => r.AddAsync(It.IsAny<ResourceUsageTrend>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceUsageTrend trend, CancellationToken _) => trend);

        var service = new UsageTrendAnalysisService(
            trendRepository.Object,
            usageRepository.Object,
            NullLogger<UsageTrendAnalysisService>.Instance);

        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(10);

        usageRepository
            .SetupSequence(r => r.GetByTenantAsync(tenantId, ResourceUsageType.ApiCalls, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 100, PeriodStart = start.AddDays(1) },
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 120, PeriodStart = start.AddDays(2) }
            })
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 100, PeriodStart = start.AddDays(1) },
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 105, PeriodStart = start.AddDays(2) },
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 135, PeriodStart = start.AddDays(6) },
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 140, PeriodStart = start.AddDays(7) }
            })
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 220, PeriodStart = start.AddDays(1) },
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 210, PeriodStart = start.AddDays(2) },
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 150, PeriodStart = start.AddDays(6) },
                new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 145, PeriodStart = start.AddDays(7) }
            });

        (await service.AnalyzeTrendAsync(tenantId, ResourceUsageType.ApiCalls, start, end)).GrowthRate.Should().Be(0);
        (await service.AnalyzeTrendAsync(tenantId, ResourceUsageType.ApiCalls, start, end)).Pattern.Should().Be("Rapid Growth");
        (await service.AnalyzeTrendAsync(tenantId, ResourceUsageType.ApiCalls, start, end)).Pattern.Should().Be("Rapid Decline");
    }

    [Fact]
    public async Task AnalyzeTrendAsync_CoversRemainingGrowthAndPatternBranches()
    {
        var tenantId = Guid.NewGuid();
        var usageRepository = new Mock<IUsageRecordRepository>();
        var trendRepository = new Mock<IResourceUsageTrendRepository>();
        trendRepository
            .Setup(r => r.AddAsync(It.IsAny<ResourceUsageTrend>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceUsageTrend trend, CancellationToken _) => trend);

        var service = new UsageTrendAnalysisService(
            trendRepository.Object,
            usageRepository.Object,
            NullLogger<UsageTrendAnalysisService>.Instance);

        var start = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(10);

        usageRepository
            .SetupSequence(r => r.GetByTenantAsync(tenantId, ResourceUsageType.Storage, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.Storage, UsageAmount = 42, PeriodStart = start.AddDays(1) }
            })
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.Storage, UsageAmount = 10, PeriodStart = start.AddDays(6) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 12, PeriodStart = start.AddDays(7) }
            })
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.Storage, UsageAmount = 0, PeriodStart = start.AddDays(1) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 0, PeriodStart = start.AddDays(2) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 10, PeriodStart = start.AddDays(6) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 10, PeriodStart = start.AddDays(7) }
            })
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.Storage, UsageAmount = 100, PeriodStart = start.AddDays(1) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 100, PeriodStart = start.AddDays(2) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 108, PeriodStart = start.AddDays(6) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 108, PeriodStart = start.AddDays(7) }
            })
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.Storage, UsageAmount = 100, PeriodStart = start.AddDays(1) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 100, PeriodStart = start.AddDays(2) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 92, PeriodStart = start.AddDays(6) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 92, PeriodStart = start.AddDays(7) }
            })
            .ReturnsAsync(new List<UsageRecord>
            {
                new() { Type = ResourceUsageType.Storage, UsageAmount = 0, PeriodStart = start.AddDays(1) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 0, PeriodStart = start.AddDays(2) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 0, PeriodStart = start.AddDays(6) },
                new() { Type = ResourceUsageType.Storage, UsageAmount = 0, PeriodStart = start.AddDays(7) }
            });

        (await service.AnalyzeTrendAsync(tenantId, ResourceUsageType.Storage, start, end)).GrowthRate.Should().Be(0);
        (await service.AnalyzeTrendAsync(tenantId, ResourceUsageType.Storage, start, end)).GrowthRate.Should().Be(0);
        (await service.AnalyzeTrendAsync(tenantId, ResourceUsageType.Storage, start, end)).GrowthRate.Should().Be(0);
        (await service.AnalyzeTrendAsync(tenantId, ResourceUsageType.Storage, start, end)).Pattern.Should().Be("Growing");
        (await service.AnalyzeTrendAsync(tenantId, ResourceUsageType.Storage, start, end)).Pattern.Should().Be("Declining");
        (await service.AnalyzeTrendAsync(tenantId, ResourceUsageType.Storage, start, end)).Pattern.Should().Be("Stable");
    }
}

public sealed class QuotaExceededAlertHandlerCoverageCompletionTests
{
    [Fact]
    public async Task Handle_RemovesExpiredViolationBeforeTrackingCurrentViolation()
    {
        var dictionary = (Dictionary<string, (int Count, DateTime FirstOccurrence)>)typeof(QuotaExceededAlertHandler)
            .GetField("RecentViolations", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        var gate = typeof(QuotaExceededAlertHandler)
            .GetField("ViolationsLock", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        lock (gate)
        {
            dictionary.Clear();
            dictionary["expired"] = (1, SystemClock.UtcNow.AddHours(-1));
        }

        var notification = new QuotaExceededEvent(
            Guid.NewGuid(),
            ResourceUsageType.ApiCalls,
            101,
            1,
            100,
            "test",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        var handler = new QuotaExceededAlertHandler(NullLogger<QuotaExceededAlertHandler>.Instance);

        await handler.Handle(notification, CancellationToken.None);

        lock (gate)
        {
            dictionary.Should().NotContainKey("expired");
        }
    }
}

internal sealed class DictionaryDistributedCache : IDistributedCache
{
    private readonly ConcurrentDictionary<string, byte[]> _entries = new();

    public byte[]? Get(string key) => _entries.GetValueOrDefault(key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        => _entries[key] = value;

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key) { }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => _entries.TryRemove(key, out _);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public string? GetString(string key)
    {
        var bytes = Get(key);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }
}

internal sealed class ThrowingApplicationDbContext : IApplicationDbContext
{
    public DbSet<T> Set<T>() where T : class
        => throw new InvalidOperationException("Set accessor exercised");

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

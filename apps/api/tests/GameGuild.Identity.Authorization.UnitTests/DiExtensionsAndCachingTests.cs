using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;

namespace GameGuild.Identity.Authorization.UnitTests;

public class DiExtensionsAndCachingTests
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    // ═══════════════════════════════════════════════════════════════════
    // AuthorizationModuleExtensions — individual DI methods
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddAuthorizationOptions_RegistersConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationOptions(EmptyConfig());
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAuthorizationApplication_EnableCachingTrue_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationApplication(enableCaching: true);
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAuthorizationApplication_EnableCachingFalse_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationApplication(enableCaching: false);
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAuthorizationRepositories_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationRepositories();
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAuthorizationPresentation_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationPresentation();
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddRuleBasedAuthorization_Registers()
    {
        var services = new ServiceCollection();
        services.AddRuleBasedAuthorization();
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddPermissionServices_Registers()
    {
        var services = new ServiceCollection();
        services.AddPermissionServices();
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAdvancedPermissionServices_Registers()
    {
        var services = new ServiceCollection();
        services.AddAdvancedPermissionServices();
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddUnifiedAuthorizationLayer_Registers()
    {
        var services = new ServiceCollection();
        services.AddUnifiedAuthorizationLayer();
        services.Should().NotBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // CachingServiceExtensions
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddAuthorizationCaching_Default_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationCaching();
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAuthorizationCaching_WithOptionsDelegate_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationCaching(opts =>
        {
            opts.PolicyTtlSeconds = 600;
            opts.EnableMetrics = false;
        });
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAuthorizationRedisCache_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationRedisCache("localhost:6379", "test:");
        services.Should().NotBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AuthorizationModule.ConfigureServices — covers all extensions at once
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AuthorizationModule_ConfigureServices_RegistersAllServices()
    {
        var module = new AuthorizationModule();
        var services = new ServiceCollection();
        module.ConfigureServices(services, EmptyConfig());
        services.Should().NotBeEmpty();
        services.Count.Should().BeGreaterThan(10);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Cached service constructors
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CacheInvalidationService_CanBeConstructed()
    {
        var svc = new CacheInvalidationService(
            Mock.Of<IMemoryCache>(),
            Mock.Of<ITenantSecurityVersionStore>(),
            Mock.Of<IHybridPermissionCache>(),
            Mock.Of<ICacheMetricsService>(),
            Options.Create(new AuthorizationCacheOptions()),
            NullLogger<CacheInvalidationService>.Instance);

        svc.Should().NotBeNull();
    }

    [Fact]
    public void CachedAccessControlListService_MinimalDeps_CanBeConstructed()
    {
        var svc = new CachedAccessControlListService(
            Mock.Of<IAccessControlListService>(),
            Mock.Of<IMemoryCache>(),
            Mock.Of<ITenantSecurityVersionStore>(),
            Mock.Of<IUserSecurityVersionStore>(),
            Options.Create(new AuthorizationCacheOptions()));

        svc.Should().NotBeNull();
    }

    [Fact]
    public void CachedAccessControlListService_AllDeps_CanBeConstructed()
    {
        var svc = new CachedAccessControlListService(
            Mock.Of<IAccessControlListService>(),
            Mock.Of<IMemoryCache>(),
            Mock.Of<ITenantSecurityVersionStore>(),
            Mock.Of<IUserSecurityVersionStore>(),
            Options.Create(new AuthorizationCacheOptions()),
            Mock.Of<IHybridPermissionCache>(),
            Mock.Of<ICacheMetricsService>());

        svc.Should().NotBeNull();
    }

    [Fact]
    public void CachedPolicyDefinitionStore_MinimalDeps_CanBeConstructed()
    {
        var svc = new CachedPolicyDefinitionStore(
            Mock.Of<IPolicyDefinitionStore>(),
            Mock.Of<IMemoryCache>(),
            Mock.Of<ITenantSecurityVersionStore>(),
            Options.Create(new AuthorizationCacheOptions()));

        svc.Should().NotBeNull();
    }

    [Fact]
    public void CachedPolicyDefinitionStore_AllDeps_CanBeConstructed()
    {
        var svc = new CachedPolicyDefinitionStore(
            Mock.Of<IPolicyDefinitionStore>(),
            Mock.Of<IMemoryCache>(),
            Mock.Of<ITenantSecurityVersionStore>(),
            Options.Create(new AuthorizationCacheOptions()),
            Mock.Of<IHybridPermissionCache>(),
            Mock.Of<ICacheMetricsService>());

        svc.Should().NotBeNull();
    }

    [Fact]
    public void ResourceAccessHandler_CanBeConstructed()
    {
        var handler = new ResourceAccessHandler(
            Mock.Of<IAuthorizationTenantContext>(),
            Mock.Of<IAccessControlListService>(),
            Options.Create(new AuthorizationTokenOptions()),
            NullLogger<ResourceAccessHandler>.Instance);

        handler.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AuthorizationCacheOptions — cover property defaults + Validate
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AuthorizationCacheOptions_CreateDefault_HasExpectedDefaults()
    {
        var opts = AuthorizationCacheOptions.CreateDefault();
        opts.Should().NotBeNull();
        opts.PolicyTtlSeconds.Should().Be(300);
        opts.PermissionTtlSeconds.Should().Be(300);
        opts.AccessControlListTtlSeconds.Should().Be(60);
        opts.RulesetTtlSeconds.Should().Be(300);
        opts.MaxPolicyCacheSize.Should().Be(1000);
        opts.MaxL1CacheSize.Should().Be(5000);
        opts.UseDistributedCache.Should().BeFalse();
        opts.RedisInstanceName.Should().Be("gg:auth:");
        opts.DistributedCacheTtlSeconds.Should().Be(600);
        opts.EnableMetrics.Should().BeTrue();
        opts.MetricsLoggingIntervalSeconds.Should().Be(60);
        opts.MinimumHitRateWarningThreshold.Should().Be(0.70);
        opts.MinimumRequestsForPerformanceWarning.Should().Be(100);
        opts.LookupLatencyWarningThresholdMilliseconds.Should().Be(100);
        opts.UsePubSubInvalidation.Should().BeTrue();
        opts.InvalidationChannelName.Should().Be("gg:auth:invalidate");
    }

    [Fact]
    public void AuthorizationCacheOptions_Validate_DoesNotThrow()
    {
        var opts = new AuthorizationCacheOptions();
        var act = () => opts.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void AuthorizationCacheOptions_Validate_RequiresDistributedTtlToCoverEveryL1CacheType()
    {
        var optionsWithLongerPermissionTtl = new AuthorizationCacheOptions
        {
            PermissionTtlSeconds = 601,
            DistributedCacheTtlSeconds = 600
        };
        var optionsWithLongerAclTtl = new AuthorizationCacheOptions
        {
            AccessControlListTtlSeconds = 601,
            DistributedCacheTtlSeconds = 600
        };
        var optionsWithLongerRulesetTtl = new AuthorizationCacheOptions
        {
            RulesetTtlSeconds = 601,
            DistributedCacheTtlSeconds = 600
        };

        optionsWithLongerPermissionTtl.Invoking(options => options.Validate())
            .Should().Throw<InvalidOperationException>();
        optionsWithLongerAclTtl.Invoking(options => options.Validate())
            .Should().Throw<InvalidOperationException>();
        optionsWithLongerRulesetTtl.Invoking(options => options.Validate())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AuthorizationCacheOptions_SetProperties_RoundTrips()
    {
        var opts = new AuthorizationCacheOptions
        {
            PolicyTtlSeconds = 100,
            PermissionTtlSeconds = 200,
            AccessControlListTtlSeconds = 30,
            RulesetTtlSeconds = 150,
            MaxPolicyCacheSize = 500,
            MaxL1CacheSize = 2000,
            UseDistributedCache = true,
            RedisConnectionString = "localhost:6379",
            RedisInstanceName = "test:",
            DistributedCacheTtlSeconds = 300,
            EnableMetrics = false,
            MetricsLoggingIntervalSeconds = 120,
            MinimumHitRateWarningThreshold = 0.8,
            MinimumRequestsForPerformanceWarning = 50,
            LookupLatencyWarningThresholdMilliseconds = 80,
            UsePubSubInvalidation = false,
            InvalidationChannelName = "test:invalidate"
        };

        opts.PolicyTtlSeconds.Should().Be(100);
        opts.UseDistributedCache.Should().BeTrue();
        opts.RedisConnectionString.Should().Be("localhost:6379");
        opts.EnableMetrics.Should().BeFalse();
        opts.MinimumHitRateWarningThreshold.Should().Be(0.8);
        opts.MinimumRequestsForPerformanceWarning.Should().Be(50);
        opts.LookupLatencyWarningThresholdMilliseconds.Should().Be(80);
    }

    [Fact]
    public void AuthorizationTokenOptions_CanBeInstantiated()
    {
        var opts = new AuthorizationTokenOptions();
        opts.Should().NotBeNull();
    }
}

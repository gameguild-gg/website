using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

/// <summary>
///     Issue #416: "Plan list queries cached for 5 minutes" + "Cache invalidation on plan
///     updates". The platform registers IMemoryCache with a SizeLimit, so entries must be
///     sized (the #779 lesson) — these tests run against a size-limited cache to prove it.
/// </summary>
public class SubscriptionPlanListCacheTests
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions { SizeLimit = 100 });
    private readonly SubscriptionPlanListCache _cache;

    public SubscriptionPlanListCacheTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _cache = new SubscriptionPlanListCache(
            _memoryCache,
            services.BuildServiceProvider().GetRequiredService<ILogger<SubscriptionPlanListCache>>());
    }

    [Fact]
    public async Task GetOrCreateActivePlansAsync_CachesTheFactoryResult()
    {
        var plans = new[] { new SubscriptionPlan("Pro", "pro", 999) };
        var factoryCalls = 0;

        Task<IEnumerable<SubscriptionPlan>> Factory(CancellationToken _)
        {
            factoryCalls++;
            return Task.FromResult<IEnumerable<SubscriptionPlan>>(plans);
        }

        var first = await _cache.GetOrCreateActivePlansAsync(Factory);
        var second = await _cache.GetOrCreateActivePlansAsync(Factory);

        first.Should().BeSameAs(second);
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateFeaturedPlansAsync_CachesIndependentlyOfActivePlans()
    {
        var activeCalls = 0;
        var featuredCalls = 0;

        await _cache.GetOrCreateActivePlansAsync(_ => { activeCalls++; return Task.FromResult<IEnumerable<SubscriptionPlan>>([]); });
        await _cache.GetOrCreateFeaturedPlansAsync(_ => { featuredCalls++; return Task.FromResult<IEnumerable<SubscriptionPlan>>([]); });
        await _cache.GetOrCreateActivePlansAsync(_ => { activeCalls++; return Task.FromResult<IEnumerable<SubscriptionPlan>>([]); });
        await _cache.GetOrCreateFeaturedPlansAsync(_ => { featuredCalls++; return Task.FromResult<IEnumerable<SubscriptionPlan>>([]); });

        activeCalls.Should().Be(1);
        featuredCalls.Should().Be(1);
    }

    [Fact]
    public async Task Invalidate_ForcesTheNextReadToRefetch()
    {
        var factoryCalls = 0;

        Task<IEnumerable<SubscriptionPlan>> Factory(CancellationToken _)
        {
            factoryCalls++;
            return Task.FromResult<IEnumerable<SubscriptionPlan>>([]);
        }

        await _cache.GetOrCreateActivePlansAsync(Factory);
        await _cache.GetOrCreateFeaturedPlansAsync(Factory);

        _cache.Invalidate();

        await _cache.GetOrCreateActivePlansAsync(Factory);
        await _cache.GetOrCreateFeaturedPlansAsync(Factory);

        factoryCalls.Should().Be(4);
    }

    [Fact]
    public async Task Entries_AreSized_AndWorkUnderASizeLimitedCache()
    {
        // A size-limited MemoryCache throws on unsized Set calls; a successful population
        // proves every entry carries a size (the #779 lesson).
        await _cache.GetOrCreateActivePlansAsync(_ => Task.FromResult<IEnumerable<SubscriptionPlan>>(
            [new SubscriptionPlan("Pro", "pro", 999), new SubscriptionPlan("Max", "max", 1999)]));
        await _cache.GetOrCreateFeaturedPlansAsync(_ => Task.FromResult<IEnumerable<SubscriptionPlan>>([]));

        _memoryCache.TryGetValue("SubscriptionPlans:Query:Active", out _).Should().BeTrue();
        _memoryCache.TryGetValue("SubscriptionPlans:Query:Featured", out _).Should().BeTrue();
    }
}

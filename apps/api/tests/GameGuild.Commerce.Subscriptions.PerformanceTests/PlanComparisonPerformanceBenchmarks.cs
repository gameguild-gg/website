using BenchmarkDotNet.Attributes;

namespace GameGuild.Commerce.Subscriptions.PerformanceTests;

/// <summary>
///     Issue #416: "Plan comparison query completes in under 200ms". Benchmarks the
///     feature-matrix construction in CompareSubscriptionPlansQueryHandler over a
///     representative plan set (repository lookups are primary-key indexed).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class PlanComparisonPerformanceBenchmarks
{
    private CompareSubscriptionPlansQueryHandler _handler = null!;
    private InMemoryPlanRepository _repository = null!;
    private CompareSubscriptionPlansQuery _query = default!;

    [Params(2, 5, 20)]
    public int PlanCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var plans = Enumerable.Range(0, PlanCount)
            .Select(index => new SubscriptionPlan($"Plan {index}", $"plan-{index}", index * 1000))
            .ToArray();

        for (var index = 0; index < plans.Length; index++)
        {
            plans[index].UpdateFeatures(features: """{"basicFeatures":true,"apiAccess":true,"maxProjects":100}""");
            plans[index].UpdateLimits(maxUsers: 10 * (index + 1), maxStorageMb: 1024 * (index + 1), maxApiCallsPerMonth: 1000 * (index + 1));
        }

        _repository = new InMemoryPlanRepository(plans);
        _handler = new CompareSubscriptionPlansQueryHandler(_repository);
        _query = new CompareSubscriptionPlansQuery(plans[0].Id, plans.Skip(1).Select(plan => plan.Id).ToList());
    }

    [Benchmark]
    public object ComparePlans() => _handler.Handle(_query, CancellationToken.None).GetAwaiter().GetResult();

    private sealed class InMemoryPlanRepository(SubscriptionPlan[] plans) : ISubscriptionPlanRepository
    {
        // Issue #392 interval price points (not exercised by these benchmarks).
        public Task<SubscriptionPlanIntervalPrice?> GetIntervalPriceAsync(Guid planId, BillingCycle interval, CancellationToken cancellationToken = default)
            => Task.FromResult<SubscriptionPlanIntervalPrice?>(null);

        public Task<IReadOnlyList<SubscriptionPlanIntervalPrice>> GetIntervalPricesAsync(Guid planId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubscriptionPlanIntervalPrice>>([]);

        public Task<SubscriptionPlanIntervalPrice> UpsertIntervalPriceAsync(SubscriptionPlanIntervalPrice intervalPrice, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Interval price points are not part of these benchmarks.");

        public Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(plans.FirstOrDefault(plan => plan.Id == id));

        public Task<SubscriptionPlan?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult(plans.FirstOrDefault(plan => plan.Slug == slug));

        public Task<SubscriptionPlan?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(plans.FirstOrDefault(plan => plan.ExternalId == externalId));

        public Task<IEnumerable<SubscriptionPlan>> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<SubscriptionPlan>>(plans.Where(plan => plan.IsActive));

        public Task<IEnumerable<SubscriptionPlan>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<SubscriptionPlan>>(plans);

        public Task<IEnumerable<SubscriptionPlan>> GetFeaturedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<SubscriptionPlan>>(plans.Where(plan => plan.IsFeatured));

        public Task<IEnumerable<SubscriptionPlan>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<SubscriptionPlan>>(plans.Where(plan => plan.Name.Contains(searchTerm)));

        public Task<IEnumerable<SubscriptionPlan>> GetByPriceRangeAsync(long minPriceInCents, long maxPriceInCents, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<SubscriptionPlan>>(plans.Where(plan =>
                plan.MonthlyPriceInCents >= minPriceInCents && plan.MonthlyPriceInCents <= maxPriceInCents));

        public Task<SubscriptionPlan> AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Benchmark repository is read-only.");

        public Task<SubscriptionPlan> UpdateAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Benchmark repository is read-only.");

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Benchmark repository is read-only.");

        public Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(!plans.Any(plan => plan.Name == name && plan.Id != excludeId));

        public Task<bool> IsSlugUniqueAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(!plans.Any(plan => plan.Slug == slug && plan.Id != excludeId));

        public Task<int> GetActiveSubscriptionCountAsync(Guid planId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<PagedResult<SubscriptionPlan>> GetPagedAsync(int skip, int pageSize, string? searchTerm = null, bool includeDeleted = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(PagedResult<SubscriptionPlan>.FromPage(plans, plans.Length, skip / pageSize + 1, pageSize));
    }
}

using FluentAssertions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Queries;

/// <summary>
///     Issue #416: the compare query previously had no handler at all —
///     POST v1/subscription-plans:compare failed at runtime. These tests pin the
///     feature-matrix contract.
/// </summary>
public class CompareSubscriptionPlansQueryHandlerTests
{
    [Fact]
    public async Task Handle_BuildsFeatureMatrixOverFeaturesAndLimits()
    {
        var free = new SubscriptionPlan("Free", "free", 0) { MaxUsers = 5 };
        free.UpdateFeatures(features: """{"basicFeatures":true,"maxProjects":10}""");
        var pro = new SubscriptionPlan("Professional", "professional", 4999) { MaxUsers = 25, MaxStorageMb = 512000 };
        pro.UpdateFeatures(features: """{"basicFeatures":true,"customDomain":true,"maxProjects":100}""");

        var repository = new Mock<ISubscriptionPlanRepository>();
        repository.Setup(r => r.GetByIdAsync(free.Id, It.IsAny<CancellationToken>())).ReturnsAsync(free);
        repository.Setup(r => r.GetByIdAsync(pro.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pro);

        var handler = new CompareSubscriptionPlansQueryHandler(repository.Object);
        var result = await handler.Handle(
            new CompareSubscriptionPlansQuery(free.Id, [pro.Id]),
            CancellationToken.None);

        var comparison = result.Should().BeOfType<SubscriptionPlanComparisonResponse>().Subject;

        comparison.BasePlan.Id.Should().Be(free.Id);
        comparison.Plans.Should().HaveCount(2);
        comparison.Plans[0].Id.Should().Be(free.Id);
        comparison.Plans[1].Id.Should().Be(pro.Id);

        var matrix = comparison.FeatureMatrix.ToDictionary(row => row.Feature);
        matrix.Keys.Should().Contain(new[] { "basicFeatures", "customDomain", "maxProjects", "maxUsers", "maxStorageMb", "maxApiCallsPerMonth" });

        matrix["basicFeatures"].Values["free"].Should().Be(true);
        matrix["basicFeatures"].Values["professional"].Should().Be(true);
        matrix["customDomain"].Values["free"].Should().BeNull();
        matrix["customDomain"].Values["professional"].Should().Be(true);
        matrix["maxProjects"].Values["free"].Should().Be(10L);
        matrix["maxProjects"].Values["professional"].Should().Be(100L);
        matrix["maxUsers"].Values["free"].Should().Be(5L);
        matrix["maxUsers"].Values["professional"].Should().Be(25L);
        matrix["maxStorageMb"].Values["free"].Should().BeNull();
        matrix["maxStorageMb"].Values["professional"].Should().Be(512000L);
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenBasePlanIsMissing()
    {
        var repository = new Mock<ISubscriptionPlanRepository>();
        var missingId = Guid.NewGuid();
        repository.Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>())).ReturnsAsync((SubscriptionPlan?)null);

        var handler = new CompareSubscriptionPlansQueryHandler(repository.Object);

        var act = () => handler.Handle(new CompareSubscriptionPlansQuery(missingId, []), CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_SkipsUnknownComparisonPlans_AndIgnoresDuplicates()
    {
        var basePlan = new SubscriptionPlan("Pro", "pro", 999);
        var repository = new Mock<ISubscriptionPlanRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => id == basePlan.Id ? basePlan : null);

        var handler = new CompareSubscriptionPlansQueryHandler(repository.Object);
        var result = await handler.Handle(
            new CompareSubscriptionPlansQuery(basePlan.Id, [basePlan.Id, basePlan.Id, Guid.NewGuid()]),
            CancellationToken.None);

        var comparison = result.Should().BeOfType<SubscriptionPlanComparisonResponse>().Subject;
        comparison.Plans.Should().ContainSingle();
        comparison.Plans[0].Id.Should().Be(basePlan.Id);
    }

    [Fact]
    public async Task Handle_CompletesWellUnder200Ms_ForTwentyPlans()
    {
        // Issue #416: "Plan comparison query completes in <200ms" — warmup first (JIT),
        // then measure a single comparison over a representative 20-plan set. The
        // repository lookups are primary-key indexed; the matrix is in-memory.
        var plans = Enumerable.Range(0, 20).Select(index =>
        {
            var plan = new SubscriptionPlan($"Plan {index}", $"plan-{index}", index * 1000) { MaxUsers = 10 * (index + 1) };
            plan.UpdateFeatures(features: """{"basicFeatures":true,"apiAccess":true,"maxProjects":100}""");
            return plan;
        }).ToArray();

        var repository = new Mock<ISubscriptionPlanRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => plans.FirstOrDefault(plan => plan.Id == id));

        var handler = new CompareSubscriptionPlansQueryHandler(repository.Object);
        var query = new CompareSubscriptionPlansQuery(plans[0].Id, plans.Skip(1).Select(plan => plan.Id).ToList());

        // Warmup.
        await handler.Handle(query, CancellationToken.None);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await handler.Handle(query, CancellationToken.None);
        stopwatch.Stop();

        result.Should().BeOfType<SubscriptionPlanComparisonResponse>()
            .Which.Plans.Should().HaveCount(20);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200);
    }

    [Fact]
    public async Task Handle_IsLenientWithLegacyFeaturePayloads()
    {
        var legacy = new SubscriptionPlan("Legacy", "legacy", 999);
        legacy.UpdateFeatures(features: "not-json-at-all"); // pre-schema-validation row

        var repository = new Mock<ISubscriptionPlanRepository>();
        repository.Setup(r => r.GetByIdAsync(legacy.Id, It.IsAny<CancellationToken>())).ReturnsAsync(legacy);

        var handler = new CompareSubscriptionPlansQueryHandler(repository.Object);
        var result = await handler.Handle(
            new CompareSubscriptionPlansQuery(legacy.Id, []),
            CancellationToken.None);

        var comparison = result.Should().BeOfType<SubscriptionPlanComparisonResponse>().Subject;
        comparison.Plans.Should().ContainSingle();
        comparison.FeatureMatrix.Should().OnlyContain(row =>
            row.Feature == "maxUsers" || row.Feature == "maxStorageMb" || row.Feature == "maxApiCallsPerMonth");
    }
}

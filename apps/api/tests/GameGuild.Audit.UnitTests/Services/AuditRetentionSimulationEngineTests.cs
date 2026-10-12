using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditRetentionSimulationEngineTests
{
    private const decimal GiB = 1073741824m;
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
    private readonly AuditRetentionSimulationEngine _engine = new();

    internal static AuditRetentionScenario Scenario(string name = "baseline", int retention = 365, int hot = 365) =>
        new() { Name = name, RetentionDays = retention, HotDays = hot, WarmUntilDays = hot, ColdUntilDays = hot };
    internal static ConfigureAuditRetentionRequest Configuration() => new()
    {
        Currency = "USD", MonthlyBudget = 100, Baseline = Scenario(),
        TierPrices = [
            new() { Tier = AuditStorageTier.Hot, MonthlyCostPerGiB = 10, ExpectedReadLatencyMilliseconds = 1 },
            new() { Tier = AuditStorageTier.Warm, MonthlyCostPerGiB = 5, ExpectedReadLatencyMilliseconds = 10 },
            new() { Tier = AuditStorageTier.Cold, MonthlyCostPerGiB = 2, ExpectedReadLatencyMilliseconds = 100 },
            new() { Tier = AuditStorageTier.Archive, MonthlyCostPerGiB = 1, ExpectedReadLatencyMilliseconds = 10000 }],
        Obligations = [new() { Name = "Configured contract", Source = "Tenant policy revision 1", MinimumRetentionDays = 30 }]
    };
    internal static RunAuditRetentionSimulationRequest Request(int months = 1) => new()
        { ForecastMonths = months, HistoricalDays = 14, GrowthModel = AuditRetentionGrowthModel.Constant, Scenarios = [Scenario("alternative", hot: 0)] };
    private static AuditRetentionDataSnapshot Snapshot(bool history = false, int accessAge = 0) => new(
        history ? Enumerable.Range(1, 14).Select(age => new AuditStorageDailyCohort(DateOnly.FromDateTime(Now).AddDays(-age), 1, GiB)).ToArray()
            : [new(DateOnly.FromDateTime(Now), 1, GiB)],
        [new(accessAge, 10)], DateOnly.FromDateTime(Now).AddDays(-13), "Test measured rows");

    [Fact]
    public void ChargesMeasuredOccupancyInFourTiersAndComparesBudget()
    {
        var report = _engine.Simulate(Configuration(), Request(), Snapshot(), Now);
        Assert.Equal(10, report.Baseline.TotalCost);
        Assert.Equal(1, report.Scenarios[0].TotalCost);
        Assert.Equal(-9, report.Scenarios[0].DifferenceFromBaseline);
        Assert.Equal(90, report.Scenarios[0].SavingsPercent);
        Assert.Equal(-90, report.Baseline.BudgetVariance);
        Assert.Equal(4, report.Baseline.Months.Single().Tiers.Count);
        Assert.Equal(GiB, report.Baseline.Months.Single().EndBillableBytes);
        Assert.Equal(new DateOnly(2026, 10, 4), report.Baseline.Months.Single().StartUtcDate);
    }

    [Fact]
    public void AppliesOverheadAndKeepsCurrencyWithoutConversion()
    {
        var report = _engine.Simulate(Configuration() with { StorageOverheadMultiplier = 2, Currency = "BRL" }, Request(), Snapshot(), Now);
        Assert.Equal(20, report.Baseline.TotalCost);
        Assert.Equal("BRL", report.Currency);
        Assert.Equal(GiB * 2, report.Baseline.Months.Single().EndBillableBytes);
    }

    [Fact]
    public void ClosingOccupancyIncludesFutureDailyCohortsAtHorizonEnd()
    {
        var report = _engine.Simulate(Configuration(), Request(), Snapshot(true), Now);
        Assert.Equal(45 * GiB, report.Baseline.Months.Single().EndBillableBytes);
        Assert.Equal(45, report.Baseline.Months.Single().EndRecordCount);
    }

    [Theory]
    [InlineData(1)] [InlineData(13)] [InlineData(24)] [InlineData(120)]
    public void ForecastsCompleteRollingMonthsAndAggregatesActualYearCosts(int months)
    {
        var report = _engine.Simulate(Configuration(), Request(months), Snapshot(true), Now);
        Assert.Equal(months, report.Baseline.Months.Count);
        Assert.Equal(months, report.Baseline.Years.Sum(year => year.Months));
        Assert.Equal(report.Baseline.Months.Sum(month => month.TotalCost), report.Baseline.TotalCost);
        Assert.Equal(report.Baseline.TotalCost, report.Baseline.Years.Sum(year => year.TotalCost));
        Assert.Equal(report.Baseline.TotalCost, report.Baseline.TotalStorageCost + report.Baseline.TotalRetrievalCost);
        Assert.Equal(report.Baseline.TotalCost - 100 * months, report.Baseline.BudgetVariance);
    }

    [Fact]
    public void MeasuresTrendsSeasonalityAndConfiguredGrowth()
    {
        var cohorts = Enumerable.Range(1, 28).Select(index => new AuditStorageDailyCohort(
            DateOnly.FromDateTime(Now).AddDays(index - 29), index, index * GiB)).ToArray();
        var snapshot = Snapshot() with { Cohorts = cohorts };
        var trend = _engine.Simulate(Configuration(), Request() with { HistoricalDays = 28, GrowthModel = AuditRetentionGrowthModel.HistoricalTrend }, snapshot, Now);
        var constant = _engine.Simulate(Configuration(), Request() with { HistoricalDays = 28 }, snapshot, Now);
        var compound = _engine.Simulate(Configuration(), Request() with { HistoricalDays = 28,
            GrowthModel = AuditRetentionGrowthModel.CompoundAnnual, AnnualGrowthPercent = 100 }, snapshot, Now);
        Assert.Equal(GiB, trend.Evidence.DailyLogicalBytesTrend);
        Assert.Equal(7, trend.Evidence.WeeklySeasonality.Count);
        Assert.True(trend.Baseline.TotalCost > constant.Baseline.TotalCost);
        Assert.True(compound.Baseline.TotalCost > constant.Baseline.TotalCost);
        Assert.Equal(14.5m * GiB, trend.Evidence.AverageDailyLogicalBytes);
    }

    [Fact]
    public void EvaluatesConfiguredMinimaMaximaAndHoldConflicts()
    {
        var config = Configuration() with { Obligations = [new() { Name = "Test obligation", Source = "Configured source",
            MinimumRetentionDays = 30, MaximumRetentionDays = 365 }], PreserveAllRecordsThroughUtcDate = new(2028, 1, 1) };
        var report = _engine.Simulate(config, Request() with { Scenarios = [Scenario("too-short", 10, 10), Scenario("too-long", 400, 400)] }, Snapshot(true), Now);
        Assert.All(report.Scenarios, result => Assert.Equal("ViolatesConfiguredObligations", result.ComplianceStatus));
        Assert.Contains(report.Baseline.ComplianceViolations, violation => violation.Reason.Contains("hold"));
        Assert.All(report.Baseline.ComplianceViolations, violation => Assert.Equal("Configured source", violation.Source));
        Assert.Null(report.Recommendation.SuggestedScenario);
    }

    [Fact]
    public void DetectsReadAvailabilityAndLatencyRisks()
    {
        var report = _engine.Simulate(Configuration(), Request() with { Scenarios = [Scenario("expires-access", 1, 1), Scenario("slow", hot: 0)] }, Snapshot(true, 5), Now);
        Assert.Equal(100, report.Scenarios[0].UnavailableObservedReadsPercent);
        Assert.Equal(100, report.Scenarios[1].SlowObservedReadsPercent);
        Assert.Equal(10000, report.Scenarios[1].P95ReadLatencyMilliseconds);
        Assert.Contains(report.Scenarios[0].Risks, risk => risk.Code == "OBSERVED_DATA_UNAVAILABLE");
        Assert.Contains(report.Scenarios[1].Risks, risk => risk.Code == "OBSERVED_LATENCY_EXCEEDED");
        Assert.NotNull(report.Recommendation.SuggestedScenario);
        Assert.Equal(0, report.Recommendation.SuggestedScenario!.SlowObservedReadsPercent);
    }

    [Fact]
    public void DoesNotRecommendAnUnmeasuredOrUnconfiguredReduction()
    {
        var snapshot = Snapshot(true) with { AccessAges = [], FirstAccessObservationDateUtc = null };
        var report = _engine.Simulate(Configuration() with { Obligations = [], MonthlyBudget = null }, Request(), snapshot, Now);
        Assert.Equal("NotAssessed", report.Baseline.ComplianceStatus);
        Assert.Null(report.Baseline.UnavailableObservedReadsPercent);
        Assert.Null(report.Baseline.BudgetVariance);
        Assert.Null(report.Recommendation.SuggestedScenario);
        Assert.Contains(report.Baseline.Risks, risk => risk.Code == "ACCESS_TELEMETRY_UNAVAILABLE");
        Assert.Contains(report.Baseline.Risks, risk => risk.Code == "OBLIGATIONS_UNSPECIFIED");
    }

    [Fact]
    public void HoldsExistingCohortsAndShowsInitialExpirationWithoutMutatingEvidence()
    {
        var snapshot = Snapshot() with { Cohorts = [new(new DateOnly(2025, 1, 1), 7, GiB)] };
        var plain = _engine.Simulate(Configuration(), Request(), snapshot, Now);
        var held = _engine.Simulate(Configuration() with { PreserveAllRecordsThroughUtcDate = new(2026, 12, 1) }, Request(), snapshot, Now);
        Assert.Equal(7, plain.Baseline.InitialExpiredRecords);
        Assert.Equal(0, plain.Baseline.TotalCost);
        Assert.Equal(0, held.Baseline.InitialExpiredRecords);
        Assert.Equal(1, held.Baseline.TotalCost); // Old held cohort remains in archive.
        Assert.Equal(GiB, snapshot.Cohorts.Single().LogicalBytes);
    }

    [Fact]
    public void EstimatesRetrievalFromActualReadAgeCounts()
    {
        var config = Configuration() with { TierPrices = Configuration().TierPrices.Select(price => price with { RetrievalCostPerGiB = 1 }).ToList() };
        var report = _engine.Simulate(config, Request(), Snapshot(true), Now);
        Assert.True(report.Baseline.TotalRetrievalCost > 0);
        Assert.Equal(10, report.Evidence.ObservedReadCount);
        Assert.Equal(14, report.Evidence.ObservedAccessDays);
    }

    [Fact]
    public void BudgetOverrunIsExplicitAndHoldsDoNotExtendFutureCohorts()
    {
        var config = Configuration() with { MonthlyBudget = 0, Baseline = Scenario(retention: 10, hot: 10),
            PreserveAllRecordsThroughUtcDate = new(2027, 1, 1) };
        var report = _engine.Simulate(config, Request(), Snapshot(true), Now);
        Assert.Contains(report.Baseline.Risks, risk => risk.Code == "PROJECTED_BUDGET_OVERRUN");
        Assert.Equal(25, report.Baseline.Months.Single().EndRecordCount); // 14 held existing + 11 unexpired future daily cohorts.
        Assert.Equal(report.Baseline.TotalCost, report.Baseline.BudgetVariance);
    }

    [Fact]
    public void RejectsInvalidInputsEvidenceAndCancellation()
    {
        Assert.Throws<AuditRetentionValidationException>(() => AuditRetentionInputValidation.Validate(Configuration() with { TierPrices = [] }));
        Assert.Throws<AuditRetentionValidationException>(() => AuditRetentionInputValidation.Validate(Configuration() with { Baseline = Scenario(hot: 400) }));
        Assert.Throws<AuditRetentionValidationException>(() => AuditRetentionInputValidation.Validate(Request() with { ForecastMonths = 121 }));
        Assert.Throws<AuditRetentionValidationException>(() => AuditRetentionInputValidation.Validate(Request() with { GrowthModel = AuditRetentionGrowthModel.CompoundAnnual }));
        Assert.Throws<AuditRetentionValidationException>(() => AuditRetentionInputValidation.Validate(Request() with { Scenarios = [Scenario("a"), Scenario("A")] }));
        Assert.Throws<AuditRetentionValidationException>(() => AuditRetentionInputValidation.Validate(Configuration() with { PreserveAllRecordsThroughUtcDate = DateOnly.MaxValue }));
        Assert.Throws<ArgumentException>(() => _engine.Simulate(Configuration(), Request(), Snapshot() with { FirstAccessObservationDateUtc = new(2027, 1, 1) }, Now));
        Assert.Throws<ArgumentException>(() => _engine.Simulate(Configuration(), Request(), Snapshot() with { Cohorts = [new(new(2026, 10, 3), 1, decimal.MaxValue)] }, Now));
        Assert.Throws<OperationCanceledException>(() => _engine.Simulate(Configuration(), Request(), Snapshot(), Now, new CancellationToken(true)));
    }

    [Fact]
    public void DeterministicSimulationHasNoNegativeOccupancyAtTierTransitions()
    {
        var request = Request(24) with { Scenarios = [new() { Name = "tiered", RetentionDays = 90, HotDays = 7, WarmUntilDays = 30, ColdUntilDays = 60 }] };
        var first = _engine.Simulate(Configuration(), request, Snapshot(true), Now);
        var second = _engine.Simulate(Configuration(), request, Snapshot(true), Now);
        Assert.Equal(first.Scenarios[0].TotalCost, second.Scenarios[0].TotalCost);
        Assert.All(first.Scenarios[0].Months.SelectMany(month => month.Tiers), tier => Assert.True(tier.EndBillableBytes >= 0 && tier.EndRecordCount >= 0));
    }

    [Fact]
    public void ReturnsAnExplicitValidationErrorWhenModeledAmountsOverflow()
    {
        var snapshot = Snapshot() with
        {
            Cohorts = [new(DateOnly.FromDateTime(Now).AddDays(-14), 1, 1000000000000000000m)],
            AccessAges = [new(0, 1000000000000000000)], FirstAccessObservationDateUtc = DateOnly.FromDateTime(Now)
        };
        var config = Configuration() with { TierPrices = Configuration().TierPrices.Select(price => price with { RetrievalCostPerGiB = 100000 }).ToList() };
        var request = Request(120) with { GrowthModel = AuditRetentionGrowthModel.CompoundAnnual, AnnualGrowthPercent = 300 };
        var exception = Assert.Throws<AuditRetentionValidationException>(() => _engine.Simulate(config, request, snapshot, Now));
        Assert.Contains("ModelRange", exception.Errors.Keys);
    }
}

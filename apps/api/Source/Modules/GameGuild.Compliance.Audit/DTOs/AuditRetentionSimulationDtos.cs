using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Compliance.Audit;

[JsonConverter(typeof(JsonStringEnumConverter<AuditStorageTier>))]
public enum AuditStorageTier { Hot, Warm, Cold, Archive }

[JsonConverter(typeof(JsonStringEnumConverter<AuditRetentionGrowthModel>))]
public enum AuditRetentionGrowthModel { Constant, HistoricalTrend, CompoundAnnual }

public sealed record AuditStorageTierPrice
{
    public AuditStorageTier Tier { get; init; }
    [Range(typeof(decimal), "0", "100000")]
    public decimal MonthlyCostPerGiB { get; init; }
    [Range(typeof(decimal), "0", "100000")]
    public decimal RetrievalCostPerGiB { get; init; }
    [Range(typeof(decimal), "0", "604800000")]
    public decimal ExpectedReadLatencyMilliseconds { get; init; }
}

/// <summary>A configured obligation, with an administrator-provided source; no regulatory minimum is assumed.</summary>
public sealed record AuditRetentionObligation
{
    [Required, MaxLength(100)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(1000)] public string Source { get; init; } = string.Empty;
    [Range(1, 36500)] public int MinimumRetentionDays { get; init; }
    [Range(1, 36500)] public int? MaximumRetentionDays { get; init; }
}

public sealed record AuditRetentionScenario
{
    [Required, MaxLength(80)] public string Name { get; init; } = string.Empty;
    [Range(1, 36500)] public int RetentionDays { get; init; }
    [Range(0, 36500)] public int HotDays { get; init; }
    [Range(0, 36500)] public int WarmUntilDays { get; init; }
    [Range(0, 36500)] public int ColdUntilDays { get; init; }
}

public sealed record ConfigureAuditRetentionRequest
{
    [Range(0, int.MaxValue)] public int ExpectedRevision { get; init; }
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; init; } = "USD";
    [Range(typeof(decimal), "1", "10")] public decimal StorageOverheadMultiplier { get; init; } = 1;
    [Range(typeof(decimal), "0", "1000000000000000")] public decimal? MonthlyBudget { get; init; }
    [Range(typeof(decimal), "0", "604800000")] public decimal MaximumReadLatencyMilliseconds { get; init; } = 1000;
    [Required] public AuditRetentionScenario Baseline { get; init; } = new();
    [Required, MinLength(4), MaxLength(4)] public List<AuditStorageTierPrice> TierPrices { get; init; } = [];
    [Required, MaxLength(50)] public List<AuditRetentionObligation> Obligations { get; init; } = [];
    /// <summary>All existing records are preserved through this inclusive UTC date in every scenario.</summary>
    public DateOnly? PreserveAllRecordsThroughUtcDate { get; init; }
}

public sealed record RunAuditRetentionSimulationRequest
{
    [Range(14, 365)] public int HistoricalDays { get; init; } = 90;
    [Range(1, 120)] public int ForecastMonths { get; init; } = 36;
    public AuditRetentionGrowthModel GrowthModel { get; init; } = AuditRetentionGrowthModel.HistoricalTrend;
    [Range(typeof(decimal), "-95", "300")] public decimal? AnnualGrowthPercent { get; init; }
    [Required, MinLength(1), MaxLength(10)] public List<AuditRetentionScenario> Scenarios { get; init; } = [];
}

public sealed record AuditRetentionSimulationListRequest
{
    [FromQuery(Name = "skip"), Range(0, int.MaxValue)] public int Skip { get; init; }
    [FromQuery(Name = "take"), Range(1, 100)] public int Take { get; init; } = 25;
}

/// <summary>
/// The tenant retention policy configuration. When the tenant has no explicit configuration and inheritance
/// was requested, the response carries the platform baseline template and <see cref="InheritedFromTemplateId"/>
/// names it; otherwise <see cref="InheritedFromTemplateId"/> is null.
/// </summary>
public sealed record AuditRetentionConfigurationResponse(
    Guid Id, Guid TenantId, int Revision, Guid UpdatedByUserId, DateTime UpdatedAtUtc,
    ConfigureAuditRetentionRequest Configuration,
    string? InheritedFromTemplateId = null);

/// <summary>Measured PostgreSQL logical row bytes; physical disk, compression and index costs are separate assumptions.</summary>
public sealed record AuditStorageDailyCohort(DateOnly DateUtc, long RecordCount, decimal LogicalBytes);
public sealed record AuditAccessAgeBucket(int AgeDays, long ReadCount);
public sealed record AuditRetentionWeekdayFactor(int DayOfWeek, decimal BytesMultiplier, decimal RecordsMultiplier);
public sealed record AuditRetentionDataSnapshot(
    IReadOnlyList<AuditStorageDailyCohort> Cohorts,
    IReadOnlyList<AuditAccessAgeBucket> AccessAges,
    DateOnly? FirstAccessObservationDateUtc,
    string MeasurementMethod);

public sealed record AuditRetentionHistoricalEvidence(
    DateTime AsOfUtc, string MeasurementMethod, decimal StoredLogicalBytes, decimal StoredRecordCount,
    int HistoricalDays, int AvailableHistoryDays, int DaysWithRecords,
    decimal AverageDailyLogicalBytes, decimal AverageDailyRecords,
    decimal DailyLogicalBytesTrend, decimal DailyRecordsTrend,
    int ObservedAccessDays, decimal ObservedReadCount, int? OldestObservedAccessAgeDays,
    IReadOnlyList<AuditStorageDailyCohort> Cohorts, IReadOnlyList<AuditAccessAgeBucket> AccessAges,
    IReadOnlyList<AuditRetentionWeekdayFactor> WeeklySeasonality);

public sealed record AuditRetentionTierForecast(
    AuditStorageTier Tier, decimal EndBillableBytes, decimal EndRecordCount,
    decimal StorageCost, decimal RetrievalCost);

public sealed record AuditRetentionMonthForecast(
    int Month, DateOnly StartUtcDate, DateOnly EndUtcDateExclusive,
    decimal EndBillableBytes, decimal EndRecordCount, decimal StorageCost,
    decimal RetrievalCost, decimal TotalCost, decimal? Budget, decimal? BudgetVariance,
    IReadOnlyList<AuditRetentionTierForecast> Tiers);

public sealed record AuditRetentionYearForecast(
    int ForecastYear, int Months, decimal StorageCost, decimal RetrievalCost, decimal TotalCost,
    decimal? Budget, decimal? BudgetVariance);

public sealed record AuditRetentionRisk(string Code, string Severity, string Description);
public sealed record AuditRetentionComplianceViolation(string Obligation, string Source, string Reason);

public sealed record AuditRetentionScenarioResult(
    AuditRetentionScenario Scenario, string ComplianceStatus,
    IReadOnlyList<AuditRetentionComplianceViolation> ComplianceViolations,
    decimal InitialExpiredRecords, decimal InitialExpiredLogicalBytes,
    decimal? ExpectedReadLatencyMilliseconds, decimal? P95ReadLatencyMilliseconds,
    decimal? UnavailableObservedReadsPercent, decimal? SlowObservedReadsPercent,
    decimal TotalStorageCost, decimal TotalRetrievalCost, decimal TotalCost,
    decimal? BudgetVariance, decimal DifferenceFromBaseline, decimal? SavingsPercent,
    IReadOnlyList<AuditRetentionRisk> Risks,
    IReadOnlyList<AuditRetentionMonthForecast> Months,
    IReadOnlyList<AuditRetentionYearForecast> Years);

public sealed record AuditRetentionRecommendation(
    AuditRetentionScenarioResult? SuggestedScenario, decimal? SavingsComparedWithBaseline,
    string Reason, int EvaluatedCandidates);

public sealed record AuditRetentionSimulationReport(
    string ModelVersion, string Currency, int ForecastMonths, AuditRetentionGrowthModel GrowthModel,
    decimal? AnnualGrowthPercent, decimal StorageOverheadMultiplier,
    AuditRetentionHistoricalEvidence Evidence, IReadOnlyList<string> Assumptions,
    AuditRetentionScenarioResult Baseline, IReadOnlyList<AuditRetentionScenarioResult> Scenarios,
    AuditRetentionRecommendation Recommendation);

public sealed record AuditRetentionSimulationResponse(
    Guid Id, Guid TenantId, Guid CreatedByUserId, DateTime CreatedAtUtc, int ConfigurationRevision,
    ConfigureAuditRetentionRequest ConfigurationSnapshot,
    RunAuditRetentionSimulationRequest Request, AuditRetentionSimulationReport Report);

public sealed record AuditRetentionSimulationSummary(
    Guid Id, Guid CreatedByUserId, DateTime CreatedAtUtc, int ConfigurationRevision,
    string Currency, int ForecastMonths, decimal BaselineTotalCost, decimal? RecommendedTotalCost);

public sealed class AuditRetentionValidationException(IReadOnlyDictionary<string, string[]> errors)
    : ArgumentException("The retention simulation input is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class AuditRetentionConcurrencyException()
    : InvalidOperationException("The retention configuration changed. Retrieve its current revision and retry.");

public static class AuditRetentionInputValidation
{
    public static void Validate(ConfigureAuditRetentionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        ValidateObject(request, string.Empty, errors);
        if (request.PreserveAllRecordsThroughUtcDate?.Year > 9800)
        {
            Add(errors, "PreserveAllRecordsThroughUtcDate", "The hold date must be no later than year 9800.");
        }
        if (request.Baseline is not null) { ValidateScenario(request.Baseline, "Baseline", errors); }
        if (request.TierPrices is not null)
        {
            for (var index = 0; index < request.TierPrices.Count; index++)
            {
                var tier = request.TierPrices[index];
                if (tier is null) { Add(errors, $"TierPrices[{index}]", "A tier price is required."); continue; }
                ValidateObject(tier, $"TierPrices[{index}]", errors);
                if (!Enum.IsDefined(tier.Tier)) { Add(errors, $"TierPrices[{index}].Tier", "Unknown storage tier."); }
            }
            if (request.TierPrices.Where(tier => tier is not null).Select(tier => tier.Tier).Distinct().Count() != 4)
            {
                Add(errors, "TierPrices", "Provide exactly one price for hot, warm, cold and archive storage.");
            }
        }
        if (request.Obligations is not null)
        {
            for (var index = 0; index < request.Obligations.Count; index++)
            {
                var rule = request.Obligations[index];
                if (rule is null) { Add(errors, $"Obligations[{index}]", "An obligation is required."); continue; }
                ValidateObject(rule, $"Obligations[{index}]", errors);
                if (rule.MaximumRetentionDays < rule.MinimumRetentionDays)
                {
                    Add(errors, $"Obligations[{index}].MaximumRetentionDays", "The maximum must be at least the minimum.");
                }
            }
        }
        ThrowIfInvalid(errors);
    }

    public static void Validate(RunAuditRetentionSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        ValidateObject(request, string.Empty, errors);
        if (!Enum.IsDefined(request.GrowthModel)) { Add(errors, "GrowthModel", "Unknown growth model."); }
        if (request.GrowthModel == AuditRetentionGrowthModel.CompoundAnnual && !request.AnnualGrowthPercent.HasValue)
        {
            Add(errors, "AnnualGrowthPercent", "Provide an annual growth rate for CompoundAnnual.");
        }
        if (request.GrowthModel != AuditRetentionGrowthModel.CompoundAnnual && request.AnnualGrowthPercent.HasValue)
        {
            Add(errors, "AnnualGrowthPercent", "An annual growth rate applies only to CompoundAnnual.");
        }
        if (request.Scenarios is not null)
        {
            for (var index = 0; index < request.Scenarios.Count; index++)
            {
                var scenario = request.Scenarios[index];
                if (scenario is null) { Add(errors, $"Scenarios[{index}]", "A scenario is required."); continue; }
                ValidateScenario(scenario, $"Scenarios[{index}]", errors);
            }
            if (request.Scenarios.Where(scenario => scenario is not null)
                    .Select(scenario => scenario.Name?.Trim() ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Scenarios.Count)
            {
                Add(errors, "Scenarios", "Scenario names must be unique.");
            }
        }
        ThrowIfInvalid(errors);
    }

    private static void ValidateScenario(AuditRetentionScenario scenario, string prefix, Dictionary<string, List<string>> errors)
    {
        ValidateObject(scenario, prefix, errors);
        if (scenario.HotDays > scenario.WarmUntilDays || scenario.WarmUntilDays > scenario.ColdUntilDays ||
            scenario.ColdUntilDays > scenario.RetentionDays)
        {
            Add(errors, prefix, "Require 0 <= HotDays <= WarmUntilDays <= ColdUntilDays <= RetentionDays.");
        }
    }

    private static void ValidateObject(object instance, string prefix, Dictionary<string, List<string>> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        foreach (var result in results)
        {
            foreach (var member in result.MemberNames.DefaultIfEmpty(string.Empty))
            {
                var key = string.IsNullOrEmpty(prefix) ? member : string.IsNullOrEmpty(member) ? prefix : $"{prefix}.{member}";
                Add(errors, key, result.ErrorMessage ?? "Invalid value.");
            }
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var values)) { errors[key] = values = []; }
        values.Add(message);
    }

    private static void ThrowIfInvalid(Dictionary<string, List<string>> errors)
    {
        if (errors.Count > 0)
        {
            throw new AuditRetentionValidationException(errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        }
    }
}

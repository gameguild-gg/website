using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>Read model for one arm of a pricing experiment.</summary>
/// <param name="Id">Variant id</param>
/// <param name="Name">Variant name</param>
/// <param name="IsControl">Whether this is the control (baseline) arm</param>
/// <param name="AllocationPercentage">Relative allocation weight (normalized when bucketing)</param>
/// <param name="DiscountPercentage">Discount percentage applied by this arm</param>
/// <param name="FixedPrice">Fixed per-unit price pinned by this arm</param>
public sealed record PricingExperimentVariantDto(
    Guid Id,
    string Name,
    bool IsControl,
    int AllocationPercentage,
    decimal? DiscountPercentage,
    decimal? FixedPrice
);

/// <summary>Read model for a pricing experiment with its arms.</summary>
/// <param name="Id">Experiment id</param>
/// <param name="ProductId">Product the experiment applies to (null = global)</param>
/// <param name="Name">Experiment name</param>
/// <param name="Description">Optional hypothesis description</param>
/// <param name="Status">Lifecycle status</param>
/// <param name="StartDate">Optional enrollment start (UTC)</param>
/// <param name="EndDate">Optional enrollment end (UTC)</param>
/// <param name="TrafficPercentage">Percentage of bucket-key traffic enrolled</param>
/// <param name="Variants">Experiment arms</param>
/// <param name="CreatedAt">Creation timestamp (UTC)</param>
/// <param name="UpdatedAt">Last update timestamp (UTC)</param>
public sealed record PricingExperimentDto(
    Guid Id,
    Guid? ProductId,
    string Name,
    string? Description,
    PricingExperimentStatus Status,
    DateTime? StartDate,
    DateTime? EndDate,
    int TrafficPercentage,
    IReadOnlyList<PricingExperimentVariantDto> Variants,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>Write model for one arm when creating an experiment.</summary>
/// <param name="Name">Variant name</param>
/// <param name="IsControl">Whether this is the control arm</param>
/// <param name="AllocationPercentage">Relative allocation weight</param>
/// <param name="DiscountPercentage">Discount percentage applied by the arm</param>
/// <param name="FixedPrice">Fixed per-unit price pinned by the arm</param>
public sealed record PricingExperimentVariantRequest(
    string Name,
    bool IsControl = false,
    int AllocationPercentage = 50,
    decimal? DiscountPercentage = null,
    decimal? FixedPrice = null
);

/// <summary>Write model for creating a pricing experiment.</summary>
/// <param name="ProductId">Product the experiment applies to (null = global)</param>
/// <param name="Name">Experiment name</param>
/// <param name="Description">Optional hypothesis description</param>
/// <param name="StartDate">Optional enrollment start (UTC)</param>
/// <param name="EndDate">Optional enrollment end (UTC)</param>
/// <param name="TrafficPercentage">Percentage of bucket-key traffic enrolled</param>
/// <param name="Variants">Experiment arms (exactly one must be the control)</param>
public sealed record CreatePricingExperimentRequest(
    Guid? ProductId,
    string Name,
    string? Description = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int TrafficPercentage = 100,
    IReadOnlyList<PricingExperimentVariantRequest>? Variants = null
);

/// <summary>Outcome of resolving (or creating) the sticky assignment of a bucket key.</summary>
/// <param name="ExperimentId">Experiment id</param>
/// <param name="BucketKey">Bucket key that was resolved</param>
/// <param name="Variant">Assigned arm (null when held out by the traffic gate)</param>
/// <param name="AssignmentId">Durable assignment id (null when held out)</param>
/// <param name="AssignedAt">When the key was enrolled (UTC)</param>
public sealed record PricingExperimentAssignmentResult(
    Guid ExperimentId,
    string BucketKey,
    PricingExperimentVariantDto? Variant,
    Guid? AssignmentId,
    DateTime AssignedAt
);

/// <summary>Per-variant analytics row for an experiment window.</summary>
/// <param name="VariantId">Variant id</param>
/// <param name="VariantName">Variant name</param>
/// <param name="IsControl">Whether this row is the control arm</param>
/// <param name="Assignments">Durable assignments in this arm</param>
/// <param name="Conversions">Monetized outcomes in the window</param>
/// <param name="ConversionRate">Conversions ÷ assignments (null when no assignments)</param>
/// <param name="Revenue">Sum of outcome amounts in the window</param>
/// <param name="AverageOrderValue">Revenue ÷ conversions (null when no conversions)</param>
/// <param name="RevenuePerAssignment">Revenue ÷ assignments (null when no assignments)</param>
/// <param name="RevenueLiftPercentage">
///     Revenue-per-assignment lift vs the control arm in percent (null when the control or
///     this arm has no assignments, or when the control's baseline is zero).
/// </param>
public sealed record PricingExperimentVariantAnalytics(
    Guid VariantId,
    string VariantName,
    bool IsControl,
    int Assignments,
    int Conversions,
    decimal? ConversionRate,
    decimal Revenue,
    decimal? AverageOrderValue,
    decimal? RevenuePerAssignment,
    decimal? RevenueLiftPercentage
);

/// <summary>Revenue-impact analytics for one experiment over a window (issue #395).</summary>
/// <param name="ExperimentId">Experiment id</param>
/// <param name="ExperimentName">Experiment name</param>
/// <param name="Status">Experiment status</param>
/// <param name="WindowFromUtc">Window start (null = unbounded)</param>
/// <param name="WindowToUtc">Window end (null = unbounded)</param>
/// <param name="TotalConversions">Conversions across arms in the window</param>
/// <param name="TotalRevenue">Revenue across arms in the window</param>
/// <param name="Currency">Currency the revenue figures are reported in (first outcome wins; USD when no outcomes)</param>
/// <param name="Variants">Per-variant analytics, control first</param>
public sealed record PricingExperimentAnalytics(
    Guid ExperimentId,
    string ExperimentName,
    PricingExperimentStatus Status,
    DateTime? WindowFromUtc,
    DateTime? WindowToUtc,
    int TotalConversions,
    decimal TotalRevenue,
    string Currency,
    IReadOnlyList<PricingExperimentVariantAnalytics> Variants
);

/// <summary>Result of one automated pricing-rule/experiment scheduling pass (issue #395).</summary>
/// <param name="RulesActivated">Rules the scheduler activated (window opened)</param>
/// <param name="RulesDeactivated">Rules the scheduler deactivated (window closed)</param>
/// <param name="ExperimentsStarted">Experiments transitioned Draft → Running (start reached)</param>
/// <param name="ExperimentsCompleted">Experiments transitioned Running → Completed (end reached)</param>
/// <param name="RanAt">When the pass ran (UTC)</param>
public sealed record PricingScheduleSweepResult(
    int RulesActivated,
    int RulesDeactivated,
    int ExperimentsStarted,
    int ExperimentsCompleted,
    DateTime RanAt
);

/// <summary>Mapping helpers for pricing experiments.</summary>
public static class PricingExperimentMappingExtensions
{
    /// <summary>Maps a variant entity to its read model.</summary>
    public static PricingExperimentVariantDto ToDto(this PricingExperimentVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);

        return new PricingExperimentVariantDto(
            variant.Id,
            variant.Name,
            variant.IsControl,
            variant.AllocationPercentage,
            variant.DiscountPercentage,
            variant.FixedPrice);
    }

    /// <summary>Maps an experiment entity (with variants) to its read model.</summary>
    public static PricingExperimentDto ToDto(this PricingExperiment experiment)
    {
        ArgumentNullException.ThrowIfNull(experiment);

        return new PricingExperimentDto(
            experiment.Id,
            experiment.ProductId,
            experiment.Name,
            experiment.Description,
            experiment.Status,
            experiment.StartDate,
            experiment.EndDate,
            experiment.TrafficPercentage,
            experiment.Variants
                .OrderByDescending(v => v.IsControl)
                .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .Select(v => v.ToDto())
                .ToList(),
            experiment.CreatedAt,
            experiment.UpdatedAt);
    }
}

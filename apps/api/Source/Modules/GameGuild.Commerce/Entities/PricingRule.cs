using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce;

/// <summary>
/// Entity representing dynamic pricing rules.
/// Consolidated entity combining volume discounts, time-based pricing, region-based pricing,
/// customer segment pricing, and promotional discount types.
/// </summary>
[Table("pricing_rules")]
[Index(nameof(ProductId))]
[Index(nameof(RuleType))]
[Index(nameof(IsActive))]
[Index(nameof(Priority))]
[Index(nameof(StartDate))]
[Index(nameof(EndDate))]
public class PricingRule : EntityBase
{
    /// <summary>Default constructor</summary>
    public PricingRule() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial pricing rule data</param>
    public PricingRule(object partial) : base(partial) { }

    /// <summary>
    /// Product ID this rule applies to.
    /// Nullable to support global pricing rules that apply across products.
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>Name of the pricing rule</summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of the pricing rule</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Type of pricing rule</summary>
    public PricingRuleType RuleType { get; set; }

    /// <summary>Priority of the rule (higher = applied first)</summary>
    public int Priority { get; set; } = 0;

    /// <summary>Whether this rule is active</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When this rule becomes active</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>When this rule expires</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Minimum quantity required for rule to apply</summary>
    public int? MinQuantity { get; set; }

    /// <summary>Maximum quantity for rule to apply</summary>
    public int? MaxQuantity { get; set; }

    /// <summary>Discount percentage (for percentage-based rules)</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercentage { get; set; }

    /// <summary>Fixed discount amount (for fixed amount rules)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountAmount { get; set; }

    /// <summary>Fixed price override</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? FixedPrice { get; set; }

    /// <summary>Buy quantity (for Buy X Get Y promotions)</summary>
    public int? BuyQuantity { get; set; }

    /// <summary>Get quantity (for Buy X Get Y promotions)</summary>
    public int? GetQuantity { get; set; }

    /// <summary>Time-based pricing start time (HH:MM)</summary>
    [MaxLength(5)]
    public string? TimeStart { get; set; }

    /// <summary>Time-based pricing end time (HH:MM)</summary>
    [MaxLength(5)]
    public string? TimeEnd { get; set; }

    /// <summary>Days of week for time-based pricing (comma-separated: 0-6)</summary>
    [MaxLength(20)]
    public string? DaysOfWeek { get; set; }

    /// <summary>Geographic region for region-based pricing</summary>
    [MaxLength(100)]
    public string? Region { get; set; }

    /// <summary>Customer segment for segment-based pricing</summary>
    [MaxLength(100)]
    public string? CustomerSegment { get; set; }

    /// <summary>
    ///     Usage metric key this rule is gated on (issue #395 usage-based dynamic adjustments).
    ///     When set, the rule only applies to calculations that report a usage value for this
    ///     metric inside [<see cref="UsageMinValue"/>, <see cref="UsageMaxValue"/>].
    /// </summary>
    [MaxLength(100)]
    public string? UsageMetricKey { get; set; }

    /// <summary>Inclusive lower bound on the usage metric value (null = unbounded)</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? UsageMinValue { get; set; }

    /// <summary>Inclusive upper bound on the usage metric value (null = unbounded)</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? UsageMaxValue { get; set; }

    /// <summary>
    ///     Opt-in to automated scheduling (issue #395): the pricing-rule scheduler keeps
    ///     <see cref="IsActive"/> in sync with the rule's date window (active inside
    ///     [StartDate, EndDate), inactive outside it). Never set automatically — an
    ///     administrator-controlled rule stays exactly as configured.
    /// </summary>
    public bool IsScheduled { get; set; }

    /// <summary>Navigation property to pricing tiers (for tiered/volume pricing)</summary>
    public virtual ICollection<PricingRuleTier> PricingTiers { get; } = new List<PricingRuleTier>();

    /// <summary>Check if the pricing rule is currently applicable based on date</summary>
    public bool IsApplicable(DateTime? checkDate = null)
    {
        if (!IsActive) return false;

        var now = checkDate ?? SystemClock.UtcNow;
        if (StartDate.HasValue && StartDate.Value > now) return false;
        if (EndDate.HasValue && EndDate.Value <= now) return false;

        return true;
    }

    /// <summary>Check if rule is applicable for quantity and date</summary>
    public bool IsApplicable(int quantity, DateTime date)
    {
        if (!IsActive) return false;
        if (StartDate.HasValue && date < StartDate.Value) return false;
        if (EndDate.HasValue && date > EndDate.Value) return false;
        if (MinQuantity.HasValue && quantity < MinQuantity.Value) return false;
        if (MaxQuantity.HasValue && quantity > MaxQuantity.Value) return false;

        return true;
    }

    /// <summary>Check if the rule applies to the given quantity</summary>
    public bool AppliesToQuantity(int quantity)
    {
        if (!IsApplicable()) return false;

        return (MinQuantity == null || quantity >= MinQuantity) && (MaxQuantity == null || quantity <= MaxQuantity);
    }

    /// <summary>
    ///     Checks the usage-metric gate (issue #395 dynamic usage adjustments). A rule without
    ///     <see cref="UsageMetricKey"/> applies regardless of reported usage. A gated rule applies
    ///     only when the calculation reported a value for that metric inside the inclusive band.
    /// </summary>
    /// <param name="usageMetrics">Usage values reported by the calculation (null = none reported)</param>
    public bool MatchesUsageMetric(IReadOnlyDictionary<string, decimal>? usageMetrics)
    {
        if (string.IsNullOrWhiteSpace(UsageMetricKey))
        {
            return true;
        }

        if (usageMetrics is null ||
            !usageMetrics.TryGetValue(UsageMetricKey.Trim(), out var value))
        {
            return false;
        }

        if (UsageMinValue.HasValue && value < UsageMinValue.Value)
        {
            return false;
        }

        if (UsageMaxValue.HasValue && value > UsageMaxValue.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>Calculate the price using this rule (for Products-style pricing)</summary>
    public decimal CalculatePrice(decimal basePrice, int quantity)
    {
        if (!AppliesToQuantity(quantity)) return basePrice;

        return RuleType switch
        {
            PricingRuleType.VolumeDiscount => basePrice * (1 - (DiscountPercentage ?? 0) / 100),
            PricingRuleType.FixedPriceOverride => FixedPrice ?? basePrice,
            PricingRuleType.TimeBased => basePrice * (1 - (DiscountPercentage ?? 0) / 100),
            PricingRuleType.RegionBased => FixedPrice ?? (basePrice * (1 - (DiscountPercentage ?? 0) / 100)),
            PricingRuleType.SegmentBased => basePrice * (1 - (DiscountPercentage ?? 0) / 100),
            PricingRuleType.UsageBased => basePrice * (1 - (DiscountPercentage ?? 0) / 100),
            _ => basePrice
        };
    }

    /// <summary>Calculate discount for this rule (for Payments-style discounting)</summary>
    public decimal CalculateDiscount(decimal originalPrice, int quantity)
    {
        return RuleType switch
        {
            PricingRuleType.Percentage => originalPrice * quantity * (DiscountPercentage ?? 0) / 100,
            PricingRuleType.FixedAmount => DiscountAmount ?? 0,
            PricingRuleType.BuyXGetY => CalculateBuyXGetYDiscount(originalPrice, quantity),
            PricingRuleType.VolumeDiscount => CalculateVolumeDiscount(originalPrice, quantity),
            PricingRuleType.TieredPricing => CalculateVolumeDiscount(originalPrice, quantity),
            PricingRuleType.Bundle => DiscountAmount ?? 0,
            _ => 0
        };
    }

    private decimal CalculateBuyXGetYDiscount(decimal originalPrice, int quantity)
    {
        if (!BuyQuantity.HasValue || !GetQuantity.HasValue) return 0;

        var sets = quantity / (BuyQuantity.Value + GetQuantity.Value);
        var freeItems = sets * GetQuantity.Value;

        return freeItems * originalPrice;
    }

    private decimal CalculateVolumeDiscount(decimal originalPrice, int quantity)
    {
        var applicableTier = PricingTiers
            .Where(t => (!t.MinQuantity.HasValue || quantity >= t.MinQuantity.Value) &&
                        (!t.MaxQuantity.HasValue || quantity <= t.MaxQuantity.Value))
            .OrderByDescending(t => t.DiscountPercentage)
            .FirstOrDefault();

        if (applicableTier == null) return 0;

        if (applicableTier.Price.HasValue)
            return (originalPrice - applicableTier.Price.Value) * quantity;

        if (applicableTier.DiscountPercentage.HasValue)
            return originalPrice * quantity * applicableTier.DiscountPercentage.Value / 100;

        return 0;
    }
}

/// <summary>Types of pricing rules - consolidated from Products and Payments modules</summary>
public enum PricingRuleType
{
    // Products-originated types
    /// <summary>Volume-based discount</summary>
    VolumeDiscount = 0,

    /// <summary>Fixed price override</summary>
    FixedPriceOverride = 1,

    /// <summary>Time-based pricing (happy hour, seasonal)</summary>
    TimeBased = 2,

    /// <summary>Region-based pricing</summary>
    RegionBased = 3,

    /// <summary>Customer segment-based pricing</summary>
    SegmentBased = 4,

    /// <summary>Dynamic market-based pricing</summary>
    MarketBased = 5,

    /// <summary>
    ///     Usage-metric-driven dynamic adjustment (issue #395): the rule applies only when the
    ///     calculation reports a value for the rule's usage metric inside its band; the
    ///     adjustment itself uses the rule's percentage/fixed-price fields.
    /// </summary>
    UsageBased = 6,

    // Payments-originated types
    /// <summary>Percentage-based discount</summary>
    Percentage = 10,

    /// <summary>Fixed amount discount</summary>
    FixedAmount = 11,

    /// <summary>Buy X Get Y promotion</summary>
    BuyXGetY = 12,

    /// <summary>Tiered pricing based on quantity</summary>
    TieredPricing = 13,

    /// <summary>Bundle discount</summary>
    Bundle = 14
}

using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Read model for a pricing rule tier (volume tier of a pricing rule).
/// </summary>
/// <param name="Id">Tier ID</param>
/// <param name="MinQuantity">Minimum quantity for the tier (null = no lower bound)</param>
/// <param name="MaxQuantity">Maximum quantity for the tier (null = no upper bound)</param>
/// <param name="Price">Fixed per-unit price for the tier</param>
/// <param name="DiscountPercentage">Percentage discount per unit for the tier</param>
public sealed record PricingRuleTierDto(
    Guid Id,
    int? MinQuantity,
    int? MaxQuantity,
    decimal? Price,
    decimal? DiscountPercentage
);

/// <summary>
/// Read model for a pricing rule (issue #395 dynamic &amp; tiered pricing engine).
/// </summary>
/// <param name="Id">Rule ID</param>
/// <param name="ProductId">Product the rule applies to (null = global rule)</param>
/// <param name="Name">Rule name</param>
/// <param name="Description">Optional description</param>
/// <param name="RuleType">Rule type</param>
/// <param name="Priority">Priority (higher wins)</param>
/// <param name="IsActive">Whether the rule is active</param>
/// <param name="StartDate">Optional activation start (UTC)</param>
/// <param name="EndDate">Optional expiry (UTC)</param>
/// <param name="MinQuantity">Minimum quantity for the rule to apply</param>
/// <param name="MaxQuantity">Maximum quantity for the rule to apply</param>
/// <param name="DiscountPercentage">Percentage discount</param>
/// <param name="DiscountAmount">Fixed discount amount</param>
/// <param name="FixedPrice">Fixed per-unit price override</param>
/// <param name="BuyQuantity">Buy X (Buy X Get Y rules)</param>
/// <param name="GetQuantity">Get Y (Buy X Get Y rules)</param>
/// <param name="TimeStart">Time-of-day window start (HH:MM, TimeBased rules)</param>
/// <param name="TimeEnd">Time-of-day window end (HH:MM, TimeBased rules)</param>
/// <param name="DaysOfWeek">Days of week the rule applies to (comma-separated 0=Sunday..6=Saturday)</param>
/// <param name="Region">Geographic region (RegionBased rules)</param>
/// <param name="CustomerSegment">Customer segment the rule is restricted to</param>
/// <param name="Tiers">Volume tiers for tiered/volume rules</param>
/// <param name="CreatedAt">Creation timestamp (UTC)</param>
/// <param name="UpdatedAt">Last update timestamp (UTC)</param>
/// <param name="Version">Optimistic concurrency version</param>
public sealed record PricingRuleDto(
    Guid Id,
    Guid? ProductId,
    string Name,
    string? Description,
    PricingRuleType RuleType,
    int Priority,
    bool IsActive,
    DateTime? StartDate,
    DateTime? EndDate,
    int? MinQuantity,
    int? MaxQuantity,
    decimal? DiscountPercentage,
    decimal? DiscountAmount,
    decimal? FixedPrice,
    int? BuyQuantity,
    int? GetQuantity,
    string? TimeStart,
    string? TimeEnd,
    string? DaysOfWeek,
    string? Region,
    string? CustomerSegment,
    IReadOnlyList<PricingRuleTierDto> Tiers,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version
);

/// <summary>
/// Write model for a pricing rule tier.
/// </summary>
/// <param name="MinQuantity">Minimum quantity for the tier (null = no lower bound)</param>
/// <param name="MaxQuantity">Maximum quantity for the tier (null = no upper bound)</param>
/// <param name="Price">Fixed per-unit price for the tier</param>
/// <param name="DiscountPercentage">Percentage discount per unit for the tier</param>
public sealed record PricingRuleTierRequest(
    int? MinQuantity = null,
    int? MaxQuantity = null,
    decimal? Price = null,
    decimal? DiscountPercentage = null
);

/// <summary>
/// Mapping helpers for pricing rules.
/// </summary>
public static class PricingRuleMappingExtensions
{
    /// <summary>
    /// Maps a <see cref="PricingRule"/> entity (with tiers) to its read model.
    /// </summary>
    public static PricingRuleDto ToDto(this PricingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new PricingRuleDto(
            rule.Id,
            rule.ProductId,
            rule.Name,
            rule.Description,
            rule.RuleType,
            rule.Priority,
            rule.IsActive,
            rule.StartDate,
            rule.EndDate,
            rule.MinQuantity,
            rule.MaxQuantity,
            rule.DiscountPercentage,
            rule.DiscountAmount,
            rule.FixedPrice,
            rule.BuyQuantity,
            rule.GetQuantity,
            rule.TimeStart,
            rule.TimeEnd,
            rule.DaysOfWeek,
            rule.Region,
            rule.CustomerSegment,
            rule.PricingTiers
                .OrderBy(t => t.MinQuantity ?? 0)
                .Select(t => new PricingRuleTierDto(t.Id, t.MinQuantity, t.MaxQuantity, t.Price, t.DiscountPercentage))
                .ToList(),
            rule.CreatedAt,
            rule.UpdatedAt,
            rule.Version);
    }
}

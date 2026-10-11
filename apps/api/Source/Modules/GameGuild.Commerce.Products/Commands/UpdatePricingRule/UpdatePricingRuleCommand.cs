using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to fully update an existing pricing rule, replacing its volume tiers (issue #395).
/// Like <see cref="SetProductPricingCommand"/>, pricing-rule changes are revenue-generation
/// actions and require the monetization permission on every dispatch path.
/// </summary>
/// <param name="RuleId">Rule ID to update</param>
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
/// <param name="TimeStart">Time-of-day window start (HH:MM)</param>
/// <param name="TimeEnd">Time-of-day window end (HH:MM)</param>
/// <param name="DaysOfWeek">Days of week (comma-separated 0-6)</param>
/// <param name="Region">Geographic region</param>
/// <param name="CustomerSegment">Customer segment restriction</param>
/// <param name="Tiers">Volume tiers (replaces the existing set; null keeps current tiers)</param>
[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]
public sealed record UpdatePricingRuleCommand(
    Guid RuleId,
    Guid? ProductId,
    string Name,
    PricingRuleType RuleType,
    string? Description = null,
    int Priority = 0,
    bool IsActive = true,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int? MinQuantity = null,
    int? MaxQuantity = null,
    decimal? DiscountPercentage = null,
    decimal? DiscountAmount = null,
    decimal? FixedPrice = null,
    int? BuyQuantity = null,
    int? GetQuantity = null,
    string? TimeStart = null,
    string? TimeEnd = null,
    string? DaysOfWeek = null,
    string? Region = null,
    string? CustomerSegment = null,
    IReadOnlyList<PricingRuleTierRequest>? Tiers = null
) : ICommand<PricingRuleDto>, IPricingRuleCommandPayload;

/// <summary>
/// Thrown when a pricing rule is not found.
/// </summary>
public sealed class PricingRuleNotFoundException(Guid ruleId)
    : InvalidOperationException($"Pricing rule '{ruleId}' was not found.");

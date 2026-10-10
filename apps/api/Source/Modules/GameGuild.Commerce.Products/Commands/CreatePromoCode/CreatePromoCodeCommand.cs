using GameGuild.CQRS;
using GameGuild.Resources;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to create a new promo code
/// </summary>
/// <param name="Code">The promo code string (must be unique)</param>
/// <param name="Name">Display name for the promo code</param>
/// <param name="Description">Optional description</param>
/// <param name="Type">Type of discount</param>
/// <param name="DiscountPercentage">Percentage discount (for percentage type)</param>
/// <param name="DiscountAmount">Fixed amount discount (for fixed type)</param>
/// <param name="Currency">Currency code</param>
/// <param name="MinimumOrderAmount">Minimum order amount required</param>
/// <param name="MaxUses">Maximum total uses (null = unlimited)</param>
/// <param name="MaxUsesPerUser">Maximum uses per user (null = unlimited)</param>
/// <param name="ValidFrom">Start date (null = immediately valid)</param>
/// <param name="ValidUntil">End date (null = never expires)</param>
/// <param name="IsActive">Whether the code is active</param>
/// <param name="IsExclusive">Whether the code cannot be stacked</param>
/// <param name="StackingPriority">Priority for stacking (higher = applied first)</param>
/// <param name="DiscountAmount">Fixed amount discount (for fixed type; target price for SetPrice)</param>
/// <param name="MaxDiscountAmount">Cap for percentage discounts (null = no cap)</param>
/// <param name="FreeTrialExtensionDays">Trial days granted by FreeTrialExtension codes</param>
/// <param name="DurationMonths">Free months granted by FreeMonths codes / discount duration</param>
/// <param name="ApplyToRenewal">Whether the discount also applies to renewals</param>
/// <param name="CampaignId">Campaign identifier for tracking (null = none)</param>
/// <param name="ProductId">Specific product ID (null = all products)</param>
/// <param name="CreatedBy">User ID of the creator</param>
[RequiresQuota(ResourceUsageType.PromoCodes, 1, Source = "CreatePromoCode")]
public sealed record CreatePromoCodeCommand(
    string Code,
    string Name,
    string? Description = null,
    PromoCodeType Type = PromoCodeType.PercentageOff,
    decimal? DiscountPercentage = null,
    decimal? DiscountAmount = null,
    decimal? MaxDiscountAmount = null,
    string Currency = "USD",
    decimal? MinimumOrderAmount = null,
    int? MaxUses = null,
    int? MaxUsesPerUser = null,
    int? FreeTrialExtensionDays = null,
    int? DurationMonths = null,
    bool ApplyToRenewal = false,
    DateTime? ValidFrom = null,
    DateTime? ValidUntil = null,
    bool IsActive = true,
    bool IsExclusive = false,
    int StackingPriority = 0,
    string? CampaignId = null,
    Guid? ProductId = null,
    Guid? CreatedBy = null
) : ICommand<PromoCodeDto>;

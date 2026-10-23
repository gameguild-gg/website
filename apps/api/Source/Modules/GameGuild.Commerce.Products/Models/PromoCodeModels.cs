namespace GameGuild.Commerce.Products;

/// <summary>
/// Promo code data transfer object
/// </summary>
/// <param name="Id">Promo code ID</param>
/// <param name="Code">The promo code string</param>
/// <param name="Name">Display name</param>
/// <param name="Description">Description of the promo</param>
/// <param name="Type">Type of discount</param>
/// <param name="DiscountPercentage">Percentage discount (for percentage type)</param>
/// <param name="DiscountAmount">Fixed amount discount (target price for SetPrice)</param>
/// <param name="MaxDiscountAmount">Cap for percentage discounts</param>
/// <param name="Currency">Currency code</param>
/// <param name="MinimumOrderAmount">Minimum order amount required</param>
/// <param name="MaxUses">Maximum total uses</param>
/// <param name="MaxUsesPerUser">Maximum uses per user</param>
/// <param name="CurrentUses">Number of recorded uses</param>
/// <param name="FreeTrialExtensionDays">Trial days granted by FreeTrialExtension codes</param>
/// <param name="DurationMonths">Free months granted / discount duration</param>
/// <param name="ApplyToRenewal">Whether the discount applies to renewals</param>
/// <param name="ValidFrom">Start date</param>
/// <param name="ValidUntil">End date</param>
/// <param name="IsActive">Whether the code is active</param>
/// <param name="IsExclusive">Whether the code cannot be stacked</param>
/// <param name="StackingPriority">Priority for stacking</param>
/// <param name="CampaignId">Campaign identifier for tracking</param>
/// <param name="ProductId">Specific product ID (null = all products)</param>
/// <param name="UsageCount">Current usage count</param>
/// <param name="CreatedAt">Creation timestamp</param>
/// <param name="UpdatedAt">Last update timestamp</param>
public sealed record PromoCodeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    PromoCodeType Type,
    decimal? DiscountPercentage,
    decimal? DiscountAmount,
    decimal? MaxDiscountAmount,
    string Currency,
    decimal? MinimumOrderAmount,
    int? MaxUses,
    int? MaxUsesPerUser,
    int CurrentUses,
    int? FreeTrialExtensionDays,
    int? DurationMonths,
    bool ApplyToRenewal,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    bool IsActive,
    bool IsExclusive,
    int StackingPriority,
    string? CampaignId,
    Guid? ProductId,
    int UsageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Result of validating a promo code
/// </summary>
/// <param name="IsValid">Whether the code is valid</param>
/// <param name="Code">The promo code if valid</param>
/// <param name="ErrorMessage">Error message if invalid</param>
/// <param name="DiscountAmount">Calculated discount amount</param>
/// <param name="DiscountPercentage">Discount percentage applied</param>
public sealed record PromoCodeValidationResult(
    bool IsValid,
    string? Code = null,
    string? ErrorMessage = null,
    decimal DiscountAmount = 0,
    decimal? DiscountPercentage = null
);

/// <summary>
/// Result of applying one or more promo codes
/// </summary>
/// <param name="OriginalAmount">Original order amount</param>
/// <param name="FinalAmount">Amount after discounts</param>
/// <param name="TotalDiscount">Total discount applied</param>
/// <param name="AppliedCodes">List of codes that were applied</param>
/// <param name="RejectedCodes">List of codes that were rejected with reasons</param>
/// <param name="StackingRuleName">Name of the stacking rule that governed resolution (null = single-code default)</param>
/// <param name="UsageRecorded">Whether usage rows were persisted for the applied codes</param>
public sealed record PromoCodeApplicationResult(
    decimal OriginalAmount,
    decimal FinalAmount,
    decimal TotalDiscount,
    List<AppliedPromoCode> AppliedCodes,
    List<RejectedPromoCode> RejectedCodes,
    string? StackingRuleName = null,
    bool UsageRecorded = false
);

/// <summary>
/// Details of an applied promo code
/// </summary>
/// <param name="Code">The promo code</param>
/// <param name="DiscountAmount">Discount amount from this code</param>
/// <param name="DiscountPercentage">Discount percentage from this code</param>
/// <param name="Type">Discount type of the applied code</param>
/// <param name="TrialExtensionDays">Trial days granted (FreeTrialExtension only)</param>
/// <param name="FreeMonthsGranted">Billing periods granted (FreeMonths only)</param>
public record AppliedPromoCode(
    string Code,
    decimal DiscountAmount,
    decimal? DiscountPercentage,
    PromoCodeType? Type = null,
    int? TrialExtensionDays = null,
    int? FreeMonthsGranted = null
);

/// <summary>
/// Details of a rejected promo code
/// </summary>
/// <param name="Code">The promo code</param>
/// <param name="Reason">Reason for rejection</param>
public record RejectedPromoCode(
    string Code,
    string Reason
);

/// <summary>
/// Promo stacking rule DTO
/// </summary>
/// <param name="Id">Rule ID</param>
/// <param name="Name">Rule name</param>
/// <param name="Description">Rule description</param>
/// <param name="IsActive">Whether the rule is active</param>
/// <param name="Priority">Priority level</param>
/// <param name="MaxStackableCount">Max codes that can stack</param>
/// <param name="AllowExclusiveStacking">Allow exclusive code stacking</param>
/// <param name="MaxTotalDiscountPercentage">Max total discount percentage</param>
/// <param name="MaxTotalDiscountAmount">Max total discount amount</param>
/// <param name="ConflictStrategy">Conflict resolution strategy</param>
public sealed record PromoStackingRuleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int Priority,
    int MaxStackableCount,
    bool AllowExclusiveStacking,
    decimal? MaxTotalDiscountPercentage,
    decimal? MaxTotalDiscountAmount,
    ConflictResolutionStrategy ConflictStrategy
);

/// <summary>
/// Result of pricing calculation
/// </summary>
/// <param name="BasePrice">Original base price</param>
/// <param name="SalePrice">Sale price if active</param>
/// <param name="IsSaleActive">Whether sale is active</param>
/// <param name="PromoDiscount">Discount from promo codes</param>
/// <param name="FinalPrice">Final calculated price</param>
/// <param name="Currency">Currency code</param>
/// <param name="AppliedPromoCodes">List of applied promo codes</param>
/// <param name="RuleDiscount">Per-unit discount produced by the winning pricing rule (issue #395)</param>
/// <param name="AppliedRuleId">ID of the winning pricing rule, if any (issue #395)</param>
/// <param name="AppliedRuleName">Name of the winning pricing rule, if any (issue #395)</param>
/// <param name="AppliedExperimentId">ID of the live A/B experiment applied to this calculation, if any (issue #395)</param>
/// <param name="AppliedVariantName">Name of the experiment arm applied to this calculation (issue #395)</param>
/// <param name="ExperimentDiscount">Per-unit discount produced by the experiment arm (issue #395)</param>
public sealed record PricingCalculationResult(
    decimal BasePrice,
    decimal? SalePrice,
    bool IsSaleActive,
    decimal PromoDiscount,
    decimal FinalPrice,
    string Currency,
    List<string> AppliedPromoCodes,
    decimal RuleDiscount = 0,
    Guid? AppliedRuleId = null,
    string? AppliedRuleName = null,
    Guid? AppliedExperimentId = null,
    string? AppliedVariantName = null,
    decimal ExperimentDiscount = 0
);

/// <summary>
/// Aggregated performance for a promo campaign within a window
/// </summary>
/// <param name="CampaignId">Campaign identifier</param>
/// <param name="WindowStart">Window start (null = unbounded)</param>
/// <param name="WindowEnd">Window end (null = unbounded)</param>
/// <param name="TotalCodes">Number of codes in the campaign</param>
/// <param name="ActiveCodes">Number of currently active codes</param>
/// <param name="TotalUses">Recorded uses within the window</param>
/// <param name="UniqueUsers">Distinct users within the window</param>
/// <param name="TotalDiscountGiven">Sum of discounts given within the window</param>
/// <param name="AverageDiscountPerUse">Average discount per use</param>
/// <param name="RedemptionRate">Uses divided by total MaxUses capacity (null when no code declares limits)</param>
/// <param name="TopCodes">Codes ranked by uses within the window</param>
/// <param name="ExpiringCodes">Campaign codes expiring within the next 30 days</param>
public sealed record CampaignPerformanceDto(
    string CampaignId,
    DateTime? WindowStart,
    DateTime? WindowEnd,
    int TotalCodes,
    int ActiveCodes,
    int TotalUses,
    int UniqueUsers,
    decimal TotalDiscountGiven,
    decimal AverageDiscountPerUse,
    decimal? RedemptionRate,
    IReadOnlyList<CampaignTopCodeDto> TopCodes,
    IReadOnlyList<CampaignExpiringCodeDto> ExpiringCodes
);

/// <summary>
/// A campaign code ranked by usage
/// </summary>
/// <param name="Code">Promo code string</param>
/// <param name="PromoCodeId">Promo code ID</param>
/// <param name="Uses">Recorded uses within the window</param>
/// <param name="DiscountGiven">Discount given within the window</param>
public sealed record CampaignTopCodeDto(
    string Code,
    Guid PromoCodeId,
    int Uses,
    decimal DiscountGiven
);

/// <summary>
/// A campaign code approaching expiry
/// </summary>
/// <param name="Code">Promo code string</param>
/// <param name="PromoCodeId">Promo code ID</param>
/// <param name="ValidUntil">Expiry timestamp</param>
public sealed record CampaignExpiringCodeDto(
    string Code,
    Guid PromoCodeId,
    DateTime ValidUntil
);

/// <summary>
/// User product access DTO
/// </summary>
/// <param name="UserId">User ID</param>
/// <param name="ProductId">Product ID</param>
/// <param name="AccessStatus">Current access status</param>
/// <param name="AcquisitionType">How the user acquired access</param>
/// <param name="PricePaid">Amount paid</param>
/// <param name="Currency">Currency code</param>
/// <param name="AccessStartDate">When access started</param>
/// <param name="AccessEndDate">When access ends (null = permanent)</param>
/// <param name="GrantedAt">When access was granted</param>
public sealed record UserProductAccessDto(
    Guid UserId,
    Guid ProductId,
    ProductAccessStatus AccessStatus,
    ProductAcquisitionType AcquisitionType,
    decimal PricePaid,
    string Currency,
    DateTime? AccessStartDate,
    DateTime? AccessEndDate,
    DateTime GrantedAt
);

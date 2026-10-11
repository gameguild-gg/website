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
/// <param name="DiscountAmount">Fixed amount discount (for fixed type)</param>
/// <param name="Currency">Currency code</param>
/// <param name="MinimumOrderAmount">Minimum order amount required</param>
/// <param name="MaxUses">Maximum total uses</param>
/// <param name="MaxUsesPerUser">Maximum uses per user</param>
/// <param name="ValidFrom">Start date</param>
/// <param name="ValidUntil">End date</param>
/// <param name="IsActive">Whether the code is active</param>
/// <param name="IsExclusive">Whether the code cannot be stacked</param>
/// <param name="StackingPriority">Priority for stacking</param>
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
    string Currency,
    decimal? MinimumOrderAmount,
    int? MaxUses,
    int? MaxUsesPerUser,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    bool IsActive,
    bool IsExclusive,
    int StackingPriority,
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
public sealed record PromoCodeApplicationResult(
    decimal OriginalAmount,
    decimal FinalAmount,
    decimal TotalDiscount,
    List<AppliedPromoCode> AppliedCodes,
    List<RejectedPromoCode> RejectedCodes
);

/// <summary>
/// Details of an applied promo code
/// </summary>
/// <param name="Code">The promo code</param>
/// <param name="DiscountAmount">Discount amount from this code</param>
/// <param name="DiscountPercentage">Discount percentage from this code</param>
public record AppliedPromoCode(
    string Code,
    decimal DiscountAmount,
    decimal? DiscountPercentage
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
    string? AppliedRuleName = null
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

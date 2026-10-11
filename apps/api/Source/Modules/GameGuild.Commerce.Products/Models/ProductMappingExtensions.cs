namespace GameGuild.Commerce.Products;

/// <summary>
/// Extension methods for mapping entities to DTOs
/// </summary>
public static class ProductMappingExtensions
{
    /// <summary>
    /// Maps a Product entity to ProductDto
    /// </summary>
    public static ProductDto ToDto(this Product product, List<ProductPricingDto>? pricing = null)
    {
        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.ShortDescription,
            product.ImageUrl,
            product.Type,
            product.IsBundle,
            product.IsPublished,
            product.CreatorId,
#pragma warning disable CS0618 // Suppress obsolete warning for backwards compatibility
            product.GetBundleItemIds(),
#pragma warning restore CS0618
            product.CommissionConfig?.ReferralCommissionPercentage ?? 30m,
            product.CommissionConfig?.MaxAffiliateDiscount ?? 0m,
            product.CommissionConfig?.AffiliateCommissionPercentage ?? 30m,
            product.CreatedAt,
            product.UpdatedAt,
            pricing,
            product.TenantId
        );
    }

    /// <summary>
    /// Maps a PromoCode entity to PromoCodeDto
    /// </summary>
    public static PromoCodeDto ToDto(this PromoCode promoCode, int? usageCount = null)
    {
        return new PromoCodeDto(
            promoCode.Id,
            promoCode.Code,
            promoCode.Name,
            promoCode.Description,
            promoCode.Type,
            promoCode.DiscountPercentage,
            promoCode.DiscountAmount,
            promoCode.MaxDiscountAmount,
            promoCode.Currency,
            promoCode.MinimumOrderAmount,
            promoCode.MaxUses,
            promoCode.MaxUsesPerUser,
            promoCode.CurrentUses,
            promoCode.FreeTrialExtensionDays,
            promoCode.DurationMonths,
            promoCode.ApplyToRenewal,
            promoCode.ValidFrom,
            promoCode.ValidUntil,
            promoCode.IsActive,
            promoCode.IsExclusive,
            promoCode.StackingPriority,
            promoCode.CampaignId,
            promoCode.ProductId,
            usageCount ?? promoCode.CurrentUses,
            promoCode.CreatedAt,
            promoCode.UpdatedAt
        );
    }

    /// <summary>
    /// Maps a PromoStackingRule entity to PromoStackingRuleDto
    /// </summary>
    public static PromoStackingRuleDto ToDto(this PromoStackingRule rule)
    {
        return new PromoStackingRuleDto(
            rule.Id,
            rule.Name,
            rule.Description,
            rule.IsActive,
            rule.Priority,
            rule.MaxStackableCount,
            rule.AllowExclusiveStacking,
            rule.MaxTotalDiscountPercentage,
            rule.MaxTotalDiscountAmount,
            rule.ConflictStrategy
        );
    }

    /// <summary>
    /// Maps a ProductPricing entity to ProductPricingDto
    /// </summary>
    public static ProductPricingDto ToDto(this ProductPricing pricing)
    {
        var isSaleActive = pricing.SalePrice.HasValue
            && pricing.IsSaleActive();

        return new ProductPricingDto(
            pricing.Id,
            pricing.ProductId,
            pricing.Name,
            pricing.BasePrice,
            pricing.SalePrice,
            pricing.Currency,
            pricing.SaleStartDate,
            pricing.SaleEndDate,
            pricing.IsDefault,
            isSaleActive ? pricing.SalePrice!.Value : pricing.BasePrice,
            isSaleActive,
            pricing.GetCurrentActiveVersion()?.Id
        );
    }
}

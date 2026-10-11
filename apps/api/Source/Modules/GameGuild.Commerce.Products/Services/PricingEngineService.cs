using GameGuild.Commerce;
using Microsoft.Extensions.Caching.Memory;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Service for calculating prices with dynamic pricing rules (issue #395), discounts, sales,
/// and promo codes.
/// </summary>
public class PricingEngineService(
    IProductRepository productRepository,
    IPromoCodeRepository promoCodeRepository,
    IPricingRuleRepository pricingRuleRepository,
    IMemoryCache cache) : IPricingEngineService
{
    private const string RulesCacheKeyPrefix = "PricingEngine:Rules:";

    private static readonly TimeSpan RulesCacheDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public async Task<PricingCalculationResult> CalculatePriceAsync(
        Product product,
        ProductPricing? pricing = null,
        IEnumerable<string>? promoCodes = null,
        Guid? userId = null,
        int quantity = 1,
        string? customerSegment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        // Get default pricing if not specified
        pricing ??= product.Pricing.FirstOrDefault(p => p.IsDefault)
                    ?? product.Pricing.FirstOrDefault();

        if (pricing == null)
        {
            return new PricingCalculationResult(
                BasePrice: 0,
                SalePrice: null,
                IsSaleActive: false,
                PromoDiscount: 0,
                FinalPrice: 0,
                Currency: "USD",
                AppliedPromoCodes: new List<string>()
            );
        }

        var effectiveQuantity = Math.Max(1, quantity);
        var basePrice = pricing.BasePrice;
        var isSaleActive = IsSaleActive(pricing);
        var effectivePrice = isSaleActive && pricing.SalePrice.HasValue
            ? pricing.SalePrice.Value
            : basePrice;

        // Resolve the highest-priority applicable pricing rule (time window, quantity window,
        // customer segment) and compute its per-unit discount on top of the base/sale price.
        var (appliedRule, ruleUnitDiscount) = await ResolvePricingRuleAsync(
            product.Id,
            effectivePrice,
            effectiveQuantity,
            customerSegment,
            cancellationToken).ConfigureAwait(false);

        var ruleAdjustedPrice = Math.Max(0, effectivePrice - ruleUnitDiscount);

        // Apply promo codes (after the rule) on the rule-adjusted unit price
        var promoDiscount = 0m;
        var appliedCodes = new List<string>();
        var promoCodesList = promoCodes?.ToList();

        if (promoCodesList is { Count: > 0 })
        {
            var promoResult = await ApplyPromoCodesAsync(
                ruleAdjustedPrice,
                promoCodesList,
                product.Id,
                userId,
                cancellationToken).ConfigureAwait(false);

            promoDiscount = promoResult.TotalDiscount;
            appliedCodes = promoResult.AppliedCodes.Select(c => c.Code).ToList();
        }

        var finalPrice = Math.Max(0, ruleAdjustedPrice - promoDiscount);

        return new PricingCalculationResult(
            BasePrice: basePrice,
            SalePrice: pricing.SalePrice,
            IsSaleActive: isSaleActive,
            PromoDiscount: promoDiscount,
            FinalPrice: finalPrice,
            Currency: pricing.Currency,
            AppliedPromoCodes: appliedCodes,
            RuleDiscount: ruleUnitDiscount,
            AppliedRuleId: appliedRule?.Id,
            AppliedRuleName: appliedRule?.Name
        );
    }

    /// <inheritdoc />
    public async Task<PricingCalculationResult> CalculatePriceByIdAsync(
        Guid productId,
        Guid? pricingId = null,
        IEnumerable<string>? promoCodes = null,
        Guid? userId = null,
        int quantity = 1,
        string? customerSegment = null,
        CancellationToken cancellationToken = default)
    {
        var product = await productRepository.GetByIdAsync(
            productId,
            cancellationToken,
            includePricing: true).ConfigureAwait(false);

        if (product == null)
        {
            throw new ProductNotFoundException(productId);
        }

        ProductPricing? pricing = null;
        if (pricingId.HasValue)
        {
            pricing = product.Pricing.FirstOrDefault(p => p.Id == pricingId.Value);
        }

        return await CalculatePriceAsync(product, pricing, promoCodes, userId, quantity, customerSegment, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PromoCodeApplicationResult> ApplyPromoCodesAsync(
        decimal orderAmount,
        IEnumerable<string> promoCodes,
        Guid? productId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var appliedCodes = new List<AppliedPromoCode>();
        var rejectedCodes = new List<RejectedPromoCode>();
        var totalDiscount = 0m;
        var remainingAmount = orderAmount;
        var hasExclusiveCode = false;

        foreach (var codeString in promoCodes.Distinct())
        {
            // Skip if we already applied an exclusive code
            if (hasExclusiveCode)
            {
                rejectedCodes.Add(new RejectedPromoCode(codeString, "Cannot stack with exclusive promo code"));
                continue;
            }

            var validation = await ValidatePromoCodeAsync(
                codeString,
                remainingAmount,
                productId,
                userId,
                cancellationToken).ConfigureAwait(false);

            if (!validation.IsValid)
            {
                rejectedCodes.Add(new RejectedPromoCode(codeString, validation.ErrorMessage ?? "Invalid code"));
                continue;
            }

            var promoCode = await promoCodeRepository.GetByCodeAsync(codeString, cancellationToken)
                .ConfigureAwait(false);

            if (promoCode == null)
            {
                rejectedCodes.Add(new RejectedPromoCode(codeString, "Code not found"));
                continue;
            }

            if (promoCode.IsExclusive)
            {
                hasExclusiveCode = true;
            }

            var discountAmount = promoCode.CalculateDiscount(remainingAmount);

            appliedCodes.Add(new AppliedPromoCode(
                codeString,
                discountAmount,
                promoCode.DiscountPercentage
            ));

            totalDiscount += discountAmount;
            remainingAmount = Math.Max(0, remainingAmount - discountAmount);
        }

        return new PromoCodeApplicationResult(
            OriginalAmount: orderAmount,
            FinalAmount: Math.Max(0, orderAmount - totalDiscount),
            TotalDiscount: totalDiscount,
            AppliedCodes: appliedCodes,
            RejectedCodes: rejectedCodes
        );
    }

    /// <inheritdoc />
    public async Task<PromoCodeValidationResult> ValidatePromoCodeAsync(
        string code,
        decimal orderAmount,
        Guid? productId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new PromoCodeValidationResult(false, ErrorMessage: "Promo code is required");
        }

        var promoCode = await promoCodeRepository.GetByCodeAsync(code, cancellationToken)
            .ConfigureAwait(false);

        if (promoCode == null)
        {
            return new PromoCodeValidationResult(false, ErrorMessage: "Promo code not found");
        }

        // Check if active
        if (!promoCode.IsActive)
        {
            return new PromoCodeValidationResult(false, ErrorMessage: "Promo code is not active");
        }

        // Check validity period
        if (!promoCode.IsCurrentlyValid())
        {
            return new PromoCodeValidationResult(false, ErrorMessage: "Promo code has expired or is not yet valid");
        }

        // Check product restriction
        if (promoCode.ProductId.HasValue && productId.HasValue && promoCode.ProductId != productId)
        {
            return new PromoCodeValidationResult(false, ErrorMessage: "Promo code is not valid for this product");
        }

        // Check minimum order amount
        if (promoCode.MinimumOrderAmount.HasValue && orderAmount < promoCode.MinimumOrderAmount.Value)
        {
            return new PromoCodeValidationResult(false, ErrorMessage: $"Minimum order amount of {promoCode.MinimumOrderAmount.Value:C} required");
        }

        // Check usage limits
        if (promoCode.MaxUses.HasValue)
        {
            var usageCount = await promoCodeRepository.GetUsageCountAsync(promoCode.Id, cancellationToken)
                .ConfigureAwait(false);
            if (usageCount >= promoCode.MaxUses.Value)
            {
                return new PromoCodeValidationResult(false, ErrorMessage: "Promo code has reached maximum uses");
            }
        }

        // Check per-user usage limits
        if (promoCode.MaxUsesPerUser.HasValue && userId.HasValue)
        {
            var userUsageCount = await promoCodeRepository.GetUserUsageCountAsync(
                promoCode.Id, userId.Value, cancellationToken).ConfigureAwait(false);
            if (userUsageCount >= promoCode.MaxUsesPerUser.Value)
            {
                return new PromoCodeValidationResult(false,
                    ErrorMessage: "You have already used this promo code the maximum number of times");
            }
        }

        // Calculate discount
        var discountAmount = promoCode.CalculateDiscount(orderAmount);

        return new PromoCodeValidationResult(
            IsValid: true,
            Code: promoCode.Code,
            DiscountAmount: discountAmount,
            DiscountPercentage: promoCode.DiscountPercentage
        );
    }

    /// <inheritdoc />
    public async Task<decimal> GetCurrentPriceAsync(
        Guid productId,
        Guid? pricingId = null,
        CancellationToken cancellationToken = default)
    {
        var product = await productRepository.GetByIdAsync(
            productId,
            cancellationToken,
            includePricing: true).ConfigureAwait(false);

        if (product == null)
        {
            throw new ProductNotFoundException(productId);
        }

        var pricing = pricingId.HasValue
            ? product.Pricing.FirstOrDefault(p => p.Id == pricingId.Value)
            : product.Pricing.FirstOrDefault(p => p.IsDefault) ?? product.Pricing.FirstOrDefault();

        if (pricing == null)
        {
            return 0;
        }

        return IsSaleActive(pricing) && pricing.SalePrice.HasValue
            ? pricing.SalePrice.Value
            : pricing.BasePrice;
    }

    /// <inheritdoc />
    public bool IsSaleActive(ProductPricing pricing)
    {
        if (!pricing.SalePrice.HasValue)
        {
            return false;
        }

        var now = SystemClock.UtcNow;

        var startValid = !pricing.SaleStartDate.HasValue || pricing.SaleStartDate.Value <= now;
        var endValid = !pricing.SaleEndDate.HasValue || pricing.SaleEndDate.Value > now;

        return startValid && endValid;
    }

    /// <summary>
    /// Resolves the highest-priority applicable pricing rule for the product and computes its
    /// per-unit discount on the effective (base or sale) unit price. Rules are cached per
    /// product; the cache key embeds a change stamp (rule count + max version + max updated
    /// ticks), so any create/update/activate/deactivate/delete of a product-scoped or global
    /// rule immediately produces a fresh key and invalidates the cached set.
    /// </summary>
    private async Task<(PricingRule? Rule, decimal UnitDiscount)> ResolvePricingRuleAsync(
        Guid productId,
        decimal effectiveUnitPrice,
        int quantity,
        string? customerSegment,
        CancellationToken cancellationToken)
    {
        var rules = await GetApplicableRulesCachedAsync(productId, cancellationToken).ConfigureAwait(false);
        if (rules.Count == 0)
        {
            return (null, 0m);
        }

        var now = SystemClock.UtcNow;

        foreach (var rule in rules) // ordered by priority (highest first)
        {
            if (!rule.IsApplicable(now))
            {
                continue; // not active or outside its date window
            }

            if (!rule.AppliesToQuantity(quantity))
            {
                continue; // quantity outside the rule's [min, max] window
            }

            if (!MatchesCustomerSegment(rule, customerSegment))
            {
                continue; // segment-scoped rule for a different segment
            }

            if (!MatchesTimeWindow(rule, now))
            {
                continue; // TimeBased rule outside its daily window / days of week
            }

            var unitPrice = GetRuleUnitPrice(rule, effectiveUnitPrice, quantity);
            var unitDiscount = Math.Max(0, effectiveUnitPrice - unitPrice);

            return (rule, unitDiscount);
        }

        return (null, 0m);
    }

    private async Task<IReadOnlyList<PricingRule>> GetApplicableRulesCachedAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var stamp = await pricingRuleRepository.GetChangeStampAsync(productId, cancellationToken).ConfigureAwait(false);
        var cacheKey = $"{RulesCacheKeyPrefix}{productId:N}:{stamp}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<PricingRule>? cached) && cached is not null)
        {
            return cached;
        }

        // Guard against a repository (or test double) returning null instead of an empty
        // list: a null rule set must be treated as "no rules apply", never propagated to
        // callers that iterate it, and never cached as a null entry.
        var rules = await pricingRuleRepository.GetActiveRulesForProductAsync(productId, cancellationToken)
            .ConfigureAwait(false)
            ?? Array.Empty<PricingRule>();

        // The platform memory cache is registered with a SizeLimit; every entry
        // must declare its size or the Set call throws at runtime.
        cache.Set(cacheKey, rules, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = RulesCacheDuration,
            Size = 1
        });

        return rules;
    }

    /// <summary>
    /// Computes the per-unit price after applying the rule. Tier-backed rules
    /// (<see cref="PricingRuleType.VolumeDiscount"/>, <see cref="PricingRuleType.TieredPricing"/>)
    /// resolve their applicable <see cref="PricingRuleTier"/> via the entity's own tier logic;
    /// every result is capped at the effective price so a rule can never raise a price.
    /// </summary>
    private static decimal GetRuleUnitPrice(PricingRule rule, decimal effectiveUnitPrice, int quantity)
    {
        // Tier-aware total discount for the whole quantity (reuses the entity's tier resolution).
        var tierTotalDiscount = rule.CalculateDiscount(effectiveUnitPrice, quantity);
        var priceAfterDiscount = quantity > 0
            ? Math.Max(0, effectiveUnitPrice - tierTotalDiscount / quantity)
            : effectiveUnitPrice;

        // Rule-level fallbacks (fixed price override, rule-level percentage, ...).
        var priceAfterOverride = Math.Min(effectiveUnitPrice, rule.CalculatePrice(effectiveUnitPrice, quantity));

        return Math.Min(priceAfterDiscount, priceAfterOverride);
    }

    /// <summary>
    /// A rule restricted to a customer segment only applies when the requested segment matches
    /// (case-insensitive). Rules without a segment restriction apply to every customer.
    /// </summary>
    private static bool MatchesCustomerSegment(PricingRule rule, string? customerSegment)
    {
        if (string.IsNullOrWhiteSpace(rule.CustomerSegment))
        {
            return true;
        }

        return string.Equals(rule.CustomerSegment.Trim(), customerSegment?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TimeBased rules may restrict applicability to a daily HH:MM window and/or days of week.
    /// Window boundaries that fail to parse are ignored (the date window on the rule still applies).
    /// </summary>
    private static bool MatchesTimeWindow(PricingRule rule, DateTime nowUtc)
    {
        if (rule.RuleType != PricingRuleType.TimeBased)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(rule.DaysOfWeek))
        {
            var days = rule.DaysOfWeek
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(dayText => int.TryParse(dayText, out var day) ? day : -1)
                .Where(day => day is >= 0 and <= 6)
                .ToHashSet();

            if (days.Count > 0 && !days.Contains((int)nowUtc.DayOfWeek))
            {
                return false;
            }
        }

        if (TimeSpan.TryParse(rule.TimeStart, out var start) &&
            TimeSpan.TryParse(rule.TimeEnd, out var end))
        {
            var timeOfDay = nowUtc.TimeOfDay;

            // Windows may wrap midnight (e.g. 22:00-02:00).
            if (start <= end)
            {
                if (timeOfDay < start || timeOfDay >= end)
                {
                    return false;
                }
            }
            else if (timeOfDay < start && timeOfDay >= end)
            {
                return false;
            }
        }

        return true;
    }
}

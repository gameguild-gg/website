using GameGuild.Commerce.Payments;

namespace GameGuild.Commerce.Products;

/// <summary>
///     <see cref="IPromoDiscountCalculator"/> implementation (issue #417) registered by the
///     Products module: delegates to the promo engine
///     (<see cref="IPricingEngineService.ApplyPromoCodesAsync"/> — code validation, user/
///     product/date eligibility, stacking rules, conflict strategies, and caps from #795)
///     and maps the outcome into the unified pricing engine's discount semantics, so plan
///     calculations apply real promo codes instead of parsing code strings.
/// </summary>
public sealed class PromoDiscountCalculatorAdapter(IPricingEngineService pricingEngineService) : IPromoDiscountCalculator
{
    private readonly IPricingEngineService _pricingEngineService =
        pricingEngineService ?? throw new ArgumentNullException(nameof(pricingEngineService));

    /// <inheritdoc />
    public async Task<PromoDiscountResult> ApplyAsync(PromoDiscountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _pricingEngineService
            .ApplyPromoCodesAsync(request.OrderAmount, request.PromoCodes, productId: null, request.UserId, planId: null, cancellationToken)
            .ConfigureAwait(false);

        var applied = result.AppliedCodes
            .Select((code, index) => new AppliedPromoDiscount(
                Code: code.Code,
                Description: Describe(code),
                Amount: code.DiscountAmount,
                Percentage: code.DiscountPercentage,
                Type: MapType(code.Type),
                Priority: 0, // application order is authoritative; priority lives in the promo engine's resolution
                IsStackable: result.AppliedCodes.Count > 1,
                Sequence: index + 1))
            .ToList();

        var rejected = result.RejectedCodes
            .Select(code => new RejectedPromoDiscount(code.Code, code.Reason))
            .ToList();

        return new PromoDiscountResult(
            result.TotalDiscount,
            applied,
            rejected,
            result.StackingRuleName);
    }

    private static string Describe(AppliedPromoCode code)
    {
        return code.Type switch
        {
            PromoCodeType.PercentageOff when code.DiscountPercentage.HasValue => $"{code.DiscountPercentage.Value:0.##}% off",
            PromoCodeType.FixedAmountOff => $"{code.DiscountAmount:0.00} off",
            PromoCodeType.FreeMonths => $"{code.FreeMonthsGranted ?? 0} free month(s)",
            PromoCodeType.FreeTrialExtension => $"{code.TrialExtensionDays ?? 0} trial day(s) extended",
            _ => $"{code.Code} discount"
        };
    }

    private static DiscountType MapType(PromoCodeType? type)
    {
        return type switch
        {
            PromoCodeType.PercentageOff => DiscountType.Percentage,
            PromoCodeType.FixedAmountOff => DiscountType.FixedAmount,
            PromoCodeType.FreeMonths => DiscountType.FreeMonths,
            PromoCodeType.FreeTrial => DiscountType.TrialExtension,
            PromoCodeType.FreeTrialExtension => DiscountType.TrialExtension,
            PromoCodeType.SetPrice => DiscountType.SetPrice,
            _ => DiscountType.Percentage
        };
    }
}

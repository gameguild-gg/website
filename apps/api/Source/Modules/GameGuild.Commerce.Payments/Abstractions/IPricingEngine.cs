namespace GameGuild.Commerce.Payments;

/// <summary>
///     The centralized pricing engine (issue #417). Every plan-level price calculation —
///     base plan, add-ons, usage tiers, promo discounts, proration, tax, credits, currency —
///     flows through this single path so the outcome is identical regardless of the caller
///     (REST calculate, subscription plan preview, renewal quoting).
/// </summary>
/// <remarks>
///     The engine composes the landed building blocks instead of reimplementing them:
///     plan prices via <see cref="IPlanPricingResolver"/>, promo stacking via
///     <see cref="IPromoDiscountCalculator"/> (the Products promo engine), proration via
///     <see cref="IPlanProrationCalculator"/> (the Subscriptions BillingCalculator), tax via
///     <see cref="ITaxCalculationService"/>, credits via <see cref="IWalletService"/>,
///     currency via the SharedKernel FX core, and every recorded decision lands in the
///     pricing-decision ledger (issue #400).
/// </remarks>
public interface IPricingEngine
{
    /// <summary>
    ///     Calculates a full price for the request: base (plan or custom amount) + add-ons +
    ///     usage charges → promo discounts → proration → tax → credits, with line items,
    ///     an explanation, self-validation invariants, and (optionally) a ledger record.
    /// </summary>
    /// <param name="request">Calculation inputs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown (fail-closed) when neither <see cref="PricingCalculationRequest.PlanId"/> nor
    ///     <see cref="PricingCalculationRequest.BaseAmount"/> is supplied, when the plan does
    ///     not exist, or when a cross-currency conversion lacks an FX quotation.
    /// </exception>
    Task<PricingCalculationResult> CalculateAsync(
        PricingCalculationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Previews a plan change (issue #417 price preview): the immediate prorated charge
    ///     and the next-renewal amount, without recording a ledger decision or consuming credits.
    /// </summary>
    /// <param name="request">Preview inputs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<PricingPreviewResult> PreviewAsync(
        PricingPreviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the plan price for a billing interval in the requested currency
    /// (FX-converted through the SharedKernel core, cached per plan/interval/currency).
    /// </summary>
    /// <param name="planId">Plan identifier</param>
    /// <param name="billingCycle">Billing interval</param>
    /// <param name="currency">Target ISO 4217 currency</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The plan price in <paramref name="currency"/>, or null when the plan does not exist.</returns>
    Task<Money?> GetPlanPriceAsync(
        Guid planId,
        BillingCycle billingCycle,
        string currency,
        CancellationToken cancellationToken = default);
}

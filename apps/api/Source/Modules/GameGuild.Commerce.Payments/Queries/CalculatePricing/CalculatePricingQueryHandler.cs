using GameGuild.CQRS;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for CalculatePricingQuery (issue #417): delegates to the unified
///     <see cref="IPricingEngine"/> so the subscription-plan pricing endpoint returns the
///     same numbers every other pricing surface returns. The historical stub — which parsed
///     "SAVE20"-style code strings and hard-coded add-ons and tax to zero — is retired.
/// </summary>
/// <remarks>
///     This is a lightweight read view: it records no ledger decision and consumes no
///     credits. Use <c>POST /api/pricing/:calculate</c> for the full, audited calculation.
/// </remarks>
public sealed class CalculatePricingQueryHandler(IPricingEngine pricingEngine) : IQueryHandler<CalculatePricingQuery, PricingCalculationResult>
{
    private readonly IPricingEngine _pricingEngine = pricingEngine ?? throw new ArgumentNullException(nameof(pricingEngine));

    public async Task<PricingCalculationResult> Handle(CalculatePricingQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<string>? discountCodes = string.IsNullOrWhiteSpace(request.DiscountCode)
            ? null
            : new[] { request.DiscountCode.Trim() };

        return await _pricingEngine.CalculateAsync(
            new PricingCalculationRequest(
                PlanId: request.PlanId,
                TenantId: request.TenantId,
                PromoCodes: discountCodes,
                // Read view: no tax without inputs, no credit spend, no ledger write.
                IncludeTax: false,
                ApplyCredits: false,
                RecordDecision: false),
            cancellationToken).ConfigureAwait(false);
    }
}

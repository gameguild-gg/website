using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Handler for calculating product price
/// </summary>
public sealed class CalculateProductPriceQueryHandler(IPricingEngineService pricingEngine)
    : IQueryHandler<CalculateProductPriceQuery, PricingCalculationResult>
{
    public async Task<PricingCalculationResult> Handle(
        CalculateProductPriceQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await pricingEngine.CalculatePriceByIdAsync(
            request.ProductId,
            request.PricingId,
            request.PromoCodes,
            request.UserId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}

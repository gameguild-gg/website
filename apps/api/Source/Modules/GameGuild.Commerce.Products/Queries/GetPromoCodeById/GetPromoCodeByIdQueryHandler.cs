using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Handler for getting a promo code by ID
/// </summary>
public sealed class GetPromoCodeByIdQueryHandler(IPromoCodeRepository promoCodeRepository)
    : IQueryHandler<GetPromoCodeByIdQuery, PromoCodeDto?>
{
    public async Task<PromoCodeDto?> Handle(GetPromoCodeByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var promoCode = await promoCodeRepository.GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (promoCode == null)
        {
            return null;
        }

        var usageCount = await promoCodeRepository.GetUsageCountAsync(promoCode.Id, cancellationToken)
            .ConfigureAwait(false);

        return promoCode.ToDto(usageCount);
    }
}

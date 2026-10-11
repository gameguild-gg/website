using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Handler for getting active promo codes
/// </summary>
public sealed class GetActivePromoCodesQueryHandler(IPromoCodeRepository promoCodeRepository)
    : IQueryHandler<GetActivePromoCodesQuery, IReadOnlyList<PromoCodeDto>>
{
    public async Task<IReadOnlyList<PromoCodeDto>> Handle(
        GetActivePromoCodesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var promoCodes = request.ProductId.HasValue
            ? await promoCodeRepository.GetByProductIdAsync(request.ProductId.Value, cancellationToken)
                .ConfigureAwait(false)
            : await promoCodeRepository.GetActiveCodesAsync(cancellationToken)
                .ConfigureAwait(false);

        var dtos = new List<PromoCodeDto>();

        foreach (var promoCode in promoCodes)
        {
            var usageCount = await promoCodeRepository.GetUsageCountAsync(promoCode.Id, cancellationToken)
                .ConfigureAwait(false);

            dtos.Add(promoCode.ToDto(usageCount));
        }

        return dtos;
    }
}

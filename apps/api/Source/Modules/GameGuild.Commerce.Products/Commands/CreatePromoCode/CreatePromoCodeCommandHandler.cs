using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Handler for creating a new promo code
/// </summary>
public sealed class CreatePromoCodeCommandHandler(IPromoCodeRepository promoCodeRepository)
    : ICommandHandler<CreatePromoCodeCommand, PromoCodeDto>
{
    public async Task<PromoCodeDto> Handle(CreatePromoCodeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check if code already exists
        if (await promoCodeRepository.CodeExistsAsync(request.Code, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Promo code '{request.Code}' already exists");
        }

        // Create new promo code entity (code is normalized to the stored uppercase form)
        var promoCode = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = PromoCodeNormalizer.Normalize(request.Code) ?? request.Code,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            DiscountPercentage = request.DiscountPercentage,
            DiscountAmount = request.DiscountAmount,
            MaxDiscountAmount = request.MaxDiscountAmount,
            Currency = request.Currency,
            MinimumOrderAmount = request.MinimumOrderAmount,
            MaxUses = request.MaxUses,
            MaxUsesPerUser = request.MaxUsesPerUser,
            FreeTrialExtensionDays = request.FreeTrialExtensionDays,
            DurationMonths = request.DurationMonths,
            ApplyToRenewal = request.ApplyToRenewal,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            IsActive = request.IsActive,
            IsExclusive = request.IsExclusive,
            StackingPriority = request.StackingPriority,
            CampaignId = request.CampaignId,
            ProductId = request.ProductId,
            PlanId = request.PlanId,
            CreatedBy = request.CreatedBy ?? Guid.Empty
        };

        // Add to repository
        await promoCodeRepository.AddAsync(promoCode, cancellationToken).ConfigureAwait(false);
        await promoCodeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO
        return promoCode.ToDto(0);
    }
}

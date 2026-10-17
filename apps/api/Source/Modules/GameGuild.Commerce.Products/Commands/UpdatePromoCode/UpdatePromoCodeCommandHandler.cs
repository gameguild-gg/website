using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Handler for updating an existing promo code
/// </summary>
public sealed class UpdatePromoCodeCommandHandler(IPromoCodeRepository promoCodeRepository)
    : ICommandHandler<UpdatePromoCodeCommand, PromoCodeDto>
{
    public async Task<PromoCodeDto> Handle(UpdatePromoCodeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var promoCode = await promoCodeRepository.GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (promoCode == null)
        {
            throw new PromoCodeNotFoundException(request.Id);
        }

        // Update properties if provided
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            promoCode.Name = request.Name;
        }

        if (request.Description != null)
        {
            promoCode.Description = request.Description;
        }

        if (request.Type.HasValue)
        {
            promoCode.Type = request.Type.Value;
        }

        if (request.DiscountPercentage.HasValue)
        {
            promoCode.DiscountPercentage = request.DiscountPercentage.Value;
        }

        if (request.DiscountAmount.HasValue)
        {
            promoCode.DiscountAmount = request.DiscountAmount.Value;
        }

        if (request.MaxDiscountAmount.HasValue)
        {
            promoCode.MaxDiscountAmount = request.MaxDiscountAmount.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            promoCode.Currency = request.Currency;
        }

        if (request.MinimumOrderAmount.HasValue)
        {
            promoCode.MinimumOrderAmount = request.MinimumOrderAmount.Value;
        }

        if (request.MaxUses.HasValue)
        {
            promoCode.MaxUses = request.MaxUses.Value;
        }

        if (request.MaxUsesPerUser.HasValue)
        {
            promoCode.MaxUsesPerUser = request.MaxUsesPerUser.Value;
        }

        if (request.FreeTrialExtensionDays.HasValue)
        {
            promoCode.FreeTrialExtensionDays = request.FreeTrialExtensionDays.Value;
        }

        if (request.DurationMonths.HasValue)
        {
            promoCode.DurationMonths = request.DurationMonths.Value;
        }

        if (request.ApplyToRenewal.HasValue)
        {
            promoCode.ApplyToRenewal = request.ApplyToRenewal.Value;
        }

        if (request.ValidFrom.HasValue)
        {
            promoCode.ValidFrom = request.ValidFrom.Value;
        }

        if (request.ValidUntil.HasValue)
        {
            promoCode.ValidUntil = request.ValidUntil.Value;
        }

        if (request.IsActive.HasValue)
        {
            promoCode.IsActive = request.IsActive.Value;
        }

        if (request.IsExclusive.HasValue)
        {
            promoCode.IsExclusive = request.IsExclusive.Value;
        }

        if (request.StackingPriority.HasValue)
        {
            promoCode.StackingPriority = request.StackingPriority.Value;
        }

        if (request.CampaignId != null)
        {
            promoCode.CampaignId = string.IsNullOrWhiteSpace(request.CampaignId) ? null : request.CampaignId;
        }

        if (request.ProductId.HasValue)
        {
            promoCode.ProductId = request.ProductId.Value;
        }

        promoCode.Touch();

        // Update in repository
        await promoCodeRepository.UpdateAsync(promoCode, cancellationToken).ConfigureAwait(false);
        await promoCodeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Get usage count
        var usageCount = await promoCodeRepository.GetUsageCountAsync(promoCode.Id, cancellationToken)
            .ConfigureAwait(false);

        // Map to DTO
        return promoCode.ToDto(usageCount);
    }
}

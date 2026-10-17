using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to activate a promo code
/// </summary>
/// <param name="PromoCodeId">Promo code ID</param>
public sealed record ActivatePromoCodeCommand(Guid PromoCodeId) : ICommand<PromoCodeDto>;

/// <summary>
/// Handler for ActivatePromoCodeCommand
/// </summary>
public sealed class ActivatePromoCodeHandler(IPromoCodeRepository repository) : ICommandHandler<ActivatePromoCodeCommand, PromoCodeDto>
{
    public async Task<PromoCodeDto> Handle(ActivatePromoCodeCommand request, CancellationToken cancellationToken)
    {
        var promoCode = await repository.GetByIdAsync(request.PromoCodeId, cancellationToken).ConfigureAwait(false);
        if (promoCode == null)
        {
            throw new PromoCodeNotFoundException(request.PromoCodeId);
        }

        promoCode.IsActive = true;
        promoCode.Touch();
        
        await repository.UpdateAsync(promoCode, cancellationToken).ConfigureAwait(false);
        return promoCode.ToDto();
    }
}

/// <summary>
/// Command to deactivate a promo code
/// </summary>
/// <param name="PromoCodeId">Promo code ID</param>
public sealed record DeactivatePromoCodeCommand(Guid PromoCodeId) : ICommand<PromoCodeDto>;

/// <summary>
/// Handler for DeactivatePromoCodeCommand
/// </summary>
public sealed class DeactivatePromoCodeHandler(IPromoCodeRepository repository) : ICommandHandler<DeactivatePromoCodeCommand, PromoCodeDto>
{
    public async Task<PromoCodeDto> Handle(DeactivatePromoCodeCommand request, CancellationToken cancellationToken)
    {
        var promoCode = await repository.GetByIdAsync(request.PromoCodeId, cancellationToken).ConfigureAwait(false);
        if (promoCode == null)
        {
            throw new PromoCodeNotFoundException(request.PromoCodeId);
        }

        promoCode.IsActive = false;
        promoCode.Touch();
        
        await repository.UpdateAsync(promoCode, cancellationToken).ConfigureAwait(false);
        return promoCode.ToDto();
    }
}

/// <summary>
/// Command to patch (partial update) a promo code
/// </summary>
/// <param name="PromoCodeId">Promo code ID</param>
/// <param name="Name">Optional new name</param>
/// <param name="Description">Optional new description</param>
/// <param name="Type">Optional new type</param>
/// <param name="DiscountPercentage">Optional discount percentage</param>
/// <param name="DiscountAmount">Optional discount amount (target price for SetPrice)</param>
/// <param name="MaxDiscountAmount">Optional cap for percentage discounts</param>
/// <param name="Currency">Optional currency</param>
/// <param name="MinimumOrderAmount">Optional minimum order</param>
/// <param name="MaxUses">Optional max uses</param>
/// <param name="MaxUsesPerUser">Optional max uses per user</param>
/// <param name="FreeTrialExtensionDays">Optional trial extension days</param>
/// <param name="DurationMonths">Optional free months / discount duration</param>
/// <param name="ApplyToRenewal">Optional apply-to-renewal flag</param>
/// <param name="ValidFrom">Optional valid from date</param>
/// <param name="ValidUntil">Optional valid until date</param>
/// <param name="IsActive">Optional active flag</param>
/// <param name="IsExclusive">Optional exclusive flag</param>
/// <param name="StackingPriority">Optional stacking priority</param>
/// <param name="CampaignId">Optional campaign identifier</param>
/// <param name="ProductId">Optional product ID</param>
public sealed record PatchPromoCodeCommand(
    Guid PromoCodeId,
    string? Name = null,
    string? Description = null,
    PromoCodeType? Type = null,
    decimal? DiscountPercentage = null,
    decimal? DiscountAmount = null,
    decimal? MaxDiscountAmount = null,
    string? Currency = null,
    decimal? MinimumOrderAmount = null,
    int? MaxUses = null,
    int? MaxUsesPerUser = null,
    int? FreeTrialExtensionDays = null,
    int? DurationMonths = null,
    bool? ApplyToRenewal = null,
    DateTime? ValidFrom = null,
    DateTime? ValidUntil = null,
    bool? IsActive = null,
    bool? IsExclusive = null,
    int? StackingPriority = null,
    string? CampaignId = null,
    Guid? ProductId = null
) : ICommand<PromoCodeDto>;

/// <summary>
/// Handler for PatchPromoCodeCommand
/// </summary>
public sealed class PatchPromoCodeHandler(IPromoCodeRepository repository) : ICommandHandler<PatchPromoCodeCommand, PromoCodeDto>
{
    public async Task<PromoCodeDto> Handle(PatchPromoCodeCommand request, CancellationToken cancellationToken)
    {
        var promoCode = await repository.GetByIdAsync(request.PromoCodeId, cancellationToken).ConfigureAwait(false);
        if (promoCode == null)
        {
            throw new PromoCodeNotFoundException(request.PromoCodeId);
        }

        // Apply only non-null values (partial update)
        if (request.Name != null)
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
            promoCode.DiscountPercentage = request.DiscountPercentage;
        }
        if (request.DiscountAmount.HasValue)
        {
            promoCode.DiscountAmount = request.DiscountAmount;
        }
        if (request.MaxDiscountAmount.HasValue)
        {
            promoCode.MaxDiscountAmount = request.MaxDiscountAmount;
        }
        if (request.Currency != null)
        {
            promoCode.Currency = request.Currency;
        }
        if (request.MinimumOrderAmount.HasValue)
        {
            promoCode.MinimumOrderAmount = request.MinimumOrderAmount;
        }
        if (request.MaxUses.HasValue)
        {
            promoCode.MaxUses = request.MaxUses;
        }
        if (request.MaxUsesPerUser.HasValue)
        {
            promoCode.MaxUsesPerUser = request.MaxUsesPerUser;
        }
        if (request.FreeTrialExtensionDays.HasValue)
        {
            promoCode.FreeTrialExtensionDays = request.FreeTrialExtensionDays;
        }
        if (request.DurationMonths.HasValue)
        {
            promoCode.DurationMonths = request.DurationMonths;
        }
        if (request.ApplyToRenewal.HasValue)
        {
            promoCode.ApplyToRenewal = request.ApplyToRenewal.Value;
        }
        if (request.ValidFrom.HasValue)
        {
            promoCode.ValidFrom = request.ValidFrom;
        }
        if (request.ValidUntil.HasValue)
        {
            promoCode.ValidUntil = request.ValidUntil;
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
            promoCode.ProductId = request.ProductId;
        }
        
        promoCode.Touch();
        
        await repository.UpdateAsync(promoCode, cancellationToken).ConfigureAwait(false);
        return promoCode.ToDto();
    }
}

/// <summary>
/// Query to check if a promo code exists
/// </summary>
/// <param name="PromoCodeId">Promo code ID</param>
public sealed record PromoCodeExistsQuery(Guid PromoCodeId) : IQuery<bool>;

/// <summary>
/// Handler for PromoCodeExistsQuery
/// </summary>
public sealed class PromoCodeExistsHandler(IPromoCodeRepository repository) : IQueryHandler<PromoCodeExistsQuery, bool>
{
    public async Task<bool> Handle(PromoCodeExistsQuery request, CancellationToken cancellationToken)
    {
        var promoCode = await repository.GetByIdAsync(request.PromoCodeId, cancellationToken).ConfigureAwait(false);
        return promoCode != null;
    }
}

/// <summary>
/// Query to get a promo code by its code string
/// </summary>
/// <param name="Code">Promo code string</param>
public sealed record GetPromoCodeByCodeQuery(string Code) : IQuery<PromoCodeDto?>;

/// <summary>
/// Handler for GetPromoCodeByCodeQuery
/// </summary>
public sealed class GetPromoCodeByCodeHandler(IPromoCodeRepository repository) : IQueryHandler<GetPromoCodeByCodeQuery, PromoCodeDto?>
{
    public async Task<PromoCodeDto?> Handle(GetPromoCodeByCodeQuery request, CancellationToken cancellationToken)
    {
        var promoCode = await repository.GetByCodeAsync(request.Code, cancellationToken).ConfigureAwait(false);
        return promoCode?.ToDto();
    }
}

/// <summary>
/// Query to get usage statistics for a promo code
/// </summary>
/// <param name="PromoCodeId">Promo code ID</param>
public sealed record GetPromoCodeUsageQuery(Guid PromoCodeId) : IQuery<PromoCodeUsageDto>;

/// <summary>
/// Handler for GetPromoCodeUsageQuery
/// </summary>
public sealed class GetPromoCodeUsageHandler(IPromoCodeRepository repository) : IQueryHandler<GetPromoCodeUsageQuery, PromoCodeUsageDto>
{
    public async Task<PromoCodeUsageDto> Handle(GetPromoCodeUsageQuery request, CancellationToken cancellationToken)
    {
        var stats = await repository.GetUsageStatsAsync(request.PromoCodeId, cancellationToken).ConfigureAwait(false);
        return stats;
    }
}

/// <summary>
/// Promo code usage statistics DTO
/// </summary>
/// <param name="PromoCodeId">Promo code ID</param>
/// <param name="Code">Promo code string</param>
/// <param name="TotalUses">Total number of times the code has been used</param>
/// <param name="UniqueUsers">Number of unique users who used the code</param>
/// <param name="TotalDiscountGiven">Total discount amount given</param>
/// <param name="AverageDiscountPerUse">Average discount per use</param>
/// <param name="MaxUses">Maximum allowed uses (null = unlimited)</param>
/// <param name="RemainingUses">Remaining uses (null = unlimited)</param>
/// <param name="FirstUsedAt">First usage timestamp</param>
/// <param name="LastUsedAt">Last usage timestamp</param>
public sealed record PromoCodeUsageDto(
    Guid PromoCodeId,
    string Code,
    int TotalUses,
    int UniqueUsers,
    decimal TotalDiscountGiven,
    decimal AverageDiscountPerUse,
    int? MaxUses,
    int? RemainingUses,
    DateTime? FirstUsedAt,
    DateTime? LastUsedAt
);

using FluentValidation;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Validator for GenerateBulkPromoCodesCommand
/// </summary>
public sealed class GenerateBulkPromoCodesCommandValidator : AbstractValidator<GenerateBulkPromoCodesCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateBulkPromoCodesCommandValidator"/> class.
    /// </summary>
    public GenerateBulkPromoCodesCommandValidator()
    {
        RuleFor(x => x.Count)
            .InclusiveBetween(1, GenerateBulkPromoCodesCommandHandler.MaxCount)
            .WithMessage($"Count must be between 1 and {GenerateBulkPromoCodesCommandHandler.MaxCount}.");

        // Prefix is validated on its normalized form (trim + uppercase), mirroring the create validator.
        RuleFor(x => x.Prefix)
            .NotEmpty()
            .WithMessage("Prefix is required.")
            .Must(p => PromoCodeNormalizer.IsValidFormat($"{PromoCodeNormalizer.Normalize(p)}-X"))
            .WithMessage("Prefix must be 2-40 uppercase letters or digits (normalized from lowercase input); a hyphenated suffix is appended.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(200)
            .WithMessage("Base name cannot exceed 200 characters (a numbered suffix is appended).");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description cannot exceed 1000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.DiscountPercentage)
            .InclusiveBetween(1m, 100m)
            .WithMessage("Discount percentage must be between 1 and 100.")
            .When(x => x.Type == PromoCodeType.PercentageOff);

        RuleFor(x => x.DiscountPercentage)
            .NotNull()
            .WithMessage("Discount percentage is required for percentage-based discounts.")
            .When(x => x.Type == PromoCodeType.PercentageOff);

        RuleFor(x => x.DiscountAmount)
            .NotNull()
            .WithMessage("Discount amount is required for fixed amount discounts.")
            .When(x => x.Type is PromoCodeType.FixedAmountOff or PromoCodeType.SetPrice);

        RuleFor(x => x.DiscountAmount)
            .GreaterThan(0)
            .WithMessage("Discount amount must be greater than 0.")
            .When(x => x.Type is PromoCodeType.FixedAmountOff or PromoCodeType.SetPrice && x.DiscountAmount.HasValue);

        RuleFor(x => x.FreeTrialExtensionDays)
            .NotNull()
            .WithMessage("Trial extension days are required for free trial extension codes.")
            .When(x => x.Type == PromoCodeType.FreeTrialExtension);

        RuleFor(x => x.FreeTrialExtensionDays)
            .InclusiveBetween(1, 365)
            .WithMessage("Trial extension days must be between 1 and 365.")
            .When(x => x.Type == PromoCodeType.FreeTrialExtension && x.FreeTrialExtensionDays.HasValue);

        RuleFor(x => x.DurationMonths)
            .NotNull()
            .WithMessage("Duration months are required for free months codes.")
            .When(x => x.Type == PromoCodeType.FreeMonths);

        RuleFor(x => x.DurationMonths)
            .InclusiveBetween(1, 36)
            .WithMessage("Duration months must be between 1 and 36.")
            .When(x => x.Type == PromoCodeType.FreeMonths && x.DurationMonths.HasValue);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Length(3)
            .WithMessage("Currency code must be exactly 3 characters.");

        RuleFor(x => x.MaxUses)
            .GreaterThan(0)
            .WithMessage("Maximum uses must be greater than 0.")
            .When(x => x.MaxUses.HasValue);

        RuleFor(x => x.MaxUsesPerUser)
            .GreaterThan(0)
            .WithMessage("Maximum uses per user must be greater than 0.")
            .When(x => x.MaxUsesPerUser.HasValue);

        RuleFor(x => x.ValidUntil)
            .GreaterThan(x => x.ValidFrom)
            .WithMessage("Valid until date must be after valid from date.")
            .When(x => x.ValidFrom.HasValue && x.ValidUntil.HasValue);

        RuleFor(x => x.StackingPriority)
            .InclusiveBetween(0, 100)
            .WithMessage("Stacking priority must be between 0 and 100.");

        RuleFor(x => x.CampaignId)
            .MaximumLength(100)
            .WithMessage("Campaign identifier cannot exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.CampaignId));
    }
}

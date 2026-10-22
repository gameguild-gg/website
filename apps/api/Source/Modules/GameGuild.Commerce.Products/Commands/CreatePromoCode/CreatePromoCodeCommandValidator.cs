using FluentValidation;
using GameGuild.Commerce.Subscriptions;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Validator for CreatePromoCodeCommand. Codes are validated on their normalized
/// form (trim + uppercase) so lowercase input is accepted and normalized, while the
/// stored format must match the pattern <c>^[A-Z0-9-]{3,50}$</c> (no underscores).
/// A plan-scoped code (issue #418) must reference an existing subscription plan.
/// </summary>
public sealed class CreatePromoCodeCommandValidator : AbstractValidator<CreatePromoCodeCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromoCodeCommandValidator"/> class.
    /// The plan repository is optional: validators constructed without it (unit tests)
    /// skip the plan-existence persistence check.
    /// </summary>
    public CreatePromoCodeCommandValidator(ISubscriptionPlanRepository? planRepository = null)
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Promo code is required.")
            .Must(c => PromoCodeNormalizer.IsValidFormat(PromoCodeNormalizer.Normalize(c)))
            .WithMessage("Promo code must be 3-50 characters and contain only uppercase letters, numbers, and hyphens (input is trimmed and uppercased).");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(255)
            .WithMessage("Name cannot exceed 255 characters.");

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
            .When(x => x.Type == PromoCodeType.FixedAmountOff);

        RuleFor(x => x.DiscountAmount)
            .GreaterThan(0)
            .WithMessage("Discount amount must be greater than 0.")
            .When(x => x.Type == PromoCodeType.FixedAmountOff && x.DiscountAmount.HasValue);

        RuleFor(x => x.DiscountAmount)
            .NotNull()
            .WithMessage("Set price is required for set-price discounts.")
            .When(x => x.Type == PromoCodeType.SetPrice);

        RuleFor(x => x.DiscountAmount)
            .GreaterThan(0)
            .WithMessage("Set price must be greater than 0.")
            .When(x => x.Type == PromoCodeType.SetPrice && x.DiscountAmount.HasValue);

        RuleFor(x => x.FreeTrialExtensionDays)
            .NotNull()
            .WithMessage("Trial extension days are required for free trial extension codes.")
            .When(x => x.Type == PromoCodeType.FreeTrialExtension);

        RuleFor(x => x.FreeTrialExtensionDays)
            .InclusiveBetween(1, 365)
            .WithMessage("Trial extension days must be between 1 and 365.")
            .When(x => x.FreeTrialExtensionDays.HasValue);

        RuleFor(x => x.DurationMonths)
            .NotNull()
            .WithMessage("Duration months are required for free months codes.")
            .When(x => x.Type == PromoCodeType.FreeMonths);

        RuleFor(x => x.DurationMonths)
            .InclusiveBetween(1, 36)
            .WithMessage("Duration months must be between 1 and 36.")
            .When(x => x.DurationMonths.HasValue);

        RuleFor(x => x.MaxDiscountAmount)
            .GreaterThan(0)
            .WithMessage("Maximum discount amount must be greater than 0.")
            .When(x => x.MaxDiscountAmount.HasValue);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Length(3)
            .WithMessage("Currency code must be exactly 3 characters.");

        RuleFor(x => x.MinimumOrderAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Minimum order amount cannot be negative.")
            .When(x => x.MinimumOrderAmount.HasValue);

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

        RuleFor(x => x.PlanId)
            .MustAsync((planId, cancellationToken) =>
                PromoPlanEligibility.PlanExistsOrNullAsync(planRepository, planId, cancellationToken))
            .WithMessage("The subscription plan referenced by PlanId does not exist.")
            .When(x => x.PlanId.HasValue);
    }
}

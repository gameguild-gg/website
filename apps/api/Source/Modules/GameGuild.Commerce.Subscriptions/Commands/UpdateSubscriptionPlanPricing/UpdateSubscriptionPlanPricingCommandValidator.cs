using FluentValidation;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Validator for UpdateSubscriptionPlanPricingCommand
/// </summary>
public sealed class UpdateSubscriptionPlanPricingCommandValidator : AbstractValidator<UpdateSubscriptionPlanPricingCommand>
{
    public UpdateSubscriptionPlanPricingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");

        // Issue #416: "Prices must be non-negative decimal values" — the pricing PATCH
        // path previously accepted negative prices.
        RuleFor(x => x.MonthlyPriceInCents)
            .GreaterThanOrEqualTo(0).WithMessage("MonthlyPriceInCents must be greater than or equal to 0");

        RuleFor(x => x.AnnualPriceInCents)
            .GreaterThanOrEqualTo(0).WithMessage("AnnualPriceInCents must be greater than or equal to 0")
            .When(x => x.AnnualPriceInCents.HasValue);
    }
}

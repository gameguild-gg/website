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

        // Issue #416: "Prices must be non-negative decimal values" — the enterprise -1
        // custom-quote sentinel is the only permitted negative monthly price; free plans
        // must resolve to zero. The stored plan type is applied by the handler's
        // fail-closed entity validation.
        RuleFor(x => x.MonthlyPriceInCents)
            .GreaterThanOrEqualTo(SubscriptionPlanPricingRules.CustomQuotePriceInCents)
            .WithMessage("MonthlyPriceInCents must be non-negative (enterprise plans may use the -1 custom-quote sentinel)");

        RuleFor(x => x.AnnualPriceInCents)
            .GreaterThanOrEqualTo(0).WithMessage("AnnualPriceInCents must be greater than or equal to 0")
            .When(x => x.AnnualPriceInCents.HasValue);

        RuleFor(x => x.AnnualDiscountPercent)
            .InclusiveBetween(0m, SubscriptionPlanPricingRules.MaximumAnnualDiscountPercent)
            .When(x => x.AnnualDiscountPercent.HasValue)
            .WithMessage("AnnualDiscountPercent must be between 0 and 100");
    }
}

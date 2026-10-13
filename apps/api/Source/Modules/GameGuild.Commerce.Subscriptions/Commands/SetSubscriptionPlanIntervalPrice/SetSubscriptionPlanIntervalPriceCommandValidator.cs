using FluentValidation;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Validator for SetSubscriptionPlanIntervalPriceCommand (issue #392).
/// </summary>
public sealed class SetSubscriptionPlanIntervalPriceCommandValidator : AbstractValidator<SetSubscriptionPlanIntervalPriceCommand>
{
    private static readonly BillingCycle[] SupportedIntervals =
    [
        BillingCycle.Monthly,
        BillingCycle.Quarterly,
        BillingCycle.SemiAnnually,
        BillingCycle.Annually
    ];

    public SetSubscriptionPlanIntervalPriceCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty().WithMessage("PlanId is required");

        RuleFor(x => x.Interval)
            .IsInEnum().WithMessage("Interval must be a valid billing cycle value")
            .Must(interval => SupportedIntervals.Contains(interval))
            .WithMessage("Interval must be one of the supported billing intervals: monthly, quarterly, semiannually, annually");

        RuleFor(x => x.PriceInCents)
            .GreaterThanOrEqualTo(0).WithMessage("PriceInCents cannot be negative")
            .LessThan(100_000_000_000).WithMessage("PriceInCents exceeds the supported range");
    }
}

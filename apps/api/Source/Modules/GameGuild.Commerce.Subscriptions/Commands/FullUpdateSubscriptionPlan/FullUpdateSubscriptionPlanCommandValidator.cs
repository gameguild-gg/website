using FluentValidation;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Validator for FullUpdateSubscriptionPlanCommand (issue #416: the full-update path
///     previously had no validator at all).
/// </summary>
public sealed class FullUpdateSubscriptionPlanCommandValidator : AbstractValidator<FullUpdateSubscriptionPlanCommand>
{
    public FullUpdateSubscriptionPlanCommandValidator(ISubscriptionPlanSlugGenerator slugGenerator)
    {
        RuleFor(x => x.PlanId).NotEmpty().WithMessage("PlanId is required");

        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required")
            .Length(3, 100).WithMessage("Name must be between 3 and 100 characters");

        // Issue #416: slug is optional on full update — regenerated from the name when
        // omitted; validated for URL-safety when provided.
        RuleFor(x => x.Slug)
            .MaximumLength(50)
            .WithMessage("Slug cannot exceed 50 characters")
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug can only contain lowercase letters, numbers, and hyphens")
            .When(x => !string.IsNullOrEmpty(x.Slug));

        RuleFor(x => x)
            .Must(command => slugGenerator.Slugify(command.Name) is not null)
            .When(x => string.IsNullOrEmpty(x.Slug))
            .WithMessage("Name must contain at least one letter or digit so a slug can be generated (or provide an explicit slug)");

        // Issue #416: plan-type pricing invariants — free plans are priced at zero and only
        // enterprise plans may carry the -1 custom-quote sentinel (validated against the
        // effective type, which defaults to Paid when the request omits it).
        RuleFor(x => x)
            .Must(command => TryValidatePricing(command, out _))
            .WithMessage("Free plans must be priced at zero; only enterprise plans may use the -1 custom-quote sentinel");

        RuleFor(x => x.AnnualDiscountPercent)
            .InclusiveBetween(0m, SubscriptionPlanPricingRules.MaximumAnnualDiscountPercent)
            .When(x => x.AnnualDiscountPercent.HasValue)
            .WithMessage("AnnualDiscountPercent must be between 0 and 100");

        RuleFor(x => x.MaxUsers)
            .GreaterThanOrEqualTo(0).WithMessage("MaxUsers must be greater than or equal to 0")
            .When(x => x.MaxUsers.HasValue);

        RuleFor(x => x.MaxStorageMb)
            .GreaterThanOrEqualTo(0).WithMessage("MaxStorageMb must be greater than or equal to 0")
            .When(x => x.MaxStorageMb.HasValue);

        RuleFor(x => x.MaxApiCallsPerMonth)
            .GreaterThanOrEqualTo(0).WithMessage("MaxApiCallsPerMonth must be greater than or equal to 0")
            .When(x => x.MaxApiCallsPerMonth.HasValue);

        // Issue #416: "Features and limits stored as JSON with schema validation".
        RuleFor(x => x.Features)
            .Must(features => SubscriptionPlanFeaturesJson.IsValid(features, out _))
            .WithMessage((_, features) =>
                SubscriptionPlanFeaturesJson.IsValid(features, out var error) ? "Features payload is invalid." : error!)
            .When(x => !string.IsNullOrEmpty(x.Features));

        // Issue #416: "Trial duration must be 0-90 days".
        RuleFor(x => x.TrialPeriodDays)
            .InclusiveBetween(0, 90).WithMessage("TrialPeriodDays must be between 0 and 90 days")
            .When(x => x.TrialPeriodDays.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }

    private static bool TryValidatePricing(FullUpdateSubscriptionPlanCommand command, out string failure)
    {
        try
        {
            SubscriptionPlanPricingRules.Validate(command.Type ?? SubscriptionPlanType.Paid, command.MonthlyPriceInCents, command.AnnualPriceInCents);
            failure = string.Empty;
            return true;
        }
        catch (SubscriptionPlanPricingRuleViolationException exception)
        {
            failure = exception.Message;
            return false;
        }
    }
}

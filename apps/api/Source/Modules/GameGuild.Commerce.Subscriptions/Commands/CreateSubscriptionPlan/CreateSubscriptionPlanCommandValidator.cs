using FluentValidation;
using GameGuild.Commerce;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Validator for CreateSubscriptionPlanCommand
/// </summary>
public sealed class CreateSubscriptionPlanCommandValidator : AbstractValidator<CreateSubscriptionPlanCommand>
{
    /// <summary>
    ///     The payment-supported ISO 4217 allowlist rendered into validation messages.
    /// </summary>
    private static readonly string SupportedCurrencies = string.Join(", ", CurrencyCodes.Supported);

    public CreateSubscriptionPlanCommandValidator(ISubscriptionPlanSlugGenerator slugGenerator)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required")
            .Length(3, 100).WithMessage("Name must be between 3 and 100 characters");

        // Issue #416: the slug is optional — when omitted it is generated from the name.
        // When provided it must stay URL-safe; when omitted the name must contain enough
        // usable characters for the generator to produce a slug.
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

        RuleFor(x => x.MonthlyPriceInCents).GreaterThanOrEqualTo(0).WithMessage("MonthlyPriceInCents must be greater than or equal to 0");

        // Issue #416: "Currency must be valid ISO 4217 code" — validated against the
        // payment-supported allowlist (CurrencyCodes in the shared commerce contracts),
        // not just the letter-triplet format.
        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .Length(3)
            .WithMessage("Currency must be exactly 3 characters (ISO 4217)")
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be uppercase letters only")
            .Must(CurrencyCodes.IsSupported)
            .WithMessage($"Currency must be a supported ISO 4217 code ({SupportedCurrencies})");

        RuleFor(x => x.Description).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Description)).WithMessage("Description cannot exceed 500 characters");
    }
}

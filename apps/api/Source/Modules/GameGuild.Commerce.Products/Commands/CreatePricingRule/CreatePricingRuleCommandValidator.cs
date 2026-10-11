using FluentValidation;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Validator for <see cref="CreatePricingRuleCommand"/>.
/// </summary>
public sealed class CreatePricingRuleCommandValidator : AbstractValidator<CreatePricingRuleCommand>
{
    /// <summary>
    /// Initializes the validator with the shared pricing-rule constraints.
    /// </summary>
    public CreatePricingRuleCommandValidator()
    {
        PricingRuleCommandValidationRules.ApplySharedRules(this);
    }
}

/// <summary>
/// Shared FluentValidation rules for pricing rule create/update commands (issue #395).
/// </summary>
internal static class PricingRuleCommandValidationRules
{
    public static void ApplySharedRules<TCommand>(AbstractValidator<TCommand> validator)
        where TCommand : IPricingRuleCommandPayload
    {
        validator.RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Rule name is required.")
            .MaximumLength(200)
            .WithMessage("Rule name cannot exceed 200 characters.");

        validator.RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description cannot exceed 1000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        validator.RuleFor(x => x.Priority)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Priority cannot be negative.");

        validator.RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);

        validator.RuleFor(x => x.MinQuantity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Minimum quantity must be at least 1.")
            .When(x => x.MinQuantity.HasValue);

        validator.RuleFor(x => x.MaxQuantity)
            .GreaterThanOrEqualTo(x => x.MinQuantity)
            .WithMessage("Maximum quantity cannot be lower than minimum quantity.")
            .When(x => x.MaxQuantity.HasValue && x.MinQuantity.HasValue);

        validator.RuleFor(x => x.DiscountPercentage)
            .InclusiveBetween(0.01m, 100m)
            .WithMessage("Discount percentage must be between 0.01 and 100.")
            .When(x => x.DiscountPercentage.HasValue);

        validator.RuleFor(x => x.DiscountAmount)
            .GreaterThan(0)
            .WithMessage("Discount amount must be greater than 0.")
            .When(x => x.DiscountAmount.HasValue);

        validator.RuleFor(x => x.FixedPrice)
            .GreaterThan(0)
            .WithMessage("Fixed price must be greater than 0.")
            .When(x => x.FixedPrice.HasValue);

        validator.RuleFor(x => x)
            .Must(command => command.BuyQuantity is null or > 0)
            .WithMessage("Buy quantity must be greater than 0 when provided.")
            .When(x => x.BuyQuantity.HasValue);

        validator.RuleFor(x => x)
            .Must(command => command.GetQuantity is null or > 0)
            .WithMessage("Get quantity must be greater than 0 when provided.")
            .When(x => x.GetQuantity.HasValue);

        validator.RuleFor(x => x)
            .Must(command => !command.BuyQuantity.HasValue || command.GetQuantity.HasValue)
            .WithMessage("Buy X Get Y rules require both buy and get quantities.")
            .When(x => x.BuyQuantity.HasValue || x.GetQuantity.HasValue);

        validator.RuleFor(x => x.CustomerSegment)
            .MaximumLength(100)
            .WithMessage("Customer segment cannot exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerSegment));

        validator.RuleFor(x => x.Region)
            .MaximumLength(100)
            .WithMessage("Region cannot exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Region));

        validator.RuleFor(x => x.TimeStart)
            .Matches(@"^([01]\d|2[0-3]):[0-5]\d$")
            .WithMessage("Time start must be in HH:MM format.")
            .When(x => !string.IsNullOrWhiteSpace(x.TimeStart));

        validator.RuleFor(x => x.TimeEnd)
            .Matches(@"^([01]\d|2[0-3]):[0-5]\d$")
            .WithMessage("Time end must be in HH:MM format.")
            .When(x => !string.IsNullOrWhiteSpace(x.TimeEnd));

        validator.RuleFor(x => x.DaysOfWeek)
            .Matches("^(?:[0-6])(?:\\s*,\\s*[0-6])*$")
            .WithMessage("Days of week must be comma-separated digits between 0 (Sunday) and 6 (Saturday).")
            .When(x => !string.IsNullOrWhiteSpace(x.DaysOfWeek));

        validator.RuleFor(x => x.Tiers)
            .Must(tiers => tiers is null || tiers.Count <= 50)
            .WithMessage("A pricing rule cannot have more than 50 tiers.")
            .When(x => x.Tiers is not null);

        validator.RuleFor(x => x.Tiers!)
            .Must(tiers => tiers.All(t =>
                t.MinQuantity is null || t.MinQuantity >= 1))
            .WithMessage("Tier minimum quantity must be at least 1.")
            .When(x => x.Tiers is { Count: > 0 });

        validator.RuleFor(x => x.Tiers!)
            .Must(tiers => tiers.All(t =>
                !t.MinQuantity.HasValue || !t.MaxQuantity.HasValue || t.MaxQuantity >= t.MinQuantity))
            .WithMessage("Tier maximum quantity cannot be lower than its minimum quantity.")
            .When(x => x.Tiers is { Count: > 0 });

        validator.RuleFor(x => x.Tiers!)
            .Must(tiers => tiers.All(t => t.Price.HasValue || t.DiscountPercentage.HasValue))
            .WithMessage("Every tier must define either a fixed price or a discount percentage.")
            .When(x => x.Tiers is { Count: > 0 });

        validator.RuleFor(x => x.Tiers!)
            .Must(tiers => tiers.All(t =>
                !t.DiscountPercentage.HasValue || (t.DiscountPercentage.Value > 0 && t.DiscountPercentage.Value <= 100)))
            .WithMessage("Tier discount percentage must be between 0.01 and 100.")
            .When(x => x.Tiers is { Count: > 0 });

        validator.RuleFor(x => x.Tiers!)
            .Must(tiers => tiers.All(t => !t.Price.HasValue || t.Price.Value > 0))
            .WithMessage("Tier price must be greater than 0.")
            .When(x => x.Tiers is { Count: > 0 });
    }
}

/// <summary>
/// The writable payload shared by pricing rule create/update commands, so both can
/// reuse the same validation rules.
/// </summary>
internal interface IPricingRuleCommandPayload
{
    /// <summary>Rule name</summary>
    string Name { get; }

    /// <summary>Optional description</summary>
    string? Description { get; }

    /// <summary>Priority (higher wins)</summary>
    int Priority { get; }

    /// <summary>Optional activation start (UTC)</summary>
    DateTime? StartDate { get; }

    /// <summary>Optional expiry (UTC)</summary>
    DateTime? EndDate { get; }

    /// <summary>Minimum quantity for the rule to apply</summary>
    int? MinQuantity { get; }

    /// <summary>Maximum quantity for the rule to apply</summary>
    int? MaxQuantity { get; }

    /// <summary>Percentage discount</summary>
    decimal? DiscountPercentage { get; }

    /// <summary>Fixed discount amount</summary>
    decimal? DiscountAmount { get; }

    /// <summary>Fixed per-unit price override</summary>
    decimal? FixedPrice { get; }

    /// <summary>Buy X (Buy X Get Y rules)</summary>
    int? BuyQuantity { get; }

    /// <summary>Get Y (Buy X Get Y rules)</summary>
    int? GetQuantity { get; }

    /// <summary>Time-of-day window start (HH:MM)</summary>
    string? TimeStart { get; }

    /// <summary>Time-of-day window end (HH:MM)</summary>
    string? TimeEnd { get; }

    /// <summary>Days of week (comma-separated 0-6)</summary>
    string? DaysOfWeek { get; }

    /// <summary>Geographic region</summary>
    string? Region { get; }

    /// <summary>Customer segment restriction</summary>
    string? CustomerSegment { get; }

    /// <summary>Volume tiers</summary>
    IReadOnlyList<PricingRuleTierRequest>? Tiers { get; }
}

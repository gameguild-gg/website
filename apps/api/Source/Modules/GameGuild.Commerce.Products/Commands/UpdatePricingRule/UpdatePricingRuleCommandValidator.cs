using FluentValidation;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Validator for <see cref="UpdatePricingRuleCommand"/>.
/// </summary>
public sealed class UpdatePricingRuleCommandValidator : AbstractValidator<UpdatePricingRuleCommand>
{
    /// <summary>
    /// Initializes the validator with the shared pricing-rule constraints plus the rule ID check.
    /// </summary>
    public UpdatePricingRuleCommandValidator()
    {
        RuleFor(x => x.RuleId)
            .NotEmpty()
            .WithMessage("Rule ID is required.");

        PricingRuleCommandValidationRules.ApplySharedRules(this);
    }
}

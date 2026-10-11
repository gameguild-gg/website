using FluentValidation;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Validator for <see cref="DeletePricingRuleCommand"/>.
/// </summary>
public sealed class DeletePricingRuleCommandValidator : AbstractValidator<DeletePricingRuleCommand>
{
    /// <summary>
    /// Initializes the validator with the rule ID check.
    /// </summary>
    public DeletePricingRuleCommandValidator()
    {
        RuleFor(x => x.RuleId)
            .NotEmpty()
            .WithMessage("Rule ID is required.");
    }
}

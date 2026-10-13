using FluentValidation;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Validator for DeductFundsCommand
/// </summary>
public sealed class DeductFundsCommandValidator : AbstractValidator<DeductFundsCommand>
{
    public DeductFundsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required").MaximumLength(500).WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.ReferenceId).MaximumLength(100).WithMessage("Reference ID cannot exceed 100 characters").When(x => !string.IsNullOrEmpty(x.ReferenceId));

        RuleFor(x => x.Currency).Length(3).Matches("^[A-Z]{3}$").When(x => !string.IsNullOrEmpty(x.Currency)).WithMessage("Currency must be a 3-letter ISO-4217 code");
    }
}

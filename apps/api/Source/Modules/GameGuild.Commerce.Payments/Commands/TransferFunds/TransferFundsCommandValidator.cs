using FluentValidation;

namespace GameGuild.Commerce.Payments;

public sealed class TransferFundsCommandValidator : AbstractValidator<TransferFundsCommand>
{
    public TransferFundsCommandValidator()
    {
        RuleFor(x => x.FromUserId).NotEmpty().WithMessage("Source User ID is required");

        RuleFor(x => x.ToUserId).NotEmpty().WithMessage("Destination User ID is required");

        RuleFor(x => x).Must(x => x.FromUserId != x.ToUserId).WithMessage("Cannot transfer funds to the same user");

        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Transfer amount must be greater than zero").LessThanOrEqualTo(50000).WithMessage("Transfer amount cannot exceed $50,000 per transaction");

        RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required").MaximumLength(500).WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.ReferenceId).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.ReferenceId)).WithMessage("Reference ID cannot exceed 100 characters");

        RuleFor(x => x.Currency).Length(3).Matches("^[A-Z]{3}$").When(x => !string.IsNullOrEmpty(x.Currency)).WithMessage("Currency must be a 3-letter ISO-4217 code");
    }
}

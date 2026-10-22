using FluentValidation;

namespace GameGuild.Commerce.Payments;

/// <summary>Validation for <see cref="ResolveFraudReviewsBulkCommand"/>.</summary>
public sealed class ResolveFraudReviewsBulkCommandValidator : AbstractValidator<ResolveFraudReviewsBulkCommand>
{
    public ResolveFraudReviewsBulkCommandValidator()
    {
        RuleFor(command => command.PaymentIds)
            .NotEmpty()
            .WithMessage("At least one payment id is required");

        RuleFor(command => command.PaymentIds.Count)
            .LessThanOrEqualTo(100)
            .WithMessage("Bulk fraud-review resolution accepts at most 100 payments per batch");

        RuleFor(command => command.Notes)
            .MaximumLength(500)
            .WithMessage("Review notes must not exceed 500 characters");
    }
}

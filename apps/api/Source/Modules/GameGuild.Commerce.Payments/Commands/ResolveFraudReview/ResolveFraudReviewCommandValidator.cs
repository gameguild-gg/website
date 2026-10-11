using FluentValidation;

namespace GameGuild.Commerce.Payments;

/// <summary>Validation for <see cref="ResolveFraudReviewCommand"/>.</summary>
public sealed class ResolveFraudReviewCommandValidator : AbstractValidator<ResolveFraudReviewCommand>
{
    public ResolveFraudReviewCommandValidator()
    {
        RuleFor(command => command.PaymentId)
            .NotEmpty()
            .WithMessage("Payment ID is required");

        RuleFor(command => command.Notes)
            .MaximumLength(500)
            .WithMessage("Review notes must not exceed 500 characters");
    }
}

using FluentValidation;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Validator for CreateSubscriptionCommand
/// </summary>
public sealed class CreateSubscriptionCommandValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public CreateSubscriptionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId is required");

        RuleFor(x => x.PlanId).NotEmpty().WithMessage("PlanId is required");

        RuleFor(x => x.CreatedByUserId).NotEmpty().WithMessage("CreatedByUserId is required");

        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage("Amount must be greater than or equal to 0");

        RuleFor(x => x.TrialDays).GreaterThan(0).When(x => x.TrialDays.HasValue).WithMessage("TrialDays must be greater than 0 when specified");

        RuleFor(x => x.StartDate).GreaterThanOrEqualTo(SystemClock.UtcNow.Date).When(x => x.StartDate.HasValue).WithMessage("StartDate must be today or in the future when specified");

        RuleFor(x => x.PromoCodes)
            .Must(codes => codes is null || codes.All(c => !string.IsNullOrWhiteSpace(c)))
            .WithMessage("Promo codes cannot contain empty entries.");

        RuleFor(x => x.PromoCodes)
            .Must(codes => codes is null || codes.Count <= 10)
            .WithMessage("At most 10 promo codes can be applied to a subscription.");

        RuleFor(x => x.PromoCodeIdempotencyKey)
            .MaximumLength(200)
            .WithMessage("Promo code idempotency key cannot exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.PromoCodeIdempotencyKey));
    }
}

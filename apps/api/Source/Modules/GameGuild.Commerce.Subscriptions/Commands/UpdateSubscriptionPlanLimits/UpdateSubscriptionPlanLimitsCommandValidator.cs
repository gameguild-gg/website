using FluentValidation;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Validator for UpdateSubscriptionPlanLimitsCommand
/// </summary>
public sealed class UpdateSubscriptionPlanLimitsCommandValidator : AbstractValidator<UpdateSubscriptionPlanLimitsCommand>
{
    public UpdateSubscriptionPlanLimitsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");

        // Issue #416: "Features and limits stored with schema validation" — the limits
        // columns must stay non-negative.
        RuleFor(x => x.MaxUsers)
            .GreaterThanOrEqualTo(0).WithMessage("MaxUsers must be greater than or equal to 0")
            .When(x => x.MaxUsers.HasValue);

        RuleFor(x => x.MaxStorageMb)
            .GreaterThanOrEqualTo(0).WithMessage("MaxStorageMb must be greater than or equal to 0")
            .When(x => x.MaxStorageMb.HasValue);

        RuleFor(x => x.MaxApiCallsPerMonth)
            .GreaterThanOrEqualTo(0).WithMessage("MaxApiCallsPerMonth must be greater than or equal to 0")
            .When(x => x.MaxApiCallsPerMonth.HasValue);
    }
}

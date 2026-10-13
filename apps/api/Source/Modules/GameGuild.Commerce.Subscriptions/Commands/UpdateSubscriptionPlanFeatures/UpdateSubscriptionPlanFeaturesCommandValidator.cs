using FluentValidation;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Validator for UpdateSubscriptionPlanFeaturesCommand
/// </summary>
public sealed class UpdateSubscriptionPlanFeaturesCommandValidator : AbstractValidator<UpdateSubscriptionPlanFeaturesCommand>
{
    public UpdateSubscriptionPlanFeaturesCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");

        // Issue #416: "Features and limits stored as JSON with schema validation" — the
        // features payload must be a flat JSON object with scalar leaves.
        RuleFor(x => x.Features)
            .Must(features => SubscriptionPlanFeaturesJson.IsValid(features, out _))
            .WithMessage((_, features) =>
                SubscriptionPlanFeaturesJson.IsValid(features, out var error) ? "Features payload is invalid." : error!)
            .When(x => !string.IsNullOrEmpty(x.Features));
    }
}

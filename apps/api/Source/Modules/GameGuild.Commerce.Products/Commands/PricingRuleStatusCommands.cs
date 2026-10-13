using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to activate a pricing rule (issue #395). Activation is a pricing change and is
/// gated on the monetization permission on every dispatch path.
/// </summary>
/// <param name="RuleId">Rule ID to activate</param>
[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]
public sealed record ActivatePricingRuleCommand(Guid RuleId) : ICommand<PricingRuleDto>;

/// <summary>
/// Handler for activating a pricing rule.
/// </summary>
public sealed class ActivatePricingRuleCommandHandler(IPricingRuleRepository pricingRuleRepository)
    : ICommandHandler<ActivatePricingRuleCommand, PricingRuleDto>
{
    public async Task<PricingRuleDto> Handle(ActivatePricingRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rule = await pricingRuleRepository.GetByIdAsync(request.RuleId, cancellationToken).ConfigureAwait(false)
            ?? throw new PricingRuleNotFoundException(request.RuleId);

        rule.IsActive = true;
        await pricingRuleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);

        return rule.ToDto();
    }
}

/// <summary>
/// Command to deactivate a pricing rule (issue #395). Deactivation is a pricing change and is
/// gated on the monetization permission on every dispatch path.
/// </summary>
/// <param name="RuleId">Rule ID to deactivate</param>
[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]
public sealed record DeactivatePricingRuleCommand(Guid RuleId) : ICommand<PricingRuleDto>;

/// <summary>
/// Handler for deactivating a pricing rule.
/// </summary>
public sealed class DeactivatePricingRuleCommandHandler(IPricingRuleRepository pricingRuleRepository)
    : ICommandHandler<DeactivatePricingRuleCommand, PricingRuleDto>
{
    public async Task<PricingRuleDto> Handle(DeactivatePricingRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rule = await pricingRuleRepository.GetByIdAsync(request.RuleId, cancellationToken).ConfigureAwait(false)
            ?? throw new PricingRuleNotFoundException(request.RuleId);

        rule.IsActive = false;
        await pricingRuleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);

        return rule.ToDto();
    }
}

/// <summary>
/// Validator for <see cref="ActivatePricingRuleCommand"/>.
/// </summary>
public sealed class ActivatePricingRuleCommandValidator : AbstractValidator<ActivatePricingRuleCommand>
{
    /// <summary>
    /// Initializes the validator with the rule ID check.
    /// </summary>
    public ActivatePricingRuleCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty().WithMessage("Rule ID is required.");
    }
}

/// <summary>
/// Validator for <see cref="DeactivatePricingRuleCommand"/>.
/// </summary>
public sealed class DeactivatePricingRuleCommandValidator : AbstractValidator<DeactivatePricingRuleCommand>
{
    /// <summary>
    /// Initializes the validator with the rule ID check.
    /// </summary>
    public DeactivatePricingRuleCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty().WithMessage("Rule ID is required.");
    }
}

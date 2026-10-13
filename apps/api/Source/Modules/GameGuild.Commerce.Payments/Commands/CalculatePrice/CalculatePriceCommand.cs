using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Payments;

/// <summary>
/// Command (issue #417): run the unified pricing engine for a full, audited calculation —
/// base plan/custom amount, add-ons, usage tiers, promo discounts, proration, tax, credits,
/// currency conversion — and append the decision to the pricing-decision ledger.
/// Dispatched as a command for pipeline parity with the Products calculate; it mutates
/// nothing unless <see cref="PricingCalculationRequest.ConsumeCredits"/> is set.
/// </summary>
/// <param name="Request">Calculation inputs</param>
public sealed record CalculatePriceCommand(PricingCalculationRequest Request) : ICommand<PricingCalculationResult>;

/// <summary>Handler for <see cref="CalculatePriceCommand"/>.</summary>
public sealed class CalculatePriceCommandHandler(IPricingEngine pricingEngine)
    : ICommandHandler<CalculatePriceCommand, PricingCalculationResult>
{
    public async Task<PricingCalculationResult> Handle(CalculatePriceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await pricingEngine.CalculateAsync(request.Request, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Validator for <see cref="CalculatePriceCommand"/>.</summary>
public sealed class CalculatePriceCommandValidator : AbstractValidator<CalculatePriceCommand>
{
    /// <summary>Initializes the validator.</summary>
    public CalculatePriceCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull();

        RuleFor(x => x.Request.PlanId)
            .NotEmpty()
            .When(x => !x.Request.BaseAmount.HasValue)
            .WithMessage("A calculation requires a PlanId or a BaseAmount.");

        RuleFor(x => x.Request.BaseAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request.BaseAmount.HasValue);

        RuleFor(x => x.Request.Currency)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a three-letter ISO 4217 code.");

        RuleFor(x => x.Request.MaxCreditUsage)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request.MaxCreditUsage.HasValue);
    }
}

/// <summary>
/// Command (issue #417): preview a plan change through the unified pricing engine — the
/// immediate prorated charge plus the next-renewal amount. Previews are read-only: no
/// ledger decision, no credit consumption.
/// </summary>
/// <param name="Request">Preview inputs</param>
public sealed record PreviewPriceChangeCommand(PricingPreviewRequest Request) : ICommand<PricingPreviewResult>;

/// <summary>Handler for <see cref="PreviewPriceChangeCommand"/>.</summary>
public sealed class PreviewPriceChangeCommandHandler(IPricingEngine pricingEngine)
    : ICommandHandler<PreviewPriceChangeCommand, PricingPreviewResult>
{
    public async Task<PricingPreviewResult> Handle(PreviewPriceChangeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await pricingEngine.PreviewAsync(request.Request, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Validator for <see cref="PreviewPriceChangeCommand"/>.</summary>
public sealed class PreviewPriceChangeCommandValidator : AbstractValidator<PreviewPriceChangeCommand>
{
    /// <summary>Initializes the validator.</summary>
    public PreviewPriceChangeCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull();

        RuleFor(x => x.Request.CurrentPlanId)
            .NotEmpty();

        RuleFor(x => x.Request.NewPlanId)
            .NotEmpty();

        RuleFor(x => x.Request.PeriodStart)
            .LessThan(x => x.Request.PeriodEnd)
            .WithMessage("PeriodStart must be before PeriodEnd.");

        RuleFor(x => x.Request.Currency)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a three-letter ISO 4217 code.");
    }
}

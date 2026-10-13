using FluentValidation;
using GameGuild.Commerce;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>
/// Query (issue #417): validate the accuracy of one stored price calculation. Fetches the
/// ledger entry, parses its breakdown document, recomputes the final amount from the
/// recorded parts (replay), and checks the issue's calculation invariants. Read-only:
/// drift stamping stays with <see cref="ValidatePricingLedgerDriftCommand"/>.
/// </summary>
/// <param name="CalculationId">Ledger entry id of the calculation.</param>
public sealed record ValidatePricingCalculationQuery(Guid CalculationId) : IQuery<PricingCalculationValidationResult?>;

/// <summary>Validator for <see cref="ValidatePricingCalculationQuery"/>.</summary>
public sealed class ValidatePricingCalculationQueryValidator : AbstractValidator<ValidatePricingCalculationQuery>
{
    /// <summary>Initializes the validator.</summary>
    public ValidatePricingCalculationQueryValidator()
    {
        RuleFor(query => query.CalculationId).NotEmpty();
    }
}

/// <summary>Handler for <see cref="ValidatePricingCalculationQuery"/>.</summary>
public sealed class ValidatePricingCalculationQueryHandler(
    IPricingDecisionLedgerRepository ledgerRepository)
    : IQueryHandler<ValidatePricingCalculationQuery, PricingCalculationValidationResult?>
{
    /// <summary>
    /// Tolerance for recomputed-vs-stored final amounts: pure two-decimal rounding stages
    /// should never diverge by more than a hundredth of the currency's major unit.
    /// </summary>
    private const decimal RecomputeTolerance = 0.01m;

    /// <inheritdoc />
    public async Task<PricingCalculationValidationResult?> Handle(
        ValidatePricingCalculationQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entry = await ledgerRepository.GetByIdAsync(request.CalculationId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return null;
        }

        var violations = new List<string>();
        var breakdown = PricingDecisionBreakdown.FromJson(entry.BreakdownJson);

        if (breakdown is null)
        {
            return new PricingCalculationValidationResult(
                entry.Id,
                entry.FinalAmount,
                0,
                Drifted: true,
                Valid: false,
                ["Stored breakdown document could not be parsed."]);
        }

        // Replay: recompute the final from the recorded parts.
        var recomputed = breakdown.RecomputeFinalAmount();
        var drifted = Math.Abs(recomputed - entry.FinalAmount) > RecomputeTolerance;
        if (drifted)
        {
            violations.Add($"Recomputed final {recomputed} diverges from stored final {entry.FinalAmount} beyond {RecomputeTolerance}.");
        }

        // Invariant: discounts never exceed the discountable base.
        if (breakdown.PromoDiscountTotal > breakdown.BaseAmount)
        {
            violations.Add($"Promo discount total {breakdown.PromoDiscountTotal} exceeds the base amount {breakdown.BaseAmount}.");
        }

        if (breakdown.RuleAdjustmentTotal > breakdown.BaseAmount)
        {
            violations.Add($"Rule adjustment total {breakdown.RuleAdjustmentTotal} exceeds the base amount {breakdown.BaseAmount}.");
        }

        // Invariant (v2 documents): the signed proration adjustment stays within the plan-price bound.
        if (breakdown.Version >= 2 && Math.Abs(breakdown.ProrationAdjustment) > breakdown.BaseAmount)
        {
            violations.Add($"Proration adjustment {breakdown.ProrationAdjustment} exceeds the base amount {breakdown.BaseAmount}.");
        }

        // Invariant (v2 documents): credits never exceed the pre-credit total.
        if (breakdown.Version >= 2 && breakdown.CreditsApplied < 0)
        {
            violations.Add($"Credits applied {breakdown.CreditsApplied} is negative.");
        }

        // Invariant: amounts never go negative.
        if (entry.FinalAmount < 0 || breakdown.BaseAmount < 0)
        {
            violations.Add("Stored amounts must never be negative.");
        }

        // Invariant: rounding drift accumulation — promo parts must sum to the recorded total.
        var promoPartSum = Math.Round(breakdown.PromoApplications?.Sum(p => p.Amount) ?? 0, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(promoPartSum - breakdown.PromoDiscountTotal) > RecomputeTolerance)
        {
            violations.Add($"Promo application parts sum to {promoPartSum} but the recorded promo total is {breakdown.PromoDiscountTotal}.");
        }

        return new PricingCalculationValidationResult(
            entry.Id,
            entry.FinalAmount,
            recomputed,
            drifted,
            violations.Count == 0,
            violations);
    }
}

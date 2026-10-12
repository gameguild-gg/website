using FluentValidation;
using GameGuild.Commerce;
using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>Result of one drift-validated ledger entry.</summary>
/// <param name="EntryId">Ledger row identifier.</param>
/// <param name="StoredFinalAmount">Final amount as persisted.</param>
/// <param name="RecomputedFinalAmount">Final amount recomputed from the breakdown.</param>
/// <param name="Drifted">Whether the two diverge beyond tolerance.</param>
/// <param name="Reason">Why the entry drifted (or could not be validated), when applicable.</param>
public sealed record PricingLedgerDriftOutcome(
    Guid EntryId,
    decimal StoredFinalAmount,
    decimal RecomputedFinalAmount,
    bool Drifted,
    string? Reason);

/// <summary>Drift-validation summary (issue #400).</summary>
/// <param name="SampleSize">Entries selected for validation.</param>
/// <param name="Validated">Entries successfully recomputed (verified or drifted).</param>
/// <param name="VerifiedCount">Entries whose recomputation matched the stored final.</param>
/// <param name="DriftedCount">Entries whose recomputation diverged beyond tolerance.</param>
/// <param name="SkippedCount">Entries skipped because their breakdown could not be parsed.</param>
/// <param name="WindowStart">Window start (null = unbounded).</param>
/// <param name="WindowEnd">Window end (null = unbounded).</param>
/// <param name="ValidatedAtUtc">When the validation pass ran.</param>
/// <param name="Outcomes">Per-entry outcomes, drifted entries first.</param>
public sealed record PricingLedgerDriftValidationResult(
    int SampleSize,
    int Validated,
    int VerifiedCount,
    int DriftedCount,
    int SkippedCount,
    DateTime? WindowStart,
    DateTime? WindowEnd,
    DateTime ValidatedAtUtc,
    IReadOnlyList<PricingLedgerDriftOutcome> Outcomes);

/// <summary>
/// Command (issue #400): drift detection over the pricing-decision ledger. Samples
/// ledger entries in a window, recomputes each entry's final amount from its breakdown
/// document (base − rules − promos + cap restorations, rounded per stage) and stamps the
/// entry Verified or Drifted. Drifted entries — where the durable record no longer
/// follows from its own arithmetic — are surfaced first for review.
/// </summary>
/// <param name="SampleSize">How many entries to validate (1-200, default 25).</param>
/// <param name="FromUtc">Inclusive window start (null = unbounded).</param>
/// <param name="ToUtc">Inclusive window end (null = unbounded).</param>
public sealed record ValidatePricingLedgerDriftCommand(
    int SampleSize = 25,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null) : ICommand<PricingLedgerDriftValidationResult>;

/// <summary>Validator for <see cref="ValidatePricingLedgerDriftCommand"/>.</summary>
public sealed class ValidatePricingLedgerDriftCommandValidator : AbstractValidator<ValidatePricingLedgerDriftCommand>
{
    /// <summary>Initializes the validator.</summary>
    public ValidatePricingLedgerDriftCommandValidator()
    {
        RuleFor(command => command.SampleSize).InclusiveBetween(1, 200);
        RuleFor(command => command.ToUtc)
            .GreaterThanOrEqualTo(command => command.FromUtc)
            .When(command => command.FromUtc.HasValue && command.ToUtc.HasValue);
    }
}

/// <summary>Handler for <see cref="ValidatePricingLedgerDriftCommand"/>.</summary>
public sealed class ValidatePricingLedgerDriftCommandHandler(
    IPricingDecisionLedgerRepository ledgerRepository,
    ILogger<ValidatePricingLedgerDriftCommandHandler> logger)
    : ICommandHandler<ValidatePricingLedgerDriftCommand, PricingLedgerDriftValidationResult>
{
    /// <summary>Two-decimal tolerance: recomputation mirrors the engine's staged rounding,
    /// so any larger divergence means the stored record no longer follows from its parts.</summary>
    public const decimal DriftTolerance = 0.01m;

    /// <inheritdoc />
    public async Task<PricingLedgerDriftValidationResult> Handle(
        ValidatePricingLedgerDriftCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fromUtc = request.FromUtc?.UtcDateTime;
        var toUtc = request.ToUtc?.UtcDateTime;

        var sample = await ledgerRepository
            .SampleForValidationAsync(fromUtc, toUtc, request.SampleSize, cancellationToken)
            .ConfigureAwait(false);

        var outcomes = new List<PricingLedgerDriftOutcome>(sample.Count);
        var skipped = 0;
        var verified = 0;
        var drifted = 0;

        foreach (var entry in sample)
        {
            var breakdown = PricingDecisionBreakdown.FromJson(entry.BreakdownJson);
            if (breakdown is null)
            {
                skipped++;
                drifted++;
                entry.StampValidation(
                    PricingDecisionDriftStatus.Drifted,
                    entry.FinalAmount,
                    "Breakdown document could not be parsed; the entry cannot be re-verified.");
                outcomes.Add(new PricingLedgerDriftOutcome(
                    entry.Id, entry.FinalAmount, entry.FinalAmount, true,
                    "Breakdown document could not be parsed"));
                continue;
            }

            var recomputed = breakdown.RecomputeFinalAmount();
            var divergence = Math.Abs(recomputed - entry.FinalAmount);
            var isDrifted = divergence > DriftTolerance;

            if (isDrifted)
            {
                drifted++;
                entry.StampValidation(
                    PricingDecisionDriftStatus.Drifted,
                    recomputed,
                    $"Recomputed final {recomputed} diverges from stored final {entry.FinalAmount} by {divergence}.");
                logger.LogWarning(
                    "Pricing ledger drift detected on entry {LedgerEntryId}: stored {Stored}, recomputed {Recomputed}",
                    entry.Id,
                    entry.FinalAmount,
                    recomputed);
            }
            else
            {
                verified++;
                entry.StampValidation(PricingDecisionDriftStatus.Verified, recomputed, null);
            }

            outcomes.Add(new PricingLedgerDriftOutcome(entry.Id, entry.FinalAmount, recomputed, isDrifted, null));
        }

        if (sample.Count > 0)
        {
            await ledgerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new PricingLedgerDriftValidationResult(
            SampleSize: sample.Count,
            Validated: verified + drifted,
            VerifiedCount: verified,
            DriftedCount: drifted,
            SkippedCount: skipped,
            WindowStart: fromUtc,
            WindowEnd: toUtc,
            ValidatedAtUtc: SystemClock.UtcNow,
            Outcomes: outcomes
                .OrderByDescending(outcome => outcome.Drifted)
                .ThenBy(outcome => outcome.EntryId)
                .ToList());
    }
}

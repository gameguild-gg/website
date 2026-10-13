using GameGuild.Commerce.Payments;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     <see cref="IPlanProrationCalculator"/> implementation (issue #417) registered by the
///     Subscriptions module: delegates to the existing
///     <see cref="IBillingCalculator.CalculateProration"/> so the unified pricing engine in
///     Payments wires the landed proration math (daily rates from actual period lengths,
///     credit for unused time, charge for the new plan over remaining days) without the
///     Payments module depending on Subscriptions.
/// </summary>
public sealed class PlanProrationCalculator(IBillingCalculator billingCalculator) : IPlanProrationCalculator
{
    private readonly IBillingCalculator _billingCalculator = billingCalculator ?? throw new ArgumentNullException(nameof(billingCalculator));

    /// <inheritdoc />
    public Task<PlanProrationResult> CalculateProrationAsync(PlanProrationInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        cancellationToken.ThrowIfCancellationRequested();

        var proration = _billingCalculator.CalculateProration(
            input.OldAmount,
            input.NewAmount,
            input.PeriodStart,
            input.PeriodEnd,
            input.EffectiveDate);

        var totalDays = (int)Math.Max(1, Math.Ceiling((input.PeriodEnd - input.PeriodStart).TotalDays));
        var daysRemaining = (int)Math.Max(0, Math.Ceiling((input.PeriodEnd - input.EffectiveDate).TotalDays));

        return Task.FromResult(new PlanProrationResult(
            proration.CreditForUnused,
            proration.ChargeForNew,
            proration.NetAdjustment,
            proration.EffectiveDate,
            totalDays,
            daysRemaining));
    }
}

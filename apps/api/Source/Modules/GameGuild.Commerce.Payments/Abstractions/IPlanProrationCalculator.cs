namespace GameGuild.Commerce.Payments;

/// <summary>Proration inputs for <see cref="IPlanProrationCalculator"/> (issue #417).</summary>
/// <param name="OldAmount">Original (current-plan) amount for the period</param>
/// <param name="NewAmount">New-plan amount for the period</param>
/// <param name="PeriodStart">Billing period start</param>
/// <param name="PeriodEnd">Billing period end</param>
/// <param name="EffectiveDate">When the change takes effect (mid-cycle)</param>
public sealed record PlanProrationInput(
    Money OldAmount,
    Money NewAmount,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime EffectiveDate);

/// <summary>
///     Proration outcome (issue #417): mirrors the Subscriptions module's
///     <c>PlanChangeProration</c> (credit for unused time, charge for the new plan over the
///     remaining days, net adjustment) plus the period context the pricing engine reports.
/// </summary>
public sealed record PlanProrationResult(
    decimal CreditForUnused,
    decimal ChargeForNew,
    decimal NetAdjustment,
    DateTime EffectiveDate,
    int TotalDays,
    int DaysRemaining)
{
    /// <summary>Whether the proration nets to a credit for the customer.</summary>
    public bool IsCredit => NetAdjustment < 0;

    /// <summary>Whether the proration nets to an immediate charge.</summary>
    public bool IsCharge => NetAdjustment > 0;
}

/// <summary>
///     Proration seam owned by the Payments module (issue #417), mirroring the
///     <see cref="IPlanPricingResolver"/> pattern: the Subscriptions module registers the
///     implementation on top of its existing <c>IBillingCalculator.CalculateProration</c>,
///     so the unified pricing engine wires the landed proration math without a module cycle.
/// </summary>
public interface IPlanProrationCalculator
{
    /// <summary>Calculates mid-cycle plan-change proration via the existing BillingCalculator.</summary>
    /// <param name="input">Proration inputs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<PlanProrationResult> CalculateProrationAsync(PlanProrationInput input, CancellationToken cancellationToken = default);
}

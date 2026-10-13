namespace GameGuild.Commerce.Payments;

/// <summary>
///     Options for the payment-method expiry monitor (issue #414).
/// </summary>
public class PaymentMethodExpiryOptions
{
    public const string SectionName = "PaymentMethodExpiry";

    /// <summary>
    ///     Whether the daily expiry monitor runs. Default true — expiry notices are the
    ///     safe default for involuntary-churn prevention.
    /// </summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>
    ///     Hours between monitor passes. Default 24 (daily).
    /// </summary>
    public int WorkerIntervalHours { get; set; } = 24;

    /// <summary>
    ///     The notice horizons, in days before the card's expiry month ends. Fixed 30/14/7
    ///     per the issue contract.
    /// </summary>
    public static IReadOnlyList<int> Horizons { get; } = [30, 14, 7];
}

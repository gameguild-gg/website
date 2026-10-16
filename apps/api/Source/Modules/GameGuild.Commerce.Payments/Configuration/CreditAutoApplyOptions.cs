namespace GameGuild.Commerce.Payments;

/// <summary>
///     Account-credit auto-apply options (issue #410 follow-up): when enabled, the local
///     invoice-payment path automatically consumes the paying user's spendable account
///     credits (FIFO by expiry, capped at the invoice amount) before charging the payment
///     method. Disabled by default — opt-in per environment.
/// </summary>
public sealed class CreditAutoApplyOptions
{
    public const string SectionName = "Credits:AutoApply";

    /// <summary>
    ///     Enables automatic credit application on invoice payments. Default false: the
    ///     manual <c>POST /billing/invoices/{id}/account-credits</c> endpoint stays the only
    ///     credit tender until this flag is turned on.
    /// </summary>
    public bool Enabled { get; set; }
}

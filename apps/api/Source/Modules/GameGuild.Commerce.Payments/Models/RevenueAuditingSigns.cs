namespace GameGuild.Commerce.Payments;

/// <summary>
///     Classification of <see cref="RevenueEventType" /> values into credit, debit and
///     neutral sides for net-revenue math. Neutral events (adjustments netted elsewhere,
///     cancellations, opaque "other" entries) are excluded from net totals so reports stay
///     explainable.
/// </summary>
public static class RevenueAuditingSigns
{
    /// <summary>Revenue event types that increase recognized net revenue.</summary>
    public static bool IsCredit(RevenueEventType eventType) => eventType switch
    {
        RevenueEventType.PaymentReceived => true,
        RevenueEventType.SubscriptionStarted => true,
        RevenueEventType.SubscriptionRenewed => true,
        RevenueEventType.CreditIssued => true,
        _ => false
    };

    /// <summary>Revenue event types that decrease recognized net revenue.</summary>
    public static bool IsDebit(RevenueEventType eventType) => eventType switch
    {
        RevenueEventType.RefundProcessed => true,
        RevenueEventType.Chargeback => true,
        RevenueEventType.FeeCharged => true,
        _ => false
    };
}

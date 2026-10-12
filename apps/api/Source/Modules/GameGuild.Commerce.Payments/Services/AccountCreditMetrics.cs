using System.Diagnostics.Metrics;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     OpenTelemetry metrics for the account credit ledger (issue #410), following the
///     payment retry queue metric conventions (GameGuild.Payments.RetryQueue).
/// </summary>
public static class AccountCreditMetrics
{
    public const string MeterName = "GameGuild.Payments.AccountCredits";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> GrantsIssued = Meter.CreateCounter<long>(
        "gameguild_account_credits_grants_issued_total",
        "grants",
        "Account credit grants issued, by currency and source.");

    private static readonly Counter<double> CreditApplied = Meter.CreateCounter<double>(
        "gameguild_account_credits_applied_total",
        "currency units",
        "Account credit applied to invoices, by currency.");

    private static readonly Counter<long> GrantsExpired = Meter.CreateCounter<long>(
        "gameguild_account_credits_grants_expired_total",
        "grants",
        "Account credit grants expired by the daily expiry worker.");

    private static readonly Counter<double> CreditExpired = Meter.CreateCounter<double>(
        "gameguild_account_credits_expired_waste_total",
        "currency units",
        "Remaining account credit balance wasted by expiry, by currency (recorded per pass).");

    private static readonly Counter<long> GiftsExpired = Meter.CreateCounter<long>(
        "gameguild_gifts_expired_total",
        "gifts",
        "Unredeemed gift subscriptions expired by the daily expiry worker.");

    private static readonly Histogram<double> ExpiryCycleDuration = Meter.CreateHistogram<double>(
        "gameguild_account_credits_expiry_cycle_duration_ms",
        "ms",
        "Duration of one bounded account-credit expiry pass.");

    private static readonly Counter<long> ReferralRewardsIssued = Meter.CreateCounter<long>(
        "gameguild_referral_rewards_issued_total",
        "rewards",
        "Referral reward credit grants issued to referrers and referees.");

    /// <summary>Records a credit grant issuance.</summary>
    public static void RecordGrantIssued(string currency, string source) =>
        GrantsIssued.Add(1,
        [
            new KeyValuePair<string, object?>("account_credits.currency", currency),
            new KeyValuePair<string, object?>("account_credits.source", source)
        ]);

    /// <summary>Records credit applied to an invoice.</summary>
    public static void RecordApplied(string currency, decimal amount) =>
        CreditApplied.Add((double)amount, [new KeyValuePair<string, object?>("account_credits.currency", currency)]);

    /// <summary>Records expired grants of one worker pass.</summary>
    public static void RecordExpiredGrants(int grantCount, decimal wastedAmount)
    {
        if (grantCount > 0)
        {
            GrantsExpired.Add(grantCount);
        }

        if (wastedAmount > 0)
        {
            CreditExpired.Add((double)wastedAmount);
        }
    }

    /// <summary>Records expired unredeemed gifts of one worker pass.</summary>
    public static void RecordExpiredGifts(int giftCount)
    {
        if (giftCount > 0)
        {
            GiftsExpired.Add(giftCount);
        }
    }

    /// <summary>Records one expiry pass duration.</summary>
    public static void RecordExpiryCycle(TimeSpan duration) =>
        ExpiryCycleDuration.Record(duration.TotalMilliseconds);

    /// <summary>Records a referral reward grant issuance.</summary>
    public static void RecordReferralReward(string side) =>
        ReferralRewardsIssued.Add(1, [new KeyValuePair<string, object?>("referrals.side", side)]);
}

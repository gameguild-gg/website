namespace GameGuild.Commerce.Payments;

/// <summary>Referral funnel counters of one referrer.</summary>
/// <param name="ReferrerUserId">Referrer the stats belong to.</param>
/// <param name="ReferralCode">The referrer's opaque unique code.</param>
/// <param name="ReferralLink">Trackable share link built from the code.</param>
/// <param name="Shared">Rows created (code shares tracked).</param>
/// <param name="Clicked">Rows with at least one tracked click.</param>
/// <param name="SignedUp">Referees that signed up.</param>
/// <param name="Converted">Referees with a first paid invoice.</param>
/// <param name="RewardIssued">Fully rewarded referrals.</param>
/// <param name="Fraudulent">Referrals blocked by the fraud rules.</param>
/// <param name="Expired">Referrals expired before conversion.</param>
public sealed record ReferralStats(
    Guid ReferrerUserId,
    string ReferralCode,
    string ReferralLink,
    int Shared,
    int Clicked,
    int SignedUp,
    int Converted,
    int RewardIssued,
    int Fraudulent,
    int Expired);

/// <summary>Signup outcome for the referee applying a referral code.</summary>
/// <param name="Referral">The tracking row created or updated.</param>
/// <param name="Blocked">True when a fraud rule blocked the signup.</param>
/// <param name="Reason">Block reason when <paramref name="Blocked" />.</param>
public sealed record ReferralSignupResult(Referral Referral, bool Blocked, string? Reason);

/// <summary>
///     Referral tracking service (issue #410 v1): per-user unique code, click/signup
///     tracking, conversion on the referee's first paid invoice with account-credit
///     rewards for both parties, capped per referrer and blocked on self-referral and
///     same-IP repeats (velocity-style fraud rules from issue #409).
/// </summary>
public interface IReferralService
{
    /// <summary>Gets the user's referral code and link, creating the anchor row on first use (idempotent).</summary>
    Task<Result<(string Code, string Link)>> GetOrCreateCodeAsync(Guid tenantId, Guid referrerUserId, CancellationToken cancellationToken = default);

    /// <summary>Records a funnel click against a code (idempotent per row).</summary>
    Task<Result<Referral>> RecordClickAsync(Guid tenantId, string referralCode, string? refereeEmail, string? ipHash, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attaches the signed-up referee to the code: reuses the anchor row when free,
    ///     otherwise appends a new tracking row. Applies the fraud blocks (self-referral,
    ///     same-IP repeat, referral cap) fail-closed.
    /// </summary>
    Task<Result<ReferralSignupResult>> RecordSignupAsync(Guid tenantId, string referralCode, Guid refereeUserId, string? ipHash, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Handles the referee's first paid invoice: marks the referral Converted and issues
    ///     account-credit rewards to both parties (only after payment, when configured).
    ///     Idempotent per referral; never throws (conversion failures are returned, not raised).
    /// </summary>
    Task<Result> HandleInvoicePaidAsync(Guid tenantId, Guid refereeUserId, Guid subscriptionId, Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Funnel stats of the referrer.</summary>
    Task<Result<ReferralStats>> GetStatsAsync(Guid tenantId, Guid referrerUserId, CancellationToken cancellationToken = default);

    /// <summary>Tracking rows of the referrer, newest first (paged).</summary>
    Task<PagedResult<Referral>> GetHistoryAsync(Guid tenantId, Guid referrerUserId, int skip, int take, CancellationToken cancellationToken = default);
}

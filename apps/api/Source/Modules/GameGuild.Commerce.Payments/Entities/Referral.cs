using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Referral tracking record (issue #410 v1). One opaque referral code is generated per
///     referrer (the first <see cref="ReferralStatus.Pending" /> row anchors the code);
///     every referee journey gets its own row carrying that shared code, so a referrer can
///     accumulate referrals up to the configured cap. The fraud blocks (self-referral,
///     same-IP repeat) reuse the velocity thinking of the payments fraud gate (issue #409).
/// </summary>
[Table("referrals")]
[Index(nameof(TenantId), nameof(ReferrerUserId), nameof(Status))]
[Index(nameof(TenantId), nameof(ReferralCode))]
[Index(nameof(TenantId), nameof(RefereeUserId))]
[Index(nameof(Status), nameof(ReferrerIpHash))]
public class Referral : EntityBase
{
    /// <summary>Default constructor for EF Core.</summary>
    public Referral() { }

    /// <summary>Creates a pending referral record carrying the referrer's unique code.</summary>
    public Referral(Guid tenantId, Guid referrerUserId, string referralCode, string? referralLink = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required for referral entities", nameof(tenantId));
        }

        if (referrerUserId == Guid.Empty)
        {
            throw new ArgumentException("Referrer user is required", nameof(referrerUserId));
        }

        if (string.IsNullOrWhiteSpace(referralCode))
        {
            throw new ArgumentException("Referral code is required", nameof(referralCode));
        }

        TenantId = tenantId;
        ReferrerUserId = referrerUserId;
        ReferralCode = referralCode;
        ReferralLink = referralLink;
        SharedAt = SystemClock.UtcNow;
        Status = ReferralStatus.Pending;
    }

    /// <summary>Tenant ID for multi-tenant isolation.</summary>
    [Required]
    public new Guid TenantId { get; private set; }

    /// <summary>Existing customer sharing the code.</summary>
    [Required]
    public Guid ReferrerUserId { get; private set; }

    /// <summary>Opaque unique referral code (URL-safe).</summary>
    [Required]
    [MaxLength(32)]
    public string ReferralCode { get; private set; } = string.Empty;

    /// <summary>Trackable share link built from the code.</summary>
    [MaxLength(500)]
    public string? ReferralLink { get; private set; }

    /// <summary>Referee email when captured at click time.</summary>
    [MaxLength(255)]
    public string? RefereeEmail { get; private set; }

    /// <summary>Referee user once they signed up.</summary>
    public Guid? RefereeUserId { get; private set; }

    /// <summary>Subscription whose first paid invoice converted the referral.</summary>
    public Guid? RefereeSubscriptionId { get; private set; }

    /// <summary>Invoice that triggered the conversion.</summary>
    public Guid? ConversionInvoiceId { get; private set; }

    /// <summary>SHA-256 hash of the referrer IP captured at click time (fraud block).</summary>
    [MaxLength(64)]
    public string? ReferrerIpHash { get; private set; }

    /// <summary>SHA-256 hash of the referee IP captured at signup (fraud block).</summary>
    [MaxLength(64)]
    public string? RefereeIpHash { get; private set; }

    /// <summary>When the code/link was shared.</summary>
    [Required]
    public DateTime SharedAt { get; private set; }

    /// <summary>First tracked click.</summary>
    public DateTime? ClickedAt { get; private set; }

    /// <summary>Referee signup.</summary>
    public DateTime? SignupAt { get; private set; }

    /// <summary>First paid invoice of the referee.</summary>
    public DateTime? ConversionAt { get; private set; }

    /// <summary>Current funnel status.</summary>
    public ReferralStatus Status { get; private set; }

    /// <summary>Reason captured when the referral was flagged fraudulent.</summary>
    [MaxLength(500)]
    public string? FraudReason { get; private set; }

    /// <summary>Credit grant issued to the referrer.</summary>
    public Guid? ReferrerRewardGrantId { get; private set; }

    /// <summary>Credit grant issued to the referee.</summary>
    public Guid? RefereeRewardGrantId { get; private set; }

    /// <summary>When rewards were issued.</summary>
    public DateTime? RewardIssuedAt { get; private set; }

    /// <summary>Records a funnel click (idempotent: keeps the first click time).</summary>
    public void RecordClick(string? refereeEmail, string? referrerIpHash, DateTime nowUtc)
    {
        if (Status is ReferralStatus.Fraudulent or ReferralStatus.Expired)
        {
            return;
        }

        ClickedAt ??= nowUtc;
        if (!string.IsNullOrWhiteSpace(refereeEmail) && string.IsNullOrEmpty(RefereeEmail))
        {
            RefereeEmail = refereeEmail[..Math.Min(refereeEmail.Length, 255)];
        }

        if (!string.IsNullOrWhiteSpace(referrerIpHash) && string.IsNullOrEmpty(ReferrerIpHash))
        {
            ReferrerIpHash = referrerIpHash;
        }

        if (Status == ReferralStatus.Pending)
        {
            Status = ReferralStatus.Clicked;
        }

        Touch();
    }

    /// <summary>
    ///     Attaches the signed-up referee. Fails closed on self-referral; the caller is
    ///     responsible for the same-IP velocity check before invoking.
    /// </summary>
    public void RecordSignup(Guid refereeUserId, string? refereeIpHash, DateTime nowUtc)
    {
        if (Status is ReferralStatus.Fraudulent or ReferralStatus.Expired)
        {
            throw new InvalidOperationException($"Referral is not signable in status {Status}.");
        }

        if (RefereeUserId.HasValue)
        {
            throw new InvalidOperationException("Referral already has a signed-up referee.");
        }

        if (refereeUserId == ReferrerUserId)
        {
            MarkFraudulent("Self-referral blocked: referee is the referrer.", nowUtc);
            return;
        }

        RefereeUserId = refereeUserId;
        RefereeIpHash = refereeIpHash;
        SignupAt = nowUtc;
        Status = ReferralStatus.SignedUp;
        Touch();
    }

    /// <summary>
    ///     Marks the referral converted by the referee's first paid invoice. Rewards are
    ///     issued separately (<see cref="AttachRewards" />) so conversion and reward grants
    ///     stay independently auditable.
    /// </summary>
    public void RecordConversion(Guid subscriptionId, Guid invoiceId, DateTime nowUtc)
    {
        if (Status is ReferralStatus.Fraudulent or ReferralStatus.Expired)
        {
            throw new InvalidOperationException($"Referral cannot convert in status {Status}.");
        }

        if (Status is ReferralStatus.Converted or ReferralStatus.RewardIssued)
        {
            return; // idempotent replay of the same conversion
        }

        if (!RefereeUserId.HasValue)
        {
            throw new InvalidOperationException("Referral must have a signed-up referee before conversion.");
        }

        RefereeSubscriptionId = subscriptionId;
        ConversionInvoiceId = invoiceId;
        ConversionAt = nowUtc;
        Status = ReferralStatus.Converted;
        Touch();
    }

    /// <summary>Attaches the issued reward credit grants and completes the funnel.</summary>
    public void AttachRewards(Guid? referrerRewardGrantId, Guid? refereeRewardGrantId, DateTime nowUtc)
    {
        if (Status != ReferralStatus.Converted)
        {
            throw new InvalidOperationException($"Rewards require a converted referral, found {Status}.");
        }

        ReferrerRewardGrantId = referrerRewardGrantId;
        RefereeRewardGrantId = refereeRewardGrantId;
        RewardIssuedAt = nowUtc;
        Status = ReferralStatus.RewardIssued;
        Touch();
    }

    /// <summary>Flags the referral fraudulent with a reason (fraud blocks).</summary>
    public void MarkFraudulent(string reason, DateTime nowUtc)
    {
        if (Status is ReferralStatus.RewardIssued)
        {
            return; // already rewarded; retroactive fraud handling is out of v1 scope
        }

        Status = ReferralStatus.Fraudulent;
        FraudReason = reason[..Math.Min(reason.Length, 500)];
        Touch();
    }
}

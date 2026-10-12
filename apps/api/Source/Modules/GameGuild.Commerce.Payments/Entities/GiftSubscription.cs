using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Gift subscription (issue #410 v1) with the status machine
///     Purchased → Delivered → Redeemed / Expired (+ Refunded for unredeemed gifts).
///     Redemption codes are high-entropy URL-safe secrets stored as SHA-256 hashes; only a
///     short non-secret prefix is kept for support lookups. Redeeming grants account credit
///     to the redeeming account (single accounting path) instead of mutating subscriptions.
/// </summary>
[Table("gift_subscriptions")]
[Index(nameof(TenantId), nameof(GiverUserId))]
[Index(nameof(TenantId), nameof(RecipientUserId))]
[Index(nameof(RedemptionCodeHash), IsUnique = true)]
[Index(nameof(Status), nameof(ExpiresAt))]
[Index(nameof(RecipientEmail))]
public class GiftSubscription : EntityBase
{
    /// <summary>Gifts unredeemed after one year expire.</summary>
    public static readonly TimeSpan DefaultRedemptionWindow = TimeSpan.FromDays(365);

    /// <summary>Default constructor for EF Core.</summary>
    public GiftSubscription() { }

    /// <summary>Creates a purchased gift awaiting delivery/redemption.</summary>
    public GiftSubscription(
        Guid tenantId,
        Guid giverUserId,
        string recipientEmail,
        decimal amount,
        string currency,
        int durationMonths,
        string redemptionCodeHash,
        string redemptionCodePrefix,
        Guid? planId = null,
        string? recipientName = null,
        string? personalMessage = null,
        DateTime? deliveryDate = null,
        Guid? paymentId = null,
        DateTime? purchasedAt = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required for gift entities", nameof(tenantId));
        }

        if (giverUserId == Guid.Empty)
        {
            throw new ArgumentException("GiverUserId is required for gift entities", nameof(giverUserId));
        }

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new ArgumentException("Recipient email is required", nameof(recipientEmail));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Gift amount must be positive", nameof(amount));
        }

        if (durationMonths is < 1 or > 36)
        {
            throw new ArgumentException("Gift duration must be between 1 and 36 months", nameof(durationMonths));
        }

        if (personalMessage is { Length: > 500 })
        {
            throw new ArgumentException("Personal message cannot exceed 500 characters", nameof(personalMessage));
        }

        if (recipientEmail.Length > 255)
        {
            throw new ArgumentException("Recipient email cannot exceed 255 characters", nameof(recipientEmail));
        }

        if (recipientName is { Length: > 255 })
        {
            throw new ArgumentException("Recipient name cannot exceed 255 characters", nameof(recipientName));
        }

        var now = purchasedAt ?? SystemClock.UtcNow;

        TenantId = tenantId;
        GiverUserId = giverUserId;
        RecipientEmail = recipientEmail;
        RecipientName = recipientName;
        Amount = amount;
        Currency = currency;
        DurationMonths = durationMonths;
        PlanId = planId;
        PersonalMessage = personalMessage;
        DeliveryDate = deliveryDate;
        PaymentId = paymentId;
        RedemptionCodeHash = redemptionCodeHash;
        RedemptionCodePrefix = redemptionCodePrefix;
        PurchasedAt = now;
        ExpiresAt = now.Add(DefaultRedemptionWindow);
        Status = GiftStatus.Purchased;
    }

    /// <summary>Tenant ID for multi-tenant isolation.</summary>
    [Required]
    public new Guid TenantId { get; private set; }

    /// <summary>User who purchased the gift.</summary>
    [Required]
    public Guid GiverUserId { get; private set; }

    /// <summary>Recipient email the gift is addressed to.</summary>
    [Required]
    [MaxLength(255)]
    public string RecipientEmail { get; private set; } = string.Empty;

    /// <summary>Optional recipient display name.</summary>
    [MaxLength(255)]
    public string? RecipientName { get; private set; }

    /// <summary>User that redeemed the gift; set at redemption.</summary>
    public Guid? RecipientUserId { get; private set; }

    /// <summary>Optional plan the gift applies to; null means any plan.</summary>
    public Guid? PlanId { get; private set; }

    /// <summary>Gift duration in months (1-36).</summary>
    public int DurationMonths { get; private set; }

    /// <summary>Monetary value granted on redemption.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; private set; }

    /// <summary>ISO 4217 currency code.</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; private set; } = "USD";

    /// <summary>Optional personal message from the giver.</summary>
    [MaxLength(500)]
    public string? PersonalMessage { get; private set; }

    /// <summary>Optional scheduled delivery date; redemption is blocked before it.</summary>
    public DateTime? DeliveryDate { get; private set; }

    /// <summary>Current status machine state.</summary>
    public GiftStatus Status { get; private set; }

    /// <summary>SHA-256 hash of the full redemption code (secret never stored in clear).</summary>
    [Required]
    [MaxLength(64)]
    public string RedemptionCodeHash { get; private set; } = string.Empty;

    /// <summary>Non-secret code prefix for support lookups.</summary>
    [Required]
    [MaxLength(8)]
    public string RedemptionCodePrefix { get; private set; } = string.Empty;

    /// <summary>When the gift was purchased.</summary>
    [Required]
    public DateTime PurchasedAt { get; private set; }

    /// <summary>When the gift was delivered (delivery date reached).</summary>
    public DateTime? DeliveredAt { get; private set; }

    /// <summary>When the gift was redeemed.</summary>
    public DateTime? RedeemedAt { get; private set; }

    /// <summary>Redemption deadline: purchased at + 1 year.</summary>
    [Required]
    public DateTime ExpiresAt { get; private set; }

    /// <summary>When the unredeemed gift expired.</summary>
    public DateTime? ExpiredAt { get; private set; }

    /// <summary>Optional payment reference for the gift purchase.</summary>
    public Guid? PaymentId { get; private set; }

    /// <summary>Credit grant produced by redemption.</summary>
    public Guid? CreditGrantId { get; private set; }

    /// <summary>Whether the gift is redeemable right now (delivered or immediately deliverable, not expired).</summary>
    public bool IsRedeemableAt(DateTime nowUtc)
        => Status is GiftStatus.Purchased or GiftStatus.Delivered
           && nowUtc < ExpiresAt
           && (DeliveryDate is null || DeliveryDate.Value <= nowUtc);

    /// <summary>Marks the gift delivered once its delivery date has been reached.</summary>
    public void MarkDelivered(DateTime nowUtc)
    {
        if (Status != GiftStatus.Purchased)
        {
            return;
        }

        if (DeliveryDate is { } delivery && delivery > nowUtc)
        {
            return;
        }

        Status = GiftStatus.Delivered;
        DeliveredAt = nowUtc;
        Touch();
    }

    /// <summary>
    ///     Redeems the gift for <paramref name="recipientUserId" />. Fails closed unless the
    ///     gift is redeemable at <paramref name="nowUtc" /> and the redeemer is not the giver.
    /// </summary>
    public void Redeem(Guid recipientUserId, DateTime nowUtc)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("Recipient user is required", nameof(recipientUserId));
        }

        if (Status == GiftStatus.Redeemed)
        {
            throw new InvalidOperationException("Gift has already been redeemed.");
        }

        if (Status is GiftStatus.Expired or GiftStatus.Refunded)
        {
            throw new InvalidOperationException($"Gift is not redeemable in status {Status}.");
        }

        if (nowUtc >= ExpiresAt)
        {
            throw new InvalidOperationException("Gift redemption window has expired.");
        }

        if (DeliveryDate is { } delivery && delivery > nowUtc)
        {
            throw new InvalidOperationException("Gift cannot be redeemed before its scheduled delivery date.");
        }

        if (recipientUserId == GiverUserId)
        {
            throw new InvalidOperationException("A gift cannot be redeemed by its giver.");
        }

        Status = GiftStatus.Redeemed;
        RecipientUserId = recipientUserId;
        RedeemedAt = nowUtc;
        if (DeliveredAt is null)
        {
            DeliveredAt = nowUtc;
        }

        Touch();
    }

    /// <summary>Links the credit grant produced by redemption.</summary>
    public void AttachCreditGrant(Guid creditGrantId)
    {
        if (Status != GiftStatus.Redeemed)
        {
            throw new InvalidOperationException("Credit grants can only attach to redeemed gifts.");
        }

        CreditGrantId = creditGrantId;
        Touch();
    }

    /// <summary>Marks the unredeemed gift expired (worker path).</summary>
    public void MarkExpired(DateTime nowUtc)
    {
        if (Status is not (GiftStatus.Purchased or GiftStatus.Delivered))
        {
            return;
        }

        if (nowUtc < ExpiresAt)
        {
            return;
        }

        Status = GiftStatus.Expired;
        ExpiredAt = nowUtc;
        Touch();
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Tenant-scoped local registry entry for a payment method tokenized at the gateway
///     (issue #414). Only gateway references and display metadata are stored — never raw
///     card data (PCI DSS: tokenization only).
/// </summary>
[Table("payment_methods")]
[Index(nameof(TenantId), nameof(UserId), nameof(IsDefault))]
[Index(nameof(ExpiresAt))]
[Index(nameof(GatewayPaymentMethodId))]
public class PaymentMethod : EntityBase
{
    /// <summary>
    ///     Parameterless constructor for EF Core.
    /// </summary>
    private PaymentMethod()
    {
    }

    /// <summary>
    ///     Creates a local payment-method record (fail-closed: tenant and gateway reference required).
    /// </summary>
    public PaymentMethod(
        Guid tenantId,
        Guid userId,
        string gatewayProvider,
        string gatewayPaymentMethodId,
        PaymentMethodType type,
        bool isDefault)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required for financial entities (fail-closed)", nameof(tenantId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required for payment method ownership (fail-closed)", nameof(userId));
        if (string.IsNullOrWhiteSpace(gatewayProvider))
            throw new ArgumentException("Gateway provider is required (fail-closed)", nameof(gatewayProvider));
        if (string.IsNullOrWhiteSpace(gatewayPaymentMethodId))
            throw new ArgumentException("Gateway payment method ID is required (fail-closed; raw card data is never accepted)", nameof(gatewayPaymentMethodId));

        TenantId = tenantId;
        UserId = userId;
        GatewayProvider = gatewayProvider.Trim().ToLowerInvariant();
        GatewayPaymentMethodId = gatewayPaymentMethodId.Trim();
        Type = type;
        IsDefault = isDefault;
        Status = PaymentMethodStatus.Active;
    }

    /// <summary>
    ///     User that owns this payment method (self-service scope within the tenant).
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     Coarse payment-method family (card / bank account / wallet). Stripe-only in v1 wiring.
    /// </summary>
    public PaymentMethodType Type { get; private set; }

    /// <summary>
    ///     Gateway provider key (e.g. "stripe"). Multi-provider support is an open extension (#780).
    /// </summary>
    [MaxLength(50)]
    public string GatewayProvider { get; private set; } = string.Empty;

    /// <summary>
    ///     Gateway payment method reference (Stripe <c>pm_...</c>). Token reference only —
    ///     never raw card data.
    /// </summary>
    [MaxLength(255)]
    public string GatewayPaymentMethodId { get; private set; } = string.Empty;

    /// <summary>
    ///     Whether this is the owner's default payment method within the tenant.
    ///     Enforced unique per (tenant, user) among non-deleted rows by
    ///     <c>ix_payment_methods_single_default</c>.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>
    ///     Lifecycle status. Expiry monitoring flips <see cref="PaymentMethodStatus.Active"/>
    ///     to <see cref="PaymentMethodStatus.Expired"/> after the card's expiry month ends.
    /// </summary>
    public PaymentMethodStatus Status { get; private set; }

    /// <summary>
    ///     Card brand for display (Visa, Mastercard, ...). Null for non-card methods.
    /// </summary>
    [MaxLength(50)]
    public string? CardBrand { get; private set; }

    /// <summary>
    ///     Last four digits for display only (e.g. "4242").
    /// </summary>
    [MaxLength(4)]
    public string? CardLast4 { get; private set; }

    /// <summary>
    ///     Card expiry month (1–12) as reported by the gateway.
    /// </summary>
    public int? CardExpMonth { get; private set; }

    /// <summary>
    ///     Card expiry year (4 digits) as reported by the gateway.
    /// </summary>
    public int? CardExpYear { get; private set; }

    /// <summary>
    ///     Gateway card fingerprint — used for duplicate-card detection, not for charging.
    /// </summary>
    [MaxLength(255)]
    public string? CardFingerprint { get; private set; }

    /// <summary>
    ///     Optional user-assigned nickname ("Corporate Card").
    /// </summary>
    [MaxLength(100)]
    public string? Nickname { get; private set; }

    /// <summary>
    ///     Optional billing address as serialized JSON (display/reference only).
    /// </summary>
    [MaxLength(1000)]
    public string? BillingAddress { get; private set; }

    /// <summary>
    ///     End of the card's expiry month (inclusive). Null when the gateway reports no expiry.
    ///     Drives the 30/14/7-day expiry notification horizons and the auto-expire transition.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    ///     When this payment method was last used for a successful charge.
    /// </summary>
    public DateTime? LastUsedAt { get; private set; }

    /// <summary>
    ///     When the 30-day expiry notice was sent (idempotency for the daily monitor).
    /// </summary>
    public DateTime? ExpiryNotice30SentAt { get; private set; }

    /// <summary>
    ///     When the 14-day expiry notice was sent (idempotency for the daily monitor).
    /// </summary>
    public DateTime? ExpiryNotice14SentAt { get; private set; }

    /// <summary>
    ///     When the 7-day expiry notice was sent (idempotency for the daily monitor).
    /// </summary>
    public DateTime? ExpiryNotice7SentAt { get; private set; }

    /// <summary>
    ///     Card validity window is the expiry month, inclusive of its last day.
    /// </summary>
    public static DateTime? ComputeExpiresAt(int? expMonth, int? expYear)
    {
        if (expMonth is not { } month || expYear is not { } year)
        {
            return null;
        }

        if (month is < 1 or > 12 || year < 2000 || year > 2200)
        {
            return null;
        }

        return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddTicks(-1);
    }

    /// <summary>
    ///     Updates the gateway-reported display metadata (idempotent upsert path).
    /// </summary>
    public void UpdateGatewayMetadata(
        PaymentMethodType type,
        string? cardBrand,
        string? cardLast4,
        int? cardExpMonth,
        int? cardExpYear,
        string? cardFingerprint)
    {
        Type = type;
        CardBrand = Normalize(cardBrand, 50);
        CardLast4 = Normalize(cardLast4, 4);
        CardFingerprint = Normalize(cardFingerprint, 255);
        CardExpMonth = cardExpMonth;
        CardExpYear = cardExpYear;
        ExpiresAt = ComputeExpiresAt(cardExpMonth, cardExpYear);
        Touch();
    }

    /// <summary>
    ///     Updates user-editable display fields only (nickname, billing address).
    /// </summary>
    public void UpdateDisplay(string? nickname, string? billingAddress)
    {
        if (nickname is not null && nickname.Trim().Length > 100)
            throw new ArgumentException("Nickname cannot exceed 100 characters", nameof(nickname));
        if (billingAddress is not null && billingAddress.Length > 1000)
            throw new ArgumentException("Billing address JSON cannot exceed 1000 characters", nameof(billingAddress));

        Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim();
        BillingAddress = string.IsNullOrWhiteSpace(billingAddress) ? null : billingAddress.Trim();
        Touch();
    }

    /// <summary>
    ///     Marks this method as its owner's default.
    /// </summary>
    public void MarkAsDefault()
    {
        if (IsDefault)
        {
            return;
        }

        IsDefault = true;
        Touch();
        Raise(new PaymentMethodSetAsDefaultEvent(Id, TenantId ?? Guid.Empty, UserId));
    }

    /// <summary>
    ///     Demotes this method from default (when another one takes over).
    /// </summary>
    public void ClearDefault()
    {
        if (!IsDefault)
        {
            return;
        }

        IsDefault = false;
        Touch();
    }

    /// <summary>
    ///     Records a successful charge on this method.
    /// </summary>
    public void MarkUsed(DateTime usedAtUtc)
    {
        LastUsedAt = usedAtUtc;
        Touch();
    }

    /// <summary>
    ///     Auto-expires the method once its expiry month has passed (expiry monitor).
    /// </summary>
    public void MarkExpired()
    {
        if (Status == PaymentMethodStatus.Expired)
        {
            return;
        }

        Status = PaymentMethodStatus.Expired;
        Raise(new PaymentMethodExpiredEvent(Id, TenantId ?? Guid.Empty, UserId));
        Touch();
    }

    /// <summary>
    ///     Records that the expiry notice for a horizon was issued (idempotency marker).
    /// </summary>
    public void RecordExpiryNotice(int horizonDays, DateTime sentAtUtc)
    {
        switch (horizonDays)
        {
            case 30:
                if (ExpiryNotice30SentAt is null) { ExpiryNotice30SentAt = sentAtUtc; Touch(); }
                break;
            case 14:
                if (ExpiryNotice14SentAt is null) { ExpiryNotice14SentAt = sentAtUtc; Touch(); }
                break;
            case 7:
                if (ExpiryNotice7SentAt is null) { ExpiryNotice7SentAt = sentAtUtc; Touch(); }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(horizonDays), horizonDays, "Only the 30/14/7-day horizons are tracked.");
        }
    }

    /// <summary>
    ///     Whether the expiry notice for a horizon was already issued.
    /// </summary>
    public bool HasExpiryNotice(int horizonDays) => horizonDays switch
    {
        30 => ExpiryNotice30SentAt.HasValue,
        14 => ExpiryNotice14SentAt.HasValue,
        7 => ExpiryNotice7SentAt.HasValue,
        _ => throw new ArgumentOutOfRangeException(nameof(horizonDays), horizonDays, "Only the 30/14/7-day horizons are tracked.")
    };

    /// <summary>
    ///     Days remaining before the card expires (rounded up; negative once expired).
    /// </summary>
    public int? GetDaysUntilExpiry(DateTime nowUtc)
        => ExpiresAt is { } expiresAt
            ? (int)Math.Ceiling((expiresAt - nowUtc).TotalDays)
            : null;

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

/// <summary>
///     Coarse payment-method families. Stripe is the only wired provider in v1.
/// </summary>
public enum PaymentMethodType
{
    /// <summary>Credit or debit card</summary>
    Card = 0,

    /// <summary>Bank account (ACH) — verification flow deferred</summary>
    BankAccount = 1,

    /// <summary>Wallet (PayPal, Apple Pay, Google Pay)</summary>
    Wallet = 2
}

/// <summary>
///     Local lifecycle status of a registered payment method.
/// </summary>
public enum PaymentMethodStatus
{
    /// <summary>Usable for charges</summary>
    Active = 0,

    /// <summary>Expiry month has passed (auto-marked by the expiry monitor)</summary>
    Expired = 1,

    /// <summary>Gateway reported the method as failed/unusable</summary>
    Failed = 2
}

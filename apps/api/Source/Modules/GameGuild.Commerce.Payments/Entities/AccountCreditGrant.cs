using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     A grant of account credit issued to a user (issue #410 v1). Grants are separated by
///     source, currency, credit type and expiry; consumption always drains grants FIFO by
///     expiry (earliest <see cref="ExpiresAt" /> first, non-expiring last). Creation is
///     idempotent per <see cref="IdempotencyKey" /> within a tenant.
/// </summary>
[Table("account_credit_grants")]
[Index(nameof(TenantId), nameof(UserId), nameof(Status))]
[Index(nameof(TenantId), nameof(IdempotencyKey), IsUnique = true)]
[Index(nameof(ExpiresAt))]
[Index(nameof(Source), nameof(SourceReferenceId))]
public class AccountCreditGrant : EntityBase
{
    /// <summary>Default constructor for EF Core.</summary>
    public AccountCreditGrant() { }

    /// <summary>
    ///     Creates a new credit grant. Fails closed on empty tenant/user, non-positive amount
    ///     and unknown ISO 4217 currencies.
    /// </summary>
    public AccountCreditGrant(
        Guid tenantId,
        Guid userId,
        decimal amount,
        string currency,
        AccountCreditSource source,
        AccountCreditType creditType,
        string description,
        Guid? sourceReferenceId = null,
        DateTime? expiresAt = null,
        string? idempotencyKey = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required for account credit entities", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required for account credit entities", nameof(userId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Credit amount must be positive", nameof(amount));
        }

        if (expiresAt.HasValue && expiresAt.Value <= SystemClock.UtcNow)
        {
            throw new ArgumentException("Credit expiry must be in the future", nameof(expiresAt));
        }

        if (idempotencyKey is { Length: > 200 })
        {
            throw new ArgumentException("Idempotency key cannot exceed 200 characters", nameof(idempotencyKey));
        }

        TenantId = tenantId;
        UserId = userId;
        Amount = amount;
        RemainingAmount = amount;
        Currency = currency;
        Source = source;
        CreditType = creditType;
        Description = description;
        SourceReferenceId = sourceReferenceId;
        ExpiresAt = expiresAt;
        IdempotencyKey = idempotencyKey;
        Status = AccountCreditGrantStatus.Active;
    }

    /// <summary>Tenant ID for multi-tenant isolation.</summary>
    [Required]
    public new Guid TenantId { get; private set; }

    /// <summary>Owner of the credit.</summary>
    [Required]
    public Guid UserId { get; private set; }

    /// <summary>ISO 4217 currency code (upper-case, three letters).</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; private set; } = "USD";

    /// <summary>Where the credit came from (gift redemption, referral reward, manual issue, refund).</summary>
    public AccountCreditSource Source { get; private set; }

    /// <summary>What the credit may be applied to (general, subscription-only, addon-only).</summary>
    public AccountCreditType CreditType { get; private set; }

    /// <summary>Optional reference to the originating entity (gift id, referral id, invoice id).</summary>
    public Guid? SourceReferenceId { get; private set; }

    /// <summary>Human-readable description captured at issuance.</summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; private set; } = string.Empty;

    /// <summary>Original granted amount.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; private set; }

    /// <summary>Amount still available to consume.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingAmount { get; private set; }

    /// <summary>Optional expiry; null means the grant does not expire.</summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>Lifecycle status of the grant.</summary>
    public AccountCreditGrantStatus Status { get; private set; }

    /// <summary>When the grant was fully consumed, if it was.</summary>
    public DateTime? FullyUsedAt { get; private set; }

    /// <summary>Reason captured when the grant was revoked.</summary>
    [MaxLength(500)]
    public string? RevocationReason { get; private set; }

    /// <summary>Idempotency key unique per tenant; replaying a grant with the same key returns the original.</summary>
    [MaxLength(200)]
    public string? IdempotencyKey { get; private set; }

    /// <summary>Whether the grant currently holds spendable credit.</summary>
    public bool IsSpendable => Status == AccountCreditGrantStatus.Active && RemainingAmount > 0;

    /// <summary>
    ///     Consumes up to <paramref name="amount" /> from this grant, FIFO-consumption safe.
    ///     Returns the amount actually taken (bounded by the remaining balance).
    /// </summary>
    public decimal Consume(decimal amount, DateTime nowUtc)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Consumption amount must be positive", nameof(amount));
        }

        if (!IsSpendable)
        {
            return 0;
        }

        var taken = Math.Min(amount, RemainingAmount);
        RemainingAmount -= taken;

        if (RemainingAmount == 0)
        {
            Status = AccountCreditGrantStatus.FullyUsed;
            FullyUsedAt = nowUtc;
        }

        Touch();
        return taken;
    }

    /// <summary>Revokes the remaining balance of the grant with an auditable reason.</summary>
    public void Revoke(string reason, DateTime nowUtc)
    {
        if (Status is AccountCreditGrantStatus.FullyUsed or AccountCreditGrantStatus.Expired
            or AccountCreditGrantStatus.Revoked)
        {
            throw new InvalidOperationException($"Cannot revoke a grant in status {Status}.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Revocation reason is required", nameof(reason));
        }

        Status = AccountCreditGrantStatus.Revoked;
        RevocationReason = reason[..Math.Min(reason.Length, 500)];
        RemainingAmount = 0;
        Touch();
    }

    /// <summary>Marks the grant expired (daily expiry worker path).</summary>
    public void MarkExpired(DateTime nowUtc)
    {
        if (Status != AccountCreditGrantStatus.Active)
        {
            return;
        }

        Status = AccountCreditGrantStatus.Expired;
        Touch();
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Immutable ledger row per account-credit lifecycle event (issue #410 v1): exactly one
///     row is appended for every grant, consumption, revocation and expiry of an
///     <see cref="AccountCreditGrant" />.
/// </summary>
[Table("account_credit_transactions")]
[Index(nameof(GrantId))]
[Index(nameof(TenantId), nameof(UserId), nameof(CreatedAt))]
[Index(nameof(ReferenceId))]
[Index(nameof(IdempotencyKey))]
public class AccountCreditTransaction : EntityBase
{
    /// <summary>Default constructor for EF Core.</summary>
    public AccountCreditTransaction() { }

    /// <summary>Appends a ledger row for <paramref name="type" /> against a grant.</summary>
    public AccountCreditTransaction(
        Guid tenantId,
        Guid userId,
        Guid grantId,
        AccountCreditTransactionType type,
        decimal amount,
        decimal remainingAfter,
        string currency,
        string description,
        Guid? referenceId = null,
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
            throw new ArgumentException("Transaction amount must be positive", nameof(amount));
        }

        if (remainingAfter < 0)
        {
            throw new ArgumentException("Remaining balance cannot be negative", nameof(remainingAfter));
        }

        TenantId = tenantId;
        UserId = userId;
        GrantId = grantId;
        Type = type;
        Amount = amount;
        RemainingAfter = remainingAfter;
        Currency = currency;
        Description = description;
        ReferenceId = referenceId;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Tenant ID for multi-tenant isolation.</summary>
    [Required]
    public new Guid TenantId { get; private set; }

    /// <summary>Owner of the credit the row belongs to.</summary>
    [Required]
    public Guid UserId { get; private set; }

    /// <summary>Grant the row was appended for.</summary>
    [Required]
    public Guid GrantId { get; private set; }

    /// <summary>Lifecycle event kind.</summary>
    public AccountCreditTransactionType Type { get; private set; }

    /// <summary>Amount involved in the event (granted, consumed, revoked or expired).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; private set; }

    /// <summary>Grant remaining balance right after the event.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingAfter { get; private set; }

    /// <summary>ISO 4217 currency code of the grant.</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; private set; } = "USD";

    /// <summary>Description captured for the ledger row.</summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; private set; } = string.Empty;

    /// <summary>Optional reference (invoice id for consumption, admin actor note, etc.).</summary>
    public Guid? ReferenceId { get; private set; }

    /// <summary>Idempotency key of the operation that produced the row, when one was supplied.</summary>
    [MaxLength(200)]
    public string? IdempotencyKey { get; private set; }
}

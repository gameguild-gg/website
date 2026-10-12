using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Atomic multi-tender settlement record (issue #410 v1): one row per invoice a user
///     applied account credits to. The unique index on (tenant, idempotency key) makes the
///     apply-to-invoice endpoint idempotent; the invoice reference separates the credit
///     tender from provider tenders recorded on the invoice itself.
/// </summary>
[Table("account_credit_invoice_settlements")]
[Index(nameof(TenantId), nameof(InvoiceId))]
[Index(nameof(TenantId), nameof(IdempotencyKey), IsUnique = true)]
public class AccountCreditInvoiceSettlement : EntityBase
{
    /// <summary>Default constructor for EF Core.</summary>
    public AccountCreditInvoiceSettlement() { }

    /// <summary>Records a credit tender applied to an invoice.</summary>
    public AccountCreditInvoiceSettlement(
        Guid tenantId,
        Guid userId,
        Guid invoiceId,
        decimal amount,
        string currency,
        string idempotencyKey)
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
            throw new ArgumentException("Settlement amount must be positive", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required for invoice settlements", nameof(idempotencyKey));
        }

        if (idempotencyKey.Length > 200)
        {
            throw new ArgumentException("Idempotency key cannot exceed 200 characters", nameof(idempotencyKey));
        }

        TenantId = tenantId;
        UserId = userId;
        InvoiceId = invoiceId;
        Amount = amount;
        Currency = currency;
        IdempotencyKey = idempotencyKey;
        AppliedAt = SystemClock.UtcNow;
    }

    /// <summary>Tenant ID for multi-tenant isolation.</summary>
    [Required]
    public new Guid TenantId { get; private set; }

    /// <summary>User whose credits settled the invoice.</summary>
    [Required]
    public Guid UserId { get; private set; }

    /// <summary>Settled invoice reference (Billing module entity).</summary>
    [Required]
    public Guid InvoiceId { get; private set; }

    /// <summary>Credit tender amount applied to the invoice.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; private set; }

    /// <summary>ISO 4217 currency code shared by the invoice and the consumed grants.</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; private set; } = "USD";

    /// <summary>Idempotency key of the apply request (unique per tenant).</summary>
    [Required]
    [MaxLength(200)]
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>When the settlement was applied.</summary>
    [Required]
    public DateTime AppliedAt { get; private set; }
}

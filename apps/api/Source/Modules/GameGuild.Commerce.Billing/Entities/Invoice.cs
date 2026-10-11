using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Represents an immutable invoice for billing purposes.
///     Once issued (status != Draft), amounts CANNOT be changed.
///     This is a critical financial invariant.
/// </summary>
[Table("invoices")]
[Index(nameof(TenantId), nameof(Status))]
[Index(nameof(SubscriptionId))]
[Index(nameof(InvoiceNumber), IsUnique = true)]
[Index(nameof(ExternalId), IsUnique = true)]
[Index(nameof(IdempotencyKey), IsUnique = true)]
[Index(nameof(DueDate))]
[Index(nameof(IssuedAt))]
[Index(nameof(PaymentId), IsUnique = true, Name = "IX_invoices_PaymentId_Unique")]
public class Invoice : EntityBase
{
    /// <summary>
    ///     Private constructor for EF Core
    /// </summary>
    private Invoice() { }

    /// <summary>
    ///     Creates a new draft invoice with required TenantId (fail-closed)
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when tenantId is empty</exception>
    public Invoice(Guid tenantId, Guid subscriptionId, decimal amount, string currency = "USD", string? idempotencyKey = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required for financial entities", nameof(tenantId));
        }

        if (idempotencyKey is { Length: > 200 })
        {
            throw new ArgumentException("Idempotency key cannot exceed 200 characters", nameof(idempotencyKey));
        }

        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        Subtotal = amount;
        Total = amount;
        Currency = currency;
        Status = InvoiceStatus.Draft;
        InvoiceNumber = GenerateInvoiceNumber();
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>
    ///     Unique invoice number (human-readable)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; private set; } = string.Empty;

    /// <summary>
    ///     External ID from payment provider (e.g., Stripe invoice ID)
    /// </summary>
    [MaxLength(255)]
    public string? ExternalId { get; private set; }

    /// <summary>
    ///     Idempotency key identifying the subscription billing cycle this invoice materializes
    ///     (format: subscription:{{subscriptionId}}:cycle:{{n}}:invoice). Unique index backstops
    ///     the check-then-create materialization so a confirmed cycle can never produce two invoices.
    /// </summary>
    [MaxLength(200)]
    public string? IdempotencyKey { get; private set; }

    /// <summary>
    ///     Related subscription ID
    /// </summary>
    [Required]
    public Guid SubscriptionId { get; private set; }

    /// <summary>
    ///     Billing period start date
    /// </summary>
    public DateTime? PeriodStart { get; private set; }

    /// <summary>
    ///     Billing period end date
    /// </summary>
    public DateTime? PeriodEnd { get; private set; }

    /// <summary>
    ///     Invoice status
    /// </summary>
    public InvoiceStatus Status { get; private set; }

    /// <summary>
    ///     Subtotal before tax
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; private set; }

    /// <summary>
    ///     Tax amount
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; private set; }

    /// <summary>
    ///     Discount amount
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; private set; }

    /// <summary>
    ///     Total amount due (immutable after issuance)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; private set; }

    /// <summary>
    ///     Amount paid
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; private set; }

    /// <summary>
    ///     Amount remaining to be paid
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountRemaining { get => Total - AmountPaid; }

    /// <summary>
    ///     Currency code (ISO 4217)
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; private set; } = "USD";

    /// <summary>
    ///     When the invoice was issued (becomes immutable)
    /// </summary>
    public DateTime? IssuedAt { get; private set; }

    /// <summary>
    ///     When payment is due
    /// </summary>
    public DateTime? DueDate { get; private set; }

    /// <summary>
    ///     When the invoice was paid in full
    /// </summary>
    public DateTime? PaidAt { get; private set; }

    /// <summary>
    ///     When the invoice was voided (if applicable)
    /// </summary>
    public DateTime? VoidedAt { get; private set; }

    /// <summary>
    ///     Reason for voiding
    /// </summary>
    [MaxLength(500)]
    public string? VoidReason { get; private set; }

    /// <summary>
    ///     Description/memo for the invoice
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; private set; }

    /// <summary>
    ///     Metadata as JSON
    /// </summary>
    [MaxLength(4000)]
    public string? Metadata { get; private set; }

    /// <summary>
    ///     ID of the payment that paid this invoice (single payment per invoice invariant)
    /// </summary>
    public Guid? PaymentId { get; private set; }

    /// <summary>
    ///     Whether this invoice has been modified (amounts cannot change after issue)
    /// </summary>
    public bool IsImmutable { get => Status != InvoiceStatus.Draft; }

    /// <summary>
    ///     Sets the billing period for this invoice.
    ///     Only allowed while invoice is in Draft status.
    /// </summary>
    public void SetBillingPeriod(DateTime periodStart, DateTime periodEnd)
    {
        EnsureMutable();
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Touch();
    }

    /// <summary>
    ///     Sets the free-form metadata JSON for this invoice.
    ///     Only allowed while invoice is in Draft status.
    /// </summary>
    public void SetMetadata(string metadata)
    {
        EnsureMutable();
        if (string.IsNullOrWhiteSpace(metadata))
        {
            throw new ArgumentException("Metadata cannot be empty", nameof(metadata));
        }

        if (metadata.Length > 4000)
        {
            throw new ArgumentException("Metadata cannot exceed 4000 characters", nameof(metadata));
        }

        Metadata = metadata;
        Touch();
    }

    /// <summary>
    ///     Sets the description/memo (the minimal single line item label).
    ///     Only allowed while invoice is in Draft status.
    /// </summary>
    public void SetDescription(string description)
    {
        EnsureMutable();
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be empty", nameof(description));
        }

        if (description.Length > 1000)
        {
            throw new ArgumentException("Description cannot exceed 1000 characters", nameof(description));
        }

        Description = description;
        Touch();
    }

    /// <summary>
    ///     Applies a discount to the invoice.
    ///     Only allowed while invoice is in Draft status.
    /// </summary>
    public void ApplyDiscount(decimal discountAmount)
    {
        EnsureMutable();
        if (discountAmount < 0)
        {
            throw new ArgumentException("Discount amount cannot be negative", nameof(discountAmount));
        }

        if (discountAmount > Subtotal)
        {
            throw new ArgumentException("Discount cannot exceed subtotal", nameof(discountAmount));
        }

        DiscountAmount = discountAmount;
        RecalculateTotal();
        Touch();
    }

    /// <summary>
    ///     Sets the tax amount for the invoice.
    ///     Only allowed while invoice is in Draft status.
    /// </summary>
    public void SetTax(decimal taxAmount)
    {
        EnsureMutable();
        if (taxAmount < 0)
        {
            throw new ArgumentException("Tax amount cannot be negative", nameof(taxAmount));
        }

        TaxAmount = taxAmount;
        RecalculateTotal();
        Touch();
    }

    /// <summary>
    ///     Issues the invoice, making it immutable.
    ///     After this, amounts cannot be changed - only voided and re-issued.
    /// </summary>
    public void Issue(DateTime? dueDate = null)
    {
        EnsureMutable();
        if (Total <= 0)
        {
            throw new InvalidOperationException("Cannot issue an invoice with zero or negative total");
        }

        Status = InvoiceStatus.Open;
        IssuedAt = SystemClock.UtcNow;
        DueDate = dueDate ?? SystemClock.UtcNow.AddDays(30);
        Touch();
    }

    /// <summary>
    ///     Records a payment against this invoice.
    ///     Enforces single-payment invariant.
    /// </summary>
    public void RecordPayment(Guid paymentId, decimal amount, DateTime paymentDate)
    {
        if (Status == InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Cannot record payment on a draft invoice");
        }

        if (Status == InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Invoice is already paid");
        }

        if (Status == InvoiceStatus.Void)
        {
            throw new InvalidOperationException("Cannot record payment on a voided invoice");
        }

        if (PaymentId.HasValue)
        {
            if (PaymentId.Value != paymentId)
            {
                throw new InvalidOperationException($"Invoice already has payment {PaymentId}. Single payment per invoice enforced.");
            }
        }

        PaymentId = paymentId;
        AmountPaid = amount;

        if (AmountPaid >= Total)
        {
            Status = InvoiceStatus.Paid;
            PaidAt = paymentDate;
        }

        Touch();
    }

    /// <summary>
    ///     Voids the invoice. Cannot void a paid invoice.
    /// </summary>
    public void Void(string reason)
    {
        if (Status == InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Cannot void a paid invoice");
        }

        if (Status == InvoiceStatus.Void)
        {
            return; // Idempotent
        }

        Status = InvoiceStatus.Void;
        VoidedAt = SystemClock.UtcNow;
        VoidReason = reason;
        Touch();
    }

    /// <summary>
    ///     Marks invoice as uncollectible (bad debt)
    /// </summary>
    public void MarkUncollectible()
    {
        if (Status != InvoiceStatus.Open && Status != InvoiceStatus.PastDue)
        {
            throw new InvalidOperationException("Can only mark open or past due invoices as uncollectible");
        }

        Status = InvoiceStatus.Uncollectible;
        Touch();
    }

    /// <summary>
    ///     Marks invoice as past due
    /// </summary>
    public void MarkPastDue()
    {
        if (Status != InvoiceStatus.Open)
        {
            throw new InvalidOperationException("Can only mark open invoices as past due");
        }

        Status = InvoiceStatus.PastDue;
        Touch();
    }

    /// <summary>
    ///     Sets the external payment provider ID
    /// </summary>
    public void SetExternalId(string externalId)
    {
        ExternalId = externalId;
        Touch();
    }

    private void EnsureMutable()
    {
        if (IsImmutable)
        {
            throw new InvalidOperationException($"Invoice {InvoiceNumber} is immutable (status: {Status}). Amounts cannot be changed after issuance.");
        }
    }

    private void RecalculateTotal()
    {
        Total = Subtotal - DiscountAmount + TaxAmount;
    }

    private static string GenerateInvoiceNumber()
    {
        var timestamp = SystemClock.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"INV-{timestamp}-{random}";
    }
}

/// <summary>
///     Invoice status enumeration with monotonic transitions
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Draft - can be modified</summary>
    Draft = 0,

    /// <summary>Open - issued, awaiting payment (immutable)</summary>
    Open = 1,

    /// <summary>Paid - fully paid (terminal state)</summary>
    Paid = 2,

    /// <summary>Void - cancelled before payment (terminal state)</summary>
    Void = 3,

    /// <summary>Past due - payment overdue</summary>
    PastDue = 4,

    /// <summary>Uncollectible - written off as bad debt (terminal state)</summary>
    Uncollectible = 5
}

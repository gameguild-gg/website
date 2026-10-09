using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     One mismatch between an external statement line and internal revenue events,
///     recorded by a <see cref="RevenueReconciliationRun" />. Discrepancies are immutable
///     once written; corrections happen in new runs.
/// </summary>
[Table("revenue_reconciliation_discrepancies")]
[Index(nameof(RunId))]
[Index(nameof(Kind))]
[Index(nameof(ExternalReference))]
public class RevenueReconciliationDiscrepancy : EntityBase
{
    /// <summary>Run that recorded the discrepancy.</summary>
    public Guid RunId { get; set; }

    /// <summary>Navigation to the run that recorded the discrepancy.</summary>
    public virtual RevenueReconciliationRun? Run { get; set; }

    /// <summary>Kind of mismatch.</summary>
    public RevenueDiscrepancyKind Kind { get; set; }

    /// <summary>External reference of the mismatched statement line.</summary>
    [Required]
    [MaxLength(200)]
    public string ExternalReference { get; set; } = string.Empty;

    /// <summary>Amount reported by the external statement, when applicable.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExternalAmount { get; set; }

    /// <summary>Currency reported by the external statement, when applicable.</summary>
    [MaxLength(3)]
    public string? ExternalCurrency { get; set; }

    /// <summary>Settlement moment reported by the external statement, when applicable.</summary>
    public DateTime? ExternalOccurredAtUtc { get; set; }

    /// <summary>Internal revenue event involved in the mismatch, when one was found.</summary>
    public Guid? RevenueEventId { get; set; }

    /// <summary>Amount recorded internally, when a revenue event was found.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? InternalAmount { get; set; }

    /// <summary>Currency recorded internally, when a revenue event was found.</summary>
    [MaxLength(3)]
    public string? InternalCurrency { get; set; }

    /// <summary>Human-readable explanation of the mismatch.</summary>
    [MaxLength(1000)]
    public string? Message { get; set; }
}

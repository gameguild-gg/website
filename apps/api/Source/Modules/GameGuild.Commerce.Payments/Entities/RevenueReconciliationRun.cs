using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     One reconciliation run comparing an external accounting/ERP statement against
///     internally recorded <see cref="RevenueEvent" /> rows for an inclusive period.
///     Runs are immutable audit records: counts and summary are set once at completion.
/// </summary>
[Table("revenue_reconciliation_runs")]
[Index(nameof(Status))]
[Index(nameof(PeriodStartUtc))]
[Index(nameof(PeriodEndUtc))]
[Index(nameof(CompletedAtUtc))]
public class RevenueReconciliationRun : EntityBase
{
    /// <summary>External system the statement came from (for example "stripe-payouts" or "manual-export").</summary>
    [Required]
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty;

    /// <summary>Optional identifier of the statement/batch within the external system.</summary>
    [MaxLength(200)]
    public string? ExternalStatementId { get; set; }

    /// <summary>Inclusive start of the reconciled period (UTC).</summary>
    public DateTime PeriodStartUtc { get; set; }

    /// <summary>Inclusive end of the reconciled period (UTC).</summary>
    public DateTime PeriodEndUtc { get; set; }

    /// <summary>Lifecycle status of the run.</summary>
    public RevenueReconciliationStatus Status { get; set; } = RevenueReconciliationStatus.Running;

    /// <summary>Number of statement lines evaluated by the run.</summary>
    public int StatementLineCount { get; set; }

    /// <summary>Number of statement lines matched to an internal revenue event.</summary>
    public int MatchedCount { get; set; }

    /// <summary>Number of discrepancies recorded by the run.</summary>
    public int DiscrepancyCount { get; set; }

    /// <summary>Moment the run started (UTC).</summary>
    public DateTime StartedAtUtc { get; set; } = SystemClock.UtcNow;

    /// <summary>Moment the run committed its results (UTC).</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Reason the run failed, when <see cref="Status" /> is <see cref="RevenueReconciliationStatus.Failed" />.</summary>
    [MaxLength(1000)]
    public string? FailureReason { get; set; }

    /// <summary>Machine-readable totals snapshot (per discrepancy kind) captured at completion.</summary>
    [MaxLength(2000)]
    public string? SummaryJson { get; set; }

    /// <summary>Actor that initiated the run, when initiated by an interactive request.</summary>
    public Guid? InitiatedByUserId { get; set; }

    /// <summary>Discrepancies recorded by this run.</summary>
    public virtual List<RevenueReconciliationDiscrepancy> Discrepancies { get; set; } = [];

    /// <summary>Mark the run as completed. Only allowed once, from a non-terminal state.</summary>
    public void Complete(int matchedCount, int discrepancyCount, string summaryJson)
    {
        EnsureNotTerminal();
        Status = RevenueReconciliationStatus.Completed;
        MatchedCount = matchedCount;
        DiscrepancyCount = discrepancyCount;
        SummaryJson = summaryJson;
        CompletedAtUtc = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>Mark the run as failed. Only allowed once, from a non-terminal state.</summary>
    public void Fail(string reason)
    {
        EnsureNotTerminal();
        Status = RevenueReconciliationStatus.Failed;
        FailureReason = reason;
        CompletedAtUtc = SystemClock.UtcNow;
        Touch();
    }

    private void EnsureNotTerminal()
    {
        if (Status is RevenueReconciliationStatus.Completed or RevenueReconciliationStatus.Failed)
        {
            throw new InvalidOperationException("A completed revenue reconciliation run is immutable.");
        }
    }
}

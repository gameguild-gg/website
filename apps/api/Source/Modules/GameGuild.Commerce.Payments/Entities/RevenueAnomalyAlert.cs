using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Durable alert raised when daily net revenue deviates abnormally from its trailing
///     baseline. Baselines are computed per currency; detection is idempotent per
///     (kind, day, currency): re-running detection for the same day and currency never
///     duplicates an alert.
/// </summary>
[Table("revenue_anomaly_alerts")]
[Index(nameof(Status))]
[Index(nameof(DetectedForDateUtc))]
[Index(nameof(Kind))]
[Index(nameof(DetectedAtUtc))]
public class RevenueAnomalyAlert : EntityBase
{
    /// <summary>Kind of anomaly.</summary>
    public RevenueAnomalyKind Kind { get; set; }

    /// <summary>UTC day (midnight) the anomalous net revenue belongs to.</summary>
    public DateTime DetectedForDateUtc { get; set; }

    /// <summary>ISO-4217 currency code of the daily net revenue series evaluated. Amounts are never compared across currencies.</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>Moment the anomaly was detected (UTC).</summary>
    public DateTime DetectedAtUtc { get; set; } = SystemClock.UtcNow;

    /// <summary>Net revenue observed for the evaluated day, in <see cref="Currency" />.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ObservedNetRevenue { get; set; }

    /// <summary>Baseline (mean) net revenue expected for the evaluated day, in <see cref="Currency" />.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedNetRevenue { get; set; }

    /// <summary>Standard-score of the observation against the baseline, rounded to 4 decimals.</summary>
    [Column(TypeName = "decimal(9,4)")]
    public decimal ZScore { get; set; }

    /// <summary>Number of baseline days used for the evaluation.</summary>
    public int BaselineDays { get; set; }

    /// <summary>Review status of the alert.</summary>
    public RevenueAnomalyStatus Status { get; set; } = RevenueAnomalyStatus.Open;

    /// <summary>Moment the alert was acknowledged (UTC).</summary>
    public DateTime? AcknowledgedAtUtc { get; set; }

    /// <summary>Operator that acknowledged the alert.</summary>
    public Guid? AcknowledgedByUserId { get; set; }

    /// <summary>Optional operator notes recorded at acknowledgement.</summary>
    [MaxLength(1000)]
    public string? AcknowledgementNotes { get; set; }

    /// <summary>Mark the alert as acknowledged by an operator. Only allowed while open.</summary>
    public void Acknowledge(Guid acknowledgedByUserId, string? notes = null)
    {
        if (Status == RevenueAnomalyStatus.Acknowledged)
        {
            throw new InvalidOperationException("The revenue anomaly alert is already acknowledged.");
        }

        Status = RevenueAnomalyStatus.Acknowledged;
        AcknowledgedAtUtc = SystemClock.UtcNow;
        AcknowledgedByUserId = acknowledgedByUserId;
        AcknowledgementNotes = notes;
        Touch();
    }
}

using System.ComponentModel.DataAnnotations;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Tunable settings for revenue auditing (issue #404). Defaults are conservative:
///     the background anomaly worker is disabled until explicitly enabled, and statement
///     line batches are bounded.
/// </summary>
public sealed class RevenueAuditingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RevenueAuditing";

    /// <summary>Maximum number of statement lines a single reconciliation run accepts.</summary>
    [Range(1, 10_000)]
    public int MaxStatementLinesPerRun { get; set; } = 1_000;

    /// <summary>How many recent days (including today) the on-demand anomaly detection evaluates.</summary>
    [Range(1, 30)]
    public int AnomalyEvaluationDays { get; set; } = 7;

    /// <summary>Size of the trailing baseline window (days) used to compute expected daily net revenue.</summary>
    [Range(7, 730)]
    public int AnomalyBaselineDays { get; set; } = 28;

    /// <summary>Minimum number of distinct baseline days with activity required before an anomaly can be raised.</summary>
    [Range(2, 60)]
    public int AnomalyMinBaselineDays { get; set; } = 5;

    /// <summary>Absolute standard-score at or above which a day is flagged as anomalous.</summary>
    [Range(1, 10)]
    public decimal AnomalyZScoreThreshold { get; set; } = 3.0m;

    /// <summary>Enables the periodic anomaly-detection worker. Safe default: disabled.</summary>
    public bool WorkerEnabled { get; set; }

    /// <summary>Interval between anomaly-detection worker passes, in minutes.</summary>
    [Range(5, 10_080)]
    public int WorkerIntervalMinutes { get; set; } = 60;

    /// <summary>Optional configuration-supplied statement lines used by the default
    /// <see cref="IExternalRevenueStatementSource" /> when a reconciliation run does not
    /// carry inline lines. This is the integration seam for ERP/accounting exports; the
    /// platform performs no outbound calls to populate it.</summary>
    public RevenueStatementSourceOptions StatementSource { get; set; } = new();
}

/// <summary>Configuration for the default external statement source.</summary>
public sealed class RevenueStatementSourceOptions
{
    /// <summary>Logical provider name reported by the configured source ("manual-export" when unset).</summary>
    public string? ProviderName { get; set; }

    /// <summary>Static statement lines made available for reconciliation runs.</summary>
    public List<ExternalRevenueStatementLine> Lines { get; set; } = [];
}

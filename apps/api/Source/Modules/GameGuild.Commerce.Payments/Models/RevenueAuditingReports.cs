namespace GameGuild.Commerce.Payments;

/// <summary>Grouped totals for one key of a grouping dimension (event type, source or status), in one currency.</summary>
/// <param name="Key">Grouping key (enum name).</param>
/// <param name="Currency">ISO-4217 currency code the totals are denominated in. Amounts are never consolidated across currencies.</param>
/// <param name="Count">Number of revenue events in the group.</param>
/// <param name="Total">Sum of event amounts in the group, in <paramref name="Currency" />.</param>
public sealed record RevenueEventGroupTotal(string Key, string Currency, int Count, decimal Total);

/// <summary>Dimension used to group revenue totals in reports.</summary>
public enum RevenueEventTotalGrouping
{
    /// <summary>Group totals by <see cref="RevenueEventType" />.</summary>
    EventType = 0,

    /// <summary>Group totals by <see cref="RevenueSource" />.</summary>
    Source = 1,

    /// <summary>Group totals by <see cref="RevenueEventStatus" />.</summary>
    Status = 2
}

/// <summary>Net revenue total for one UTC day and one currency.</summary>
/// <param name="DateUtc">UTC day (midnight) the totals belong to.</param>
/// <param name="Currency">ISO-4217 currency code the totals are denominated in. Amounts are never consolidated across currencies.</param>
/// <param name="CreditTotal">Sum of credit-side revenue event amounts, in <paramref name="Currency" />.</param>
/// <param name="DebitTotal">Sum of debit-side revenue event amounts (refunds, chargebacks, fees), in <paramref name="Currency" />.</param>
/// <param name="NetTotal">CreditTotal minus DebitTotal.</param>
/// <param name="EventCount">Number of revenue events counted for the day in this currency.</param>
public sealed record RevenueDailyTotal(
    DateTime DateUtc,
    string Currency,
    decimal CreditTotal,
    decimal DebitTotal,
    decimal NetTotal,
    int EventCount);

/// <summary>One point of a historical revenue trend, for one currency.</summary>
/// <param name="DateUtc">UTC day (midnight) the point covers.</param>
/// <param name="Currency">ISO-4217 currency code the point is denominated in.</param>
/// <param name="CreditTotal">Credit-side total for the day, in <paramref name="Currency" />.</param>
/// <param name="DebitTotal">Debit-side total for the day, in <paramref name="Currency" />.</param>
/// <param name="NetTotal">Net total for the day, in <paramref name="Currency" />.</param>
/// <param name="EventCount">Revenue events counted for the day in this currency.</param>
public sealed record RevenueTrendPoint(
    DateTime DateUtc,
    string Currency,
    decimal CreditTotal,
    decimal DebitTotal,
    decimal NetTotal,
    int EventCount);

/// <summary>Range totals of a historical revenue trend for one currency.</summary>
/// <param name="Currency">ISO-4217 currency code the totals are denominated in.</param>
/// <param name="TotalCredit">Credit total across the range, in <paramref name="Currency" />.</param>
/// <param name="TotalDebit">Debit total across the range, in <paramref name="Currency" />.</param>
/// <param name="TotalNet">Net total across the range, in <paramref name="Currency" />.</param>
public sealed record RevenueTrendCurrencyTotal(
    string Currency,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal TotalNet);

/// <summary>Historical revenue trend over an inclusive date range, broken down by currency.</summary>
/// <param name="FromUtc">Start of the range (inclusive).</param>
/// <param name="ToUtc">End of the range (inclusive).</param>
/// <param name="Points">One point per (UTC day, currency) pair for every currency observed in the range, including zero-activity days for those currencies. Empty when the range has no revenue events, since zero amounts cannot be attributed to a currency.</param>
/// <param name="TotalsByCurrency">Range totals per currency observed in the period. Amounts are never consolidated across currencies.</param>
public sealed record RevenueTrendReport(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<RevenueTrendPoint> Points,
    IReadOnlyList<RevenueTrendCurrencyTotal> TotalsByCurrency);

/// <summary>Reconciliation coverage summary included in compliance reports.</summary>
/// <param name="ReconciliationRuns">Number of reconciliation runs overlapping the period.</param>
/// <param name="MatchedLines">Total statement lines matched by those runs.</param>
/// <param name="Discrepancies">Total discrepancies recorded by those runs.</param>
/// <param name="LastRunCompletedAtUtc">Completion timestamp of the most recent run, if any.</param>
public sealed record RevenueReconciliationCoverage(
    int ReconciliationRuns,
    int MatchedLines,
    int Discrepancies,
    DateTime? LastRunCompletedAtUtc);

/// <summary>Compliance-grade revenue summary for an inclusive period, broken down by currency.</summary>
/// <param name="FromUtc">Start of the period (inclusive).</param>
/// <param name="ToUtc">End of the period (inclusive).</param>
/// <param name="GeneratedAtUtc">Moment the report was produced.</param>
/// <param name="TotalsByEventType">Revenue totals grouped by (event type, currency).</param>
/// <param name="TotalsBySource">Revenue totals grouped by (source, currency).</param>
/// <param name="TotalsByStatus">Revenue totals grouped by (processing status, currency).</param>
/// <param name="UncountedEventCount">Revenue events still pending processing (not counted as recognized revenue).</param>
/// <param name="Reconciliation">Reconciliation coverage across the period.</param>
/// <param name="Attestation">Human-readable attestation statement for compliance filings. States gross revenue per currency; no cross-currency consolidation is performed.</param>
public sealed record RevenueComplianceReport(
    DateTime FromUtc,
    DateTime ToUtc,
    DateTime GeneratedAtUtc,
    IReadOnlyList<RevenueEventGroupTotal> TotalsByEventType,
    IReadOnlyList<RevenueEventGroupTotal> TotalsBySource,
    IReadOnlyList<RevenueEventGroupTotal> TotalsByStatus,
    int UncountedEventCount,
    RevenueReconciliationCoverage Reconciliation,
    string Attestation);

/// <summary>Serialized revenue report ready for delivery to an accounting/ERP system.</summary>
/// <param name="FileName">Suggested file name for the export.</param>
/// <param name="ContentType">MIME type of the payload (text/csv or application/json).</param>
/// <param name="Content">Serialized report content.</param>
public sealed record RevenueReportExport(string FileName, string ContentType, string Content);

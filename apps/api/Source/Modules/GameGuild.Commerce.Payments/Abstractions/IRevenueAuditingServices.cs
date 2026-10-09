namespace GameGuild.Commerce.Payments;

/// <summary>
///     Input for a reconciliation run (issue #404). When <see cref="Lines" /> is null the
///     lines are pulled from the configured <see cref="IExternalRevenueStatementSource" />.
/// </summary>
/// <param name="TenantId">Optional tenant scope; null reconciles the global aggregate.</param>
/// <param name="Source">External system the statement came from.</param>
/// <param name="PeriodStartUtc">Inclusive period start (UTC).</param>
/// <param name="PeriodEndUtc">Inclusive period end (UTC).</param>
/// <param name="Lines">Inline statement lines, or null to use the configured external source.</param>
/// <param name="ExternalStatementId">Optional statement/batch identifier for traceability.</param>
/// <param name="InitiatedByUserId">Optional actor that initiated the run.</param>
public sealed record RevenueReconciliationRequest(
    Guid? TenantId,
    string Source,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    IReadOnlyList<ExternalRevenueStatementLine>? Lines,
    string? ExternalStatementId = null,
    Guid? InitiatedByUserId = null);

/// <summary>
///     External reconciliation and discrepancy detection (issue #404): compares external
///     accounting/ERP statements against internally recorded revenue events and records a
///     durable, immutable run with every mismatch.
/// </summary>
public interface IRevenueReconciliationService
{
    /// <summary>Run a reconciliation for the requested period and return the committed run.</summary>
    /// <param name="request">Reconciliation input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The committed reconciliation run with discrepancies persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resolved statement exceeds the configured line limit.</exception>
    Task<RevenueReconciliationRun> ReconcileAsync(RevenueReconciliationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>An anomaly candidate computed by the detector before persistence.</summary>
/// <param name="Kind">Kind of anomaly.</param>
/// <param name="DetectedForDateUtc">UTC day (midnight) the anomaly belongs to.</param>
/// <param name="ObservedNetRevenue">Net revenue observed for the day.</param>
/// <param name="ExpectedNetRevenue">Baseline mean net revenue.</param>
/// <param name="ZScore">Standard score of the observation (rounded to four decimals).</param>
/// <param name="BaselineDays">Number of baseline days used.</param>
public sealed record RevenueAnomalyCandidate(
    RevenueAnomalyKind Kind,
    DateTime DetectedForDateUtc,
    decimal ObservedNetRevenue,
    decimal ExpectedNetRevenue,
    decimal ZScore,
    int BaselineDays);

/// <summary>
///     Statistical anomaly detection over daily net revenue (issue #404): flags days whose
///     net revenue deviates from the trailing baseline by at least the configured z-score.
/// </summary>
public interface IRevenueAnomalyService
{
    /// <summary>Evaluate the most recent days (per options) and return anomaly candidates without persisting them.</summary>
    /// <param name="evaluationDateUtc">Reference "today"; evaluation covers the preceding days per options.</param>
    /// <param name="tenantId">Optional tenant scope; null evaluates the global aggregate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Anomaly candidates, oldest evaluated day first.</returns>
    Task<IReadOnlyList<RevenueAnomalyCandidate>> DetectAsync(
        DateTime evaluationDateUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Evaluate the most recent days and persist alerts, skipping (kind, day) pairs already alerted.</summary>
    /// <param name="evaluationDateUtc">Reference "today"; evaluation covers the preceding days per options.</param>
    /// <param name="tenantId">Optional tenant scope; null evaluates the global aggregate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of new alerts created.</returns>
    Task<int> DetectAndPersistAsync(
        DateTime evaluationDateUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Compliance reporting, historical trend analysis and export serialization for
///     revenue auditing (issue #404).
/// </summary>
public interface IRevenueReportService
{
    /// <summary>Build the compliance report for an inclusive period.</summary>
    /// <param name="fromUtc">Inclusive period start (UTC).</param>
    /// <param name="toUtc">Inclusive period end (UTC).</param>
    /// <param name="tenantId">Optional tenant scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compliance report.</returns>
    Task<RevenueComplianceReport> GetComplianceReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Build the daily net-revenue trend for an inclusive period.</summary>
    /// <param name="fromUtc">Inclusive period start (UTC).</param>
    /// <param name="toUtc">Inclusive period end (UTC).</param>
    /// <param name="tenantId">Optional tenant scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The trend report with one point per day, including zero-activity days.</returns>
    Task<RevenueTrendReport> GetTrendReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Serialize the compliance and trend reports for external delivery.</summary>
    /// <param name="fromUtc">Inclusive period start (UTC).</param>
    /// <param name="toUtc">Inclusive period end (UTC).</param>
    /// <param name="format">Export format: "csv" or "json".</param>
    /// <param name="tenantId">Optional tenant scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The serialized export with file name and content type.</returns>
    /// <exception cref="ArgumentException">Thrown when the format is not csv or json.</exception>
    Task<RevenueReportExport> ExportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string format,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}

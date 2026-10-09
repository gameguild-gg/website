using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Default <see cref="IRevenueReportService" /> (issue #404): builds compliance-grade
///     summaries, historical daily trends, and RFC 4180 CSV / JSON exports for external
///     accounting and ERP consumption.
/// </summary>
public sealed partial class RevenueReportService(
    IRevenueEventRepository revenueEventRepository,
    IRevenueReconciliationRepository reconciliationRepository) : IRevenueReportService
{
    private static readonly JsonSerializerOptions ExportSerializerOptions = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled)]
    private static partial Regex SafeFormatRegex();

    /// <inheritdoc />
    public async Task<RevenueComplianceReport> GetComplianceReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var totalsByEventType = await revenueEventRepository
            .GetGroupedTotalsAsync(fromUtc, toUtc, tenantId, RevenueEventTotalGrouping.EventType, cancellationToken)
            .ConfigureAwait(false);
        var totalsBySource = await revenueEventRepository
            .GetGroupedTotalsAsync(fromUtc, toUtc, tenantId, RevenueEventTotalGrouping.Source, cancellationToken)
            .ConfigureAwait(false);
        var totalsByStatus = await revenueEventRepository
            .GetGroupedTotalsAsync(fromUtc, toUtc, tenantId, RevenueEventTotalGrouping.Status, cancellationToken)
            .ConfigureAwait(false);

        var runs = await reconciliationRepository
            .GetRunsOverlappingPeriodAsync(fromUtc, toUtc, tenantId, cancellationToken)
            .ConfigureAwait(false);

        var coverage = new RevenueReconciliationCoverage(
            ReconciliationRuns: runs.Count,
            MatchedLines: runs.Sum(run => run.MatchedCount),
            Discrepancies: runs.Sum(run => run.DiscrepancyCount),
            LastRunCompletedAtUtc: runs
                .Select(run => run.CompletedAtUtc)
                .Where(completedAt => completedAt is not null)
                .DefaultIfEmpty()
                .Max());

        var uncountedEventCount = totalsByStatus
            .Where(total => total.Key == nameof(RevenueEventStatus.Pending) || total.Key == nameof(RevenueEventStatus.Failed))
            .Sum(total => total.Count);

        var recognizedTotal = totalsByEventType.Sum(total => total.Total);
        var attestation =
            $"Revenue audit summary for {fromUtc:yyyy-MM-ddTHH:mm:ssZ} to {toUtc:yyyy-MM-ddTHH:mm:ssZ}: " +
            $"{totalsByStatus.Sum(total => total.Count)} revenue events recorded, {recognizedTotal.ToString("0.00", CultureInfo.InvariantCulture)} gross across event types, " +
            $"{uncountedEventCount} events not yet processed, {coverage.ReconciliationRuns} reconciliation run(s) with {coverage.Discrepancies} discrepancy(ies). " +
            "Totals are computed from immutable revenue event records as of the report timestamp.";

        return new RevenueComplianceReport(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            GeneratedAtUtc: SystemClock.UtcNow,
            TotalsByEventType: totalsByEventType,
            TotalsBySource: totalsBySource,
            TotalsByStatus: totalsByStatus,
            UncountedEventCount: uncountedEventCount,
            Reconciliation: coverage,
            Attestation: attestation);
    }

    /// <inheritdoc />
    public async Task<RevenueTrendReport> GetTrendReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var dailyTotals = await revenueEventRepository
            .GetDailyTotalsAsync(fromUtc, toUtc, tenantId, cancellationToken)
            .ConfigureAwait(false);
        var totalsByDay = dailyTotals.ToDictionary(total => total.DateUtc, total => total);

        var points = new List<RevenueTrendPoint>();
        for (var day = fromUtc.Date; day <= toUtc.Date; day = day.AddDays(1))
        {
            if (totalsByDay.TryGetValue(day, out var total))
            {
                points.Add(new RevenueTrendPoint(day, total.CreditTotal, total.DebitTotal, total.NetTotal, total.EventCount));
            }
            else
            {
                points.Add(new RevenueTrendPoint(day, 0m, 0m, 0m, 0));
            }
        }

        return new RevenueTrendReport(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Points: points,
            TotalCredit: points.Sum(point => point.CreditTotal),
            TotalDebit: points.Sum(point => point.DebitTotal),
            TotalNet: points.Sum(point => point.NetTotal));
    }

    /// <inheritdoc />
    public async Task<RevenueReportExport> ExportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string format,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(format)
            || !SafeFormatRegex().IsMatch(format)
            || !string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
              && !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Export format must be 'csv' or 'json'.", nameof(format));
        }

        var complianceReport = await GetComplianceReportAsync(fromUtc, toUtc, tenantId, cancellationToken).ConfigureAwait(false);
        var trendReport = await GetTrendReportAsync(fromUtc, toUtc, tenantId, cancellationToken).ConfigureAwait(false);

        var periodLabel = $"{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}";
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return new RevenueReportExport(
                $"revenue-audit-{periodLabel}.csv",
                "text/csv; charset=utf-8",
                BuildCsv(complianceReport, trendReport));
        }

        return new RevenueReportExport(
            $"revenue-audit-{periodLabel}.json",
            "application/json",
            JsonSerializer.Serialize(
                new
                {
                    complianceReport.FromUtc,
                    complianceReport.ToUtc,
                    complianceReport.GeneratedAtUtc,
                    Compliance = complianceReport,
                    Trend = trendReport
                },
                ExportSerializerOptions));
    }

    private static string BuildCsv(RevenueComplianceReport complianceReport, RevenueTrendReport trendReport)
    {
        var builder = new StringBuilder();
        builder.AppendLine("section,key,count,total");

        foreach (var total in complianceReport.TotalsByEventType)
        {
            builder.AppendLine($"event_type,{Escape(total.Key)},{total.Count},{Format(total.Total)}");
        }

        foreach (var total in complianceReport.TotalsBySource)
        {
            builder.AppendLine($"source,{Escape(total.Key)},{total.Count},{Format(total.Total)}");
        }

        foreach (var total in complianceReport.TotalsByStatus)
        {
            builder.AppendLine($"status,{Escape(total.Key)},{total.Count},{Format(total.Total)}");
        }

        foreach (var point in trendReport.Points)
        {
            builder.AppendLine(
                $"daily,{point.DateUtc:yyyy-MM-dd},{point.EventCount},{Format(point.NetTotal)}");
        }

        return builder.ToString();
    }

    private static string Format(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Escape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return field;
    }
}

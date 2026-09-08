using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Resources;

/// <summary>
///     Implementation of cost allocation and chargeback reporting
/// </summary>
public class CostAllocationService(
    ICostAllocationReportRepository reportRepository,
    IUsageRecordRepository usageRepository,
    IResourceQuotaRepository quotaRepository,
    IOptions<ResourcesOptions> options,
    ILogger<CostAllocationService> logger,
    IInternalCostLedgerReader? internalCostLedgerReader = null
) : ICostAllocationService
{
    private readonly ResourcesOptions _options = options.Value;

    public async Task<CostAllocationReport> GenerateReportAsync(Guid tenantId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Generating cost allocation report for tenant {TenantId} from {Start} to {End}", tenantId, periodStart, periodEnd);

        var usageRecords = await usageRepository.GetByTenantAsync(tenantId, null, periodStart, periodEnd, cancellationToken).ConfigureAwait(false);

        var groupedByType = usageRecords.GroupBy(r => r.Type).Select(g => new { UsageType = g.Key, TotalUsage = g.Sum(r => r.UsageAmount) }).ToList();

        var ledgerSummary = internalCostLedgerReader is null
            ? null
            : await internalCostLedgerReader.SummarizeAsync(tenantId, periodStart, periodEnd, cancellationToken)
                .ConfigureAwait(false);
        decimal totalCost = ledgerSummary?.UsdCost ?? 0;
        var allocationTags = new Dictionary<string, string>();

        foreach (var group in groupedByType)
        {
            if (ledgerSummary is null)
            {
                var costPerUnit = GetCostPerUnit(group.UsageType);
                totalCost += costPerUnit * group.TotalUsage;
            }

            // Get allocation tags from resource quotas
            var quota = await quotaRepository.GetByTenantAndTypeAsync(tenantId, group.UsageType, cancellationToken).ConfigureAwait(false);

            if (quota?.Metadata != null)
            {
                // Extract allocation tags from strongly-typed metadata
                var metadata = quota.Metadata;

                // Add custom properties to allocation tags
                if (metadata.CustomProperties != null)
                {
                    foreach (var prop in metadata.CustomProperties) { allocationTags.TryAdd(prop.Key, prop.Value); }
                }

                // Add source and external reference as allocation metadata if present
                if (!string.IsNullOrEmpty(metadata.Source)) { allocationTags.TryAdd("Source", metadata.Source); }

                if (!string.IsNullOrEmpty(metadata.ExternalReferenceId))
                {
                    allocationTags.TryAdd("ExternalReferenceId", metadata.ExternalReferenceId);
                }
            }
        }

        var totalUsage = ledgerSummary?.UsageEntries ?? groupedByType.Sum(group => group.TotalUsage);
        var report = new CostAllocationReport
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            ResourceUsageType = ResourceUsageType.ApiCalls,
            TotalUsage = totalUsage,
            CostPerUnit = totalCost / Math.Max(1, totalUsage),
            TotalCost = totalCost,
            AllocationTags = allocationTags.Count > 0 ? JsonSerializer.Serialize(allocationTags) : null,
            CostCenter = allocationTags.GetValueOrDefault("CostCenter"),
            Project = allocationTags.GetValueOrDefault("Project"),
            Owner = allocationTags.GetValueOrDefault("Owner"),
            IsExported = false,
            Metadata = ledgerSummary is null ? null : JsonSerializer.Serialize(new { Source = "internal-cost-ledger", Currency = "USD" })
        };
        report.SetProperties(new Dictionary<string, object?> { ["TenantId"] = tenantId });

        var savedReport = await reportRepository.AddAsync(report, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Generated cost allocation report {ReportId} for tenant {TenantId} with total cost {TotalCost:C}", savedReport.Id, tenantId, totalCost);

        return savedReport;
    }

    public async Task<IEnumerable<CostAllocationReport>> GetTenantReportsAsync(Guid tenantId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        return await reportRepository.GetByTenantAsync(tenantId, fromDate, toDate, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CostAllocationReport?> GetReportAsync(Guid reportId, CancellationToken cancellationToken = default) { return await reportRepository.GetByIdAsync(reportId, cancellationToken).ConfigureAwait(false); }

    public async Task<decimal> CalculateTotalCostAsync(Guid tenantId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        if (internalCostLedgerReader is not null)
        {
            var summary = await internalCostLedgerReader.SummarizeAsync(
                    tenantId,
                    periodStart,
                    periodEnd,
                    cancellationToken)
                .ConfigureAwait(false);
            return summary.UsdCost;
        }

        // Get all usage records for tenant and filter by date
        var allRecords = await usageRepository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var usageRecords = allRecords.Where(r => r.PeriodStart >= periodStart && r.PeriodStart <= periodEnd);

        decimal totalCost = 0;

        foreach (var group in usageRecords.GroupBy(r => r.Type))
        {
            var costPerUnit = GetCostPerUnit(group.Key);
            var totalUsage = group.Sum(r => r.UsageAmount);
            totalCost += costPerUnit * totalUsage;
        }

        return totalCost;
    }

    public async Task<bool> MarkAsExportedAsync(Guid reportId, string? invoiceReference = null, CancellationToken cancellationToken = default)
    {
        var report = await reportRepository.GetByIdAsync(reportId, cancellationToken).ConfigureAwait(false);

        if (report == null) return false;

        report.IsExported = true;
        report.ExportedAt = SystemClock.UtcNow;
        report.InvoiceReference = invoiceReference;
        report.Touch();

        await reportRepository.UpdateAsync(report, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Marked report {ReportId} as exported with invoice reference {InvoiceReference}", reportId, invoiceReference);

        return true;
    }

    public async Task<IEnumerable<CostAllocationReport>> GetUnexportedReportsAsync(CancellationToken cancellationToken = default) { return await reportRepository.GetUnexportedReportsAsync(cancellationToken).ConfigureAwait(false); }

    public async Task<bool> UpdateAllocationTagsAsync(Guid reportId, Dictionary<string, string> tags, CancellationToken cancellationToken = default)
    {
        var report = await reportRepository.GetByIdAsync(reportId, cancellationToken).ConfigureAwait(false);

        if (report == null) return false;

        report.AllocationTags = JsonSerializer.Serialize(tags);
        report.CostCenter = tags.GetValueOrDefault("CostCenter");
        report.Project = tags.GetValueOrDefault("Project");
        report.Owner = tags.GetValueOrDefault("Owner");
        report.Touch();

        await reportRepository.UpdateAsync(report, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Updated allocation tags for report {ReportId}", reportId);

        return true;
    }

    private decimal GetCostPerUnit(ResourceUsageType type)
    {
        var typeName = type.ToString();
        return _options.CostPerUnit.GetValueOrDefault(typeName, _options.DefaultCostPerUnit);
    }

    // PLANNED: Integration with Billing module for invoice generation (depends on GameGuild.Commerce.Billing)
    // PLANNED: Integration with Finance module for cost center validation (depends on GameGuild.Finance)
}

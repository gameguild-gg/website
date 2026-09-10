using GameGuild.Finance.Economy.AdRewards.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed record DurableAdRewardReconciliationStatus(
    long EstimatedRevenueUsdNanos,
    long PreviousActualRevenueUsdNanos,
    long ActualRevenueUsdNanos,
    long ActualDeltaUsdNanos,
    long VarianceUsdNanos,
    long HistoricalRewardSoftUnits,
    DateTimeOffset ReconciledAt);

public sealed record DurableAdProviderReportStatus(
    Guid ProviderReportId,
    string Network,
    string ReportId,
    int Version,
    string BatchId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long ActualRevenueUsdNanos,
    string EvidenceHash,
    string PayloadHash,
    bool SignatureVerified,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string? ProcessingError,
    DurableAdRewardReconciliationStatus? Reconciliation);

public interface IDurableAdRewardReportReader
{
    ValueTask<IReadOnlyList<DurableAdProviderReportStatus>> ListAsync(
        Guid tenantId,
        string? network,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlDurableAdRewardReportReader : IDurableAdRewardReportReader
{
    private readonly DbContext _db;

    public PostgreSqlDurableAdRewardReportReader(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Durable ad provider report reads require the application's relational DbContext.");
    }

    public async ValueTask<IReadOnlyList<DurableAdProviderReportStatus>> ListAsync(
        Guid tenantId,
        string? network,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (network is not null && string.IsNullOrWhiteSpace(network))
            throw new ArgumentException("Network cannot be blank.", nameof(network));
        if (limit is <= 0 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));

        var query = _db.Set<AdProviderReportRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (network is not null)
        {
            var normalizedNetwork = network.Trim();
            query = query.Where(row => row.Network == normalizedNetwork);
        }
        var reports = await query
            .OrderByDescending(row => row.ReceivedAt)
            .ThenByDescending(row => row.Version)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        var ids = reports.Select(row => row.Id).ToArray();
        var reconciliations = await _db.Set<AdRewardReconciliationRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId && ids.Contains(row.ProviderReportId))
            .ToDictionaryAsync(row => row.ProviderReportId, cancellationToken);

        return Array.AsReadOnly(reports.Select(row =>
        {
            reconciliations.TryGetValue(row.Id, out var reconciliation);
            return new DurableAdProviderReportStatus(
                row.Id, row.Network, row.ReportId, row.Version, row.BatchId,
                row.PeriodStart, row.PeriodEnd, row.ActualRevenueUsdNanos,
                row.EvidenceHash, row.PayloadHash, row.SignatureVerified,
                row.ReceivedAt, row.ProcessedAt, row.ProcessingError,
                reconciliation is null ? null : new DurableAdRewardReconciliationStatus(
                    reconciliation.EstimatedRevenueUsdNanos,
                    reconciliation.PreviousActualRevenueUsdNanos,
                    reconciliation.ActualRevenueUsdNanos,
                    reconciliation.ActualDeltaUsdNanos,
                    reconciliation.VarianceUsdNanos,
                    reconciliation.HistoricalRewardSoftUnits,
                    reconciliation.ReconciledAt));
        }).ToArray());
    }
}

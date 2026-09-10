using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed class DurableAdRewardReportService : IDurableAdRewardReportService
{
    private readonly DbContext _db;
    private readonly IAdRewardProviderAdapterResolver _providerAdapters;

    public DurableAdRewardReportService(
        IApplicationDbContext context,
        IAdRewardProviderAdapterResolver providerAdapters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(providerAdapters);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Durable ad provider reports require the application's relational DbContext.");
        _providerAdapters = providerAdapters;
    }

    public async ValueTask<DurableAdProviderReportImportResult> ImportAsync(
        ImportDurableAdProviderReportRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var report = request.Report;
        if (!await _providerAdapters.Resolve(report.Network).VerifyReportAsync(report, cancellationToken))
            throw new AdProviderReportVerificationException(
                "Provider report signature or timing is invalid.");
        var payloadHash = Hash(Canonicalize(report));

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var existing = await _db.Set<AdProviderReportRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.TenantId == request.TenantId &&
                       row.Network == report.Network &&
                       row.ReportId == report.ReportId &&
                       row.Version == report.Version,
                cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
                throw new AdProviderReportConflictException(
                    "Provider report version has conflicting content.");
            return await MapResultAsync(existing, true, cancellationToken);
        }

        var previous = await _db.Set<AdProviderReportRow>()
            .AsNoTracking()
            .Where(row => row.TenantId == request.TenantId &&
                          row.Network == report.Network &&
                          row.ReportId == report.ReportId)
            .OrderByDescending(row => row.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var expectedVersion = previous is null ? 1 : previous.Version + 1;
        if (report.Version != expectedVersion)
            throw new AdProviderReportConflictException(
                "Provider report versions must be contiguous and forward-only.");

        var sessionIds = report.VerifiedSessionIds.Distinct().ToArray();
        var sessions = await _db.Set<AdRewardSessionRow>()
            .Where(row => row.TenantId == request.TenantId &&
                          row.Network == report.Network &&
                          sessionIds.Contains(row.Id))
            .ToArrayAsync(cancellationToken);
        if (sessions.Length != sessionIds.Length)
            throw new AdProviderReportVerificationException(
                "Provider report references sessions outside its tenant or network.");

        var reportId = Guid.NewGuid();
        var row = new AdProviderReportRow
        {
            Id = reportId,
            TenantId = request.TenantId,
            Network = report.Network,
            ReportId = report.ReportId,
            Version = report.Version,
            BatchId = report.BatchId,
            PeriodStart = report.PeriodStart,
            PeriodEnd = report.PeriodEnd,
            ActualRevenueUsdNanos = report.ActualRevenueUsdNanos,
            VerifiedSessionIds = JsonSerializer.Serialize(sessionIds.Order()),
            EvidenceHash = report.EvidenceHash,
            ImportedAt = report.ImportedAt,
            Signature = report.Signature,
            PayloadHash = payloadHash,
            SignatureVerified = true,
            ReceivedAt = request.ReceivedAt,
            ProcessedAt = request.ReceivedAt
        };
        _db.Set<AdProviderReportRow>().Add(row);

        var attributions = await _db.Set<AdRewardAttributionRow>()
            .AsNoTracking()
            .Where(item => item.TenantId == request.TenantId &&
                           item.Network == report.Network &&
                           item.ProviderBatchId == report.BatchId &&
                           sessionIds.Contains(item.SessionId))
            .ToArrayAsync(cancellationToken);
        var estimated = Sum(attributions.Select(item => item.EstimatedRevenueUsdNanos));
        var historicalReward = Sum(attributions.Select(item => item.RewardSoftUnits));
        var previousActual = previous?.ActualRevenueUsdNanos ?? 0;
        var reconciliation = new AdRewardReconciliation(
            report.Network,
            report.ReportId,
            report.Version,
            report.BatchId,
            estimated,
            previousActual,
            report.ActualRevenueUsdNanos,
            checked(report.ActualRevenueUsdNanos - previousActual),
            checked(report.ActualRevenueUsdNanos - estimated),
            historicalReward,
            request.ReceivedAt);
        _db.Set<AdRewardReconciliationRow>().Add(new AdRewardReconciliationRow
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ProviderReportId = reportId,
            Network = reconciliation.Network,
            ReportId = reconciliation.ReportId,
            Version = reconciliation.Version,
            BatchId = reconciliation.BatchId,
            EstimatedRevenueUsdNanos = reconciliation.EstimatedRevenueUsdNanos,
            PreviousActualRevenueUsdNanos = reconciliation.PreviousActualRevenueUsdNanos,
            ActualRevenueUsdNanos = reconciliation.ActualRevenueUsdNanos,
            ActualDeltaUsdNanos = reconciliation.ActualDeltaUsdNanos,
            VarianceUsdNanos = reconciliation.VarianceUsdNanos,
            HistoricalRewardSoftUnits = reconciliation.HistoricalRewardSoftUnits,
            ReconciledAt = reconciliation.ReconciledAt
        });

        var pendingIds = sessions
            .Where(session => session.State == DurableAdRewardSessionState.Deferred)
            .Select(session => session.Id)
            .ToArray();
        var pendingClaims = pendingIds.Length == 0
            ? []
            : await _db.Set<AdRewardPendingClaimRow>()
                .Where(claim => pendingIds.Contains(claim.SessionId) && claim.ProviderReportId == null)
                .ToArrayAsync(cancellationToken);
        foreach (var session in sessions)
        {
            _db.Set<AdRewardProviderBatchClaimRow>().Add(new AdRewardProviderBatchClaimRow
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                ProviderReportId = reportId,
                SessionId = session.Id,
                BatchId = report.BatchId,
                ClaimedAt = request.ReceivedAt
            });
        }
        foreach (var pending in pendingClaims)
        {
            pending.ProviderReportId = reportId;
            pending.ConfirmedAt = request.ReceivedAt;
            var session = sessions.Single(item => item.Id == pending.SessionId);
            session.State = DurableAdRewardSessionState.Verified;
            session.UpdatedAt = request.ReceivedAt;
            session.Version++;
            _db.Set<AdRewardSessionEventRow>().Add(new AdRewardSessionEventRow
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Sequence = 3,
                State = DurableAdRewardSessionState.Verified,
                EvidenceHash = report.EvidenceHash,
                OccurredAt = request.ReceivedAt
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new DurableAdProviderReportImportResult(
            reportId,
            false,
            reconciliation,
            Array.AsReadOnly(pendingClaims.Select(item => item.SessionId).ToArray()));
        }, cancellationToken);
    }

    private async ValueTask<DurableAdProviderReportImportResult> MapResultAsync(
        AdProviderReportRow row,
        bool duplicate,
        CancellationToken cancellationToken)
    {
        var reconciliation = await _db.Set<AdRewardReconciliationRow>()
            .AsNoTracking()
            .SingleAsync(item => item.ProviderReportId == row.Id, cancellationToken);
        var verifiedPending = await _db.Set<AdRewardPendingClaimRow>()
            .AsNoTracking()
            .Where(item => item.ProviderReportId == row.Id)
            .Select(item => item.SessionId)
            .ToArrayAsync(cancellationToken);
        return new DurableAdProviderReportImportResult(
            row.Id,
            duplicate,
            new AdRewardReconciliation(
                reconciliation.Network,
                reconciliation.ReportId,
                reconciliation.Version,
                reconciliation.BatchId,
                reconciliation.EstimatedRevenueUsdNanos,
                reconciliation.PreviousActualRevenueUsdNanos,
                reconciliation.ActualRevenueUsdNanos,
                reconciliation.ActualDeltaUsdNanos,
                reconciliation.VarianceUsdNanos,
                reconciliation.HistoricalRewardSoftUnits,
                reconciliation.ReconciledAt),
            Array.AsReadOnly(verifiedPending));
    }

    private static void Validate(ImportDurableAdProviderReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Report);
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(request));
        var report = request.Report;
        ArgumentException.ThrowIfNullOrWhiteSpace(report.Network);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.ReportId);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.BatchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.EvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.Signature);
        if (report.Version <= 0 || report.PeriodEnd <= report.PeriodStart ||
            report.ImportedAt < report.PeriodEnd || report.ImportedAt > request.ReceivedAt ||
            report.ActualRevenueUsdNanos < 0 || report.VerifiedSessionIds.Count == 0 ||
            report.VerifiedSessionIds.Any(id => id == Guid.Empty) ||
            report.VerifiedSessionIds.Distinct().Count() != report.VerifiedSessionIds.Count)
            throw new AdProviderReportVerificationException("Provider report payload is invalid.");
    }

    private static string Canonicalize(AdProviderReport report) => string.Join('|',
        report.Network,
        report.ReportId,
        report.Version.ToString(CultureInfo.InvariantCulture),
        report.BatchId,
        report.PeriodStart.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        report.PeriodEnd.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        report.ActualRevenueUsdNanos.ToString(CultureInfo.InvariantCulture),
        string.Join(',', report.VerifiedSessionIds.Order().Select(id => id.ToString("N"))),
        report.EvidenceHash,
        report.ImportedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        report.Signature);

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static long Sum(IEnumerable<long> values) =>
        values.Aggregate(0L, static (sum, value) => checked(sum + value));
}

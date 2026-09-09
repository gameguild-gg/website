using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed record AdRewardAttribution(
    Guid SessionId,
    string Network,
    PolicyVersion PolicyVersion,
    string ProviderBatchId,
    long EstimatedRevenueUsdNanos,
    long RewardSoftUnits,
    DateTimeOffset CompletedAt);

public sealed record AdProviderReport(
    string Network,
    string ReportId,
    int Version,
    string BatchId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long ActualRevenueUsdNanos,
    IReadOnlyList<Guid> VerifiedSessionIds,
    string EvidenceHash,
    DateTimeOffset ImportedAt,
    string Signature);

public sealed class VerifiedAdProviderReport
{
    internal VerifiedAdProviderReport(AdProviderReport report) => Report = report;

    internal AdProviderReport Report { get; }
    public string Network => Report.Network;
    public string ReportId => Report.ReportId;
    public int Version => Report.Version;
    public string BatchId => Report.BatchId;
    public DateTimeOffset PeriodEnd => Report.PeriodEnd;
    public long ActualRevenueUsdNanos => Report.ActualRevenueUsdNanos;
    public IReadOnlyList<Guid> VerifiedSessionIds => Report.VerifiedSessionIds;
    public string EvidenceHash => Report.EvidenceHash;
    public DateTimeOffset ImportedAt => Report.ImportedAt;
}

public sealed class HmacAdProviderReportService
{
    private readonly string _network;
    private readonly byte[] _secret;

    public HmacAdProviderReportService(string network, byte[] secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length < 32) throw new ArgumentException("Provider report secret must contain at least 32 bytes.", nameof(secret));
        _network = network.Trim();
        _secret = [.. secret];
    }

    public AdProviderReport Sign(
        string reportId,
        int version,
        string batchId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        long actualRevenueUsdNanos,
        IReadOnlyList<Guid> verifiedSessionIds,
        string evidenceHash,
        DateTimeOffset importedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        if (periodEnd <= periodStart || importedAt < periodEnd)
            throw new ArgumentException("Provider report timing is invalid.", nameof(periodEnd));
        ArgumentOutOfRangeException.ThrowIfNegative(actualRevenueUsdNanos);
        ArgumentNullException.ThrowIfNull(verifiedSessionIds);
        if (verifiedSessionIds.Count == 0 || verifiedSessionIds.Any(id => id == Guid.Empty) ||
            verifiedSessionIds.Distinct().Count() != verifiedSessionIds.Count)
            throw new ArgumentException("Provider report sessions must be unique and non-empty.", nameof(verifiedSessionIds));
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceHash);
        var sessions = verifiedSessionIds.Order().ToArray();
        var unsigned = new AdProviderReport(
            _network, reportId.Trim(), version, batchId.Trim(), periodStart, periodEnd,
            actualRevenueUsdNanos, sessions, evidenceHash.Trim(), importedAt, string.Empty);
        return unsigned with { Signature = SignCanonical(unsigned) };
    }

    public bool Verify(AdProviderReport report, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!string.Equals(report.Network, _network, StringComparison.Ordinal) ||
            report.Version <= 0 || report.PeriodEnd <= report.PeriodStart ||
            report.ImportedAt < report.PeriodEnd || report.ImportedAt > now ||
            report.ActualRevenueUsdNanos < 0 || report.VerifiedSessionIds.Count == 0 ||
            report.VerifiedSessionIds.Any(id => id == Guid.Empty) ||
            report.VerifiedSessionIds.Distinct().Count() != report.VerifiedSessionIds.Count)
            return false;
        byte[] supplied;
        try { supplied = Convert.FromBase64String(report.Signature); }
        catch (FormatException) { return false; }
        var expected = Convert.FromBase64String(SignCanonical(report));
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private string SignCanonical(AdProviderReport report) => Convert.ToBase64String(
        HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(Canonical(report))));

    private static string Canonical(AdProviderReport report) => string.Join('|',
        report.Network,
        report.ReportId,
        report.Version.ToString(CultureInfo.InvariantCulture),
        report.BatchId,
        report.PeriodStart.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        report.PeriodEnd.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        report.ActualRevenueUsdNanos.ToString(CultureInfo.InvariantCulture),
        string.Join(',', report.VerifiedSessionIds.Order().Select(id => id.ToString("N"))),
        report.EvidenceHash,
        report.ImportedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}

public sealed record AdRewardReconciliation(
    string Network,
    string ReportId,
    int Version,
    string BatchId,
    long EstimatedRevenueUsdNanos,
    long PreviousActualRevenueUsdNanos,
    long ActualRevenueUsdNanos,
    long ActualDeltaUsdNanos,
    long VarianceUsdNanos,
    long HistoricalRewardSoftUnits,
    DateTimeOffset ReconciledAt);

public sealed record AdProviderReportImportResult(
    AdRewardReconciliation Reconciliation,
    VerifiedAdProviderReport VerifiedReport,
    AdNetworkPolicy FuturePolicy);

public sealed class AdRewardReconciler
{
    private readonly object _gate = new();
    private readonly AdNetworkPolicyStore _policies;
    private readonly HmacAdProviderReportService _reports;
    private readonly Dictionary<(string Network, string ReportId, int Version), AdProviderReportImportResult> _imports = [];
    private readonly Dictionary<(string Network, string BatchId, int Version), string> _batchClaims = [];

    public AdRewardReconciler(AdNetworkPolicyStore policies, HmacAdProviderReportService reports)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _reports = reports ?? throw new ArgumentNullException(nameof(reports));
    }

    public IReadOnlyList<AdRewardReconciliation> Reconciliations
    {
        get { lock (_gate) return [.. _imports.Values.Select(item => item.Reconciliation)]; }
    }

    public AdProviderReportImportResult Import(
        AdProviderReport report,
        IReadOnlyCollection<AdRewardAttribution> attributions,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(attributions);
        if (!_reports.Verify(report, now))
            throw new AdProviderReportVerificationException("Provider report signature or timing is invalid.");

        lock (_gate)
        {
            var key = (report.Network, report.ReportId, report.Version);
            if (_imports.TryGetValue(key, out var duplicate))
            {
                if (duplicate.VerifiedReport.Report == report) return duplicate;
                throw new AdProviderReportConflictException("Provider report version has conflicting content.");
            }

            var previous = _imports.Values
                .Where(item => item.VerifiedReport.ReportId == report.ReportId)
                .OrderByDescending(item => item.VerifiedReport.Version)
                .FirstOrDefault();
            var expectedVersion = previous is null ? 1 : previous.VerifiedReport.Version + 1;
            if (report.Version != expectedVersion)
                throw new AdProviderReportConflictException("Provider report versions must be contiguous and forward-only.");
            var batchKey = (report.Network, report.BatchId, report.Version);
            if (_batchClaims.TryGetValue(batchKey, out var claimedBy) && claimedBy != report.ReportId)
                throw new AdProviderReportConflictException("Provider batch version was already reconciled by another report.");

            var sessions = report.VerifiedSessionIds.ToHashSet();
            var matched = attributions.Where(item =>
                    item.Network == report.Network && item.ProviderBatchId == report.BatchId && sessions.Contains(item.SessionId))
                .ToArray();
            var estimated = Sum(matched.Select(item => item.EstimatedRevenueUsdNanos));
            var historicalRewards = Sum(matched.Select(item => item.RewardSoftUnits));
            var previousActual = previous?.VerifiedReport.ActualRevenueUsdNanos ?? 0;
            var actualDelta = checked(report.ActualRevenueUsdNanos - previousActual);
            var variance = checked(report.ActualRevenueUsdNanos - estimated);
            var currentPolicy = _policies.Latest(report.Network);
            var futurePolicy = TuneFuturePolicy(currentPolicy, report, estimated, matched.Length, variance);
            _policies.Publish(futurePolicy);
            var reconciliation = new AdRewardReconciliation(
                report.Network,
                report.ReportId,
                report.Version,
                report.BatchId,
                estimated,
                previousActual,
                report.ActualRevenueUsdNanos,
                actualDelta,
                variance,
                historicalRewards,
                now);
            var result = new AdProviderReportImportResult(
                reconciliation, new VerifiedAdProviderReport(report), futurePolicy);
            _imports.Add(key, result);
            _batchClaims[batchKey] = report.ReportId;
            return result;
        }
    }

    private static AdNetworkPolicy TuneFuturePolicy(
        AdNetworkPolicy current,
        AdProviderReport report,
        long estimated,
        int sessionCount,
        long variance)
    {
        long ecpm;
        if (estimated > 0 && report.ActualRevenueUsdNanos < estimated)
            ecpm = checked((long)((Int128)current.EstimatedNetEcpmUsdNanos *
                                  report.ActualRevenueUsdNanos / estimated));
        else if (sessionCount > 0)
            ecpm = checked((long)((Int128)report.ActualRevenueUsdNanos * 1_000 / sessionCount));
        else
            ecpm = current.EstimatedNetEcpmUsdNanos;
        ecpm = Math.Max(1, ecpm);
        var buffer = variance switch
        {
            < 0 => Math.Min(900_000, current.SafetyBufferPpm + 50_000),
            > 0 => Math.Max(0, current.SafetyBufferPpm - 10_000),
            _ => current.SafetyBufferPpm
        };
        var ranking = variance switch
        {
            < 0 => Math.Max(0, current.Ranking - 10),
            > 0 => checked(current.Ranking + 5),
            _ => current.Ranking
        };
        var effectiveAt = report.ImportedAt > current.ExpiresAt ? report.ImportedAt : current.ExpiresAt;
        var duration = current.ExpiresAt - current.EffectiveAt;
        return new AdNetworkPolicy(
            current.Network,
            new PolicyVersion(current.Version.Value + 1),
            effectiveAt,
            effectiveAt + duration,
            current.IssuanceMode,
            AdNetworkYieldState.Trailing,
            ecpm,
            current.ContractedRevenueSharePpm,
            buffer,
            current.MinimumVisiblePpm,
            current.MaximumFocusLoss,
            current.MaximumRewardSoftUnits,
            report.PeriodEnd,
            current.ReportStaleAfter,
            ranking);
    }

    private static long Sum(IEnumerable<long> values) =>
        values.Aggregate(0L, static (sum, value) => checked(sum + value));
}

public sealed class AdProviderReportVerificationException(string message) : InvalidOperationException(message);
public sealed class AdProviderReportConflictException(string message) : InvalidOperationException(message);

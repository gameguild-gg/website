using System.Data;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Core.Compliance;

/// <summary>Captures bounded, minimized evidence in a tenant-filtered repeatable-read snapshot.</summary>
public sealed class PostgreSqlComplianceEvidenceDataSource(IServiceScopeFactory scopes, AuditChainEvidenceVerifier verifier) : IComplianceEvidenceDataSource
{
    private const int MaximumRows = 50000;
    private const int MaximumIntegrityPayloadBytes = 262144;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ComplianceEvidenceDataset>> CaptureAsync(Guid tenantId, DateTime periodStartUtc,
        DateTime periodEndUtc, IReadOnlyList<ComplianceEvidenceKind> kinds, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || periodStartUtc.Kind != DateTimeKind.Utc || periodEndUtc.Kind != DateTimeKind.Utc ||
            periodStartUtc > periodEndUtc || (periodEndUtc - periodStartUtc).TotalDays > 1827 ||
            kinds.Count > 6 || kinds.Any(kind => !Enum.IsDefined(kind)) || kinds.Distinct().Count() != kinds.Count)
        {
            throw new ArgumentException("A tenant, ordered UTC period and unique supported source kinds are required.");
        }
        // The prepare command owns a separate write transaction; this connection captures read evidence consistently.
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.SetCommandTimeout(60);
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            var result = new List<ComplianceEvidenceDataset>();
            foreach (var kind in kinds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(kind == ComplianceEvidenceKind.Integrity
                    ? await CaptureIntegrityAsync(context, tenantId, periodStartUtc, periodEndUtc, cancellationToken).ConfigureAwait(false)
                    : kind == ComplianceEvidenceKind.Retention
                        ? await CaptureRetentionAsync(context, tenantId, cancellationToken).ConfigureAwait(false)
                        : await CaptureEventsAsync(context, tenantId, periodStartUtc, periodEndUtc, kind, cancellationToken).ConfigureAwait(false));
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (IReadOnlyList<ComplianceEvidenceDataset>)result;
        }).ConfigureAwait(false);
    }

    private static async Task<ComplianceEvidenceDataset> CaptureEventsAsync(ApplicationDbContext context, Guid tenant,
        DateTime start, DateTime end, ComplianceEvidenceKind kind, CancellationToken cancellationToken)
    {
        var query = context.Set<AuditLog>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenant && item.CreatedAt >= start && item.CreatedAt <= end);
        query = kind switch
        {
            ComplianceEvidenceKind.Authentication => query.Where(item => item.Category == AuditCategory.Authentication),
            ComplianceEvidenceKind.Authorization => query.Where(item => item.Category == AuditCategory.Authorization || item.Category == AuditCategory.Permission),
            ComplianceEvidenceKind.Incidents => query.Where(item => item.Category == AuditCategory.Security && item.RiskLevel >= AuditRiskLevel.High),
            _ => query
        };
        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        using var capture = new DatasetCapture(kind, kind == ComplianceEvidenceKind.Incidents
            ? "AuditLogs: high-risk security events; incident adjudication requires reviewed evidence"
            : $"AuditLogs: {kind}; descriptions, metadata, resource/user/session identifiers, IP and user agent omitted");
        var rows = query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).Take(MaximumRows + 1)
            .Select(item => new AuditEvidenceRow(item.Id, item.CreatedAt, item.ActionType, item.ResourceType, item.Success, item.Category, item.RiskLevel));
        await foreach (var row in rows.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!capture.TryAdd(row, row.ObservedAtUtc)) { break; }
        }
        if (capture.Count != total) { capture.AddError("Collection was truncated by the row or byte limit; not all available events were captured."); }
        return capture.Complete();
    }

    private async Task<ComplianceEvidenceDataset> CaptureIntegrityAsync(ApplicationDbContext context, Guid tenant,
        DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var all = context.Set<TamperEvidentAuditLog>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenant && item.Timestamp >= start && item.Timestamp <= end);
        var total = await all.LongCountAsync(cancellationToken).ConfigureAwait(false);
        using var capture = new DatasetCapture(ComplianceEvidenceKind.Integrity,
            "TamperEvidentAuditLogs: canonical hash/signature checks for captured interval and predecessor; personal content omitted");
        var rows = BoundedIntegrityRows(context, tenant).Where(item => item.Timestamp >= start && item.Timestamp <= end)
            .OrderBy(item => item.SequenceNumber).Take(MaximumRows + 1);
        var firstSequence = await rows.Select(item => (long?)item.SequenceNumber).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        long? previousSequence = firstSequence == 1 ? 0 : null;
        string? previousHash = firstSequence == 1 ? string.Empty : null;
        if (firstSequence is > 1)
        {
            // Fetch the anchor before opening the streaming reader on this connection.
            var predecessor = await BoundedIntegrityRows(context, tenant)
                .SingleOrDefaultAsync(item => item.SequenceNumber == firstSequence - 1, cancellationToken).ConfigureAwait(false);
            if (predecessor is null || !verifier.VerifyEntry(predecessor))
            {
                capture.AddError("The interval predecessor is missing, too large or cannot be cryptographically verified.");
            }
            previousSequence = predecessor?.SequenceNumber;
            previousHash = predecessor?.ChainHash;
        }
        await foreach (var row in rows.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var (verified, signedTimestamp) = verifier.VerifyEntryAndTimestamp(row);
            if (!verified) { capture.AddError("At least one captured canonical content hash or trusted audit signature failed verification."); }
            if (previousSequence is null || row.SequenceNumber != previousSequence + 1 || row.PreviousHash != previousHash)
            {
                capture.AddError("The captured interval has a missing or disconnected chain entry.");
            }
            var evidence = new IntegrityEvidenceRow(row.Id, row.Timestamp, row.SequenceNumber,
                row.ContentHash, row.PreviousHash, row.ChainHash, row.DigitalSignature, row.SigningKeyId, verified, signedTimestamp);
            if (!capture.TryAdd(evidence, row.Timestamp)) { break; }
            previousSequence = row.SequenceNumber;
            previousHash = row.ChainHash;
        }
        if (capture.Count != total) { capture.AddError("Integrity collection was truncated by payload, row or byte limits; not all interval entries were captured."); }
        return capture.Complete();
    }

    private static IQueryable<TamperEvidentAuditLog> BoundedIntegrityRows(ApplicationDbContext context, Guid tenant) =>
        context.Set<TamperEvidentAuditLog>().FromSqlInterpolated($"""
            SELECT * FROM "TamperEvidentAuditLogs" t
            WHERE t."TenantId" = {tenant}
              AND COALESCE(octet_length(t."BeforeSnapshot"), 0)::bigint + COALESCE(octet_length(t."AfterSnapshot"), 0)::bigint
                  + octet_length(t."Changes")::bigint <= {MaximumIntegrityPayloadBytes}
            """).IgnoreQueryFilters().AsNoTracking();

    private static async Task<ComplianceEvidenceDataset> CaptureRetentionAsync(ApplicationDbContext context, Guid tenant, CancellationToken cancellationToken)
    {
        using var capture = new DatasetCapture(ComplianceEvidenceKind.Retention, "AuditRetentionConfigurations: simulation assumptions, not enforced retention policy");
        var configuration = await context.Set<AuditRetentionConfiguration>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenant).Select(item => new { item.Id, item.Revision, ObservedAtUtc = item.UpdatedAt })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (configuration is not null) { capture.TryAdd(configuration, configuration.ObservedAtUtc); }
        capture.AddError("Only retention simulation configuration is available; an enforced retention policy was not collected.");
        return capture.Complete();
    }

    private sealed record AuditEvidenceRow(Guid Id, DateTime ObservedAtUtc, string Action, string ResourceType, bool Success, AuditCategory Category, AuditRiskLevel RiskLevel);
    private sealed record IntegrityEvidenceRow(Guid Id, DateTime ObservedAtUtc, long SequenceNumber,
        string ContentHash, string PreviousHash, string ChainHash, string Signature, string KeyId, bool CanonicalContentAndSignatureVerified, DateTime SignedTimestampUtc);

    private sealed class DatasetCapture(ComplianceEvidenceKind kind, string source) : IDisposable
    {
        private readonly MemoryStream _content = new();
        private readonly List<string> _errors = [];
        private readonly HashSet<DateOnly> _dates = [];
        private Utf8JsonWriter? _writer;
        private DateTime? _first;
        private DateTime? _last;
        public int Count { get; private set; }

        public bool TryAdd<T>(T row, DateTime observedAtUtc)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(row, JsonOptions);
            if (Count >= MaximumRows || _content.Length + bytes.Length + 2 > ComplianceEvidenceValidationEngine.MaximumDatasetBytes)
            {
                return false;
            }
            EnsureWriter();
            _writer!.WriteRawValue(bytes, skipInputValidation: true);
            _writer.Flush();
            Count++;
            if (_first is null || observedAtUtc < _first) { _first = observedAtUtc; }
            if (_last is null || observedAtUtc > _last) { _last = observedAtUtc; }
            _dates.Add(DateOnly.FromDateTime(observedAtUtc));
            return true;
        }
        public void AddError(string error) { if (_errors.Count < 100 && !_errors.Contains(error, StringComparer.Ordinal)) { _errors.Add(error); } }
        public ComplianceEvidenceDataset Complete()
        {
            EnsureWriter();
            _writer!.WriteEndArray();
            _writer.Flush();
            return new(kind, source, _content.ToArray(), Count, _first, _last, _dates.Order().ToArray(), _errors);
        }
        private void EnsureWriter()
        {
            if (_writer is not null) { return; }
            _writer = new Utf8JsonWriter(_content);
            _writer.WriteStartArray();
            _writer.Flush();
        }
        public void Dispose() { _writer?.Dispose(); _content.Dispose(); }
    }
}

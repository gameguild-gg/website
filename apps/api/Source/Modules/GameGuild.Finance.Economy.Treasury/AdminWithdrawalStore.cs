using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Finance.Economy.Treasury;

public interface IAdminWithdrawalStore
{
    AdminWithdrawalRun? FindReplay(string key, string requestHash);
    AdminWithdrawalRun? FindPeriod(DateOnly periodStart);
    void Add(AdminWithdrawalRun run);
    AdminWithdrawalRun Get(Guid runId);
    AdminWithdrawalRun Update(AdminWithdrawalRun run, long expectedVersion);
    Guid? FindProviderEvent(string eventId, string eventHash);
    void RecordProviderEvent(string eventId, string eventHash, AdminWithdrawalRun run, long expectedVersion);

    IReadOnlyList<AdminWithdrawalRun> List(Guid tenantId, int limit = 100) =>
        throw new NotSupportedException("This administrative-withdrawal store does not support listing.");

    AdminWithdrawalRun? FindReplay(Guid tenantId, string key, string requestHash)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var run = FindReplay(key, requestHash);
        return run?.TenantId == tenantId ? run : null;
    }

    AdminWithdrawalRun? FindPeriod(Guid tenantId, DateOnly periodStart)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var run = FindPeriod(periodStart);
        return run?.TenantId == tenantId ? run : null;
    }

    AdminWithdrawalRun Get(Guid tenantId, Guid runId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var run = Get(runId);
        return run.TenantId == tenantId
            ? run
            : throw new KeyNotFoundException("Admin withdrawal run was not found in the tenant.");
    }

    Guid? FindProviderEvent(Guid tenantId, string eventId, string eventHash)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var runId = FindProviderEvent(eventId, eventHash);
        return runId.HasValue && Get(runId.Value).TenantId == tenantId ? runId : null;
    }

    void RecordProviderEvent(
        Guid tenantId,
        string eventId,
        string eventHash,
        AdminWithdrawalRun run,
        long expectedVersion)
    {
        if (tenantId == Guid.Empty || run.TenantId != tenantId)
            throw new ArgumentException("The run must belong to the actor tenant.", nameof(tenantId));
        RecordProviderEvent(eventId, eventHash, run, expectedVersion);
    }
}

public interface IAdminWithdrawalAuditTrail
{
    AdminWithdrawalAuditEvent Append(
        Guid runId,
        string kind,
        Guid? actorId,
        string evidence,
        DateTimeOffset occurredAt);

    IReadOnlyList<AdminWithdrawalAuditEvent> Events(Guid runId);
    bool Verify(Guid runId);

    AdminWithdrawalAuditEvent Append(
        Guid tenantId,
        Guid runId,
        string kind,
        Guid? actorId,
        string evidence,
        DateTimeOffset occurredAt)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        return Append(runId, kind, actorId, evidence, occurredAt);
    }

    IReadOnlyList<AdminWithdrawalAuditEvent> Events(Guid tenantId, Guid runId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        return Events(runId);
    }

    bool Verify(Guid tenantId, Guid runId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        return Verify(runId);
    }
}

public sealed class InMemoryAdminWithdrawalStore : IAdminWithdrawalStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, AdminWithdrawalRun> _runs = [];
    private readonly Dictionary<string, (string Hash, Guid RunId)> _idempotency = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Hash, Guid RunId)> _providerEvents = new(StringComparer.Ordinal);

    public int Count
    {
        get { lock (_gate) return _runs.Count; }
    }

    public AdminWithdrawalRun? FindReplay(string key, string requestHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        lock (_gate)
        {
            if (!_idempotency.TryGetValue(key.Trim(), out var existing)) return null;
            if (!string.Equals(existing.Hash, requestHash, StringComparison.Ordinal))
                throw new AdminWithdrawalStaleCommandException(
                    "The withdrawal idempotency key is bound to a different request.");
            return _runs[existing.RunId];
        }
    }

    public AdminWithdrawalRun? FindPeriod(DateOnly periodStart)
    {
        lock (_gate)
            return _runs.Values.SingleOrDefault(run =>
                run.PeriodStart == periodStart &&
                run.State is not (AdminWithdrawalRunState.Failed or AdminWithdrawalRunState.Cancelled));
    }

    public void Add(AdminWithdrawalRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        lock (_gate)
        {
            if (_runs.ContainsKey(run.Id))
                throw new AdminWithdrawalStaleCommandException("The withdrawal run already exists.");
            if (_idempotency.ContainsKey(run.IdempotencyKey.Value))
                throw new AdminWithdrawalStaleCommandException("The withdrawal idempotency key already exists.");
            if (_runs.Values.Any(current => current.PeriodStart == run.PeriodStart &&
                                            current.State is not (AdminWithdrawalRunState.Failed or
                                                AdminWithdrawalRunState.Cancelled)))
                throw new AdminWithdrawalOverlapException("A withdrawal run already owns this monthly period.");
            _runs.Add(run.Id, run);
            _idempotency.Add(run.IdempotencyKey.Value, (run.RequestHash, run.Id));
        }
    }

    public AdminWithdrawalRun Get(Guid runId)
    {
        if (runId == Guid.Empty) throw new ArgumentException("Run ID is required.", nameof(runId));
        lock (_gate)
            return _runs.TryGetValue(runId, out var run)
                ? run
                : throw new KeyNotFoundException("Admin withdrawal run was not found.");
    }

    public AdminWithdrawalRun Update(AdminWithdrawalRun run, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(run);
        lock (_gate)
        {
            var current = GetUnderLock(run.Id);
            if (current.Version != expectedVersion || run.Version != checked(expectedVersion + 1))
                throw new AdminWithdrawalStaleCommandException("Admin withdrawal run version is stale.");
            _runs[run.Id] = run;
            return run;
        }
    }

    public Guid? FindProviderEvent(string eventId, string eventHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHash);
        lock (_gate)
        {
            if (!_providerEvents.TryGetValue(eventId.Trim(), out var existing)) return null;
            if (!string.Equals(existing.Hash, eventHash, StringComparison.Ordinal))
                throw new AdminWithdrawalEvidenceException(
                    "The provider event ID is bound to different evidence.");
            return existing.RunId;
        }
    }

    public void RecordProviderEvent(string eventId, string eventHash, AdminWithdrawalRun run, long expectedVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHash);
        lock (_gate)
        {
            if (_providerEvents.ContainsKey(eventId.Trim()))
                throw new AdminWithdrawalEvidenceException("The provider event was already recorded.");
            var current = GetUnderLock(run.Id);
            if (current.Version != expectedVersion || run.Version != checked(expectedVersion + 1))
                throw new AdminWithdrawalStaleCommandException("Admin withdrawal provider event is stale.");
            _runs[run.Id] = run;
            _providerEvents.Add(eventId.Trim(), (eventHash, run.Id));
        }
    }

    public IReadOnlyList<AdminWithdrawalRun> List(Guid tenantId, int limit = 100)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (limit is <= 0 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
            return _runs.Values.Where(run => run.TenantId == tenantId)
                .OrderByDescending(run => run.CreatedAt).ThenBy(run => run.Id)
                .Take(limit).ToArray();
    }

    private AdminWithdrawalRun GetUnderLock(Guid runId) =>
        _runs.TryGetValue(runId, out var run)
            ? run
            : throw new KeyNotFoundException("Admin withdrawal run was not found.");
}

public sealed record AdminWithdrawalAuditEvent(
    Guid RunId,
    long Sequence,
    string Kind,
    Guid? ActorId,
    string Evidence,
    DateTimeOffset OccurredAt,
    string PreviousHash,
    string Hash);

public sealed class AdminWithdrawalAuditTrail : IAdminWithdrawalAuditTrail
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, List<AdminWithdrawalAuditEvent>> _events = [];

    public AdminWithdrawalAuditEvent Append(
        Guid runId,
        string kind,
        Guid? actorId,
        string evidence,
        DateTimeOffset occurredAt)
    {
        if (runId == Guid.Empty) throw new ArgumentException("Run ID is required.", nameof(runId));
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        lock (_gate)
        {
            if (!_events.TryGetValue(runId, out var events))
            {
                events = [];
                _events.Add(runId, events);
            }
            var sequence = checked(events.Count + 1L);
            var previousHash = events.Count == 0 ? new string('0', 64) : events[^1].Hash;
            var hash = Hash(runId, sequence, kind.Trim(), actorId, evidence.Trim(), occurredAt, previousHash);
            var auditEvent = new AdminWithdrawalAuditEvent(
                runId, sequence, kind.Trim(), actorId, evidence.Trim(), occurredAt, previousHash, hash);
            events.Add(auditEvent);
            return auditEvent;
        }
    }

    public IReadOnlyList<AdminWithdrawalAuditEvent> Events(Guid runId)
    {
        lock (_gate)
            return _events.TryGetValue(runId, out var events) ? events.ToArray() : [];
    }

    public bool Verify(Guid runId)
    {
        lock (_gate)
        {
            if (!_events.TryGetValue(runId, out var events) || events.Count == 0) return false;
            var previousHash = new string('0', 64);
            for (var index = 0; index < events.Count; index++)
            {
                var item = events[index];
                var expected = Hash(
                    item.RunId, index + 1L, item.Kind, item.ActorId,
                    item.Evidence, item.OccurredAt, previousHash);
                if (item.RunId != runId || item.Sequence != index + 1L ||
                    !string.Equals(item.PreviousHash, previousHash, StringComparison.Ordinal) ||
                    !string.Equals(item.Hash, expected, StringComparison.Ordinal))
                    return false;
                previousHash = item.Hash;
            }
            return true;
        }
    }

    private static string Hash(
        Guid runId,
        long sequence,
        string kind,
        Guid? actorId,
        string evidence,
        DateTimeOffset occurredAt,
        string previousHash)
    {
        var canonical = string.Join('|',
            runId.ToString("N"),
            sequence.ToString(CultureInfo.InvariantCulture),
            kind,
            actorId?.ToString("N") ?? string.Empty,
            evidence,
            occurredAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
            previousHash);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

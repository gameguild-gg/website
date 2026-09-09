namespace GameGuild.Finance.Economy.Payouts;

public sealed record PayoutProviderEventRecord(
    string EventId,
    string EventHash,
    Guid OperationId,
    PayoutOperationState ResultingState,
    DateTimeOffset RecordedAt);

public interface IPayoutOperationStore
{
    PayoutOperation Get(Guid operationId);
    PayoutOperation GetForTenant(Guid tenantId, Guid operationId);
    IReadOnlyList<PayoutOperation> ListForTenant(Guid tenantId, int take);
    IReadOnlyList<PayoutOperation> ListForPayee(Guid tenantId, Guid payeeId, int take);
    PayoutOperation? FindReplay(Guid tenantId, string idempotencyKey, string requestHash);
    void Add(PayoutOperation operation);
    PayoutOperation Update(PayoutOperation operation, long expectedVersion);
    PayoutProviderEventRecord? FindProviderEvent(string eventId, string eventHash);
    PayoutProviderEventRecord RecordProviderEvent(
        string eventId,
        string eventHash,
        PayoutOperation resultingOperation,
        long expectedVersion,
        DateTimeOffset recordedAt);
}

public sealed class InMemoryPayoutOperationStore : IPayoutOperationStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, PayoutOperation> _operations = [];
    private readonly Dictionary<(Guid TenantId, string Key), Guid> _idempotency = [];
    private readonly Dictionary<string, PayoutProviderEventRecord> _events = new(StringComparer.Ordinal);

    public IReadOnlyList<PayoutOperation> Operations
    {
        get { lock (_gate) return [.. _operations.Values.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)]; }
    }

    public IReadOnlyList<PayoutProviderEventRecord> ProviderEvents
    {
        get { lock (_gate) return [.. _events.Values.OrderBy(item => item.RecordedAt).ThenBy(item => item.EventId)]; }
    }

    public PayoutOperation Get(Guid operationId)
    {
        lock (_gate)
            return _operations.TryGetValue(operationId, out var operation)
                ? operation
                : throw new KeyNotFoundException($"Payout operation {operationId:N} was not found.");
    }

    public PayoutOperation GetForTenant(Guid tenantId, Guid operationId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var operation = Get(operationId);
        return operation.TenantId == tenantId
            ? operation
            : throw new KeyNotFoundException($"Payout operation {operationId:N} was not found.");
    }

    public IReadOnlyList<PayoutOperation> ListForTenant(Guid tenantId, int take)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        ValidateTake(take);
        lock (_gate)
            return [.. _operations.Values.Where(operation => operation.TenantId == tenantId)
                .OrderByDescending(operation => operation.CreatedAt).ThenByDescending(operation => operation.Id).Take(take)];
    }

    public IReadOnlyList<PayoutOperation> ListForPayee(Guid tenantId, Guid payeeId, int take)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (payeeId == Guid.Empty)
            throw new ArgumentException("Payee ID is required.", nameof(payeeId));
        ValidateTake(take);

        lock (_gate)
        {
            return [.. _operations.Values
                .Where(operation => operation.TenantId == tenantId && operation.PayeeId == payeeId)
                .OrderByDescending(operation => operation.CreatedAt)
                .ThenByDescending(operation => operation.Id)
                .Take(take)];
        }
    }

    public PayoutOperation? FindReplay(Guid tenantId, string idempotencyKey, string requestHash)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        lock (_gate)
        {
            if (!_idempotency.TryGetValue((tenantId, idempotencyKey), out var operationId)) return null;
            var operation = _operations[operationId];
            if (!string.Equals(operation.RequestHash, requestHash, StringComparison.Ordinal))
                throw new PayoutReplayConflictException("Payout idempotency key was reused with different inputs.");
            return operation;
        }
    }

    public void Add(PayoutOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.TenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(operation));
        lock (_gate)
        {
            var replayKey = (operation.TenantId, operation.IdempotencyKey.Value);
            if (_operations.ContainsKey(operation.Id) || _idempotency.ContainsKey(replayKey))
                throw new PayoutReplayConflictException("Payout operation or idempotency key already exists.");
            _operations.Add(operation.Id, operation);
            _idempotency.Add(replayKey, operation.Id);
        }
    }

    public PayoutOperation Update(PayoutOperation operation, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            var current = GetUnsafe(operation.Id);
            if (current.Version != expectedVersion || operation.Version != checked(expectedVersion + 1))
                throw new PayoutStaleCommandException("Payout operation version is stale.");
            _operations[operation.Id] = operation;
            return operation;
        }
    }

    public PayoutProviderEventRecord? FindProviderEvent(string eventId, string eventHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHash);
        lock (_gate)
        {
            if (!_events.TryGetValue(eventId, out var record)) return null;
            if (!string.Equals(record.EventHash, eventHash, StringComparison.Ordinal))
                throw new PayoutReplayConflictException("Provider event ID was replayed with different evidence.");
            return record;
        }
    }

    public PayoutProviderEventRecord RecordProviderEvent(
        string eventId,
        string eventHash,
        PayoutOperation resultingOperation,
        long expectedVersion,
        DateTimeOffset recordedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHash);
        ArgumentNullException.ThrowIfNull(resultingOperation);
        lock (_gate)
        {
            var replay = FindProviderEventUnsafe(eventId, eventHash);
            if (replay is not null) return replay;
            UpdateUnsafe(resultingOperation, expectedVersion);
            var record = new PayoutProviderEventRecord(
                eventId.Trim(), eventHash.Trim(), resultingOperation.Id, resultingOperation.State, recordedAt);
            _events.Add(record.EventId, record);
            return record;
        }
    }

    private PayoutOperation GetUnsafe(Guid operationId) =>
        _operations.TryGetValue(operationId, out var operation)
            ? operation
            : throw new KeyNotFoundException($"Payout operation {operationId:N} was not found.");

    private void UpdateUnsafe(PayoutOperation operation, long expectedVersion)
    {
        var current = GetUnsafe(operation.Id);
        if (current.Version != expectedVersion)
            throw new PayoutStaleCommandException("Payout operation current version is stale.");
        if (operation.Version != checked(expectedVersion + 1))
            throw new PayoutStaleCommandException("Payout operation replacement version is not sequential.");
        _operations[operation.Id] = operation;
    }

    private PayoutProviderEventRecord? FindProviderEventUnsafe(string eventId, string eventHash)
    {
        if (!_events.TryGetValue(eventId, out var record)) return null;
        if (!string.Equals(record.EventHash, eventHash, StringComparison.Ordinal))
            throw new PayoutReplayConflictException("Provider event ID was replayed with different evidence.");
        return record;
    }

    private static void ValidateTake(int take)
    {
        if (take is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 100.");
    }
}

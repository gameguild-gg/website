using GameGuild.Finance.Economy.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Payouts;

public sealed class PostgreSqlPayoutOperationStore : IPayoutOperationStore
{
    private readonly DbContext _db;

    public PostgreSqlPayoutOperationStore(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "PostgreSQL payout persistence requires the application's relational DbContext.");
    }

    public PayoutOperation Get(Guid operationId)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Payout operation ID is required.", nameof(operationId));

        var row = ReadOperations($"""
            SELECT * FROM economy_private.read_payout_operation_by_id_v1({operationId})
            """).SingleOrDefault();

        return row is null
            ? throw new KeyNotFoundException($"Payout operation {operationId:N} was not found.")
            : ToContract(row);
    }

    public PayoutOperation GetForTenant(Guid tenantId, Guid operationId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (operationId == Guid.Empty)
            throw new ArgumentException("Payout operation ID is required.", nameof(operationId));

        var row = ReadOperations($"""
            SELECT * FROM economy_private.read_payout_operation_for_tenant_v2({tenantId}, {operationId})
            """).SingleOrDefault();
        return row is null
            ? throw new KeyNotFoundException($"Payout operation {operationId:N} was not found.")
            : ToContract(row);
    }

    public IReadOnlyList<PayoutOperation> ListForTenant(Guid tenantId, int take)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        ValidateTake(take);
        return ReadOperations($"""
            SELECT * FROM economy_private.read_payout_operations_for_tenant_v2({tenantId}, {take})
            """).Select(ToContract).ToArray();
    }

    public IReadOnlyList<PayoutOperation> ListForPayee(Guid tenantId, Guid payeeId, int take)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (payeeId == Guid.Empty)
            throw new ArgumentException("Payee ID is required.", nameof(payeeId));
        ValidateTake(take);

        return ReadOperations($"""
            SELECT * FROM economy_private.read_payout_operations_by_payee_v2({tenantId}, {payeeId}, {take})
            """)
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .Select(ToContract)
            .ToArray();
    }

    public PayoutOperation? FindReplay(Guid tenantId, string idempotencyKey, string requestHash)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        var row = ReadOperations($"""
            SELECT * FROM economy_private.read_payout_operation_by_idempotency_v2({tenantId}, {idempotencyKey.Trim()})
            """).SingleOrDefault();

        if (row is null)
            return null;
        if (!string.Equals(row.RequestHash, requestHash, StringComparison.Ordinal))
            throw new PayoutReplayConflictException(
                "Payout idempotency key was reused with different inputs.");

        return ToContract(row);
    }

    public void Add(PayoutOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.TenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(operation));

        Execute($"""
            SELECT economy_private.create_payout_operation_v2(
                {operation.Id},
                {operation.TenantId},
                {operation.IdempotencyKey.Value},
                {operation.RequestHash},
                {operation.ActorId},
                {operation.PayeeId},
                {operation.WalletId.Value},
                {operation.Amount.Units},
                {operation.ProviderAccountId},
                {operation.DestinationHash},
                {operation.ProviderBindingHash},
                {operation.EligibilityHash},
                {operation.DispatchSnapshotHash},
                {operation.ProviderPayoutId},
                {(int)operation.State},
                {operation.Version},
                {operation.FencingToken},
                {operation.KillSwitchEpoch},
                {operation.ReserveVersion.Value},
                {operation.ReserveAuthorizationEpoch},
                {operation.PolicyVersion.Value},
                {operation.RiskDecisionId},
                {operation.CreatedAt},
                {operation.UpdatedAt});
            """);
    }

    public PayoutOperation Update(PayoutOperation operation, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(operation);

        Execute($"""
            SELECT economy_private.transition_payout_operation_v1(
                {operation.Id},
                {expectedVersion},
                {(int)operation.State},
                {operation.DispatchSnapshotHash},
                {operation.ProviderPayoutId},
                {operation.UpdatedAt});
            """);
        return operation;
    }

    public PayoutProviderEventRecord? FindProviderEvent(string eventId, string eventHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHash);

        var row = _db.Set<PayoutProviderEventRow>()
            .FromSqlInterpolated($"""
                SELECT * FROM economy_private.read_payout_provider_event_v1({eventId.Trim()})
                """)
            .AsNoTracking()
            .SingleOrDefault();

        if (row is null)
            return null;
        if (!string.Equals(row.EventHash, eventHash, StringComparison.Ordinal))
            throw new PayoutReplayConflictException(
                "Provider event ID was replayed with different evidence.");

        return new PayoutProviderEventRecord(
            row.EventId,
            row.EventHash,
            row.OperationId,
            row.ResultingState,
            row.RecordedAt);
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

        Execute($"""
            SELECT economy_private.complete_payout_provider_event_v1(
                {eventId.Trim()},
                {eventHash.Trim()},
                {resultingOperation.Id},
                {expectedVersion},
                {(int)resultingOperation.State},
                {resultingOperation.ProviderPayoutId},
                {recordedAt});
            """);

        return new PayoutProviderEventRecord(
            eventId.Trim(),
            eventHash.Trim(),
            resultingOperation.Id,
            resultingOperation.State,
            recordedAt);
    }

    private IQueryable<PayoutOperationRow> ReadOperations(FormattableString sql) =>
        _db.Set<PayoutOperationRow>().FromSqlInterpolated(sql).AsNoTracking();

    private void Execute(FormattableString sql)
    {
        try
        {
            _db.Database.ExecuteSqlInterpolated(sql);
        }
        catch (Exception exception) when (
            exception.Message.Contains("payout", StringComparison.OrdinalIgnoreCase))
        {
            throw Translate(exception);
        }
    }

    private static Exception Translate(Exception exception)
    {
        var message = exception.Message;
        return message.Contains("idempotency", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("provider event", StringComparison.OrdinalIgnoreCase)
            ? new PayoutReplayConflictException(message)
            : new PayoutStaleCommandException(message);
    }

    private static PayoutOperation ToContract(PayoutOperationRow row) => new(
        row.Id,
        new IdempotencyKey(row.IdempotencyKey),
        row.RequestHash,
        row.ActorId,
        row.PayeeId,
        new WalletId(row.WalletId),
        new CoinAmount(CurrencyCode.HardCoin, row.AmountUnits),
        row.ProviderAccountId,
        row.DestinationHash,
        row.ProviderBindingHash,
        row.EligibilityHash,
        row.DispatchSnapshotHash,
        row.ProviderPayoutId,
        row.State,
        row.Version,
        row.FencingToken,
        row.KillSwitchEpoch,
        new ReserveVersion(row.ReserveVersion),
        row.ReserveAuthorizationEpoch,
        new PolicyVersion(row.PolicyVersion),
        row.RiskDecisionId,
        row.CreatedAt,
        row.UpdatedAt,
        row.TenantId);

    private static void ValidateTake(int take)
    {
        if (take is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 100.");
    }
}

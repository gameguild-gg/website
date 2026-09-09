using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Payouts;

public interface IPayoutDispatchOutboxWriter
{
    Task AddAsync(PayoutDispatchOutboxRow row, CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlPayoutDispatchOutboxWriter : IPayoutDispatchOutboxWriter
{
    private readonly DbContext _db;

    public PostgreSqlPayoutDispatchOutboxWriter(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException("Payout outbox writing requires a relational DbContext.");
    }

    public async Task AddAsync(PayoutDispatchOutboxRow row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        _db.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record PayoutDispatchOutboxResult(
    Guid OperationId,
    PayoutProviderOutcome Outcome,
    bool Processed,
    int AttemptCount);

public interface IPayoutDispatchOutboxProcessor
{
    ValueTask<PayoutDispatchOutboxResult?> ProcessNextAsync(
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlPayoutDispatchOutboxProcessor : IPayoutDispatchOutboxProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContext _db;
    private readonly IPayoutOperationStore _operations;
    private readonly IConnectPayoutProvider _provider;
    private readonly IPayoutProviderEvidenceVerifier _evidence;

    public PostgreSqlPayoutDispatchOutboxProcessor(
        IApplicationDbContext context,
        IPayoutOperationStore operations,
        IConnectPayoutProvider provider,
        IPayoutProviderEvidenceVerifier evidence)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(evidence);
        _db = context as DbContext
            ?? throw new InvalidOperationException("Payout outbox processing requires a relational DbContext.");
        _operations = operations;
        _provider = provider;
        _evidence = evidence;
    }

    public async ValueTask<PayoutDispatchOutboxResult?> ProcessNextAsync(
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (workerId.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(workerId));
        var lease = await ClaimAsync(workerId.Trim(), now, cancellationToken);
        if (lease is null) return null;

        PayoutDispatchCommand command;
        try
        {
            if (!string.Equals(Hash(lease.Payload), lease.PayloadHash, StringComparison.Ordinal))
                throw new PayoutEvidenceException("The durable payout dispatch payload hash is invalid.");
            command = JsonSerializer.Deserialize<PayoutDispatchCommand>(lease.Payload, JsonOptions)
                ?? throw new PayoutEvidenceException("The durable payout dispatch payload is invalid.");
            if (lease.OperationId != command.OperationId)
                throw new PayoutEvidenceException(
                    "The durable payout dispatch payload is not bound to its outbox operation.");
            var receipt = await _provider.DispatchAsync(command, cancellationToken);
            if (!_evidence.Verify(receipt))
                throw new PayoutEvidenceException("The payout provider dispatch receipt is invalid.");
            ValidateBinding(command, receipt);

            return await PostgreSqlTransactionExecutor.ExecuteAsync(
                _db, IsolationLevel.ReadCommitted, async _ =>
            {
            var currentOutbox = await _db.Set<PayoutDispatchOutboxRow>()
                .SingleAsync(row => row.Id == lease.Id, cancellationToken);
            if (currentOutbox.CompletedAt.HasValue)
                return new PayoutDispatchOutboxResult(
                    command.OperationId, receipt.Outcome, false, currentOutbox.AttemptCount);
            if (!string.Equals(currentOutbox.LeaseOwner, workerId.Trim(), StringComparison.Ordinal) ||
                !currentOutbox.LeaseExpiresAt.HasValue || currentOutbox.LeaseExpiresAt.Value < now)
                throw new PayoutStaleCommandException("The payout dispatch outbox lease is stale.");

            var operation = _operations.Get(command.OperationId);
            if (operation.State == PayoutOperationState.Dispatching)
            {
                var changed = receipt.Outcome == PayoutProviderOutcome.Ambiguous
                    ? operation.Transition(
                        PayoutOperationState.Ambiguous, receipt.ObservedAt,
                        providerPayoutId: receipt.ProviderPayoutId)
                    : operation.BindProviderDispatch(receipt.ProviderPayoutId, receipt.ObservedAt);
                _operations.Update(changed, operation.Version);
            }
            else if (operation.State != PayoutOperationState.Ambiguous)
            {
                throw new PayoutStaleCommandException("The payout is no longer dispatchable.");
            }

            currentOutbox.CompletedAt = receipt.ObservedAt;
            currentOutbox.LeaseOwner = null;
            currentOutbox.LeaseExpiresAt = null;
            currentOutbox.LastErrorCode = null;
            await _db.SaveChangesAsync(cancellationToken);
            return new PayoutDispatchOutboxResult(
                command.OperationId, receipt.Outcome, true, currentOutbox.AttemptCount);
            }, cancellationToken);
        }
        catch
        {
            await ReleaseAsync(lease.Id, workerId.Trim(), now, cancellationToken);
            throw;
        }
    }

    private async ValueTask<PayoutDispatchOutboxRow?> ClaimAsync(
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.ReadCommitted, async _ =>
        {
        var row = await _db.Set<PayoutDispatchOutboxRow>()
            .FromSqlInterpolated($"""
                SELECT * FROM public.economy_payout_dispatch_outbox
                WHERE "CompletedAt" IS NULL AND "AvailableAt" <= {now}
                  AND ("LeaseExpiresAt" IS NULL OR "LeaseExpiresAt" <= {now})
                ORDER BY "CreatedAt", "Id"
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
            return null;
        row.LeaseOwner = workerId;
        row.LeaseExpiresAt = now.AddMinutes(2);
        row.AttemptCount = checked(row.AttemptCount + 1);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
        }, cancellationToken);
    }

    private async ValueTask ReleaseAsync(
        Guid id,
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.economy_payout_dispatch_outbox
            SET "LeaseOwner" = NULL,
                "LeaseExpiresAt" = NULL,
                "AvailableAt" = {now.AddMinutes(1)},
                "LastErrorCode" = {"provider-error"}
            WHERE "Id" = {id} AND "CompletedAt" IS NULL AND "LeaseOwner" = {workerId}
            """, cancellationToken);
    }

    private static void ValidateBinding(PayoutDispatchCommand command, PayoutDispatchReceipt receipt)
    {
        if (receipt.OperationId != command.OperationId ||
            !string.Equals(receipt.ProviderAccountId, command.ProviderAccountId, StringComparison.Ordinal) ||
            !string.Equals(receipt.DestinationHash, command.DestinationHash, StringComparison.Ordinal) ||
            receipt.ObservedAt < command.RequestedAt)
            throw new PayoutProviderBindingException(
                "The payout dispatch receipt is not bound to the durable outbox command.");
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

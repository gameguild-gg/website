using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Treasury;

public interface IAdminWithdrawalDispatchOutboxWriter
{
    Task AddAsync(AdminWithdrawalDispatchOutboxRow row, CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlAdminWithdrawalDispatchOutboxWriter :
    IAdminWithdrawalDispatchOutboxWriter
{
    private readonly DbContext _db;

    public PostgreSqlAdminWithdrawalDispatchOutboxWriter(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException("Treasury outbox writing requires a relational DbContext.");
    }

    public async Task AddAsync(
        AdminWithdrawalDispatchOutboxRow row,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        _db.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record AdminWithdrawalDispatchOutboxResult(
    Guid TenantId,
    Guid RunId,
    AdminWithdrawalProviderOutcome Outcome,
    bool Processed,
    int AttemptCount);

public interface IAdminWithdrawalDispatchOutboxProcessor
{
    ValueTask<AdminWithdrawalDispatchOutboxResult?> ProcessNextAsync(
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlAdminWithdrawalDispatchOutboxProcessor :
    IAdminWithdrawalDispatchOutboxProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContext _db;
    private readonly IAdminWithdrawalStore _runs;
    private readonly IAdminWithdrawalAuditTrail _audit;
    private readonly IAdminWithdrawalProvider _provider;
    private readonly IAdminWithdrawalProviderEvidenceVerifier _evidence;

    public PostgreSqlAdminWithdrawalDispatchOutboxProcessor(
        IApplicationDbContext context,
        IAdminWithdrawalStore runs,
        IAdminWithdrawalAuditTrail audit,
        IAdminWithdrawalProvider provider,
        IAdminWithdrawalProviderEvidenceVerifier evidence)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(evidence);
        _db = context as DbContext
            ?? throw new InvalidOperationException("Treasury outbox processing requires a relational DbContext.");
        _runs = runs;
        _audit = audit;
        _provider = provider;
        _evidence = evidence;
    }

    public async ValueTask<AdminWithdrawalDispatchOutboxResult?> ProcessNextAsync(
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (workerId.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(workerId));
        var lease = await ClaimAsync(workerId.Trim(), now, cancellationToken);
        if (lease is null) return null;
        try
        {
            if (!string.Equals(Hash(lease.Payload), lease.PayloadHash, StringComparison.Ordinal))
                throw new AdminWithdrawalEvidenceException(
                    "The durable Treasury dispatch payload hash is invalid.");
            var command = JsonSerializer.Deserialize<AdminWithdrawalDispatchCommand>(lease.Payload, JsonOptions)
                ?? throw new AdminWithdrawalEvidenceException(
                    "The durable Treasury dispatch payload is invalid.");
            if (command.TenantId == Guid.Empty || lease.TenantId != command.TenantId || lease.RunId != command.RunId)
                throw new AdminWithdrawalEvidenceException(
                    "The durable Treasury dispatch payload is not bound to its tenant outbox row.");
            var receipt = await _provider.DispatchAsync(command, cancellationToken);
            if (!_evidence.Verify(receipt))
                throw new AdminWithdrawalEvidenceException(
                    "The Treasury provider dispatch receipt is invalid.");
            ValidateBinding(command, receipt);

            return await PostgreSqlTransactionExecutor.ExecuteAsync(
                _db, IsolationLevel.ReadCommitted, async _ =>
            {
            var currentOutbox = await _db.Set<AdminWithdrawalDispatchOutboxRow>()
                .SingleAsync(row => row.Id == lease.Id, cancellationToken);
            if (currentOutbox.CompletedAt.HasValue)
                return new AdminWithdrawalDispatchOutboxResult(
                    command.TenantId, command.RunId, receipt.Outcome, false, currentOutbox.AttemptCount);
            if (!string.Equals(currentOutbox.LeaseOwner, workerId.Trim(), StringComparison.Ordinal) ||
                !currentOutbox.LeaseExpiresAt.HasValue || currentOutbox.LeaseExpiresAt.Value < now)
                throw new AdminWithdrawalStaleCommandException(
                    "The Treasury dispatch outbox lease is stale.");

            var run = _runs.Get(command.TenantId, command.RunId);
            if (run.State == AdminWithdrawalRunState.Dispatching)
            {
                var changed = run with
                {
                    State = receipt.Outcome == AdminWithdrawalProviderOutcome.Ambiguous
                        ? AdminWithdrawalRunState.Ambiguous
                        : AdminWithdrawalRunState.Dispatching,
                    ProviderTransferId = receipt.ProviderTransferId,
                    Version = checked(run.Version + 1),
                    UpdatedAt = receipt.ObservedAt
                };
                _runs.Update(changed, run.Version);
                _audit.Append(run.TenantId, run.Id,
                    receipt.Outcome == AdminWithdrawalProviderOutcome.Ambiguous
                        ? "dispatch-ambiguous"
                        : "provider-accepted",
                    null,
                    receipt.EvidenceHash,
                    receipt.ObservedAt);
            }
            else if (run.State != AdminWithdrawalRunState.Ambiguous)
            {
                throw new AdminWithdrawalStaleCommandException(
                    "The Treasury withdrawal is no longer dispatchable.");
            }

            currentOutbox.CompletedAt = receipt.ObservedAt;
            currentOutbox.LeaseOwner = null;
            currentOutbox.LeaseExpiresAt = null;
            currentOutbox.LastErrorCode = null;
            await _db.SaveChangesAsync(cancellationToken);
            return new AdminWithdrawalDispatchOutboxResult(
                command.TenantId, command.RunId, receipt.Outcome, true, currentOutbox.AttemptCount);
            }, cancellationToken);
        }
        catch
        {
            await ReleaseAsync(lease.Id, workerId.Trim(), now, cancellationToken);
            throw;
        }
    }

    private async ValueTask<AdminWithdrawalDispatchOutboxRow?> ClaimAsync(
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.ReadCommitted, async _ =>
        {
        var row = await _db.Set<AdminWithdrawalDispatchOutboxRow>()
            .FromSqlInterpolated($"""
                SELECT * FROM public.economy_admin_withdrawal_dispatch_outbox
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

    private ValueTask<int> ReleaseAsync(
        Guid id,
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken) => new(_db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.economy_admin_withdrawal_dispatch_outbox
            SET "LeaseOwner" = NULL,
                "LeaseExpiresAt" = NULL,
                "AvailableAt" = {now.AddMinutes(1)},
                "LastErrorCode" = {"provider-error"}
            WHERE "Id" = {id} AND "CompletedAt" IS NULL AND "LeaseOwner" = {workerId}
            """, cancellationToken));

    private static void ValidateBinding(
        AdminWithdrawalDispatchCommand command,
        AdminWithdrawalProviderReceipt receipt)
    {
        if (receipt.TenantId != command.TenantId || receipt.RunId != command.RunId ||
            receipt.FencingToken != command.FencingToken ||
            receipt.ExecutionEpoch != command.ExecutionEpoch || receipt.Amount != command.Amount ||
            !string.Equals(receipt.SourceAssetKey, command.SourceAssetKey, StringComparison.Ordinal) ||
            !string.Equals(receipt.DestinationHash, command.DestinationHash, StringComparison.Ordinal) ||
            receipt.ObservedAt < command.RequestedAt)
            throw new AdminWithdrawalEvidenceException(
                "The Treasury dispatch receipt is not bound to the durable outbox command.");
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

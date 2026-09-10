using System.Text.Json;
using GameGuild.Finance.Economy.Bounties.Persistence;
using GameGuild.Finance.Economy.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Bounties;

/// <summary>
/// Immutable terminal evidence for a bounty. The terminal writer stores this only after the
/// corresponding registered posting has been accepted in the same database transaction.
/// </summary>
public sealed record PersistedBountyTerminalEvent(
    Guid Id,
    Guid TenantId,
    BountyId BountyId,
    BountyStatus Status,
    Guid ActorId,
    WalletId DestinationWalletId,
    IdempotencyKey IdempotencyKey,
    Guid? RiskDecisionId,
    SourceStampId? ProceedsSourceStampId,
    CreditLotId? ProceedsLotId,
    long ReturnedUnits,
    long FeeUnits,
    long FirstJournalSequence,
    IReadOnlyList<BountyTerminalOutputLot> OutputLots,
    DateTimeOffset OccurredAt);

/// <summary>
/// Immutable evidence of a materialized terminal output. Monetary authority remains the
/// append-only journal, lots, and lineage rather than this read model.
/// </summary>
public sealed record BountyTerminalOutputLot(
    CreditLotId LotId,
    WalletId WalletId,
    CoinAmount Amount,
    ProvenanceKind Provenance,
    SourceStampId RootSourceStampId,
    DateTimeOffset ConfirmedAt,
    DateTimeOffset OriginalMaturesAt,
    bool CashOutEligible);

public interface IBountyTerminalEventStore
{
    PersistedBountyTerminalEvent? FindByBounty(Guid tenantId, BountyId bountyId);

    PersistedBountyTerminalEvent? FindByIdempotency(Guid tenantId, IdempotencyKey idempotencyKey);
}

/// <summary>
/// Reads terminal bounty evidence through private SECURITY DEFINER procedures. Terminal state
/// can only be written by a specialized claim or reclaim ledger writer, never by an arbitrary
/// application service that has not atomically posted the value movement.
/// </summary>
public sealed class PostgreSqlBountyTerminalEventStore : IBountyTerminalEventStore
{
    private readonly DbContext _db;

    public PostgreSqlBountyTerminalEventStore(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "PostgreSQL bounty terminal persistence requires the application's relational DbContext.");
    }

    public PersistedBountyTerminalEvent? FindByBounty(Guid tenantId, BountyId bountyId)
    {
        ValidateTenant(tenantId);
        return Read("""
            SELECT * FROM economy_private.read_bounty_terminal_by_bounty_v2({0}, {1})
            """, tenantId, bountyId.Value).SingleOrDefault() is { } row
            ? ToContract(row)
            : null;
    }

    public PersistedBountyTerminalEvent? FindByIdempotency(Guid tenantId, IdempotencyKey idempotencyKey)
    {
        ValidateTenant(tenantId);
        return Read("""
            SELECT * FROM economy_private.read_bounty_terminal_by_idempotency_v2({0}, {1})
            """, tenantId, idempotencyKey.Value).SingleOrDefault() is { } row
            ? ToContract(row)
            : null;
    }

    private IQueryable<BountyTerminalEventRow> Read(string sql, params object[] parameters) =>
        _db.Set<BountyTerminalEventRow>().FromSqlRaw(sql, parameters).AsNoTracking();

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A non-quarantine tenant ID is required.", nameof(tenantId));
    }

    private static PersistedBountyTerminalEvent ToContract(BountyTerminalEventRow row) => new(
        row.Id,
        row.TenantId,
        new BountyId(row.BountyId),
        row.Status,
        row.ActorId,
        new WalletId(row.DestinationWalletId),
        new IdempotencyKey(row.IdempotencyKey),
        row.RiskDecisionId,
        row.ProceedsSourceStampId is { } sourceStampId ? new SourceStampId(sourceStampId) : null,
        row.ProceedsLotId is { } proceedsLotId ? new CreditLotId(proceedsLotId) : null,
        row.ReturnedUnits,
        row.FeeUnits,
        row.FirstJournalSequence,
        JsonSerializer.Deserialize<BountyTerminalOutputLotPayload[]>(row.OutputLots)?.Select(item => item.ToContract()).ToArray()
            ?? throw new InvalidOperationException("Persisted bounty terminal output evidence is missing."),
        row.OccurredAt);

    private sealed record BountyTerminalOutputLotPayload(
        Guid LotId,
        Guid WalletId,
        int Currency,
        long AmountUnits,
        int Provenance,
        Guid RootSourceStampId,
        DateTimeOffset ConfirmedAt,
        DateTimeOffset OriginalMaturesAt,
        bool CashOutEligible)
    {
        public BountyTerminalOutputLot ToContract() => new(
            new CreditLotId(LotId),
            new WalletId(WalletId),
            new CoinAmount((CurrencyCode)Currency, AmountUnits),
            (ProvenanceKind)Provenance,
            new SourceStampId(RootSourceStampId),
            ConfirmedAt,
            OriginalMaturesAt,
            CashOutEligible);
    }
}

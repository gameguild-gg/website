using System.Text.Json;
using GameGuild.Finance.Economy.Bounties.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Bounties;

/// <summary>
/// Immutable, durable representation of a posted bounty escrow. Terminal claim/reclaim
/// state is added by the terminal workflow; this store only owns the post/replay boundary.
/// </summary>
public sealed record PersistedBountyEscrow(
    BountyId Id,
    Guid TenantId,
    Guid PosterId,
    WalletId PosterWalletId,
    WalletId EscrowWalletId,
    CoinAmount Amount,
    BountyEligibilityRequirements Eligibility,
    int ReclaimFeePpm,
    BountyStatus Status,
    IdempotencyKey IdempotencyKey,
    string RequestHash,
    DateTimeOffset PostedAt,
    DateTimeOffset ExpiresAt,
    long Version,
    IReadOnlyList<PersistedBountyEscrowFragment> Fragments);

public sealed record PersistedBountyEscrowFragment(
    CreditLotId ParentLotId,
    CreditLotId? EscrowLotId,
    CoinAmount Amount,
    ProvenanceKind Provenance,
    long TraceUnitsPerCoinUnit,
    IReadOnlyList<RootTraceRange> SelectedRanges);

public sealed record CreateBountyEscrowPersistenceCommand(
    BountyEscrowPosition Position,
    Guid TenantId,
    IdempotencyKey IdempotencyKey,
    string RequestHash,
    PostingId PostingId);

public interface IBountyEscrowStore
{
    PersistedBountyEscrow Get(Guid tenantId, BountyId bountyId);

    PersistedBountyEscrow? FindPostReplay(Guid tenantId, IdempotencyKey idempotencyKey, string requestHash);

    PersistedBountyEscrow Create(CreateBountyEscrowPersistenceCommand command);
}

/// <summary>
/// Uses only the dedicated Economy <c>SECURITY DEFINER</c> procedures. Direct DML against
/// bounty tables is deliberately unavailable to the application writer role.
/// </summary>
public sealed class PostgreSqlBountyEscrowStore : IBountyEscrowStore
{
    private readonly DbContext _db;

    public PostgreSqlBountyEscrowStore(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "PostgreSQL bounty persistence requires the application's relational DbContext.");
    }

    public PersistedBountyEscrow Get(Guid tenantId, BountyId bountyId)
    {
        ValidateTenant(tenantId);
        var row = ReadBounties($"""
            SELECT * FROM economy_private.read_bounty_escrow_by_id_v2({tenantId}, {bountyId.Value})
            """).SingleOrDefault();

        return row is null
            ? throw new KeyNotFoundException($"Bounty {bountyId.Value:N} was not found.")
            : ToContract(row);
    }

    public PersistedBountyEscrow? FindPostReplay(Guid tenantId, IdempotencyKey idempotencyKey, string requestHash)
    {
        ValidateTenant(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        var row = ReadBounties($"""
            SELECT * FROM economy_private.read_bounty_escrow_by_idempotency_v2({tenantId}, {idempotencyKey.Value})
            """).SingleOrDefault();

        if (row is null)
            return null;
        if (string.IsNullOrWhiteSpace(row.RequestHash) ||
            !string.Equals(row.RequestHash, requestHash.Trim(), StringComparison.Ordinal))
            throw new BountyIdempotencyConflictException(
                "Bounty post idempotency key was reused with different inputs.");

        return ToContract(row);
    }

    public PersistedBountyEscrow Create(CreateBountyEscrowPersistenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Position);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.RequestHash);
        ValidateTenant(command.TenantId);
        if (command.Position.Status != BountyStatus.Open)
            throw new BountyTerminalConflictException("Only an open bounty escrow can be persisted.");
        if (command.Position.Id.Value == Guid.Empty)
            throw new ArgumentException("Bounty ID is required.", nameof(command));

        var payload = JsonSerializer.Serialize(BuildFragmentPayload(command.Position));
        try
        {
            _db.Database.ExecuteSqlInterpolated($"""
                SELECT economy_private.create_bounty_escrow_v4(
                    {command.TenantId},
                    {command.Position.Id.Value},
                    {command.Position.PosterId},
                    {command.Position.PosterWalletId.Value},
                    {command.Position.EscrowWalletId.Value},
                    {(int)command.Position.Amount.Currency},
                    {command.Position.Amount.Units},
                    {command.Position.ReclaimFeePpm},
                    {command.Position.Eligibility.RequiresPrerequisite},
                    {command.Position.Eligibility.MinimumReputation},
                    {command.Position.Eligibility.RequiresInstructorVerification},
                    {command.IdempotencyKey.Value},
                    {command.RequestHash.Trim()},
                    {command.Position.PostedAt},
                    {command.Position.ExpiresAt},
                    {payload}::jsonb,
                    {command.PostingId.Value});
                """);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new BountyIdempotencyConflictException(
                "The persistent bounty escrow writer rejected the request.");
        }

        return Get(command.TenantId, command.Position.Id);
    }

    private IQueryable<BountyRow> ReadBounties(FormattableString sql) =>
        _db.Set<BountyRow>().FromSqlInterpolated(sql).AsNoTracking();

    private PersistedBountyEscrow ToContract(BountyRow row)
    {
        if (string.IsNullOrWhiteSpace(row.RequestHash))
            throw new InvalidOperationException("A persisted bounty escrow is missing its immutable request hash.");

        var fragments = ReadFragments(row.TenantId, row.Id)
            .AsEnumerable()
            .Select(fragment => new PersistedBountyEscrowFragment(
            new CreditLotId(fragment.ParentLotId),
                fragment.EscrowLotId is { } escrowLotId ? new CreditLotId(escrowLotId) : null,
                new CoinAmount(fragment.Currency, fragment.AmountUnits),
                fragment.Provenance,
                fragment.TraceUnitsPerCoinUnit,
                DeserializeRanges(fragment.SelectedRootRanges)))
            .ToArray();

        return new PersistedBountyEscrow(
            new BountyId(row.Id),
            row.TenantId,
            row.PosterId,
            new WalletId(row.PosterWalletId),
            new WalletId(row.EscrowWalletId),
            new CoinAmount(row.Currency, row.AmountUnits),
            new BountyEligibilityRequirements(
                row.RequiresPrerequisite,
                row.MinimumReputation,
                row.RequiresInstructorVerification),
            row.ReclaimFeePpm,
            row.Status,
            new IdempotencyKey(row.IdempotencyKey),
            row.RequestHash,
            row.PostedAt,
            row.ExpiresAt,
            row.Version,
            fragments);
    }

    private IQueryable<BountyEscrowFragmentProjection> ReadFragments(Guid tenantId, Guid bountyId) =>
        _db.Database.SqlQuery<BountyEscrowFragmentProjection>($"""
            SELECT "ParentLotId", "EscrowLotId", "Currency", "Provenance", "AmountUnits", "TraceUnitsPerCoinUnit", "SelectedRootRanges"
            FROM economy_private.read_bounty_escrow_fragments_v4({tenantId}, {bountyId})
            """);

    private static IReadOnlyList<RootTraceRange> DeserializeRanges(string payload)
    {
        var rows = JsonSerializer.Deserialize<RootRangePayload[]>(payload)
            ?? throw new InvalidOperationException("Bounty escrow fragment ranges are missing.");

        return rows.Select(range => new RootTraceRange(
            new SourceStampId(range.RootSourceStampId),
            range.StartInclusive,
            checked(range.EndExclusive - range.StartInclusive),
            range.ReversalEpoch)).ToArray();
    }

    private static FragmentPayload[] BuildFragmentPayload(BountyEscrowPosition position) =>
        position.EscrowFragments.Select(fragment => new FragmentPayload(
            fragment.ParentLot.Id.Value,
            (int)fragment.Amount.Currency,
            (int)fragment.ParentLot.Provenance,
            fragment.Amount.Units,
            fragment.ParentLot.TraceUnitsPerCoinUnit,
            fragment.SelectedRanges.Select(range => new RootRangePayload(
                range.Root.Value,
                range.Start,
                range.EndExclusive,
                range.Epoch)).ToArray())).ToArray();

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is System.Data.Common.DbException;

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A non-quarantine tenant ID is required.", nameof(tenantId));
    }

    private sealed record FragmentPayload(
        Guid ParentLotId,
        int Currency,
        int Provenance,
        long AmountUnits,
        long TraceUnitsPerCoinUnit,
        RootRangePayload[] SelectedRootRanges);

    private sealed record RootRangePayload(
        Guid RootSourceStampId,
        long StartInclusive,
        long EndExclusive,
        long ReversalEpoch);

    private sealed class BountyEscrowFragmentProjection
    {
        public Guid ParentLotId { get; init; }
        public Guid? EscrowLotId { get; init; }
        public CurrencyCode Currency { get; init; }
        public ProvenanceKind Provenance { get; init; }
        public long AmountUnits { get; init; }
        public long TraceUnitsPerCoinUnit { get; init; }
        public string SelectedRootRanges { get; init; } = "[]";
    }
}

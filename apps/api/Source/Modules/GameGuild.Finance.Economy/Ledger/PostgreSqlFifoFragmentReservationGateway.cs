using System.Data.Common;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public enum PersistedFragmentReservationPurpose
{
    Payout = 1,
    AdminWithdrawal = 2,
    HardToSoftConversion = 3,
    Spend = 4,
    ProviderReversal = 5,
    BountyEscrow = 6,
    MarketplaceSettlement = 7
}

public enum PersistedFragmentReservationStatus
{
    Reserved = 1,
    Released = 2,
    Consumed = 3,
    Dispatching = 4
}

public sealed record FifoFragmentReservationRequest(
    Guid OperationId,
    WalletId WalletId,
    CurrencyCode Currency,
    ProvenanceKind Provenance,
    CoinAmount Amount,
    PersistedFragmentReservationPurpose Purpose,
    DateTimeOffset ReservedAt);

public sealed record PersistedFragmentReservation(
    Guid Id,
    Guid OperationId,
    CreditLotId ParentLotId,
    SourceStampId RootSourceStampId,
    long ReversalEpoch,
    RootTraceRange Range,
    CoinAmount Amount);

public interface IFifoFragmentReservationGateway
{
    IReadOnlyList<PersistedFragmentReservation> Reserve(FifoFragmentReservationRequest request);

    long Transition(
        Guid operationId,
        PersistedFragmentReservationStatus expected,
        PersistedFragmentReservationStatus next,
        DateTimeOffset terminalAt);
}

public interface IFifoFragmentReservationReader
{
    IReadOnlyList<PersistedFragmentReservation> Read(
        Guid operationId,
        PersistedFragmentReservationStatus status);
}

public sealed class PostgreSqlFifoFragmentReservationGateway :
    IFifoFragmentReservationGateway,
    IFifoFragmentReservationReader
{
    private readonly DbContext _db;

    public PostgreSqlFifoFragmentReservationGateway(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent FIFO reservations require the application's relational DbContext.");
    }

    public IReadOnlyList<PersistedFragmentReservation> Reserve(FifoFragmentReservationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(request));
        if (!Enum.IsDefined(request.Purpose)) throw new ArgumentOutOfRangeException(nameof(request));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Amount.Units);
        if (request.Amount.Currency != request.Currency)
            throw new ArgumentException("Reservation currency must match its amount.", nameof(request));

        try
        {
            return ReservationReceipts(request)
                .AsNoTracking()
                .AsEnumerable()
                .Select(row => new PersistedFragmentReservation(
                    row.ReservationId,
                    request.OperationId,
                    new CreditLotId(row.ParentLotId),
                    new SourceStampId(row.RootSourceStampId),
                    row.ReversalEpoch,
                    new RootTraceRange(
                        new SourceStampId(row.RootSourceStampId),
                        row.StartInclusive,
                        checked(row.EndExclusive - row.StartInclusive),
                        row.ReversalEpoch),
                    new CoinAmount(request.Currency, row.AmountUnits)))
                .ToArray();
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The persistent Economy FIFO reservation writer rejected the request.", exception);
        }
    }

    private IQueryable<FifoFragmentReservationReceiptRow> ReservationReceipts(
        FifoFragmentReservationRequest request) =>
        request.Purpose == PersistedFragmentReservationPurpose.BountyEscrow
            ? _db.Set<FifoFragmentReservationReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.reserve_bounty_fifo_fragments_v1(
                        {request.OperationId},
                        {request.WalletId.Value},
                        {(int)request.Currency},
                        {(int)request.Provenance},
                        {request.Amount.Units},
                        {(int)request.Purpose},
                        {request.ReservedAt})
                    """)
            : _db.Set<FifoFragmentReservationReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.reserve_fifo_fragments_v1(
                        {request.OperationId},
                        {request.WalletId.Value},
                        {(int)request.Currency},
                        {(int)request.Provenance},
                        {request.Amount.Units},
                        {(int)request.Purpose},
                        {request.ReservedAt})
                    """);

    public long Transition(
        Guid operationId,
        PersistedFragmentReservationStatus expected,
        PersistedFragmentReservationStatus next,
        DateTimeOffset terminalAt)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
        if (!Enum.IsDefined(expected) || !Enum.IsDefined(next)) throw new ArgumentOutOfRangeException(nameof(expected));

        try
        {
            return _db.Database.SqlQuery<long>($"""
                    SELECT economy_private.transition_fifo_fragment_reservations_v1(
                        {operationId}, {(int)expected}, {(int)next}, {terminalAt}) AS "Value"
                    """)
                .Single();
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The persistent Economy FIFO reservation transition was rejected.", exception);
        }
    }

    public IReadOnlyList<PersistedFragmentReservation> Read(
        Guid operationId,
        PersistedFragmentReservationStatus status)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));

        return _db.Database.SqlQuery<FifoFragmentReservationStateRow>($"""
                SELECT reservation."Id", reservation."OperationId", reservation."ParentLotId",
                       reservation."RootSourceStampId", reservation."ReversalEpoch",
                       reservation."StartInclusive", reservation."EndExclusive", reservation."Currency",
                       CASE WHEN reservation."Currency" = 1
                            THEN (reservation."EndExclusive" - reservation."StartInclusive") / 1000
                            ELSE (reservation."EndExclusive" - reservation."StartInclusive")
                       END AS "AmountUnits"
                FROM public.economy_fragment_reservations AS reservation
                JOIN public.economy_credit_lots AS lot ON lot."Id" = reservation."ParentLotId"
                WHERE reservation."OperationId" = {operationId} AND reservation."Status" = {(int)status}
                ORDER BY lot."ConfirmedAt", lot."JournalSequence", lot."Id",
                         reservation."StartInclusive", reservation."Id"
                """)
            .AsNoTracking()
            .AsEnumerable()
            .Select(row => new PersistedFragmentReservation(
                row.Id,
                row.OperationId,
                new CreditLotId(row.ParentLotId),
                new SourceStampId(row.RootSourceStampId),
                row.ReversalEpoch,
                new RootTraceRange(
                    new SourceStampId(row.RootSourceStampId),
                    row.StartInclusive,
                    checked(row.EndExclusive - row.StartInclusive),
                    row.ReversalEpoch),
                new CoinAmount(row.Currency, row.AmountUnits)))
            .ToArray();
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is DbException;
}

internal sealed class FifoFragmentReservationStateRow
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public Guid ParentLotId { get; set; }
    public Guid RootSourceStampId { get; set; }
    public long ReversalEpoch { get; set; }
    public long StartInclusive { get; set; }
    public long EndExclusive { get; set; }
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
}

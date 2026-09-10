using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public enum FragmentReservationPurpose
{
    Payout = 1,
    AdminWithdrawal = 2
}

public enum FragmentReservationStatus
{
    Reserved = 1,
    Dispatching = 2,
    Consumed = 3,
    Released = 4
}

public sealed class ValueFragmentReservation
{
    private readonly IReadOnlyList<RootTraceRange> _ranges;

    public ValueFragmentReservation(
        Guid id,
        Guid operationId,
        FragmentReservationPurpose purpose,
        CreditLotId lotId,
        WalletId walletId,
        CoinAmount amount,
        IReadOnlyCollection<RootTraceRange> ranges,
        long operationVersion,
        long fencingToken,
        long killSwitchEpoch,
        FragmentReservationStatus status,
        DateTimeOffset reservedAt,
        DateTimeOffset? terminalAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Reservation ID is required.", nameof(id));
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        ArgumentNullException.ThrowIfNull(ranges);
        if (ranges.Count == 0) throw new ArgumentException("A reservation requires exact root ranges.", nameof(ranges));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(operationVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fencingToken);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(killSwitchEpoch);
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        var terminal = status is FragmentReservationStatus.Consumed or FragmentReservationStatus.Released;
        if (terminal != terminalAt.HasValue)
            throw new ArgumentException("Only terminal reservations require a terminal timestamp.", nameof(terminalAt));
        if (terminalAt < reservedAt)
            throw new ArgumentException("A reservation cannot terminate before it was created.", nameof(terminalAt));

        var traceUnits = ranges.Aggregate(0L, static (total, range) => checked(total + range.Length));
        var expected = checked(amount.Units * CurrencyTraceScale.For(amount.Currency));
        if (traceUnits != expected)
            throw new LineageConservationException("Reservation ranges must exactly conserve the reserved amount.");

        Id = id;
        OperationId = operationId;
        Purpose = purpose;
        LotId = lotId;
        WalletId = walletId;
        Amount = amount;
        _ranges = Array.AsReadOnly(ranges.ToArray());
        OperationVersion = operationVersion;
        FencingToken = fencingToken;
        KillSwitchEpoch = killSwitchEpoch;
        Status = status;
        ReservedAt = reservedAt;
        TerminalAt = terminalAt;
    }

    public Guid Id { get; }
    public Guid OperationId { get; }
    public FragmentReservationPurpose Purpose { get; }
    public CreditLotId LotId { get; }
    public WalletId WalletId { get; }
    public CoinAmount Amount { get; }
    public IReadOnlyList<RootTraceRange> Ranges => _ranges;
    public long OperationVersion { get; }
    public long FencingToken { get; }
    public long KillSwitchEpoch { get; }
    public FragmentReservationStatus Status { get; }
    public DateTimeOffset ReservedAt { get; }
    public DateTimeOffset? TerminalAt { get; }

    public ValueFragmentReservation Transition(FragmentReservationStatus next, DateTimeOffset occurredAt)
    {
        var allowed = Status switch
        {
            FragmentReservationStatus.Reserved => next is FragmentReservationStatus.Dispatching or FragmentReservationStatus.Released,
            FragmentReservationStatus.Dispatching => next is FragmentReservationStatus.Consumed or FragmentReservationStatus.Released,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"Reservation cannot transition from {Status} to {next}.");
        return new ValueFragmentReservation(
            Id, OperationId, Purpose, LotId, WalletId, Amount, Ranges,
            checked(OperationVersion + 1), FencingToken, KillSwitchEpoch, next, ReservedAt,
            next is FragmentReservationStatus.Consumed or FragmentReservationStatus.Released ? occurredAt : null);
    }
}

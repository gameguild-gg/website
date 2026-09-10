using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Money;

namespace GameGuild.Finance.Economy.Funding;

public enum ProviderDisputeStatus
{
    Open = 1,
    Won = 2,
    Lost = 3
}

public sealed record ProviderDisputeNotification(
    string ProviderEventId,
    string ProviderDisputeReference,
    SourceStampId SourceId,
    long ProviderSequence,
    long CumulativeDisputedHardUnits,
    ProviderDisputeStatus Status,
    ProviderReversalDisposition IrrecoverableDisposition,
    string Evidence,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    DateTimeOffset OccurredAt);

public sealed record ProviderDisputeEventRecord(
    string ProviderEventId,
    string ProviderDisputeReference,
    SourceStampId SourceId,
    long ProviderSequence,
    ProviderDisputeStatus Status,
    long CumulativeDisputedHardUnits,
    string RequestHash,
    DateTimeOffset OccurredAt);

public sealed record DisputeFragmentFreeze
{
    public DisputeFragmentFreeze(
        Guid id,
        string providerDisputeReference,
        SourceStampId rootSourceId,
        CreditLotId lotId,
        WalletId walletId,
        CoinAmount amount,
        IReadOnlyList<RootTraceRange> ranges,
        HoldStatus status,
        DateTimeOffset placedAt,
        DateTimeOffset? terminalAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Freeze ID is required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(providerDisputeReference);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(ranges);
        if (ranges.Count == 0 || ranges.Any(range => range.Root != rootSourceId))
            throw new ArgumentException("A dispute freeze requires ranges from its root source.", nameof(ranges));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if ((status == HoldStatus.Active) != (terminalAt is null))
            throw new ArgumentException("Only active dispute freezes may omit a terminal timestamp.", nameof(terminalAt));
        if (amount.Currency == CurrencyCode.SoftCoin && amount.Units % FixedParity.SoftCoinsPerHardCoin != 0)
            throw new LineageConservationException(
                "A provider dispute freeze must resolve to whole HardCoin-equivalent units.");

        var traceUnits = ranges.Aggregate(0L, static (total, range) => checked(total + range.Length));
        var expectedTraceUnits = checked(amount.Units * (amount.Currency == CurrencyCode.HardCoin
            ? CurrencyTraceScale.HardCoinTraceUnitsPerCoin
            : CurrencyTraceScale.SoftCoinTraceUnitsPerCoin));
        if (traceUnits != expectedTraceUnits)
            throw new LineageConservationException("Dispute freeze amount must equal its exact root ranges.");

        Id = id;
        ProviderDisputeReference = providerDisputeReference.Trim();
        RootSourceId = rootSourceId;
        LotId = lotId;
        WalletId = walletId;
        Amount = amount;
        Ranges = Array.AsReadOnly(ranges.ToArray());
        Status = status;
        PlacedAt = placedAt;
        TerminalAt = terminalAt;
    }

    public Guid Id { get; }
    public string ProviderDisputeReference { get; }
    public SourceStampId RootSourceId { get; }
    public CreditLotId LotId { get; }
    public WalletId WalletId { get; }
    public CoinAmount Amount { get; }
    public IReadOnlyList<RootTraceRange> Ranges { get; }
    public HoldStatus Status { get; }
    public DateTimeOffset PlacedAt { get; }
    public DateTimeOffset? TerminalAt { get; }
    public long HardEquivalentUnits => Amount.Currency == CurrencyCode.HardCoin
        ? Amount.Units
        : Amount.Units / FixedParity.SoftCoinsPerHardCoin;

    public DisputeFragmentFreeze Transition(HoldStatus status, DateTimeOffset occurredAt)
    {
        if (Status != HoldStatus.Active)
            throw new InvalidOperationException("Only an active dispute freeze can enter a terminal state.");
        if (status is not (HoldStatus.Released or HoldStatus.Consumed))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (occurredAt < PlacedAt)
            throw new ArgumentException("A dispute freeze transition cannot precede placement.", nameof(occurredAt));
        return new DisputeFragmentFreeze(
            Id, ProviderDisputeReference, RootSourceId, LotId, WalletId, Amount, Ranges,
            status, PlacedAt, occurredAt);
    }
}

public sealed record ProviderDisputeCase(
    string ProviderDisputeReference,
    SourceStampId SourceId,
    WalletId ResponsibleWalletId,
    ProviderDisputeStatus Status,
    long LatestProviderSequence,
    long CumulativeDisputedHardUnits,
    long BaselineReversedHardUnits,
    long FrozenHardEquivalentUnits,
    IReadOnlyList<Guid> FreezeIds,
    ProviderReversalResult? Reversal,
    DateTimeOffset UpdatedAt);

public sealed record WalletDebtPosition(WalletId WalletId, long OutstandingHardUnits, DateTimeOffset UpdatedAt);

public sealed record WalletDebtEvent(
    long Sequence,
    WalletId WalletId,
    SourceStampId SourceId,
    long DeltaHardUnits,
    long OutstandingHardUnits,
    DateTimeOffset OccurredAt);

public sealed class ProviderDisputeWorkflow
{
    private readonly InMemoryLedgerKernelStore _store;
    private readonly TransactionalPostingService _posting;
    private readonly RootReversalFenceRegistry _fences;

    public ProviderDisputeWorkflow(
        InMemoryLedgerKernelStore store,
        TransactionalPostingService posting,
        RootReversalFenceRegistry fences)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _posting = posting ?? throw new ArgumentNullException(nameof(posting));
        _fences = fences ?? throw new ArgumentNullException(nameof(fences));
    }

    public ProviderDisputeCase Handle(ProviderDisputeNotification notification)
    {
        Validate(notification);
        notification = notification with
        {
            ProviderEventId = notification.ProviderEventId.Trim(),
            ProviderDisputeReference = notification.ProviderDisputeReference.Trim(),
            Evidence = notification.Evidence.Trim()
        };
        var requestHash = Hash(notification);
        var duplicate = _store.FindProviderDisputeEvent(notification.ProviderEventId);
        if (duplicate is not null)
        {
            if (!StringComparer.Ordinal.Equals(duplicate.RequestHash, requestHash))
                throw new ProviderDisputeEventConflictException(notification.ProviderEventId);
            return _store.GetProviderDisputeCase(notification.ProviderDisputeReference);
        }

        return notification.Status switch
        {
            ProviderDisputeStatus.Open => HandleOpen(notification, requestHash),
            ProviderDisputeStatus.Won => HandleWon(notification, requestHash),
            ProviderDisputeStatus.Lost => HandleLost(notification, requestHash),
            _ => throw new ArgumentOutOfRangeException(nameof(notification))
        };
    }

    private ProviderDisputeCase HandleOpen(ProviderDisputeNotification notification, string requestHash) =>
        WithRootFence(notification.SourceId, () => _store.Execute(transaction =>
        {
            var current = transaction.FindProviderDisputeCase(notification.ProviderDisputeReference);
            EnsureIdentityAndOrder(current, notification);
            if (current?.Status is ProviderDisputeStatus.Won or ProviderDisputeStatus.Lost)
                throw new ProviderDisputeTerminalStateException(current.Status, ProviderDisputeStatus.Open);
            if (current is not null &&
                notification.CumulativeDisputedHardUnits < current.CumulativeDisputedHardUnits)
                throw new ProviderMonetaryTotalExceededException("Cumulative provider dispute cannot regress.");

            var claim = transaction.CurrentFundingClaim(notification.SourceId);
            if (claim.State is not (SourceConfirmationState.Confirmed or SourceConfirmationState.Disputed))
                throw new InvalidFundingStateTransitionException(claim.State, SourceConfirmationState.Disputed);
            if (notification.CumulativeDisputedHardUnits > claim.Amount.Units)
                throw new ProviderMonetaryTotalExceededException(
                    "Cumulative provider dispute cannot exceed confirmed HardCoin units.");

            var reversal = transaction.CurrentProviderReversalState(notification.SourceId);
            var baseline = current?.BaselineReversedHardUnits ?? reversal?.CumulativeProviderHardUnits ?? 0;
            if (notification.CumulativeDisputedHardUnits < baseline)
                throw new ProviderMonetaryTotalExceededException(
                    "Cumulative provider dispute cannot precede already committed reversals.");

            if (claim.State == SourceConfirmationState.Confirmed)
            {
                transaction.UpdateFundingClaim(claim.Transition(
                    SourceConfirmationState.Disputed, notification.Evidence, notification.OccurredAt));
                var confirmed = transaction.LatestSource(notification.SourceId)
                    ?? throw new InvalidOperationException("Confirmed source evidence was not found.");
                transaction.AddSource(confirmed.Dispute(notification.OccurredAt));
            }

            var activeFreezes = current is null
                ? []
                : transaction.GetDisputeFreezes(current.FreezeIds)
                    .Where(item => item.Status == HoldStatus.Active)
                    .ToArray();
            var history = (reversal?.ReversedRanges ?? [])
                .Concat(activeFreezes.SelectMany(item => item.Ranges))
                .OrderBy(range => range.Start)
                .ToArray();
            var plan = ProviderReversalPlanner.Plan(
                notification.SourceId,
                checked(notification.CumulativeDisputedHardUnits * CurrencyTraceScale.HardCoinTraceUnitsPerCoin),
                history,
                transaction.GetAvailableRootLots(notification.SourceId));

            var freezeIds = current?.FreezeIds.ToList() ?? [];
            for (var index = 0; index < plan.Fragments.Count; index++)
            {
                var fragment = plan.Fragments[index];
                var freeze = new DisputeFragmentFreeze(
                    DeterministicGuid($"freeze:{notification.ProviderDisputeReference}:{notification.ProviderSequence}:{index}"),
                    notification.ProviderDisputeReference,
                    notification.SourceId,
                    fragment.Lot.Id,
                    fragment.Lot.WalletId,
                    fragment.Amount,
                    fragment.Ranges,
                    HoldStatus.Active,
                    notification.OccurredAt,
                    null);
                transaction.AddDisputeFreeze(freeze);
                freezeIds.Add(freeze.Id);
            }

            var frozenHard = transaction.GetDisputeFreezes(freezeIds)
                .Where(item => item.Status == HoldStatus.Active)
                .Aggregate(0L, static (total, item) => checked(total + item.HardEquivalentUnits));
            var next = new ProviderDisputeCase(
                notification.ProviderDisputeReference,
                notification.SourceId,
                claim.WalletId,
                ProviderDisputeStatus.Open,
                notification.ProviderSequence,
                notification.CumulativeDisputedHardUnits,
                baseline,
                frozenHard,
                freezeIds,
                null,
                notification.OccurredAt);
            transaction.SetProviderDisputeCase(next);
            transaction.AddProviderDisputeEvent(Event(notification, requestHash));
            return next;
        }));

    private ProviderDisputeCase HandleWon(ProviderDisputeNotification notification, string requestHash) =>
        WithRootFence(notification.SourceId, () => _store.Execute(transaction =>
        {
            var current = RequiredOpenCase(transaction, notification);
            var reversal = transaction.CurrentProviderReversalState(notification.SourceId);
            if ((reversal?.CumulativeProviderHardUnits ?? 0) != current.BaselineReversedHardUnits)
                throw new InvalidOperationException("A dispute with committed reversal value cannot be marked won.");

            transaction.TransitionDisputeFreezes(current.FreezeIds, HoldStatus.Released, notification.OccurredAt);
            if (current.BaselineReversedHardUnits == 0)
            {
                var claim = transaction.CurrentFundingClaim(notification.SourceId);
                transaction.UpdateFundingClaim(claim.Transition(
                    SourceConfirmationState.Confirmed, notification.Evidence, notification.OccurredAt));
                var disputed = transaction.LatestSource(notification.SourceId)
                    ?? throw new InvalidOperationException("Disputed source evidence was not found.");
                transaction.AddSource(disputed.ResolveDispute(notification.OccurredAt));
            }

            var next = current with
            {
                Status = ProviderDisputeStatus.Won,
                LatestProviderSequence = notification.ProviderSequence,
                FrozenHardEquivalentUnits = 0,
                UpdatedAt = notification.OccurredAt
            };
            transaction.SetProviderDisputeCase(next);
            transaction.AddProviderDisputeEvent(Event(notification, requestHash));
            return next;
        }));

    private ProviderDisputeCase HandleLost(ProviderDisputeNotification notification, string requestHash) =>
        WithRootFence(notification.SourceId, () =>
        {
            return _posting.ReverseTopUpUnderActiveFence(
                Reversal(notification),
                beforePosting: transaction =>
                {
                    var current = RequiredOpenCase(transaction, notification);
                    transaction.TransitionDisputeFreezes(
                        current.FreezeIds, HoldStatus.Consumed, notification.OccurredAt);
                },
                afterPosting: (transaction, reversal) =>
                {
                    var current = transaction.FindProviderDisputeCase(notification.ProviderDisputeReference)!;
                    var resolved = current with
                    {
                        Status = ProviderDisputeStatus.Lost,
                        LatestProviderSequence = notification.ProviderSequence,
                        FrozenHardEquivalentUnits = 0,
                        Reversal = reversal,
                        UpdatedAt = notification.OccurredAt
                    };
                    transaction.SetProviderDisputeCase(resolved);
                    transaction.AddProviderDisputeEvent(Event(notification, requestHash));
                    return resolved;
                });
        });

    private ProviderDisputeCase RequiredOpenCase(
        LedgerKernelTransaction transaction,
        ProviderDisputeNotification notification)
    {
        var current = transaction.FindProviderDisputeCase(notification.ProviderDisputeReference)
            ?? throw new KeyNotFoundException(
                $"Provider dispute '{notification.ProviderDisputeReference}' was not found.");
        EnsureIdentityAndOrder(current, notification);
        if (current.Status != ProviderDisputeStatus.Open)
            throw new ProviderDisputeTerminalStateException(current.Status, notification.Status);
        if (notification.CumulativeDisputedHardUnits != current.CumulativeDisputedHardUnits)
            throw new ProviderMonetaryTotalExceededException(
                "A terminal dispute event must preserve the open cumulative disputed amount.");
        return current;
    }

    private static void EnsureIdentityAndOrder(
        ProviderDisputeCase? current,
        ProviderDisputeNotification notification)
    {
        if (current is null) return;
        if (current.SourceId != notification.SourceId)
            throw new ProviderDisputeEventConflictException(notification.ProviderEventId);
        if (notification.ProviderSequence <= current.LatestProviderSequence)
            throw new StaleProviderDisputeEventException(
                notification.ProviderSequence, current.LatestProviderSequence);
    }

    private T WithRootFence<T>(SourceStampId root, Func<T> operation)
    {
        var epoch = _fences.BeginReversal(root);
        try
        {
            return operation();
        }
        finally
        {
            _fences.CompleteReversal(root, epoch);
        }
    }

    private static void Validate(ProviderDisputeNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.ProviderEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.ProviderDisputeReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.Evidence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(notification.ProviderSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(notification.CumulativeDisputedHardUnits);
        if (!Enum.IsDefined(notification.IrrecoverableDisposition))
            throw new ArgumentOutOfRangeException(nameof(notification));
    }

    private static ProviderDisputeEventRecord Event(
        ProviderDisputeNotification notification,
        string requestHash) => new(
        notification.ProviderEventId.Trim(),
        notification.ProviderDisputeReference.Trim(),
        notification.SourceId,
        notification.ProviderSequence,
        notification.Status,
        notification.CumulativeDisputedHardUnits,
        requestHash,
        notification.OccurredAt);

    private static ReverseTopUpCommand Reversal(ProviderDisputeNotification notification) => new(
        new PostingId(DeterministicGuid($"posting:{notification.ProviderDisputeReference}:{notification.ProviderSequence}")),
        new IdempotencyKey(
            $"economy:dispute:{notification.ProviderDisputeReference}:lost:{notification.CumulativeDisputedHardUnits}"),
        notification.SourceId,
        notification.CumulativeDisputedHardUnits,
        notification.IrrecoverableDisposition,
        notification.Evidence,
        notification.ReserveVersion,
        notification.PolicyVersion,
        notification.OccurredAt);

    private static string Hash(ProviderDisputeNotification notification)
    {
        var canonical = string.Join('|',
            notification.ProviderEventId.Trim(),
            notification.ProviderDisputeReference.Trim(),
            notification.SourceId.Value.ToString("N"),
            notification.ProviderSequence.ToString(CultureInfo.InvariantCulture),
            notification.CumulativeDisputedHardUnits.ToString(CultureInfo.InvariantCulture),
            ((int)notification.Status).ToString(CultureInfo.InvariantCulture),
            ((int)notification.IrrecoverableDisposition).ToString(CultureInfo.InvariantCulture),
            notification.Evidence.Trim(),
            notification.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
            notification.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
            notification.OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static Guid DeterministicGuid(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
}

public sealed class ProviderDisputeEventConflictException(string providerEventId)
    : InvalidOperationException($"Provider dispute event '{providerEventId}' conflicts with its prior replay.");

public sealed class StaleProviderDisputeEventException(long receivedSequence, long currentSequence)
    : InvalidOperationException(
        $"Provider dispute sequence {receivedSequence} is stale; current sequence is {currentSequence}.");

public sealed class ProviderDisputeTerminalStateException(
    ProviderDisputeStatus current,
    ProviderDisputeStatus requested)
    : InvalidOperationException($"Provider dispute already reached {current} and cannot transition to {requested}.");

public sealed class WalletDebtRestrictionException(WalletId walletId, long outstandingHardUnits)
    : InvalidOperationException(
        $"Wallet {walletId.Value:N} is restricted by {outstandingHardUnits} outstanding HardCoin debt units.");

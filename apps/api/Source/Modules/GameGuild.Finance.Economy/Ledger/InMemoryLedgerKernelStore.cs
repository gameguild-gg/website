using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Policy;

namespace GameGuild.Finance.Economy.Ledger;

public sealed class InMemoryLedgerKernelStore
{
    private readonly object _gate = new();
    private LedgerKernelState _state = new();

    public IReadOnlyList<SourceEvidence> SourceEvidenceHistory => Read(state => state.Sources.ToArray());
    public IReadOnlyList<HardCoinFundingClaim> FundingClaims => Read(state => state.FundingClaims.Values.ToArray());
    public IReadOnlyList<HardCoinFundingClaim> PendingFundingClaims => Read(state => state.FundingClaims.Values
        .Where(claim => claim.IsPending)
        .ToArray());
    public IReadOnlyList<ProviderReversalState> ProviderReversalStates => Read(state =>
        state.ProviderReversalStates.Values.ToArray());
    public IReadOnlyList<ProviderDisputeCase> ProviderDisputes => Read(state =>
        state.ProviderDisputes.Values.ToArray());
    public IReadOnlyList<ProviderDisputeEventRecord> ProviderDisputeEvents => Read(state =>
        state.ProviderDisputeEvents.Values.ToArray());
    public IReadOnlyList<DisputeFragmentFreeze> DisputeFragmentFreezes => Read(state =>
        state.DisputeFragmentFreezes.Values.ToArray());
    public IReadOnlyList<WalletDebtEvent> DebtEvents => Read(state => state.DebtEvents.ToArray());
    public IReadOnlyList<ValueFragmentReservation> FragmentReservations => Read(state =>
        state.FragmentReservations.Values.ToArray());
    public IReadOnlyList<ValueFragmentReservation> GetFragmentReservations(Guid operationId) => Read(state =>
        state.FragmentReservations.Values.Where(item => item.OperationId == operationId)
            .OrderBy(item => item.Id).ToArray());
    public ProviderDisputeEventRecord? FindProviderDisputeEvent(string providerEventId) => Read(state =>
        state.ProviderDisputeEvents.GetValueOrDefault(providerEventId));
    public ProviderDisputeCase GetProviderDisputeCase(string providerDisputeReference) => Read(state =>
        state.ProviderDisputes.TryGetValue(providerDisputeReference, out var dispute)
            ? dispute
            : throw new KeyNotFoundException($"Provider dispute '{providerDisputeReference}' was not found."));
    public WalletDebtPosition GetDebt(WalletId walletId) => Read(state =>
        state.DebtPositions.GetValueOrDefault(walletId) ?? new WalletDebtPosition(walletId, 0, DateTimeOffset.MinValue));
    public IReadOnlyList<JournalEntry> JournalEntries => Read(state => state.JournalEntries.ToArray());
    public IReadOnlyList<CreditLot> CreditLots => Read(state => state.CreditLots.ToArray());
    public IReadOnlyList<FragmentConsumption> FragmentConsumptions => Read(state => state.Consumptions.ToArray());
    public IReadOnlyList<DerivedCreditLot> Lineages => Read(state => state.Lineages.ToArray());
    public IReadOnlyList<WalletProjectionUpdate> ProjectionUpdates => Read(state => state.ProjectionUpdates.ToArray());
    public IReadOnlyList<IdempotencyRecord> IdempotencyRecords => Read(state => state.Idempotency.Values.ToArray());
    public IReadOnlyList<ImmutableOutboxMessage> OutboxMessages => Read(state => state.Outbox.ToArray());
    public IReadOnlyList<ChainAnchor> ChainAnchors => Read(state => state.Anchors.ToArray());
    public IReadOnlyList<HoldEvent> HoldEvents => Read(state => state.HoldEvents.ToArray());
    public IReadOnlyList<HoldContract> Holds => Read(state => state.Holds.Values.ToArray());
    public HoldContract GetHold(HoldId id) => Read(state =>
        state.Holds.TryGetValue(id, out var hold)
            ? hold
            : throw new KeyNotFoundException($"Hold {id.Value:N} was not found."));
    public IReadOnlyList<HoldContract> GetActiveHolds(WalletId walletId) => Read(state =>
        state.Holds.Values
            .Where(hold => hold.WalletId == walletId && hold.Status == HoldStatus.Active)
            .OrderBy(hold => hold.EffectiveAt)
            .ThenBy(hold => hold.Id.Value)
            .ToArray());

    public T Execute<T>(Func<LedgerKernelTransaction, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            var staged = _state.Clone();
            var result = operation(new LedgerKernelTransaction(staged));
            _state = staged;
            return result;
        }
    }

    public IReadOnlyList<CreditLot> GetAvailableLots(WalletId walletId, CurrencyCode currency) =>
        Read(state => LedgerKernelTransaction.GetAvailableLots(state, walletId, currency));

    public LedgerKernelCounts SnapshotCounts() => Read(state => new LedgerKernelCounts(
        state.Sources.Count,
        state.JournalEntries.Count,
        state.CreditLots.Count,
        state.Consumptions.Count,
        state.Lineages.Count,
        state.ProjectionUpdates.Count,
        state.Idempotency.Count,
        state.Outbox.Count));

    private T Read<T>(Func<LedgerKernelState, T> read)
    {
        lock (_gate) return read(_state);
    }
}

internal sealed class LedgerKernelState
{
    internal List<SourceEvidence> Sources { get; } = [];
    internal Dictionary<SourceStampId, HardCoinFundingClaim> FundingClaims { get; } = [];
    internal Dictionary<string, SourceStampId> ProviderMonetaryLegs { get; } = new(StringComparer.Ordinal);
    internal Dictionary<SourceStampId, ProviderReversalState> ProviderReversalStates { get; } = [];
    internal Dictionary<string, ProviderReversalResult> ProviderReversalResults { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, ProviderDisputeCase> ProviderDisputes { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, ProviderDisputeEventRecord> ProviderDisputeEvents { get; } = new(StringComparer.Ordinal);
    internal Dictionary<Guid, DisputeFragmentFreeze> DisputeFragmentFreezes { get; } = [];
    internal Dictionary<WalletId, WalletDebtPosition> DebtPositions { get; } = [];
    internal List<WalletDebtEvent> DebtEvents { get; } = [];
    internal Dictionary<Guid, ValueFragmentReservation> FragmentReservations { get; } = [];
    internal List<JournalEntry> JournalEntries { get; set; } = [];
    internal List<CreditLot> CreditLots { get; } = [];
    internal List<FragmentConsumption> Consumptions { get; } = [];
    internal List<DerivedCreditLot> Lineages { get; } = [];
    internal List<WalletProjectionUpdate> ProjectionUpdates { get; } = [];
    internal Dictionary<string, IdempotencyRecord> Idempotency { get; } = new(StringComparer.Ordinal);
    internal List<ImmutableOutboxMessage> Outbox { get; } = [];
    internal List<ChainAnchor> Anchors { get; } = [];
    internal List<HoldEvent> HoldEvents { get; } = [];
    internal Dictionary<HoldId, HoldContract> Holds { get; } = [];

    internal LedgerKernelState Clone()
    {
        var clone = new LedgerKernelState { JournalEntries = [.. JournalEntries] };
        clone.Sources.AddRange(Sources);
        foreach (var pair in FundingClaims) clone.FundingClaims.Add(pair.Key, pair.Value);
        foreach (var pair in ProviderMonetaryLegs) clone.ProviderMonetaryLegs.Add(pair.Key, pair.Value);
        foreach (var pair in ProviderReversalStates) clone.ProviderReversalStates.Add(pair.Key, pair.Value);
        foreach (var pair in ProviderReversalResults) clone.ProviderReversalResults.Add(pair.Key, pair.Value);
        foreach (var pair in ProviderDisputes) clone.ProviderDisputes.Add(pair.Key, pair.Value);
        foreach (var pair in ProviderDisputeEvents) clone.ProviderDisputeEvents.Add(pair.Key, pair.Value);
        foreach (var pair in DisputeFragmentFreezes) clone.DisputeFragmentFreezes.Add(pair.Key, pair.Value);
        foreach (var pair in DebtPositions) clone.DebtPositions.Add(pair.Key, pair.Value);
        clone.DebtEvents.AddRange(DebtEvents);
        foreach (var pair in FragmentReservations) clone.FragmentReservations.Add(pair.Key, pair.Value);
        clone.CreditLots.AddRange(CreditLots);
        clone.Consumptions.AddRange(Consumptions);
        clone.Lineages.AddRange(Lineages);
        clone.ProjectionUpdates.AddRange(ProjectionUpdates);
        foreach (var pair in Idempotency) clone.Idempotency.Add(pair.Key, pair.Value);
        clone.Outbox.AddRange(Outbox);
        clone.Anchors.AddRange(Anchors);
        clone.HoldEvents.AddRange(HoldEvents);
        foreach (var pair in Holds) clone.Holds.Add(pair.Key, pair.Value);
        return clone;
    }
}

public sealed class LedgerKernelTransaction
{
    private readonly LedgerKernelState _state;

    internal LedgerKernelTransaction(LedgerKernelState state) => _state = state;

    public SourceEvidence? LatestSource(SourceStampId sourceId) =>
        _state.Sources.LastOrDefault(source => source.Id == sourceId);

    public void AddSource(SourceEvidence source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _state.Sources.Add(source);
    }

    public void AddFundingClaim(HardCoinFundingClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (_state.FundingClaims.ContainsKey(claim.SourceId))
            throw new InvalidOperationException("Funding source already exists.");
        if (!_state.ProviderMonetaryLegs.TryAdd(claim.ProviderLeg.Key, claim.SourceId))
            throw new DuplicateProviderMonetaryLegException(claim.ProviderLeg);
        _state.FundingClaims.Add(claim.SourceId, claim);
    }

    public HardCoinFundingClaim CurrentFundingClaim(SourceStampId sourceId) =>
        _state.FundingClaims.TryGetValue(sourceId, out var claim)
            ? claim
            : throw new KeyNotFoundException($"Funding source {sourceId.Value:N} was not found.");

    public void UpdateFundingClaim(HardCoinFundingClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (!_state.FundingClaims.ContainsKey(claim.SourceId))
            throw new KeyNotFoundException($"Funding source {claim.SourceId.Value:N} was not found.");
        _state.FundingClaims[claim.SourceId] = claim;
    }

    public ProviderReversalState? CurrentProviderReversalState(SourceStampId sourceId) =>
        _state.ProviderReversalStates.GetValueOrDefault(sourceId);

    public void SetProviderReversalState(ProviderReversalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state.ProviderReversalStates[state.SourceId] = state;
    }

    public ProviderReversalResult? FindProviderReversalResult(IdempotencyKey key) =>
        _state.ProviderReversalResults.GetValueOrDefault(key.Value);

    public void AddProviderReversalResult(IdempotencyKey key, ProviderReversalResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _state.ProviderReversalResults.Add(key.Value, result);
    }

    public ProviderDisputeEventRecord? FindProviderDisputeEvent(string providerEventId) =>
        _state.ProviderDisputeEvents.GetValueOrDefault(providerEventId);

    public void AddProviderDisputeEvent(ProviderDisputeEventRecord disputeEvent)
    {
        ArgumentNullException.ThrowIfNull(disputeEvent);
        if (!_state.ProviderDisputeEvents.TryAdd(disputeEvent.ProviderEventId, disputeEvent))
            throw new ProviderDisputeEventConflictException(disputeEvent.ProviderEventId);
    }

    public ProviderDisputeCase? FindProviderDisputeCase(string providerDisputeReference) =>
        _state.ProviderDisputes.GetValueOrDefault(providerDisputeReference);

    public void SetProviderDisputeCase(ProviderDisputeCase dispute)
    {
        ArgumentNullException.ThrowIfNull(dispute);
        _state.ProviderDisputes[dispute.ProviderDisputeReference] = dispute;
    }

    public void AddDisputeFreeze(DisputeFragmentFreeze freeze)
    {
        ArgumentNullException.ThrowIfNull(freeze);
        if (!_state.DisputeFragmentFreezes.TryAdd(freeze.Id, freeze))
            throw new InvalidOperationException($"Dispute freeze {freeze.Id:N} already exists.");
    }

    public IReadOnlyList<DisputeFragmentFreeze> GetDisputeFreezes(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return ids.Select(id => _state.DisputeFragmentFreezes.TryGetValue(id, out var freeze)
                ? freeze
                : throw new KeyNotFoundException($"Dispute freeze {id:N} was not found."))
            .ToArray();
    }

    public void TransitionDisputeFreezes(
        IEnumerable<Guid> ids,
        HoldStatus status,
        DateTimeOffset occurredAt)
    {
        foreach (var freeze in GetDisputeFreezes(ids).Where(item => item.Status == HoldStatus.Active))
            _state.DisputeFragmentFreezes[freeze.Id] = freeze.Transition(status, occurredAt);
    }

    public IReadOnlyList<CreditLot> GetAvailableRootLots(SourceStampId rootSourceId)
    {
        var result = new List<CreditLot>();
        foreach (var lot in _state.CreditLots.Where(item => item.State == CreditLotState.Active))
        {
            var rootRanges = lot.Ranges.Where(range => range.Root == rootSourceId).ToArray();
            if (rootRanges.Length == 0) continue;
            var remaining = Subtract(rootRanges, ExcludedRanges(_state, lot.Id));
            var traceUnits = remaining.Aggregate(0L, static (total, range) => checked(total + range.Length));
            if (traceUnits == 0) continue;
            if (traceUnits % lot.TraceUnitsPerCoinUnit != 0)
                throw new LineageConservationException("Available root trace units must resolve to whole coin units.");
            result.Add(CopyAvailableLot(lot, remaining, traceUnits / lot.TraceUnitsPerCoinUnit));
        }
        return result;
    }

    public void EnsureWalletNotDebtRestricted(WalletId walletId)
    {
        if (_state.DebtPositions.TryGetValue(walletId, out var debt) && debt.OutstandingHardUnits > 0)
            throw new WalletDebtRestrictionException(walletId, debt.OutstandingHardUnits);
    }

    public void RecordDebt(WalletId walletId, SourceStampId sourceId, long deltaHardUnits, DateTimeOffset occurredAt)
    {
        if (deltaHardUnits == 0) return;
        var current = _state.DebtPositions.GetValueOrDefault(walletId)?.OutstandingHardUnits ?? 0;
        var outstanding = checked(current + deltaHardUnits);
        if (outstanding < 0) throw new InvalidOperationException("Wallet debt cannot become negative.");
        _state.DebtPositions[walletId] = new WalletDebtPosition(walletId, outstanding, occurredAt);
        _state.DebtEvents.Add(new WalletDebtEvent(
            checked(_state.DebtEvents.Count + 1L), walletId, sourceId, deltaHardUnits, outstanding, occurredAt));
    }

    public void AddFragmentReservation(ValueFragmentReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (_state.FragmentReservations.ContainsKey(reservation.Id))
            throw new InvalidOperationException($"Fragment reservation {reservation.Id:N} already exists.");
        var lot = GetCreditLot(reservation.LotId);
        if (lot.WalletId != reservation.WalletId || lot.Amount.Currency != reservation.Amount.Currency)
            throw new InvalidOperationException("Fragment reservation does not match its credit lot.");
        var excluded = ExcludedRanges(_state, reservation.LotId);
        if (reservation.Ranges.Any(range => excluded.Any(item => Overlaps(range, item))))
            throw new InvalidOperationException("Fragment reservation overlaps unavailable lineage ranges.");
        if (reservation.Ranges.Any(range => !lot.Ranges.Any(parent => Contains(parent, range))))
            throw new InvalidOperationException("Fragment reservation range is outside its credit lot.");
        _state.FragmentReservations.Add(reservation.Id, reservation);
    }

    public IReadOnlyList<ValueFragmentReservation> GetFragmentReservations(Guid operationId) =>
        _state.FragmentReservations.Values.Where(item => item.OperationId == operationId)
            .OrderBy(item => item.Id).ToArray();

    public IReadOnlyList<ValueFragmentReservation> TransitionFragmentReservations(
        Guid operationId,
        FragmentReservationStatus expected,
        FragmentReservationStatus next,
        DateTimeOffset occurredAt)
    {
        var current = GetFragmentReservations(operationId);
        if (current.Count == 0) throw new KeyNotFoundException($"Fragment reservations for {operationId:N} were not found.");
        if (current.Any(item => item.Status != expected))
            throw new InvalidOperationException($"All fragment reservations must be {expected}.");
        var transitioned = current.Select(item => item.Transition(next, occurredAt)).ToArray();
        foreach (var item in transitioned) _state.FragmentReservations[item.Id] = item;
        return transitioned;
    }

    public IReadOnlyList<Guid> ReleaseReservedFragmentsForRoot(
        SourceStampId root,
        DateTimeOffset occurredAt)
    {
        var operationIds = _state.FragmentReservations.Values
            .Where(item => item.Status == FragmentReservationStatus.Reserved &&
                           item.Ranges.Any(range => range.Root == root))
            .Select(item => item.OperationId)
            .Distinct()
            .ToArray();
        foreach (var operationId in operationIds)
        {
            var reservations = GetFragmentReservations(operationId);
            foreach (var reservation in reservations.Where(item => item.Status == FragmentReservationStatus.Reserved))
                _state.FragmentReservations[reservation.Id] = reservation.Transition(
                    FragmentReservationStatus.Released, occurredAt);
        }
        return operationIds;
    }

    private static bool Overlaps(RootTraceRange left, RootTraceRange right) =>
        left.Root == right.Root && left.Start < right.EndExclusive && right.Start < left.EndExclusive;

    private static bool Contains(RootTraceRange parent, RootTraceRange child) =>
        parent.Root == child.Root && parent.Epoch == child.Epoch &&
        parent.Start <= child.Start && parent.EndExclusive >= child.EndExclusive;

    public JournalAppendResult AppendJournal(PostingRequest request, DateTimeOffset recordedAt)
    {
        var chain = new JournalChain(_state.JournalEntries);
        var result = chain.Append(request, recordedAt);
        _state.JournalEntries = [.. chain.Entries];
        return result;
    }

    public IReadOnlyList<CreditLot> GetAvailableLots(WalletId walletId, CurrencyCode currency) =>
        GetAvailableLots(_state, walletId, currency);

    public void AddCreditLot(CreditLot lot) => _state.CreditLots.Add(lot);
    public CreditLot GetCreditLot(CreditLotId lotId) =>
        _state.CreditLots.SingleOrDefault(lot => lot.Id == lotId)
        ?? throw new KeyNotFoundException($"Credit lot {lotId.Value:N} was not found.");

    public PostingResult GetPostingResult(PostingId postingId)
    {
        var entry = _state.JournalEntries.SingleOrDefault(item => item.PostingId == postingId)
            ?? throw new KeyNotFoundException($"Posting {postingId.Value:N} was not found.");
        return new PostingResult(
            entry.PostingId,
            PostingStatus.Accepted,
            entry.Hash,
            entry.RecordedAt,
            entry.Lines.Select(line => new PostedLineResult(line.Sequence, line.Id)).ToArray());
    }
    public void AddConsumption(FragmentConsumption consumption) => _state.Consumptions.Add(consumption);
    public void AddLineage(DerivedCreditLot lineage) => _state.Lineages.Add(lineage);
    public void AddProjectionUpdate(WalletProjectionUpdate update) => _state.ProjectionUpdates.Add(update);
    public void AddOutbox(ImmutableOutboxMessage message) => _state.Outbox.Add(message);
    public void AddAnchor(ChainAnchor anchor) => _state.Anchors.Add(anchor);
    public HoldContract PlaceHold(
        HoldId id,
        WalletId walletId,
        CoinAmount amount,
        HoldReason reason,
        DateTimeOffset effectiveAt)
    {
        if (_state.Holds.ContainsKey(id)) throw new InvalidOperationException($"Hold {id.Value:N} already exists.");
        var available = GetAvailableLots(walletId, amount.Currency)
            .Aggregate(0L, static (total, lot) => checked(total + lot.Amount.Units));
        var alreadyHeld = ActiveHoldUnits(walletId, amount.Currency);
        var unheld = Math.Max(0, available - alreadyHeld);
        if (amount.Units > unheld) throw new InsufficientFragmentsException(amount.Units - unheld);

        var hold = new HoldContract(id, walletId, amount, reason, HoldStatus.Active, effectiveAt, null);
        _state.Holds.Add(id, hold);
        AppendHoldEvent(HoldEventKind.Placed, hold, effectiveAt);
        return hold;
    }

    public HoldContract TransitionHold(
        HoldId id,
        HoldStatus status,
        HoldEventKind kind,
        DateTimeOffset occurredAt)
    {
        var current = CurrentHold(id);
        if (current.Status != HoldStatus.Active)
            throw new InvalidOperationException("Only an active hold can enter a terminal state.");
        if (occurredAt < current.EffectiveAt)
            throw new ArgumentException("A hold transition cannot precede placement.", nameof(occurredAt));
        var terminal = new HoldContract(
            current.Id,
            current.WalletId,
            current.Amount,
            current.Reason,
            status,
            current.EffectiveAt,
            occurredAt);
        _state.Holds[id] = terminal;
        AppendHoldEvent(kind, terminal, occurredAt);
        return terminal;
    }

    public HoldContract CurrentHold(HoldId id) =>
        _state.Holds.TryGetValue(id, out var hold)
            ? hold
            : throw new KeyNotFoundException($"Hold {id.Value:N} was not found.");

    public long ActiveHoldUnits(WalletId walletId, CurrencyCode currency) =>
        _state.Holds.Values
            .Where(hold => hold.WalletId == walletId && hold.Status == HoldStatus.Active &&
                           hold.Amount.Currency == currency)
            .Aggregate(0L, static (total, hold) => checked(total + hold.Amount.Units));

    public JournalEntry? JournalHead => _state.JournalEntries.Count == 0 ? null : _state.JournalEntries[^1];

    private void AppendHoldEvent(HoldEventKind kind, HoldContract hold, DateTimeOffset occurredAt) =>
        _state.HoldEvents.Add(new HoldEvent(
            checked(_state.HoldEvents.Count + 1L),
            kind,
            hold.Id,
            hold.WalletId,
            hold.Amount,
            hold.Reason,
            occurredAt));

    public PostingResult? FindIdempotent(IdempotencyKey key, string requestHash)
    {
        if (!_state.Idempotency.TryGetValue(key.Value, out var existing)) return null;
        if (!StringComparer.Ordinal.Equals(existing.RequestHash, requestHash))
            throw new IdempotencyConflictException(key);
        return existing.Result;
    }

    public void AddIdempotency(IdempotencyRecord record) =>
        _state.Idempotency.Add(record.Key.Value, record);

    internal static IReadOnlyList<CreditLot> GetAvailableLots(
        LedgerKernelState state,
        WalletId walletId,
        CurrencyCode currency)
    {
        var result = new List<CreditLot>();
        foreach (var lot in state.CreditLots.Where(lot =>
                     lot.WalletId == walletId && lot.Amount.Currency == currency && lot.State == CreditLotState.Active))
        {
            var remaining = Subtract(lot.Ranges, ExcludedRanges(state, lot.Id));
            var remainingTraceUnits = remaining.Aggregate(0L, static (total, range) => checked(total + range.Length));
            if (remainingTraceUnits == 0) continue;
            if (remainingTraceUnits % lot.TraceUnitsPerCoinUnit != 0)
                throw new LineageConservationException("Available trace units must resolve to whole coin units.");
            var remainingUnits = remainingTraceUnits / lot.TraceUnitsPerCoinUnit;
            result.Add(CopyAvailableLot(lot, remaining, remainingUnits));
        }

        return result;
    }

    private static RootTraceRange[] ExcludedRanges(LedgerKernelState state, CreditLotId lotId) =>
        state.Consumptions
            .Where(consumption => consumption.ParentLotId == lotId)
            .SelectMany(consumption => consumption.Ranges)
            .Concat(state.DisputeFragmentFreezes.Values
                .Where(freeze => freeze.LotId == lotId && freeze.Status == HoldStatus.Active)
                .SelectMany(freeze => freeze.Ranges))
            .Concat(state.FragmentReservations.Values
                .Where(reservation => reservation.LotId == lotId &&
                                      reservation.Status is FragmentReservationStatus.Reserved or FragmentReservationStatus.Dispatching)
                .SelectMany(reservation => reservation.Ranges))
            .ToArray();

    private static CreditLot CopyAvailableLot(
        CreditLot lot,
        IReadOnlyList<RootTraceRange> ranges,
        long units) => new(
        lot.Id,
        lot.WalletId,
        new CoinAmount(lot.Amount.Currency, units),
        lot.Provenance,
        lot.ConfirmedAt,
        lot.OriginalMaturesAt,
        lot.JournalSequence,
        CreditLotState.Active,
        ranges,
        lot.TraceUnitsPerCoinUnit);

    private static IReadOnlyList<RootTraceRange> Subtract(
        IReadOnlyList<RootTraceRange> sources,
        IReadOnlyCollection<RootTraceRange> consumed)
    {
        var result = new List<RootTraceRange>();
        foreach (var source in sources)
        {
            var cursor = source.Start;
            foreach (var exclusion in consumed
                         .Where(range => range.Root == source.Root && range.EndExclusive > source.Start && range.Start < source.EndExclusive)
                         .OrderBy(range => range.Start))
            {
                if (exclusion.Start > cursor)
                    result.Add(new RootTraceRange(source.Root, cursor, exclusion.Start - cursor, source.Epoch));
                cursor = Math.Max(cursor, exclusion.EndExclusive);
                if (cursor >= source.EndExclusive) break;
            }

            if (cursor < source.EndExclusive)
                result.Add(new RootTraceRange(source.Root, cursor, source.EndExclusive - cursor, source.Epoch));
        }

        return result;
    }
}

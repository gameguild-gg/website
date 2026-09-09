using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;

namespace GameGuild.Finance.Economy.Ledger;

public sealed class TransactionalPostingService
{
    private readonly InMemoryLedgerKernelStore _store;
    private readonly IEconomyOutboxFactory _outboxFactory;
    private readonly RootReversalFenceRegistry _fences;

    public TransactionalPostingService(
        InMemoryLedgerKernelStore store,
        IEconomyOutboxFactory? outboxFactory = null,
        RootReversalFenceRegistry? fences = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _outboxFactory = outboxFactory ?? new EconomyOutboxFactory();
        _fences = fences ?? new RootReversalFenceRegistry();
    }

    public HardCoinFundingClaim ObserveTopUp(ObserveHardCoinTopUpCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _store.Execute(transaction =>
        {
            if (transaction.LatestSource(command.SourceId) is not null)
                throw new InvalidOperationException("Source evidence already exists.");
            var claim = HardCoinFundingClaim.Observe(
                command.SourceId,
                command.WalletId,
                command.ProviderLeg,
                command.Evidence,
                command.AuthoritativeUsdMinorUnits,
                command.ObservedAt);
            var source = SourceEvidence.Observe(
                command.SourceId,
                claim.ProviderLeg.Provider,
                claim.ProviderLeg.Key,
                command.Evidence,
                command.ObservedAt);
            transaction.AddSource(source);
            transaction.AddFundingClaim(claim);
            return claim;
        });
    }

    public PostingResult ConfirmObservedTopUp(ConfirmObservedTopUpCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Evidence);
        ArgumentNullException.ThrowIfNull(command.Authorization);
        var commandHash = ComputeObservedTopUpHash(command);
        return _store.Execute(transaction =>
        {
            var duplicate = transaction.FindIdempotent(command.IdempotencyKey, commandHash);
            if (duplicate is not null) return duplicate;

            var currentClaim = transaction.CurrentFundingClaim(command.SourceId);
            command.Authorization.EnsureMatches(
                PostingTemplateKind.ConfirmedTopUpMint,
                command.IdempotencyKey,
                currentClaim.Amount,
                command.ReserveVersion,
                command.ConfirmedAt);
            command.Authorization.EnsureSourceRoots([currentClaim.SourceId]);
            var confirmedClaim = currentClaim.Transition(
                SourceConfirmationState.Confirmed,
                command.Evidence,
                command.ConfirmedAt);
            var observed = transaction.LatestSource(command.SourceId)
                ?? throw new InvalidOperationException("Observed source evidence was not found.");
            var confirmed = observed.Confirm(command.ConfirmedAt);
            transaction.AddSource(confirmed);
            transaction.UpdateFundingClaim(confirmedClaim);

            var sourceContract = new SourceStampContract(
                confirmed.Id,
                confirmed.EvidenceHash,
                SourceConfirmationState.Confirmed,
                confirmed.ObservedAt,
                confirmed.ConfirmedAt,
                confirmed.ProviderReference);
            var request = new PostingRequest(
                command.PostingId,
                new PostingTemplate(PostingTemplateKind.ConfirmedTopUpMint, PostingTemplate.CurrentVersion),
                command.IdempotencyKey,
                PostingAuthority.ProviderConfirmation,
                command.ReserveVersion,
                command.PolicyVersion,
                sourceContract,
                command.ConfirmedAt,
                [
                    new PostingLine(1, EntrySide.Debit, EconomyAccountCode.ExternalClearingHard,
                        confirmedClaim.Amount, null, null, null),
                    new PostingLine(2, EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability,
                        confirmedClaim.Amount, confirmedClaim.WalletId, command.CreditLotId, ProvenanceKind.PurchasedHard)
                ]);
            var append = transaction.AppendJournal(request, command.ConfirmedAt);
            var lot = ConfirmedCreditFactory.CreateRootLot(
                command.CreditLotId,
                confirmedClaim.WalletId,
                confirmedClaim.Amount,
                ProvenanceKind.PurchasedHard,
                confirmed,
                command.ConfirmedAt,
                append.Entry.Sequence);
            transaction.AddCreditLot(lot);
            transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                command.PostingId,
                confirmedClaim.WalletId,
                confirmedClaim.Amount.Currency,
                confirmedClaim.Amount.Units,
                append.Entry.Sequence));
            transaction.AddIdempotency(new IdempotencyRecord(command.IdempotencyKey, commandHash, append.Result));
            transaction.AddOutbox(_outboxFactory.PostingAccepted(append.Result));
            return append.Result;
        });
    }

    public HardCoinFundingClaim FinalizeObservedTopUp(FinalizeObservedTopUpCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _store.Execute(transaction =>
        {
            var currentClaim = transaction.CurrentFundingClaim(command.SourceId);
            var terminal = currentClaim.Transition(command.State, command.Evidence, command.OccurredAt);
            var observed = transaction.LatestSource(command.SourceId)
                ?? throw new InvalidOperationException("Observed source evidence was not found.");
            var source = command.State switch
            {
                SourceConfirmationState.Failed => observed.Fail(command.OccurredAt),
                SourceConfirmationState.Expired => observed.Expire(command.OccurredAt),
                _ => throw new InvalidFundingStateTransitionException(observed.State, command.State)
            };
            transaction.AddSource(source);
            transaction.UpdateFundingClaim(terminal);
            return terminal;
        });
    }

    public HardToSoftConversionResult ConvertHardToSoft(ConvertHardToSoftCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Authorization);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.PrincipalHardCoinUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(command.FeeHardCoinUnits);
        var principal = new CoinAmount(CurrencyCode.HardCoin, command.PrincipalHardCoinUnits);
        var soft = new CoinAmount(
            CurrencyCode.SoftCoin,
            checked(command.PrincipalHardCoinUnits * Money.FixedParity.SoftCoinsPerHardCoin));
        var totalHard = new CoinAmount(
            CurrencyCode.HardCoin,
            checked(command.PrincipalHardCoinUnits + command.FeeHardCoinUnits));
        var commandHash = ComputeConversionHash(command);
        command.Authorization.EnsureMatches(
            PostingTemplateKind.HardToSoftConversion,
            command.IdempotencyKey,
            totalHard,
            command.ReserveVersion,
            command.RequestedAt);

        return _store.Execute(transaction =>
        {
            var duplicate = transaction.FindIdempotent(command.IdempotencyKey, commandHash);
            if (duplicate is not null)
            {
                var duplicateFee = command.FeeHardCoinUnits == 0
                    ? null
                    : transaction.GetPostingResult(command.FeePostingId);
                return new HardToSoftConversionResult(
                    duplicate,
                    duplicateFee,
                    transaction.GetCreditLot(command.OutputLotId));
            }

            transaction.EnsureWalletNotDebtRestricted(command.WalletId);
            var available = transaction.GetAvailableLots(command.WalletId, CurrencyCode.HardCoin)
                .Where(lot => lot.Provenance == ProvenanceKind.PurchasedHard)
                .ToArray();
            var availableUnits = available.Aggregate(0L, static (total, lot) => checked(total + lot.Amount.Units));
            var heldUnits = transaction.ActiveHoldUnits(command.WalletId, CurrencyCode.HardCoin);
            var spendableUnits = Math.Max(0, availableUnits - heldUnits);
            if (totalHard.Units > spendableUnits)
                throw new InsufficientFragmentsException(totalHard.Units - spendableUnits);

            var selected = FifoFragmentSelector.Select(available, totalHard);
            var outputAmounts = command.FeeHardCoinUnits == 0
                ? new[] { principal }
                : new[] { principal, new CoinAmount(CurrencyCode.HardCoin, command.FeeHardCoinUnits) };
            var partitions = LineagePartitioner.Partition(selected.Selections, outputAmounts);
            var roots = selected.Selections
                .SelectMany(selection => selection.SelectedRanges)
                .Select(range => range.Root)
                .Distinct()
                .ToArray();
            command.Authorization.EnsureSourceRoots(roots);
            var snapshot = _fences.Capture(roots);

            return _fences.WithAllocationFence(snapshot, roots, () =>
            {
                var principalRequest = new PostingRequest(
                    command.PrincipalPostingId,
                    new PostingTemplate(PostingTemplateKind.HardToSoftConversion, PostingTemplate.CurrentVersion),
                    command.IdempotencyKey,
                    PostingAuthority.WalletOwner,
                    command.ReserveVersion,
                    command.PolicyVersion,
                    null,
                    command.RequestedAt,
                    [
                        new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability,
                            principal, command.WalletId, null, ProvenanceKind.PurchasedHard),
                        new PostingLine(2, EntrySide.Credit, EconomyAccountCode.HardCoinReserve,
                            principal, null, null, null),
                        new PostingLine(3, EntrySide.Debit, EconomyAccountCode.SoftCoinReserve,
                            soft, null, null, null),
                        new PostingLine(4, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                            soft, command.WalletId, command.OutputLotId, ProvenanceKind.ConvertedSoft)
                    ]);
                var principalAppend = transaction.AppendJournal(principalRequest, command.RequestedAt);
                var principalSelections = partitions[0].Selections;
                var converted = LineageAllocator.CreateConvertedSoftLot(
                    command.OutputLotId,
                    command.WalletId,
                    soft,
                    command.RequestedAt,
                    command.RequestedAt,
                    principalAppend.Entry.Sequence,
                    principalSelections,
                    snapshot,
                    _fences);
                AddConsumptions(transaction, command.PrincipalPostingId, principalSelections);
                transaction.AddCreditLot(converted.Lot);
                transaction.AddLineage(converted);
                transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                    command.PrincipalPostingId,
                    command.WalletId,
                    CurrencyCode.HardCoin,
                    -principal.Units,
                    principalAppend.Entry.Sequence));
                transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                    command.PrincipalPostingId,
                    command.WalletId,
                    CurrencyCode.SoftCoin,
                    soft.Units,
                    principalAppend.Entry.Sequence));
                transaction.AddOutbox(_outboxFactory.PostingAccepted(principalAppend.Result));

                PostingResult? feeResult = null;
                if (command.FeeHardCoinUnits > 0)
                {
                    var fee = new CoinAmount(CurrencyCode.HardCoin, command.FeeHardCoinUnits);
                    var feeRequest = new PostingRequest(
                        command.FeePostingId,
                        new PostingTemplate(PostingTemplateKind.HardToSoftConversionFee, PostingTemplate.CurrentVersion),
                        new IdempotencyKey($"{command.IdempotencyKey.Value}:fee"),
                        PostingAuthority.WalletOwner,
                        command.ReserveVersion,
                        command.PolicyVersion,
                        null,
                        command.RequestedAt,
                        [
                            new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability,
                                fee, command.WalletId, null, ProvenanceKind.PurchasedHard),
                            new PostingLine(2, EntrySide.Credit, EconomyAccountCode.FeeRevenueHard,
                                fee, null, null, null)
                        ]);
                    var feeAppend = transaction.AppendJournal(feeRequest, command.RequestedAt);
                    AddConsumptions(transaction, command.FeePostingId, partitions[1].Selections);
                    transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                        command.FeePostingId,
                        command.WalletId,
                        CurrencyCode.HardCoin,
                        -fee.Units,
                        feeAppend.Entry.Sequence));
                    transaction.AddOutbox(_outboxFactory.PostingAccepted(feeAppend.Result));
                    feeResult = feeAppend.Result;
                }

                transaction.AddIdempotency(new IdempotencyRecord(
                    command.IdempotencyKey,
                    commandHash,
                    principalAppend.Result));
                return new HardToSoftConversionResult(principalAppend.Result, feeResult, converted.Lot);
            });
        });
    }

    public SystemBackedGrantResult IssueSystemBackedGrant(IssueSystemBackedGrantCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Authorization);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.TreasuryEvidence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.HardBackingUnits);
        var hard = new CoinAmount(CurrencyCode.HardCoin, command.HardBackingUnits);
        var soft = new CoinAmount(
            CurrencyCode.SoftCoin,
            checked(command.HardBackingUnits * Money.FixedParity.SoftCoinsPerHardCoin));
        command.Authorization.EnsureMatches(
            PostingTemplateKind.SystemBackedGrant,
            command.IdempotencyKey,
            soft,
            command.ReserveVersion,
            command.IssuedAt);
        command.Authorization.EnsureSourceRoots([command.SourceId]);
        var commandHash = ComputeSystemGrantHash(command);

        return _store.Execute(transaction =>
        {
            var duplicate = transaction.FindIdempotent(command.IdempotencyKey, commandHash);
            if (duplicate is not null)
                return new SystemBackedGrantResult(duplicate, transaction.GetCreditLot(command.OutputLotId));
            if (transaction.LatestSource(command.SourceId) is not null)
                throw new InvalidOperationException("Grant source evidence already exists.");

            var observed = SourceEvidence.Observe(
                command.SourceId,
                "platform-treasury",
                command.PostingId.Value.ToString("N"),
                command.TreasuryEvidence,
                command.IssuedAt);
            var confirmed = observed.Confirm(command.IssuedAt);
            transaction.AddSource(observed);
            transaction.AddSource(confirmed);
            var request = new PostingRequest(
                command.PostingId,
                new PostingTemplate(PostingTemplateKind.SystemBackedGrant, PostingTemplate.CurrentVersion),
                command.IdempotencyKey,
                PostingAuthority.PlatformSystem,
                command.ReserveVersion,
                command.PolicyVersion,
                null,
                command.IssuedAt,
                [
                    new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PlatformHardTreasury,
                        hard, null, null, null),
                    new PostingLine(2, EntrySide.Credit, EconomyAccountCode.HardCoinReserve,
                        hard, null, null, null),
                    new PostingLine(3, EntrySide.Debit, EconomyAccountCode.SoftCoinReserve,
                        soft, null, null, null),
                    new PostingLine(4, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                        soft, command.WalletId, command.OutputLotId, ProvenanceKind.SystemGrantSoft)
                ]);
            var append = transaction.AppendJournal(request, command.IssuedAt);
            var lot = ConfirmedCreditFactory.CreateRootLot(
                command.OutputLotId,
                command.WalletId,
                soft,
                ProvenanceKind.SystemGrantSoft,
                confirmed,
                command.IssuedAt,
                append.Entry.Sequence);
            transaction.AddCreditLot(lot);
            transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                command.PostingId,
                command.WalletId,
                CurrencyCode.SoftCoin,
                soft.Units,
                append.Entry.Sequence));
            transaction.AddIdempotency(new IdempotencyRecord(command.IdempotencyKey, commandHash, append.Result));
            transaction.AddOutbox(_outboxFactory.PostingAccepted(append.Result));
            return new SystemBackedGrantResult(append.Result, lot);
        });
    }

    public AdRewardIssuanceResult IssueAdReward(IssueAdRewardCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Authorization);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ProviderEvidence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.SoftUnits);
        var soft = new CoinAmount(CurrencyCode.SoftCoin, command.SoftUnits);
        command.Authorization.EnsureMatches(
            PostingTemplateKind.AdRewardIssuance,
            command.IdempotencyKey,
            soft,
            command.ReserveVersion,
            command.IssuedAt);
        command.Authorization.EnsureSourceRoots([command.SourceId]);
        var commandHash = ComputeAdRewardHash(command);

        return _store.Execute(transaction =>
        {
            var duplicate = transaction.FindIdempotent(command.IdempotencyKey, commandHash);
            if (duplicate is not null)
                return new AdRewardIssuanceResult(duplicate, transaction.GetCreditLot(command.OutputLotId));
            if (transaction.LatestSource(command.SourceId) is not null)
                throw new InvalidOperationException("Ad reward source evidence already exists.");

            var observed = SourceEvidence.Observe(
                command.SourceId,
                "ad-network",
                command.PostingId.Value.ToString("N"),
                command.ProviderEvidence,
                command.IssuedAt);
            var confirmed = observed.Confirm(command.IssuedAt);
            transaction.AddSource(observed);
            transaction.AddSource(confirmed);
            var source = new SourceStampContract(
                confirmed.Id,
                confirmed.EvidenceHash,
                SourceConfirmationState.Confirmed,
                confirmed.ObservedAt,
                confirmed.ConfirmedAt,
                confirmed.ProviderReference);
            var request = new PostingRequest(
                command.PostingId,
                new PostingTemplate(PostingTemplateKind.AdRewardIssuance, PostingTemplate.CurrentVersion),
                command.IdempotencyKey,
                PostingAuthority.PlatformSystem,
                command.ReserveVersion,
                command.PolicyVersion,
                source,
                command.IssuedAt,
                [
                    new PostingLine(1, EntrySide.Debit, EconomyAccountCode.SoftCoinReserve,
                        soft, null, null, null),
                    new PostingLine(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                        soft, command.WalletId, command.OutputLotId, ProvenanceKind.AdRewardSoft)
                ]);
            var append = transaction.AppendJournal(request, command.IssuedAt);
            var lot = ConfirmedCreditFactory.CreateRootLot(
                command.OutputLotId,
                command.WalletId,
                soft,
                ProvenanceKind.AdRewardSoft,
                confirmed,
                command.IssuedAt,
                append.Entry.Sequence);
            transaction.AddCreditLot(lot);
            transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                command.PostingId,
                command.WalletId,
                CurrencyCode.SoftCoin,
                soft.Units,
                append.Entry.Sequence));
            transaction.AddIdempotency(new IdempotencyRecord(command.IdempotencyKey, commandHash, append.Result));
            transaction.AddOutbox(_outboxFactory.PostingAccepted(append.Result));
            return new AdRewardIssuanceResult(append.Result, lot);
        });
    }

    public ProviderReversalResult ReverseTopUp(ReverseTopUpCommand command)
    {
        var commandHash = ValidateAndHash(command);
        var epoch = _fences.BeginReversal(command.SourceId);
        try
        {
            return _store.Execute(transaction => ReverseTopUpInTransaction(transaction, command, commandHash));
        }
        finally
        {
            _fences.CompleteReversal(command.SourceId, epoch);
        }
    }

    internal T ReverseTopUpUnderActiveFence<T>(
        ReverseTopUpCommand command,
        Action<LedgerKernelTransaction> beforePosting,
        Func<LedgerKernelTransaction, ProviderReversalResult, T> afterPosting)
    {
        ArgumentNullException.ThrowIfNull(beforePosting);
        ArgumentNullException.ThrowIfNull(afterPosting);
        var commandHash = ValidateAndHash(command);
        return _store.Execute(transaction =>
        {
            beforePosting(transaction);
            var result = ReverseTopUpInTransaction(transaction, command, commandHash);
            return afterPosting(transaction, result);
        });
    }

    private static string ValidateAndHash(ReverseTopUpCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Evidence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.CumulativeProviderHardUnits);
        if (!Enum.IsDefined(command.IrrecoverableDisposition))
            throw new ArgumentOutOfRangeException(nameof(command));
        return ComputeProviderReversalHash(command);
    }

    private ProviderReversalResult ReverseTopUpInTransaction(
        LedgerKernelTransaction transaction,
        ReverseTopUpCommand command,
        string commandHash)
    {
            var duplicatePosting = transaction.FindIdempotent(command.IdempotencyKey, commandHash);
            if (duplicatePosting is not null)
                return transaction.FindProviderReversalResult(command.IdempotencyKey)
                       ?? throw new InvalidOperationException("Provider reversal result was not committed atomically.");

            var claim = transaction.CurrentFundingClaim(command.SourceId);
            if (claim.State is not (SourceConfirmationState.Confirmed or SourceConfirmationState.Disputed))
                throw new InvalidFundingStateTransitionException(claim.State, SourceConfirmationState.Disputed);
            if (command.CumulativeProviderHardUnits > claim.Amount.Units)
                throw new ProviderMonetaryTotalExceededException(
                    "Cumulative provider reversal cannot exceed confirmed HardCoin units.");
            var current = transaction.CurrentProviderReversalState(command.SourceId) ??
                          new ProviderReversalState(command.SourceId, claim.Amount.Units, 0, 0, 0, 0, 0, []);
            if (command.CumulativeProviderHardUnits <= current.CumulativeProviderHardUnits)
                throw new ProviderMonetaryTotalExceededException(
                    "Cumulative provider reversal must increase monotonically.");

            var available = transaction.GetAvailableRootLots(command.SourceId);
            var plan = ProviderReversalPlanner.Plan(
                command.SourceId,
                checked(command.CumulativeProviderHardUnits * CurrencyTraceScale.HardCoinTraceUnitsPerCoin),
                current.ReversedRanges,
                available);
            var full = command.CumulativeProviderHardUnits == claim.Amount.Units;
            var targetState = full ? SourceConfirmationState.Reversed : SourceConfirmationState.Disputed;
            var nextClaim = claim.Transition(targetState, command.Evidence, command.OccurredAt);
            var previousEvidence = transaction.LatestSource(command.SourceId)
                ?? throw new InvalidOperationException("Confirmed source evidence was not found.");
            var nextEvidence = full
                ? previousEvidence.Reverse(command.OccurredAt)
                : previousEvidence.Dispute(command.OccurredAt);
            transaction.AddSource(nextEvidence);
            transaction.UpdateFundingClaim(nextClaim);
            var source = new SourceStampContract(
                nextEvidence.Id,
                nextEvidence.EvidenceHash,
                nextEvidence.State,
                nextEvidence.ObservedAt,
                nextEvidence.ConfirmedAt,
                nextEvidence.ProviderReference);
            var postings = new List<PostingResult>();
            var recoveredHard = 0L;
            var recoveredSoft = 0L;
            var postingIndex = 0;

            foreach (var fragment in plan.Fragments)
            {
                var postingId = DeterministicPostingId(command.PostingIdSeed, postingIndex++);
                PostingRequest request;
                if (fragment.Amount.Currency == CurrencyCode.HardCoin)
                {
                    recoveredHard = checked(recoveredHard + fragment.Amount.Units);
                    request = ProviderHardReversalRequest(
                        command, source, fragment, postingId, postingIndex, full);
                }
                else
                {
                    if (fragment.Amount.Units % Money.FixedParity.SoftCoinsPerHardCoin != 0)
                        throw new UnrecoverableParityFractionException(
                            "Converted-soft recovery must resolve to whole provider HardCoin units.");
                    recoveredSoft = checked(recoveredSoft + fragment.Amount.Units);
                    request = ProviderSoftReversalRequest(
                        command, source, fragment, postingId, postingIndex);
                }

                var append = transaction.AppendJournal(request, command.OccurredAt);
                transaction.AddConsumption(new FragmentConsumption(
                    postingId, fragment.Lot.Id, fragment.Amount, fragment.Ranges));
                transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                    postingId,
                    fragment.Lot.WalletId,
                    fragment.Amount.Currency,
                    -fragment.Amount.Units,
                    append.Entry.Sequence));
                transaction.AddOutbox(_outboxFactory.PostingAccepted(append.Result));
                postings.Add(append.Result);
            }

            var gapHardUnits = plan.UnrecoverableTraceUnits / CurrencyTraceScale.HardCoinTraceUnitsPerCoin;
            var debt = 0L;
            var loss = 0L;
            if (gapHardUnits > 0)
            {
                var debtDisposition = command.IrrecoverableDisposition == ProviderReversalDisposition.ResponsibleDebt;
                debt = debtDisposition ? gapHardUnits : 0;
                loss = debtDisposition ? 0 : gapHardUnits;
                var gapRequest = ProviderGapReversalRequest(
                    command,
                    source,
                    DeterministicPostingId(command.PostingIdSeed, postingIndex),
                    gapHardUnits,
                    debtDisposition);
                var append = transaction.AppendJournal(gapRequest, command.OccurredAt);
                transaction.AddOutbox(_outboxFactory.PostingAccepted(append.Result));
                postings.Add(append.Result);
            }

            var state = new ProviderReversalState(
                command.SourceId,
                claim.Amount.Units,
                command.CumulativeProviderHardUnits,
                checked(current.RecoveredHardUnits + recoveredHard),
                checked(current.RecoveredConvertedSoftUnits + recoveredSoft),
                checked(current.ResponsibleDebtHardUnits + debt),
                checked(current.PlatformLossHardUnits + loss),
                plan.AllReversedRanges);
            if (state.PartitionedHardEquivalentUnits != command.CumulativeProviderHardUnits)
                throw new LineageConservationException(
                    "Provider reversal recovery, debt, and loss must exactly partition the cumulative provider total.");
            var debtDelta = checked(state.ResponsibleDebtHardUnits - current.ResponsibleDebtHardUnits);
            if (debtDelta > 0)
                transaction.RecordDebt(claim.WalletId, command.SourceId, debtDelta, command.OccurredAt);
            var result = new ProviderReversalResult(postings, state);
            transaction.SetProviderReversalState(state);
            transaction.AddIdempotency(new IdempotencyRecord(
                command.IdempotencyKey, commandHash, postings[0]));
            transaction.AddProviderReversalResult(command.IdempotencyKey, result);
            return result;
    }

    public PostingResult Transfer(TransferFragmentsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.SourceWalletId == command.DestinationWalletId)
            throw new ArgumentException("Source and destination wallets must differ.", nameof(command));

        var commandHash = ComputeTransferHash(command);
        var account = LiabilityAccount(command.Amount.Currency, command.Provenance);
        return _store.Execute(transaction =>
        {
            var duplicate = transaction.FindIdempotent(command.IdempotencyKey, commandHash);
            if (duplicate is not null) return duplicate;

            transaction.EnsureWalletNotDebtRestricted(command.SourceWalletId);
            var available = transaction.GetAvailableLots(command.SourceWalletId, command.Amount.Currency)
                .Where(lot => lot.Provenance == command.Provenance)
                .ToArray();
            var availableUnits = available.Aggregate(0L, static (total, lot) => checked(total + lot.Amount.Units));
            var heldUnits = transaction.ActiveHoldUnits(command.SourceWalletId, command.Amount.Currency);
            var spendableUnits = Math.Max(0, availableUnits - heldUnits);
            if (command.Amount.Units > spendableUnits)
                throw new InsufficientFragmentsException(command.Amount.Units - spendableUnits);
            var selected = FifoFragmentSelector.Select(available, command.Amount);
            var roots = selected.Selections.SelectMany(selection => selection.SelectedRanges).Select(range => range.Root).Distinct().ToArray();
            var snapshot = _fences.Capture(roots);

            return _fences.WithAllocationFence(snapshot, roots, () =>
            {
                var request = new PostingRequest(
                    command.PostingId,
                    new PostingTemplate(PostingTemplateKind.Spend, PostingTemplate.CurrentVersion),
                    command.IdempotencyKey,
                    PostingAuthority.WalletOwner,
                    command.ReserveVersion,
                    command.PolicyVersion,
                    null,
                    command.RequestedAt,
                    [
                        new PostingLine(1, EntrySide.Debit, account, command.Amount,
                            command.SourceWalletId, null, command.Provenance),
                        new PostingLine(2, EntrySide.Credit, account, command.Amount,
                            command.DestinationWalletId, null, command.Provenance)
                    ]);
                var append = transaction.AppendJournal(request, command.RequestedAt);

                for (var index = 0; index < selected.Selections.Count; index++)
                {
                    var selection = selected.Selections[index];
                    var parent = available.Single(lot => lot.Id == selection.ParentLotId);
                    var output = LineageAllocator.CreateDerivedLot(
                        DeterministicLotId(command.PostingId, index),
                        command.DestinationWalletId,
                        parent.Provenance,
                        parent.ConfirmedAt,
                        parent.OriginalMaturesAt,
                        append.Entry.Sequence,
                        [selection],
                        snapshot,
                        _fences);
                    transaction.AddConsumption(new FragmentConsumption(
                        command.PostingId,
                        selection.ParentLotId,
                        selection.Amount,
                        selection.SelectedRanges.ToArray()));
                    transaction.AddCreditLot(output.Lot);
                    transaction.AddLineage(output);
                }

                transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                    command.PostingId, command.SourceWalletId, command.Amount.Currency,
                    -command.Amount.Units, append.Entry.Sequence));
                transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                    command.PostingId, command.DestinationWalletId, command.Amount.Currency,
                    command.Amount.Units, append.Entry.Sequence));
                transaction.AddIdempotency(new IdempotencyRecord(command.IdempotencyKey, commandHash, append.Result));
                transaction.AddOutbox(_outboxFactory.PostingAccepted(append.Result));
                return append.Result;
            });
        });
    }

    private static PostingRequest ProviderHardReversalRequest(
        ReverseTopUpCommand command,
        SourceStampContract source,
        ProviderReversalFragment fragment,
        PostingId postingId,
        int index,
        bool full) =>
        new(
            postingId,
            new PostingTemplate(
                full ? PostingTemplateKind.ProviderReversalFull : PostingTemplateKind.ProviderReversalPartial,
                PostingTemplate.CurrentVersion),
            new IdempotencyKey($"{command.IdempotencyKey.Value}:hard:{index}"),
            PostingAuthority.ProviderConfirmation,
            command.ReserveVersion,
            command.PolicyVersion,
            source,
            command.OccurredAt,
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability,
                    fragment.Amount, fragment.Lot.WalletId, null, ProvenanceKind.PurchasedHard),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard,
                    fragment.Amount, null, null, null)
            ]);

    private static PostingRequest ProviderSoftReversalRequest(
        ReverseTopUpCommand command,
        SourceStampContract source,
        ProviderReversalFragment fragment,
        PostingId postingId,
        int index)
    {
        var hardEquivalent = new CoinAmount(
            CurrencyCode.HardCoin,
            fragment.Amount.Units / Money.FixedParity.SoftCoinsPerHardCoin);
        return new PostingRequest(
            postingId,
            new PostingTemplate(PostingTemplateKind.ProviderConvertedSoftReversal, PostingTemplate.CurrentVersion),
            new IdempotencyKey($"{command.IdempotencyKey.Value}:soft:{index}"),
            PostingAuthority.ProviderConfirmation,
            command.ReserveVersion,
            command.PolicyVersion,
            source,
            command.OccurredAt,
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability,
                    fragment.Amount, fragment.Lot.WalletId, null, ProvenanceKind.ConvertedSoft),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.SoftCoinReserve,
                    fragment.Amount, null, null, null),
                new PostingLine(3, EntrySide.Debit, EconomyAccountCode.HardCoinReserve,
                    hardEquivalent, null, null, null),
                new PostingLine(4, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard,
                    hardEquivalent, null, null, null)
            ]);
    }

    private static PostingRequest ProviderGapReversalRequest(
        ReverseTopUpCommand command,
        SourceStampContract source,
        PostingId postingId,
        long hardUnits,
        bool debt)
    {
        var amount = new CoinAmount(CurrencyCode.HardCoin, hardUnits);
        return new PostingRequest(
            postingId,
            new PostingTemplate(
                debt ? PostingTemplateKind.ProviderReversalDebt : PostingTemplateKind.ProviderReversalLoss,
                PostingTemplate.CurrentVersion),
            new IdempotencyKey($"{command.IdempotencyKey.Value}:gap"),
            PostingAuthority.ProviderConfirmation,
            command.ReserveVersion,
            command.PolicyVersion,
            source,
            command.OccurredAt,
            [
                new PostingLine(1, EntrySide.Debit,
                    debt ? EconomyAccountCode.RecoveryReceivableHard : EconomyAccountCode.ProviderLossHard,
                    amount, null, null, null),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard,
                    amount, null, null, null)
            ]);
    }

    private static EconomyAccountCode LiabilityAccount(CurrencyCode currency, ProvenanceKind provenance) =>
        currency switch
        {
            CurrencyCode.HardCoin when provenance == ProvenanceKind.EarnedHard => EconomyAccountCode.EarnedHardLiability,
            CurrencyCode.HardCoin => EconomyAccountCode.PurchasedHardLiability,
            CurrencyCode.SoftCoin => EconomyAccountCode.SoftCoinLiability,
            _ => throw new ArgumentOutOfRangeException(nameof(currency))
        };

    private static CreditLotId DeterministicLotId(PostingId postingId, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{postingId.Value:N}:{index.ToString(CultureInfo.InvariantCulture)}"));
        return new CreditLotId(new Guid(bytes.AsSpan(0, 16)));
    }

    private static PostingId DeterministicPostingId(PostingId seed, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{seed.Value:N}:posting:{index.ToString(CultureInfo.InvariantCulture)}"));
        return new PostingId(new Guid(bytes.AsSpan(0, 16)));
    }

    private static string ComputeObservedTopUpHash(ConfirmObservedTopUpCommand command) => Hash(
        command.PostingId.Value.ToString("N"),
        command.SourceId.Value.ToString("N"),
        command.CreditLotId.Value.ToString("N"),
        command.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.Evidence.Trim(),
        command.ConfirmedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string ComputeTransferHash(TransferFragmentsCommand command) => Hash(
        command.PostingId.Value.ToString("N"), command.SourceWalletId.Value.ToString("N"),
        command.DestinationWalletId.Value.ToString("N"),
        ((int)command.Amount.Currency).ToString(CultureInfo.InvariantCulture),
        command.Amount.Units.ToString(CultureInfo.InvariantCulture),
        ((int)command.Provenance).ToString(CultureInfo.InvariantCulture),
        command.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.RequestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string ComputeConversionHash(ConvertHardToSoftCommand command) => Hash(
        command.PrincipalPostingId.Value.ToString("N"),
        command.FeePostingId.Value.ToString("N"),
        command.WalletId.Value.ToString("N"),
        command.OutputLotId.Value.ToString("N"),
        command.PrincipalHardCoinUnits.ToString(CultureInfo.InvariantCulture),
        command.FeeHardCoinUnits.ToString(CultureInfo.InvariantCulture),
        command.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.RequestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string ComputeSystemGrantHash(IssueSystemBackedGrantCommand command) => Hash(
        command.PostingId.Value.ToString("N"),
        command.SourceId.Value.ToString("N"),
        command.WalletId.Value.ToString("N"),
        command.OutputLotId.Value.ToString("N"),
        command.HardBackingUnits.ToString(CultureInfo.InvariantCulture),
        command.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.TreasuryEvidence.Trim(),
        command.IssuedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string ComputeAdRewardHash(IssueAdRewardCommand command) => Hash(
        command.PostingId.Value.ToString("N"),
        command.SourceId.Value.ToString("N"),
        command.WalletId.Value.ToString("N"),
        command.OutputLotId.Value.ToString("N"),
        command.SoftUnits.ToString(CultureInfo.InvariantCulture),
        command.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.ProviderEvidence.Trim(),
        command.IssuedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string ComputeProviderReversalHash(ReverseTopUpCommand command) => Hash(
        command.PostingIdSeed.Value.ToString("N"),
        command.SourceId.Value.ToString("N"),
        command.CumulativeProviderHardUnits.ToString(CultureInfo.InvariantCulture),
        ((int)command.IrrecoverableDisposition).ToString(CultureInfo.InvariantCulture),
        command.Evidence.Trim(),
        command.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
        command.OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static void AddConsumptions(
        LedgerKernelTransaction transaction,
        PostingId postingId,
        IReadOnlyList<FragmentSelection> selections)
    {
        foreach (var selection in selections)
        {
            transaction.AddConsumption(new FragmentConsumption(
                postingId,
                selection.ParentLotId,
                selection.Amount,
                selection.SelectedRanges.ToArray()));
        }
    }

    private static string Hash(params string[] values)
    {
        var canonical = string.Join('|', values.Select(value => $"{value.Length}:{value}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

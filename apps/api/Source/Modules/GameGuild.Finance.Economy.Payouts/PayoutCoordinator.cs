using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Policy;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Payouts;

public sealed class PayoutCoordinator
{
    private readonly object _gate = new();
    private readonly InMemoryLedgerKernelStore _ledger;
    private readonly IPayoutOperationStore _operations;
    private readonly RootReversalFenceRegistry _rootFences;
    private readonly RiskDecisionAuthorizer _riskAuthorizer;
    private readonly CoreReserveAuthority _reserveAuthority;
    private readonly ProtectedChangeCooldownRegistry _cooldowns;
    private readonly EntityRiskGraph _entityGraph;
    private readonly IConnectPayoutProvider _provider;
    private readonly IPayoutKycEligibilitySource _kyc;
    private readonly IFinancialCrimeRiskInputSource _financialCrime;
    private readonly ITrustSafetyRiskInputSource _trustSafety;
    private readonly IPayoutRollingReserveSource _rollingReserve;
    private readonly IPayoutRiskDecisionSource _riskDecisions;
    private readonly IPayoutReauthenticationSource _reauthentication;
    private readonly IPayoutProviderEvidenceVerifier _providerEvidence;
    private readonly ChainAnchorService _anchors;
    private readonly IIndependentAnchorVerifier _anchorVerifier;
    private readonly PayoutExecutionGate _execution;
    private long _nextFencingToken;

    public PayoutCoordinator(
        InMemoryLedgerKernelStore ledger,
        IPayoutOperationStore operations,
        RootReversalFenceRegistry rootFences,
        RiskDecisionAuthorizer riskAuthorizer,
        CoreReserveAuthority reserveAuthority,
        ProtectedChangeCooldownRegistry cooldowns,
        EntityRiskGraph entityGraph,
        IConnectPayoutProvider provider,
        IPayoutKycEligibilitySource kyc,
        IFinancialCrimeRiskInputSource financialCrime,
        ITrustSafetyRiskInputSource trustSafety,
        IPayoutRollingReserveSource rollingReserve,
        IPayoutRiskDecisionSource riskDecisions,
        IPayoutReauthenticationSource reauthentication,
        IPayoutProviderEvidenceVerifier providerEvidence,
        ChainAnchorService anchors,
        IIndependentAnchorVerifier anchorVerifier,
        PayoutExecutionGate execution)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _rootFences = rootFences ?? throw new ArgumentNullException(nameof(rootFences));
        _riskAuthorizer = riskAuthorizer ?? throw new ArgumentNullException(nameof(riskAuthorizer));
        _reserveAuthority = reserveAuthority ?? throw new ArgumentNullException(nameof(reserveAuthority));
        _cooldowns = cooldowns ?? throw new ArgumentNullException(nameof(cooldowns));
        _entityGraph = entityGraph ?? throw new ArgumentNullException(nameof(entityGraph));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _kyc = kyc ?? throw new ArgumentNullException(nameof(kyc));
        _financialCrime = financialCrime ?? throw new ArgumentNullException(nameof(financialCrime));
        _trustSafety = trustSafety ?? throw new ArgumentNullException(nameof(trustSafety));
        _rollingReserve = rollingReserve ?? throw new ArgumentNullException(nameof(rollingReserve));
        _riskDecisions = riskDecisions ?? throw new ArgumentNullException(nameof(riskDecisions));
        _reauthentication = reauthentication ?? throw new ArgumentNullException(nameof(reauthentication));
        _providerEvidence = providerEvidence ?? throw new ArgumentNullException(nameof(providerEvidence));
        _anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
        _anchorVerifier = anchorVerifier ?? throw new ArgumentNullException(nameof(anchorVerifier));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
    }

    public async ValueTask<ConnectOnboardingResult> CreateOrRefreshConnectAccountAsync(
        Guid payeeId,
        CancellationToken cancellationToken = default)
    {
        if (payeeId == Guid.Empty) throw new ArgumentException("Payee ID is required.", nameof(payeeId));
        var result = await _provider.CreateOrRefreshAccountAsync(payeeId, cancellationToken).ConfigureAwait(false);
        ValidateAccountIdentity(result.Account, payeeId);
        return result;
    }

    public async ValueTask<PayoutOperation> ReserveAsync(
        PayoutReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var requestHash = RequestHash(request);
        var replay = _operations.FindReplay(request.TenantId, request.IdempotencyKey.Value, requestHash);
        if (replay is not null) return replay;
        _execution.EnsureEnabled();

        var account = await _provider.GetAccountAsync(request.PayeeId, cancellationToken).ConfigureAwait(false);
        ValidateReadyAccount(account, request, request.RequestedAt);
        var kyc = await _kyc.ReadAsync(request.PayeeId, request.RequestedAt, cancellationToken).ConfigureAwait(false);
        ValidateKyc(kyc, request.PayeeId, request.RequestedAt);
        var financialCrime = await _financialCrime
            .ReadAsync(request.AccountNode.IdentifierHash, request.RequestedAt, cancellationToken)
            .ConfigureAwait(false);
        var trustSafety = await _trustSafety
            .ReadAsync(request.AccountNode.IdentifierHash, request.RequestedAt, cancellationToken)
            .ConfigureAwait(false);
        var externalEvidence = ExternalRiskEvidenceValidator.RequireFreshAllow(
            [financialCrime.ToEvidence(), trustSafety.ToEvidence()], request.RequestedAt);
        var cooldown = _cooldowns.Evaluate(request.PayeeId, ProtectedChangeKind.PayoutDestination, request.RequestedAt);
        if (!cooldown.IsElapsed || !string.Equals(cooldown.Change.ValueHash, request.DestinationHash, StringComparison.Ordinal))
            throw new PayoutEligibilityException("Payout destination is still in protected-change review.");
        var cluster = _entityGraph.ClusterFor(request.AccountNode);
        if (!cluster.Nodes.Contains(request.DestinationNode))
            throw new PayoutEligibilityException("Payout destination is not bound to the current related-account graph.");
        var rolling = await _rollingReserve.ReadAsync(request.WalletId, request.RequestedAt, cancellationToken)
            .ConfigureAwait(false);
        ValidateRollingReserve(rolling, request.Amount, request.RequestedAt);

        var selection = SelectEligibleFragments(request);
        var providerBindingHash = ProviderBindingHash(account, kyc, externalEvidence, cooldown.Change, rolling);
        var eligibilityHash = EligibilityHash(request, selection, providerBindingHash, cluster);
        var context = new ProtectedOperationContext(
            request.IdempotencyKey,
            request.ActorId,
            PostingTemplateKind.PayoutReservation,
            request.WalletId,
            request.WalletId,
            request.Amount,
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, request.Amount.Units)],
            Roots(selection),
            providerBindingHash,
            request.PolicyVersion,
            request.ReserveVersion,
            request.FeatureVersion,
            _execution.Epoch,
            cluster.Version,
            cluster.EvidenceHash,
            rolling.Version,
            request.ReserveAuthorizationEpoch);
        var riskRequest = new PayoutRiskRequest(
            context, kyc, externalEvidence, rolling, account, cluster, eligibilityHash, request.RequestedAt);
        var decision = await _riskDecisions.DecideAsync(riskRequest, cancellationToken).ConfigureAwait(false);
        _riskAuthorizer.AuthorizeValueMovement(decision, context, request.RequestedAt);
        var reauthentication = await _reauthentication
            .ReadAsync(request.ActorId, context.Fingerprint(), request.RequestedAt, cancellationToken)
            .ConfigureAwait(false);
        ReauthenticationEvidenceValidator.RequireFresh(
            reauthentication,
            request.ActorId,
            ProtectedOperationKind.Payout,
            context.Fingerprint(),
            ReauthenticationAssurance.MultiFactor,
            request.RequestedAt);
        _reserveAuthority.Authorize(request.ReserveVersion, request.ReserveAuthorizationEpoch, request.RequestedAt);

        lock (_gate)
        {
            replay = _operations.FindReplay(request.TenantId, request.IdempotencyKey.Value, requestHash);
            if (replay is not null) return replay;
            var roots = Roots(selection);
            var rootSnapshot = _rootFences.Capture(roots);
            return _rootFences.WithAllocationFence(rootSnapshot, roots, () =>
            {
                var fencingToken = checked(++_nextFencingToken);
                var operation = _ledger.Execute(transaction =>
                {
                    transaction.EnsureWalletNotDebtRestricted(request.WalletId);
                    if (transaction.ActiveHoldUnits(request.WalletId, CurrencyCode.HardCoin) > 0)
                        throw new PayoutEligibilityException("Active holds block payout reservation.");
                    FragmentSelectionResult current;
                    try
                    {
                        current = SelectExactAvailable(transaction, request.WalletId, request.Amount, request.RequestedAt);
                    }
                    catch (InsufficientFragmentsException)
                    {
                        throw new PayoutStaleCommandException("Eligible payout fragments changed before reservation.");
                    }
                    if (!string.Equals(SelectionHash(current), SelectionHash(selection), StringComparison.Ordinal))
                        throw new PayoutStaleCommandException("Eligible payout fragments changed before reservation.");
                    var append = transaction.AppendJournal(ReservationPosting(request), request.RequestedAt);
                    foreach (var item in selection.Selections)
                        transaction.AddFragmentReservation(new ValueFragmentReservation(
                            Guid.NewGuid(), request.OperationId, FragmentReservationPurpose.Payout,
                            item.ParentLotId, request.WalletId, item.Amount, item.SelectedRanges,
                            1, fencingToken, _execution.Epoch, FragmentReservationStatus.Reserved,
                            request.RequestedAt, null));
                    transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                        new PostingId(request.OperationId), request.WalletId, CurrencyCode.HardCoin,
                        -request.Amount.Units, append.Entry.Sequence));
                    transaction.AddOutbox(new ImmutableOutboxMessage(
                        Guid.NewGuid(), "economy.payout.reserved.v1",
                        JsonSerializer.Serialize(new { request.OperationId, EligibilityHash = eligibilityHash }),
                        request.RequestedAt));
                    return new PayoutOperation(
                        request.OperationId, request.IdempotencyKey, requestHash, request.ActorId, request.PayeeId,
                        request.WalletId, request.Amount, account.ProviderAccountId, account.DestinationHash,
                        providerBindingHash, eligibilityHash, null, null, PayoutOperationState.Reserved, 1,
                        fencingToken, _execution.Epoch, request.ReserveVersion, request.ReserveAuthorizationEpoch,
                        request.PolicyVersion, decision.Id, request.RequestedAt, request.RequestedAt, request.TenantId);
                });
                _operations.Add(operation);
                return operation;
            });
        }
    }

    public async ValueTask<PayoutOperation> DispatchAsync(
        Guid operationId,
        long expectedVersion,
        long fencingToken,
        long killSwitchEpoch,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        _execution.EnsureEnabled();
        var operation = _operations.Get(operationId);
        EnsureCommandVersion(operation, expectedVersion, fencingToken, killSwitchEpoch);
        var reservations = _ledger.GetFragmentReservations(operationId);
        if (reservations.Count == 0)
            throw new PayoutStaleCommandException("Payout reservation fragments are missing.");
        if (reservations.All(item => item.Status == FragmentReservationStatus.Released))
            return _operations.Update(
                operation.Transition(PayoutOperationState.Cancelled, requestedAt), operation.Version);
        if (reservations.Any(item => item.Status != FragmentReservationStatus.Reserved))
            throw new PayoutStaleCommandException("Payout fragments are no longer reserved for dispatch.");
        if (_ledger.GetDebt(operation.WalletId).OutstandingHardUnits > 0 ||
            _ledger.GetActiveHolds(operation.WalletId).Any())
            throw new PayoutEligibilityException("Debt or an active hold blocks payout dispatch.");
        _reserveAuthority.Authorize(operation.ReserveVersion, operation.ReserveAuthorizationEpoch, requestedAt);
        var account = await _provider.GetAccountAsync(operation.PayeeId, cancellationToken).ConfigureAwait(false);
        ValidateDispatchAccount(account, operation, requestedAt);

        var roots = reservations.SelectMany(item => item.Ranges).Select(item => item.Root).Distinct().ToArray();
        var rootSnapshot = _rootFences.Capture(roots);
        PayoutOperation dispatching;
        string snapshotHash;
        lock (_gate)
        {
            dispatching = _rootFences.WithAllocationFence(rootSnapshot, roots, () =>
            {
                snapshotHash = DispatchSnapshotHash(operation, reservations, account, requestedAt);
                var anchor = _anchors.CreateOnDemand(snapshotHash, requestedAt);
                if (!_anchorVerifier.Verify(anchor) ||
                    !string.Equals(anchor.DispatchSnapshotHash, snapshotHash, StringComparison.Ordinal))
                    throw new PayoutEvidenceException("Independent dispatch anchor verification failed.");
                _ledger.Execute(transaction =>
                {
                    transaction.TransitionFragmentReservations(
                        operationId, FragmentReservationStatus.Reserved,
                        FragmentReservationStatus.Dispatching, requestedAt);
                    transaction.AddOutbox(new ImmutableOutboxMessage(
                        Guid.NewGuid(), "economy.payout.dispatch.v1",
                        JsonSerializer.Serialize(new { operationId, snapshotHash, fencingToken, killSwitchEpoch }),
                        requestedAt));
                    return 0;
                });
                var changed = operation.Transition(
                    PayoutOperationState.Dispatching, requestedAt, snapshotHash);
                return _operations.Update(changed, operation.Version);
            });
        }

        var receipt = await _provider.DispatchAsync(new PayoutDispatchCommand(
            operationId, dispatching.Version, fencingToken, killSwitchEpoch,
            operation.ProviderAccountId, operation.DestinationHash, operation.Amount,
            dispatching.DispatchSnapshotHash!, operation.IdempotencyKey.Value, requestedAt), cancellationToken)
            .ConfigureAwait(false);
        return ApplyDispatchReceipt(dispatching, receipt);
    }

    public ValueTask<PayoutOperation> ApplyProviderEventAsync(
        PayoutProviderEvent providerEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(providerEvent);
        var eventHash = ProviderEventHash(providerEvent);
        var replay = _operations.FindProviderEvent(providerEvent.EventId, eventHash);
        if (replay is not null) return ValueTask.FromResult(_operations.Get(replay.OperationId));
        if (!_providerEvidence.Verify(providerEvent))
            throw new PayoutEvidenceException("Provider payout event signature is invalid.");
        var operation = _operations.Get(providerEvent.OperationId);
        ValidateProviderEventBinding(providerEvent, operation);
        return ValueTask.FromResult(CompleteFromProviderEvent(operation, providerEvent, eventHash));
    }

    public async ValueTask<PayoutOperation> ReconcileAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var operation = _operations.Get(operationId);
        if (string.IsNullOrWhiteSpace(operation.ProviderPayoutId))
            throw new PayoutStaleCommandException("A provider payout ID is required before reconciliation.");
        var providerEvent = await _provider.ReconcileAsync(
            operationId, operation.ProviderPayoutId, cancellationToken).ConfigureAwait(false);
        return await ApplyProviderEventAsync(providerEvent, cancellationToken).ConfigureAwait(false);
    }

    private PayoutOperation ApplyDispatchReceipt(PayoutOperation operation, PayoutDispatchReceipt receipt)
    {
        if (!ReceiptMatches(receipt, operation) || !_providerEvidence.Verify(receipt))
        {
            var ambiguous = operation.Transition(
                PayoutOperationState.Ambiguous, receipt.ObservedAt,
                providerPayoutId: EmptyToNull(receipt.ProviderPayoutId));
            _operations.Update(ambiguous, operation.Version);
            throw new PayoutEvidenceException("Provider dispatch receipt is invalid or not bound to the payout.");
        }

        return receipt.Outcome switch
        {
            PayoutProviderOutcome.Submitted => _operations.Update(
                operation.BindProviderDispatch(receipt.ProviderPayoutId, receipt.ObservedAt), operation.Version),
            PayoutProviderOutcome.Ambiguous => _operations.Update(
                operation.Transition(PayoutOperationState.Ambiguous, receipt.ObservedAt,
                    providerPayoutId: receipt.ProviderPayoutId), operation.Version),
            PayoutProviderOutcome.Succeeded or PayoutProviderOutcome.Failed =>
                CompleteFromProviderEvent(operation, ReceiptAsEvent(receipt), ProviderReceiptHash(receipt)),
            _ => throw new PayoutEvidenceException("Unsupported provider payout outcome.")
        };
    }

    private PayoutOperation CompleteFromProviderEvent(
        PayoutOperation operation,
        PayoutProviderEvent providerEvent,
        string eventHash)
    {
        if (operation.State is not (PayoutOperationState.Dispatching or PayoutOperationState.Ambiguous))
            throw new PayoutStaleCommandException("Provider terminal event is out of order.");
        if (providerEvent.Outcome is not (PayoutProviderOutcome.Succeeded or PayoutProviderOutcome.Failed))
            throw new PayoutEvidenceException("Only terminal provider events can complete a payout.");
        var expectedStatus = FragmentReservationStatus.Dispatching;
        var nextStatus = providerEvent.Outcome == PayoutProviderOutcome.Succeeded
            ? FragmentReservationStatus.Consumed
            : FragmentReservationStatus.Released;
        var nextState = providerEvent.Outcome == PayoutProviderOutcome.Succeeded
            ? PayoutOperationState.Succeeded
            : PayoutOperationState.Failed;
        var postingKind = providerEvent.Outcome == PayoutProviderOutcome.Succeeded
            ? PostingTemplateKind.PayoutSuccess
            : PostingTemplateKind.PayoutFailure;
        var reservations = _ledger.GetFragmentReservations(operation.Id);
        if (reservations.Count == 0 || reservations.Any(item => item.Status != expectedStatus))
            throw new PayoutStaleCommandException("Payout fragments are not dispatching.");

        _ledger.Execute(transaction =>
        {
            var append = transaction.AppendJournal(
                TerminalPosting(operation, postingKind, providerEvent.ObservedAt), providerEvent.ObservedAt);
            if (nextStatus == FragmentReservationStatus.Consumed)
                foreach (var reservation in reservations)
                    transaction.AddConsumption(new FragmentConsumption(
                        DeterministicPostingId(operation.Id, "success"), reservation.LotId,
                        reservation.Amount, reservation.Ranges));
            else
                transaction.AddProjectionUpdate(new WalletProjectionUpdate(
                    DeterministicPostingId(operation.Id, "failure"), operation.WalletId,
                    CurrencyCode.HardCoin, operation.Amount.Units, append.Entry.Sequence));
            transaction.TransitionFragmentReservations(
                operation.Id, expectedStatus, nextStatus, providerEvent.ObservedAt);
            transaction.AddOutbox(new ImmutableOutboxMessage(
                Guid.NewGuid(), "economy.payout.terminal.v1",
                JsonSerializer.Serialize(new { operation.Id, providerEvent.EventId, providerEvent.Outcome }),
                providerEvent.ObservedAt));
            return 0;
        });
        var changed = operation.Transition(
            nextState, providerEvent.ObservedAt, providerPayoutId: providerEvent.ProviderPayoutId);
        _operations.RecordProviderEvent(
            providerEvent.EventId, eventHash, changed, operation.Version, providerEvent.ObservedAt);
        return changed;
    }

    private FragmentSelectionResult SelectEligibleFragments(PayoutReservationRequest request)
    {
        var holds = _ledger.GetActiveHolds(request.WalletId);
        var debt = _ledger.GetDebt(request.WalletId);
        var restriction = new WalletRestrictionSnapshot(request.WalletId, request.WalletState, debt.OutstandingHardUnits);
        var eligible = _ledger.GetAvailableLots(request.WalletId, CurrencyCode.HardCoin)
            .Where(lot => PayoutEligibilityEvaluator.Evaluate(lot, request.RequestedAt, holds, restriction).IsEligible &&
                          IsAuthoritativelyConfirmed(lot, root => _ledger.SourceEvidenceHistory
                              .LastOrDefault(source => source.Id == root)))
            .ToArray();
        return FifoFragmentSelector.Select(eligible, request.Amount);
    }

    private static FragmentSelectionResult SelectExactAvailable(
        LedgerKernelTransaction transaction,
        WalletId walletId,
        CoinAmount amount,
        DateTimeOffset asOf)
    {
        var eligible = transaction.GetAvailableLots(walletId, CurrencyCode.HardCoin)
            .Where(lot => lot.Provenance == ProvenanceKind.EarnedHard &&
                          lot.State == CreditLotState.Active &&
                          lot.OriginalMaturesAt <= asOf &&
                          IsAuthoritativelyConfirmed(lot, transaction.LatestSource))
            .ToArray();
        return FifoFragmentSelector.Select(eligible, amount);
    }

    private static bool IsAuthoritativelyConfirmed(
        CreditLot lot,
        Func<SourceStampId, SourceEvidence?> sourceLookup) =>
        lot.Ranges.Select(range => range.Root).Distinct().All(root =>
        {
            var source = sourceLookup(root);
            return source is { State: SourceConfirmationState.Confirmed, ConfirmedAt: not null } &&
                   source.ConfirmedAt.Value <= lot.ConfirmedAt;
        });

    private static PostingRequest ReservationPosting(PayoutReservationRequest request) => new(
        new PostingId(request.OperationId),
        new PostingTemplate(PostingTemplateKind.PayoutReservation, PostingTemplate.CurrentVersion),
        request.IdempotencyKey,
        PostingAuthority.PayoutCoordinator,
        request.ReserveVersion,
        request.PolicyVersion,
        null,
        request.RequestedAt,
        [
            new PostingLine(1, EntrySide.Debit, EconomyAccountCode.EarnedHardLiability,
                request.Amount, request.WalletId, null, ProvenanceKind.EarnedHard),
            new PostingLine(2, EntrySide.Credit, EconomyAccountCode.PayoutPayableHard,
                request.Amount, null, null, null)
        ]);

    private static PostingRequest TerminalPosting(
        PayoutOperation operation,
        PostingTemplateKind kind,
        DateTimeOffset requestedAt) => new(
        DeterministicPostingId(operation.Id, kind == PostingTemplateKind.PayoutSuccess ? "success" : "failure"),
        new PostingTemplate(kind, PostingTemplate.CurrentVersion),
        new IdempotencyKey($"{operation.IdempotencyKey.Value}:{kind}"),
        PostingAuthority.PayoutCoordinator,
        operation.ReserveVersion,
        operation.PolicyVersion,
        null,
        requestedAt,
        kind == PostingTemplateKind.PayoutSuccess
            ?
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PayoutPayableHard,
                    operation.Amount, null, null, null),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard,
                    operation.Amount, null, null, null)
            ]
            :
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PayoutPayableHard,
                    operation.Amount, null, null, null),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.EarnedHardLiability,
                    operation.Amount, operation.WalletId, null, ProvenanceKind.EarnedHard)
            ]);

    private static void ValidateRequest(PayoutReservationRequest request)
    {
        if (request.OperationId == Guid.Empty || request.TenantId == Guid.Empty ||
            request.ActorId == Guid.Empty || request.PayeeId == Guid.Empty)
            throw new ArgumentException("Operation, tenant, actor, and payee identities are required.", nameof(request));
        if (request.Amount.Currency != CurrencyCode.HardCoin || request.Amount.Units <= 0)
            throw new PayoutEligibilityException("Payouts require a positive hard-coin amount.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExpectedProviderAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationHash);
        if (request.AccountNode.Type != RiskEntityType.Account ||
            request.DestinationNode.Type != RiskEntityType.PayoutDestination)
            throw new ArgumentException("Payout risk nodes must identify the account and destination.", nameof(request));
    }

    private static void ValidateAccountIdentity(ConnectAccountSnapshot account, Guid payeeId)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.PayeeId != payeeId)
            throw new PayoutProviderBindingException("Connect account is not bound to the requested payee.");
    }

    private static void ValidateReadyAccount(
        ConnectAccountSnapshot account,
        PayoutReservationRequest request,
        DateTimeOffset now)
    {
        ValidateAccountIdentity(account, request.PayeeId);
        if (account.State != ConnectAccountState.Ready || !account.ChargesEnabled || !account.PayoutsEnabled ||
            account.Version <= 0 || account.ObservedAt > now || account.ExpiresAt <= now ||
            string.IsNullOrWhiteSpace(account.EvidenceHash))
            throw new PayoutEligibilityException("Connect account is not ready for payout.");
        if (!string.Equals(account.ProviderAccountId, request.ExpectedProviderAccountId, StringComparison.Ordinal) ||
            !string.Equals(account.DestinationHash, request.DestinationHash, StringComparison.Ordinal))
            throw new PayoutProviderBindingException("Connect account or payout destination binding changed.");
    }

    private static void ValidateDispatchAccount(
        ConnectAccountSnapshot account,
        PayoutOperation operation,
        DateTimeOffset now)
    {
        ValidateAccountIdentity(account, operation.PayeeId);
        if (account.State != ConnectAccountState.Ready || !account.PayoutsEnabled || account.ExpiresAt <= now ||
            !string.Equals(account.ProviderAccountId, operation.ProviderAccountId, StringComparison.Ordinal) ||
            !string.Equals(account.DestinationHash, operation.DestinationHash, StringComparison.Ordinal))
            throw new PayoutProviderBindingException("Connect payout binding is stale at dispatch.");
    }

    private static void ValidateKyc(PayoutKycSnapshot kyc, Guid payeeId, DateTimeOffset now)
    {
        if (kyc.PayeeId != payeeId || kyc.Version <= 0 || !kyc.IsApproved || kyc.ObservedAt > now ||
            kyc.ExpiresAt <= now || string.IsNullOrWhiteSpace(kyc.EvidenceHash))
            throw new PayoutEligibilityException("Fresh approved KYC evidence is required for payout.");
    }

    private static void ValidateRollingReserve(
        PayoutRollingReserveSnapshot reserve,
        CoinAmount amount,
        DateTimeOffset now)
    {
        if (reserve.Version <= 0 || reserve.EligibleHardUnits < 0 || reserve.ReservedHardUnits < 0 ||
            reserve.ReserveBasisPoints is < 0 or > 10_000 || reserve.ObservedAt > now || reserve.ExpiresAt <= now ||
            string.IsNullOrWhiteSpace(reserve.EvidenceHash))
            throw new PayoutEligibilityException("Rolling-reserve evidence is invalid or stale.");
        if (amount.Units > reserve.ReleasableHardUnits)
            throw new PayoutEligibilityException("Rolling-reserve policy blocks the requested payout amount.");
    }

    private static void EnsureCommandVersion(
        PayoutOperation operation,
        long expectedVersion,
        long fencingToken,
        long killSwitchEpoch)
    {
        if (operation.State != PayoutOperationState.Reserved || operation.Version != expectedVersion ||
            operation.FencingToken != fencingToken || operation.KillSwitchEpoch != killSwitchEpoch)
            throw new PayoutStaleCommandException("Payout dispatch command is stale or fenced.");
    }

    private static void ValidateProviderEventBinding(PayoutProviderEvent providerEvent, PayoutOperation operation)
    {
        if (!string.Equals(providerEvent.ProviderAccountId, operation.ProviderAccountId, StringComparison.Ordinal) ||
            !string.Equals(providerEvent.DestinationHash, operation.DestinationHash, StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(operation.ProviderPayoutId) &&
             !string.Equals(providerEvent.ProviderPayoutId, operation.ProviderPayoutId, StringComparison.Ordinal)))
            throw new PayoutProviderBindingException("Provider payout event is not bound to this operation.");
    }

    private static bool ReceiptMatches(PayoutDispatchReceipt receipt, PayoutOperation operation) =>
        receipt.OperationId == operation.Id &&
        string.Equals(receipt.ProviderAccountId, operation.ProviderAccountId, StringComparison.Ordinal) &&
        string.Equals(receipt.DestinationHash, operation.DestinationHash, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(receipt.ProviderPayoutId);

    private static PayoutProviderEvent ReceiptAsEvent(PayoutDispatchReceipt receipt) => new(
        $"dispatch:{receipt.ProviderPayoutId}:{receipt.Outcome}",
        receipt.OperationId,
        receipt.Outcome,
        receipt.ProviderPayoutId,
        receipt.ProviderAccountId,
        receipt.DestinationHash,
        receipt.EvidenceHash,
        receipt.Signature,
        receipt.ObservedAt);

    private static string RequestHash(PayoutReservationRequest request) => Hash(
        request.OperationId.ToString("N"), request.IdempotencyKey.Value, request.ActorId.ToString("N"),
        request.PayeeId.ToString("N"), request.WalletId.Value.ToString("N"),
        request.Amount.Units.ToString(CultureInfo.InvariantCulture),
        request.ExpectedProviderAccountId, request.DestinationHash,
        request.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
        request.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
        request.ReserveAuthorizationEpoch.ToString(CultureInfo.InvariantCulture),
        request.FeatureVersion.ToString(CultureInfo.InvariantCulture),
        request.RequestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string ProviderBindingHash(
        ConnectAccountSnapshot account,
        PayoutKycSnapshot kyc,
        IReadOnlyList<ExternalRiskEvidence> externalEvidence,
        ProtectedChangeCooldown cooldown,
        PayoutRollingReserveSnapshot rolling) => Hash(
        account.PayeeId.ToString("N"), account.ProviderAccountId, account.DestinationHash,
        account.Version.ToString(CultureInfo.InvariantCulture), account.EvidenceHash,
        kyc.Version.ToString(CultureInfo.InvariantCulture), kyc.EvidenceHash,
        string.Join(',', externalEvidence.OrderBy(item => item.Source).Select(item => item.EvidenceHash)),
        cooldown.Version.ToString(CultureInfo.InvariantCulture), cooldown.ValueHash,
        rolling.Version.ToString(CultureInfo.InvariantCulture), rolling.EvidenceHash);

    private static string EligibilityHash(
        PayoutReservationRequest request,
        FragmentSelectionResult selection,
        string providerBindingHash,
        EntityRiskCluster cluster) => Hash(
        RequestHash(request), providerBindingHash, cluster.Id,
        cluster.Version.ToString(CultureInfo.InvariantCulture), cluster.EvidenceHash, SelectionHash(selection));

    private string DispatchSnapshotHash(
        PayoutOperation operation,
        IReadOnlyList<ValueFragmentReservation> reservations,
        ConnectAccountSnapshot account,
        DateTimeOffset requestedAt)
    {
        var head = _ledger.JournalEntries.LastOrDefault()
            ?? throw new PayoutEvidenceException("Payout dispatch requires an eligibility journal head.");
        var holds = _ledger.GetActiveHolds(operation.WalletId);
        var debt = _ledger.GetDebt(operation.WalletId);
        return Hash(
            operation.Id.ToString("N"), operation.EligibilityHash, operation.ProviderBindingHash,
            operation.Amount.Units.ToString(CultureInfo.InvariantCulture),
            account.ProviderAccountId, account.DestinationHash, account.EvidenceHash,
            ReservationHash(reservations),
            holds.Count.ToString(CultureInfo.InvariantCulture),
            debt.OutstandingHardUnits.ToString(CultureInfo.InvariantCulture),
            operation.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
            operation.ReserveAuthorizationEpoch.ToString(CultureInfo.InvariantCulture),
            operation.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
            head.Sequence.ToString(CultureInfo.InvariantCulture), head.Hash,
            operation.Version.ToString(CultureInfo.InvariantCulture),
            operation.FencingToken.ToString(CultureInfo.InvariantCulture),
            operation.KillSwitchEpoch.ToString(CultureInfo.InvariantCulture),
            requestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    private static string SelectionHash(FragmentSelectionResult selection) => Hash(
        selection.Selections.Select(item => string.Join(':',
            item.ParentLotId.Value.ToString("N"),
            item.Amount.Units.ToString(CultureInfo.InvariantCulture),
            string.Join(',', item.SelectedRanges.Select(RangeCanonical)))).ToArray());

    private static string ReservationHash(IReadOnlyList<ValueFragmentReservation> reservations) => Hash(
        reservations.OrderBy(item => item.Id).Select(item => string.Join(':',
            item.Id.ToString("N"), item.LotId.Value.ToString("N"),
            item.Amount.Units.ToString(CultureInfo.InvariantCulture),
            string.Join(',', item.Ranges.Select(RangeCanonical)),
            item.OperationVersion.ToString(CultureInfo.InvariantCulture),
            item.FencingToken.ToString(CultureInfo.InvariantCulture),
            item.KillSwitchEpoch.ToString(CultureInfo.InvariantCulture))).ToArray());

    private static string RangeCanonical(RootTraceRange range) => string.Join('-',
        range.Root.Value.ToString("N"),
        range.Start.ToString(CultureInfo.InvariantCulture),
        range.Length.ToString(CultureInfo.InvariantCulture),
        range.Epoch.ToString(CultureInfo.InvariantCulture));

    private static SourceStampId[] Roots(FragmentSelectionResult selection) =>
        [.. selection.Selections.SelectMany(item => item.SelectedRanges).Select(item => item.Root).Distinct()];

    private static string ProviderEventHash(PayoutProviderEvent providerEvent) => Hash(
        providerEvent.EventId, providerEvent.OperationId.ToString("N"),
        ((int)providerEvent.Outcome).ToString(CultureInfo.InvariantCulture),
        providerEvent.ProviderPayoutId, providerEvent.ProviderAccountId, providerEvent.DestinationHash,
        providerEvent.EvidenceHash, providerEvent.Signature,
        providerEvent.ObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string ProviderReceiptHash(PayoutDispatchReceipt receipt) => Hash(
        receipt.OperationId.ToString("N"), ((int)receipt.Outcome).ToString(CultureInfo.InvariantCulture),
        receipt.ProviderPayoutId, receipt.ProviderAccountId, receipt.DestinationHash,
        receipt.EvidenceHash, receipt.Signature,
        receipt.ObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static PostingId DeterministicPostingId(Guid operationId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId:N}:{purpose}"));
        return new PostingId(new Guid(bytes.AsSpan(0, 16)));
    }

    private static string Hash(params string[] values)
    {
        var canonical = string.Join('|', values.Select(value => $"{value.Length}:{value}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

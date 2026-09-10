using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury;

public sealed class AdminWithdrawalCoordinator
{
    private const long UsdNanosPerCent = 10_000_000;
    private readonly object _gate = new();
    private readonly InMemoryLedgerKernelStore _ledger;
    private readonly IAdminWithdrawalStore _operations;
    private readonly RootReversalFenceRegistry _rootFences;
    private readonly TreasuryOperationGate _treasuryGate;
    private readonly CoreReserveAuthority _reserveAuthority;
    private readonly IAdminWithdrawalProvider _provider;
    private readonly IAdminWithdrawalProviderEvidenceVerifier _providerEvidence;
    private readonly IAdminWithdrawalAuditTrail _audit;
    private readonly AdminWithdrawalExecutionGate _execution;
    private long _nextFencingToken;

    public AdminWithdrawalCoordinator(
        InMemoryLedgerKernelStore ledger,
        IAdminWithdrawalStore operations,
        RootReversalFenceRegistry rootFences,
        TreasuryOperationGate treasuryGate,
        CoreReserveAuthority reserveAuthority,
        IAdminWithdrawalProvider provider,
        IAdminWithdrawalProviderEvidenceVerifier providerEvidence,
        IAdminWithdrawalAuditTrail audit,
        AdminWithdrawalExecutionGate execution)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _rootFences = rootFences ?? throw new ArgumentNullException(nameof(rootFences));
        _treasuryGate = treasuryGate ?? throw new ArgumentNullException(nameof(treasuryGate));
        _reserveAuthority = reserveAuthority ?? throw new ArgumentNullException(nameof(reserveAuthority));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _providerEvidence = providerEvidence ?? throw new ArgumentNullException(nameof(providerEvidence));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
    }

    public AdminWithdrawalRun ReserveMonthlyRun(AdminWithdrawalReservationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var requestHash = RequestHash(request);

        lock (_gate)
        {
            var replay = _operations.FindReplay(request.IdempotencyKey.Value, requestHash);
            if (replay is not null) return replay;
            if (_operations.FindPeriod(request.PeriodStart) is not null)
                throw new AdminWithdrawalOverlapException("A withdrawal run already owns this monthly period.");
            EnsureNoActiveHold(request.PlatformFeeWalletId);
            var eligible = EligibleLots(
                _ledger.GetAvailableLots(request.PlatformFeeWalletId, CurrencyCode.HardCoin),
                root => _ledger.SourceEvidenceHistory.LastOrDefault(source => source.Id == root),
                request.RequestedAt);
            var total = SumUnits(eligible);
            if (total <= 0)
                throw new AdminWithdrawalEligibilityException(
                    "No mature, confirmed, unheld platform fee fragments are eligible for withdrawal.");
            var amount = new CoinAmount(CurrencyCode.HardCoin, total);
            var selection = FifoFragmentSelector.Select(eligible, amount);
            var roots = Roots(selection);
            var rootSnapshot = _rootFences.Capture(roots);
            var fencingToken = checked(++_nextFencingToken);
            var run = new AdminWithdrawalRun(
                request.RunId, request.TenantId, request.IdempotencyKey, requestHash, request.PeriodStart,
                request.RequestedBy, null, request.PlatformFeeWalletId, amount,
                request.SourceAssetKey.Trim(), request.DestinationHash.Trim(),
                AdminWithdrawalRunState.PendingApproval, 1, fencingToken, _execution.Epoch,
                request.ReserveVersion, request.ReserveAuthorizationEpoch, request.PolicyVersion,
                null, null, request.RequestedAt, request.RequestedAt);

            _rootFences.WithAllocationFence(rootSnapshot, roots, () =>
            {
                _ledger.Execute(transaction =>
                {
                    var current = EligibleLots(
                        transaction.GetAvailableLots(request.PlatformFeeWalletId, CurrencyCode.HardCoin),
                        transaction.LatestSource,
                        request.RequestedAt);
                    var currentSelection = FifoFragmentSelector.Select(current, amount);
                    AdminWithdrawalReservationSnapshotGuard.EnsureUnchanged(
                        transaction.ActiveHoldUnits(request.PlatformFeeWalletId, CurrencyCode.HardCoin),
                        SelectionHash(selection),
                        SelectionHash(currentSelection));
                    transaction.AppendJournal(ReservationPosting(run), request.RequestedAt);
                    foreach (var item in selection.Selections)
                        transaction.AddFragmentReservation(new ValueFragmentReservation(
                            Guid.NewGuid(), run.Id, FragmentReservationPurpose.AdminWithdrawal,
                            item.ParentLotId, run.PlatformFeeWalletId, item.Amount, item.SelectedRanges,
                            1, fencingToken, run.ExecutionEpoch, FragmentReservationStatus.Reserved,
                            request.RequestedAt, null));
                    transaction.AddOutbox(new ImmutableOutboxMessage(
                        Guid.NewGuid(), "economy.admin-withdrawal.reserved.v1",
                        JsonSerializer.Serialize(new { run.Id, run.PeriodStart, run.Amount.Units, requestHash }),
                        request.RequestedAt));
                    return 0;
                });
                return 0;
            });
            _operations.Add(run);
            _audit.Append(run.Id, "reserved", request.RequestedBy, SelectionHash(selection), request.RequestedAt);
            return run;
        }
    }

    public AdminWithdrawalRun Approve(
        Guid runId,
        long expectedVersion,
        Guid approvedBy,
        DateTimeOffset approvedAt)
    {
        if (approvedBy == Guid.Empty) throw new ArgumentException("Approver ID is required.", nameof(approvedBy));
        lock (_gate)
        {
            var run = _operations.Get(runId);
            if (run.Version != expectedVersion || run.State != AdminWithdrawalRunState.PendingApproval)
                throw new AdminWithdrawalStaleCommandException("The withdrawal approval command is stale.");
            if (run.RequestedBy == approvedBy)
                throw new AdminWithdrawalApprovalException(
                    "The withdrawal requester cannot approve the same run.");
            var approved = run with
            {
                ApprovedBy = approvedBy,
                State = AdminWithdrawalRunState.Approved,
                Version = checked(run.Version + 1),
                UpdatedAt = approvedAt
            };
            _operations.Update(approved, run.Version);
            _audit.Append(run.Id, "approved", approvedBy, run.RequestHash, approvedAt);
            return approved;
        }
    }

    public async ValueTask<AdminWithdrawalRun> DispatchAsync(
        Guid runId,
        long expectedVersion,
        long fencingToken,
        long executionEpoch,
        TreasuryCustodyReport custody,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        _execution.EnsureEnabled();
        ArgumentNullException.ThrowIfNull(custody);
        AdminWithdrawalRun dispatching;
        string snapshotHash;
        lock (_gate)
        {
            var run = _operations.Get(runId);
            EnsureDispatchCommand(run, expectedVersion, fencingToken, executionEpoch);
            var reservations = RequireReservations(run.Id, FragmentReservationStatus.Reserved);
            EnsureNoActiveHold(run.PlatformFeeWalletId);
            _treasuryGate.Authorize(
                TreasuryProtectedOperation.AdminWithdrawal,
                run.ReserveVersion,
                run.ReserveAuthorizationEpoch,
                custody,
                null,
                requestedAt);
            EnsurePostWithdrawalCoverage(run, custody);
            var roots = reservations.SelectMany(item => item.Ranges).Select(item => item.Root).Distinct().ToArray();
            var rootSnapshot = _rootFences.Capture(roots);
            snapshotHash = DispatchSnapshotHash(run, reservations, custody, requestedAt);
            dispatching = _rootFences.WithAllocationFence(rootSnapshot, roots, () =>
            {
                _ledger.Execute(transaction =>
                {
                    transaction.TransitionFragmentReservations(
                        run.Id, FragmentReservationStatus.Reserved,
                        FragmentReservationStatus.Dispatching, requestedAt);
                    transaction.AddOutbox(new ImmutableOutboxMessage(
                        Guid.NewGuid(), "economy.admin-withdrawal.dispatch.v1",
                        JsonSerializer.Serialize(new { run.Id, snapshotHash, fencingToken, executionEpoch }),
                        requestedAt));
                    return 0;
                });
                var changed = run with
                {
                    State = AdminWithdrawalRunState.Dispatching,
                    Version = checked(run.Version + 1),
                    DispatchSnapshotHash = snapshotHash,
                    UpdatedAt = requestedAt
                };
                return _operations.Update(changed, run.Version);
            });
            _audit.Append(run.Id, "dispatching", run.ApprovedBy, snapshotHash, requestedAt);
        }

        AdminWithdrawalProviderReceipt receipt;
        try
        {
            receipt = await _provider.DispatchAsync(new AdminWithdrawalDispatchCommand(
                runId, dispatching.TenantId, dispatching.Version, fencingToken, executionEpoch,
                dispatching.Amount, dispatching.SourceAssetKey, dispatching.DestinationHash,
                snapshotHash, dispatching.IdempotencyKey.Value, requestedAt), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            lock (_gate)
            {
                var current = _operations.Get(runId);
                var ambiguous = Transition(current, AdminWithdrawalRunState.Ambiguous, requestedAt);
                _operations.Update(ambiguous, current.Version);
                _audit.Append(runId, "provider-timeout", null, snapshotHash, requestedAt);
                return ambiguous;
            }
        }

        return ApplyReceipt(dispatching, receipt);
    }

    public async ValueTask<AdminWithdrawalRun> ReconcileAsync(
        Guid runId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        var run = _operations.Get(runId);
        if (run.State is not (AdminWithdrawalRunState.Dispatching or AdminWithdrawalRunState.Ambiguous))
            throw new AdminWithdrawalStaleCommandException(
                "Only an in-flight or ambiguous withdrawal can be reconciled.");
        var providerEvent = await _provider.ReconcileAsync(
            run.TenantId, run.Id, run.IdempotencyKey.Value, run.ProviderTransferId, cancellationToken).ConfigureAwait(false);
        return ApplyProviderEvent(providerEvent, requestedAt);
    }

    public AdminWithdrawalRun ApplyProviderEvent(
        AdminWithdrawalProviderEvent providerEvent,
        DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(providerEvent);
        if (string.IsNullOrWhiteSpace(providerEvent.EventId))
            throw new AdminWithdrawalEvidenceException("Provider withdrawal event ID is required.");
        var eventHash = ProviderEventHash(providerEvent);
        var replay = _operations.FindProviderEvent(providerEvent.EventId, eventHash);
        if (replay.HasValue) return _operations.Get(replay.Value);
        if (!_providerEvidence.Verify(providerEvent))
            throw new AdminWithdrawalEvidenceException("Provider withdrawal event signature is invalid.");
        var run = _operations.Get(providerEvent.RunId);
        ValidateProviderEvent(providerEvent, run);
        if (providerEvent.Outcome is not (AdminWithdrawalProviderOutcome.Succeeded or
            AdminWithdrawalProviderOutcome.Failed))
            throw new AdminWithdrawalEvidenceException(
                "Only a terminal provider event can complete an admin withdrawal.");
        return Complete(run, providerEvent, eventHash, requestedAt);
    }

    private AdminWithdrawalRun ApplyReceipt(
        AdminWithdrawalRun dispatching,
        AdminWithdrawalProviderReceipt receipt)
    {
        if (!_providerEvidence.Verify(receipt) || !ReceiptMatches(receipt, dispatching))
        {
            lock (_gate)
            {
                var current = _operations.Get(dispatching.Id);
                var ambiguous = Transition(current, AdminWithdrawalRunState.Ambiguous, receipt.ObservedAt);
                _operations.Update(ambiguous, current.Version);
                _audit.Append(current.Id, "invalid-provider-receipt", null,
                    string.IsNullOrWhiteSpace(receipt.EvidenceHash)
                        ? "missing-provider-evidence"
                        : receipt.EvidenceHash,
                    receipt.ObservedAt);
            }
            throw new AdminWithdrawalEvidenceException(
                "Provider withdrawal receipt is invalid or not bound to the run.");
        }

        if (receipt.Outcome is AdminWithdrawalProviderOutcome.Succeeded or AdminWithdrawalProviderOutcome.Failed)
        {
            var providerEvent = new AdminWithdrawalProviderEvent(
                $"dispatch:{receipt.ProviderTransferId}:{receipt.Outcome}",
                receipt.RunId, receipt.TenantId, receipt.Outcome, receipt.ProviderTransferId,
                receipt.FencingToken, receipt.ExecutionEpoch, receipt.Amount,
                receipt.SourceAssetKey, receipt.DestinationHash,
                receipt.EvidenceHash, receipt.Signature, receipt.ObservedAt);
            return Complete(dispatching, providerEvent, ProviderReceiptHash(receipt), receipt.ObservedAt);
        }

        lock (_gate)
        {
            var current = _operations.Get(dispatching.Id);
            var next = current with
            {
                State = receipt.Outcome == AdminWithdrawalProviderOutcome.Ambiguous
                    ? AdminWithdrawalRunState.Ambiguous
                    : AdminWithdrawalRunState.Dispatching,
                ProviderTransferId = receipt.ProviderTransferId,
                Version = checked(current.Version + 1),
                UpdatedAt = receipt.ObservedAt
            };
            _operations.Update(next, current.Version);
            _audit.Append(next.Id, "provider-receipt", null,
                ProviderReceiptHash(receipt), receipt.ObservedAt);
            return next;
        }
    }

    private AdminWithdrawalRun Complete(
        AdminWithdrawalRun run,
        AdminWithdrawalProviderEvent providerEvent,
        string eventHash,
        DateTimeOffset requestedAt)
    {
        lock (_gate)
        {
            var current = _operations.Get(run.Id);
            if (current.State is not (AdminWithdrawalRunState.Dispatching or AdminWithdrawalRunState.Ambiguous))
                throw new AdminWithdrawalStaleCommandException(
                    "Provider terminal evidence is out of order.");
            var reservations = RequireReservations(current.Id, FragmentReservationStatus.Dispatching);
            var succeeded = providerEvent.Outcome == AdminWithdrawalProviderOutcome.Succeeded;
            var postingKind = succeeded
                ? PostingTemplateKind.AdminWithdrawalSuccess
                : PostingTemplateKind.AdminWithdrawalFailure;
            var nextReservationState = succeeded
                ? FragmentReservationStatus.Consumed
                : FragmentReservationStatus.Released;
            _ledger.Execute(transaction =>
            {
                transaction.AppendJournal(TerminalPosting(current, postingKind, requestedAt), requestedAt);
                if (succeeded)
                    foreach (var reservation in reservations)
                        transaction.AddConsumption(new FragmentConsumption(
                            DeterministicPostingId(current.Id, "success"), reservation.LotId,
                            reservation.Amount, reservation.Ranges));
                transaction.TransitionFragmentReservations(
                    current.Id, FragmentReservationStatus.Dispatching,
                    nextReservationState, requestedAt);
                transaction.AddOutbox(new ImmutableOutboxMessage(
                    Guid.NewGuid(), "economy.admin-withdrawal.terminal.v1",
                    JsonSerializer.Serialize(new { current.Id, providerEvent.EventId, providerEvent.Outcome }),
                    requestedAt));
                return 0;
            });
            var terminal = current with
            {
                State = succeeded ? AdminWithdrawalRunState.Succeeded : AdminWithdrawalRunState.Failed,
                ProviderTransferId = providerEvent.ProviderTransferId,
                Version = checked(current.Version + 1),
                UpdatedAt = requestedAt
            };
            _operations.RecordProviderEvent(
                providerEvent.EventId, eventHash, terminal, current.Version);
            _audit.Append(current.Id, succeeded ? "succeeded" : "failed", null, eventHash, requestedAt);
            return terminal;
        }
    }

    private void EnsurePostWithdrawalCoverage(
        AdminWithdrawalRun run,
        TreasuryCustodyReport custody)
    {
        var head = _reserveAuthority.ActiveHead ??
                   throw new ReserveAuthorizationException("No authoritative reserve head is active.");
        if (head.Version != run.ReserveVersion ||
            head.AuthorizationEpoch != run.ReserveAuthorizationEpoch)
            throw new AdminWithdrawalStaleCommandException(
                "The active reserve head changed before admin withdrawal dispatch.");
        var sourceAsset = head.AssetAllocations.SingleOrDefault(asset =>
            string.Equals(asset.AssetKey, run.SourceAssetKey, StringComparison.Ordinal));
        if (sourceAsset is null || sourceAsset.Purpose != ReserveBackingPurpose.HardCoin)
            throw new AdminWithdrawalEligibilityException(
                "The withdrawal source is not an allocated hard-reserve asset.");
        var withdrawalNanos = checked(run.Amount.Units * UsdNanosPerCent);
        var sourceCustody = custody.Variances.SingleOrDefault(item =>
            string.Equals(item.AssetKey, run.SourceAssetKey, StringComparison.Ordinal));
        if (sourceCustody is null || sourceCustody.ActualUsdNanos < withdrawalNanos ||
            sourceAsset.EligibleUsdNanos < withdrawalNanos)
            throw new ReserveShortfallException(
                "The selected custody asset cannot fund the admin withdrawal.");
        var requiredHardNanos = checked(head.Requirements.RequiredHardReserveUsdMinor * UsdNanosPerCent);
        if (head.HardBackingUsdNanos - withdrawalNanos < requiredHardNanos)
            throw new ReserveShortfallException(
                "The admin withdrawal would reduce hard-reserve backing below policy.");
    }

    private IReadOnlyList<ValueFragmentReservation> RequireReservations(
        Guid runId,
        FragmentReservationStatus status)
    {
        var reservations = _ledger.GetFragmentReservations(runId);
        if (reservations.Count == 0 || reservations.Any(item =>
                item.Purpose != FragmentReservationPurpose.AdminWithdrawal || item.Status != status))
            throw new AdminWithdrawalStaleCommandException(
                "Admin withdrawal fragment reservations are missing or stale.");
        return reservations;
    }

    private void EnsureNoActiveHold(WalletId walletId)
    {
        if (_ledger.GetActiveHolds(walletId).Any(hold => hold.Amount.Currency == CurrencyCode.HardCoin))
            throw new AdminWithdrawalEligibilityException(
                "An active hold blocks platform fee withdrawal.");
    }

    private static CreditLot[] EligibleLots(
        IEnumerable<CreditLot> lots,
        Func<SourceStampId, SourceEvidence?> sourceLookup,
        DateTimeOffset asOf) => lots
        .Where(lot => lot.Provenance == ProvenanceKind.EarnedHard &&
                      lot.State == CreditLotState.Active &&
                      lot.OriginalMaturesAt <= asOf &&
                      lot.Ranges.Select(range => range.Root).Distinct().All(root =>
                      {
                          var source = sourceLookup(root);
                          return source is { State: SourceConfirmationState.Confirmed, ConfirmedAt: not null } &&
                                 source.ConfirmedAt.Value <= lot.ConfirmedAt;
                      }))
        .OrderBy(lot => lot.ConfirmedAt)
        .ThenBy(lot => lot.JournalSequence)
        .ThenBy(lot => lot.Id.Value)
        .ToArray();

    private static void ValidateRequest(AdminWithdrawalReservationRequest request)
    {
        if (request.RunId == Guid.Empty || request.TenantId == Guid.Empty || request.RequestedBy == Guid.Empty)
            throw new ArgumentException("Run, tenant, and requester identities are required.", nameof(request));
        if (request.PeriodStart.Day != 1)
            throw new ArgumentException("Withdrawal period must start on the first day of a month.", nameof(request));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ReserveAuthorizationEpoch);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceAssetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationHash);
    }

    private void EnsureDispatchCommand(
        AdminWithdrawalRun run,
        long expectedVersion,
        long fencingToken,
        long executionEpoch)
    {
        if (run.State != AdminWithdrawalRunState.Approved) ThrowStaleDispatch();
        if (run.Version != expectedVersion) ThrowStaleDispatch();
        if (run.FencingToken != fencingToken) ThrowStaleDispatch();
        if (run.ExecutionEpoch != executionEpoch) ThrowStaleDispatch();
        if (_execution.Epoch != executionEpoch) ThrowStaleDispatch();
        if (!run.ApprovedBy.HasValue) ThrowStaleDispatch();
        if (run.ApprovedBy.GetValueOrDefault() == run.RequestedBy) ThrowStaleDispatch();
    }

    private static void ThrowStaleDispatch() =>
        throw new AdminWithdrawalStaleCommandException(
            "Admin withdrawal dispatch command is stale, unapproved, or fenced.");

    private static bool ReceiptMatches(
        AdminWithdrawalProviderReceipt receipt,
        AdminWithdrawalRun run) =>
        receipt.TenantId == run.TenantId &&
        receipt.RunId == run.Id &&
        Enum.IsDefined(receipt.Outcome) &&
        !string.IsNullOrWhiteSpace(receipt.ProviderTransferId) &&
        receipt.FencingToken == run.FencingToken &&
        receipt.ExecutionEpoch == run.ExecutionEpoch &&
        receipt.Amount == run.Amount &&
        string.Equals(receipt.SourceAssetKey, run.SourceAssetKey, StringComparison.Ordinal) &&
        string.Equals(receipt.DestinationHash, run.DestinationHash, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(receipt.EvidenceHash) &&
        !string.IsNullOrWhiteSpace(receipt.Signature);

    private static void ValidateProviderEvent(
        AdminWithdrawalProviderEvent providerEvent,
        AdminWithdrawalRun run)
    {
        if (providerEvent.TenantId != run.TenantId ||
            string.IsNullOrWhiteSpace(providerEvent.ProviderTransferId) ||
            !Enum.IsDefined(providerEvent.Outcome) ||
            providerEvent.FencingToken != run.FencingToken ||
            providerEvent.ExecutionEpoch != run.ExecutionEpoch ||
            providerEvent.Amount != run.Amount ||
            !string.Equals(providerEvent.SourceAssetKey, run.SourceAssetKey, StringComparison.Ordinal) ||
            !string.Equals(providerEvent.DestinationHash, run.DestinationHash, StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(run.ProviderTransferId) &&
             !string.Equals(providerEvent.ProviderTransferId, run.ProviderTransferId, StringComparison.Ordinal)) ||
            string.IsNullOrWhiteSpace(providerEvent.EvidenceHash) ||
            string.IsNullOrWhiteSpace(providerEvent.Signature))
            throw new AdminWithdrawalEvidenceException(
                "Provider withdrawal event is not bound to the fenced run.");
    }

    private static AdminWithdrawalRun Transition(
        AdminWithdrawalRun run,
        AdminWithdrawalRunState state,
        DateTimeOffset occurredAt) => run with
        {
            State = state,
            Version = checked(run.Version + 1),
            UpdatedAt = occurredAt
        };

    private static PostingRequest ReservationPosting(AdminWithdrawalRun run) => new(
        new PostingId(run.Id),
        new PostingTemplate(PostingTemplateKind.AdminWithdrawalReservation, PostingTemplate.CurrentVersion),
        run.IdempotencyKey,
        PostingAuthority.Administrator,
        run.ReserveVersion,
        run.PolicyVersion,
        null,
        run.CreatedAt,
        [
            new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PlatformHardTreasury,
                run.Amount, null, null, null),
            new PostingLine(2, EntrySide.Credit, EconomyAccountCode.AdminWithdrawalPayableHard,
                run.Amount, null, null, null)
        ]);

    private static PostingRequest TerminalPosting(
        AdminWithdrawalRun run,
        PostingTemplateKind kind,
        DateTimeOffset occurredAt) => new(
        DeterministicPostingId(run.Id, kind == PostingTemplateKind.AdminWithdrawalSuccess ? "success" : "failure"),
        new PostingTemplate(kind, PostingTemplate.CurrentVersion),
        new IdempotencyKey($"{run.IdempotencyKey.Value}:{kind}"),
        PostingAuthority.Administrator,
        run.ReserveVersion,
        run.PolicyVersion,
        null,
        occurredAt,
        kind == PostingTemplateKind.AdminWithdrawalSuccess
            ?
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.AdminWithdrawalPayableHard,
                    run.Amount, null, null, null),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard,
                    run.Amount, null, null, null)
            ]
            :
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.AdminWithdrawalPayableHard,
                    run.Amount, null, null, null),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.PlatformHardTreasury,
                    run.Amount, null, null, null)
            ]);

    private static long SumUnits(IEnumerable<CreditLot> lots) =>
        lots.Aggregate(0L, static (total, lot) => checked(total + lot.Amount.Units));

    private static SourceStampId[] Roots(FragmentSelectionResult selection) =>
        selection.Selections.SelectMany(item => item.SelectedRanges)
            .Select(item => item.Root).Distinct().OrderBy(item => item.Value).ToArray();

    private static string SelectionHash(FragmentSelectionResult selection) => Hash(string.Join('|',
        selection.Selections.Select(item => string.Join(':',
            item.ParentLotId.Value.ToString("N"),
            item.Amount.Units.ToString(CultureInfo.InvariantCulture),
            string.Join(',', item.SelectedRanges.Select(range => string.Join('-',
                range.Root.Value.ToString("N"), range.Start, range.Length, range.Epoch)))))));

    private static string RequestHash(AdminWithdrawalReservationRequest request) => Hash(string.Join('|',
        request.RunId.ToString("N"), request.TenantId.ToString("N"), request.IdempotencyKey.Value, request.RequestedBy.ToString("N"),
        request.PlatformFeeWalletId.Value.ToString("N"), request.PeriodStart.ToString("O", CultureInfo.InvariantCulture),
        request.PolicyVersion.Value, request.ReserveVersion.Value, request.ReserveAuthorizationEpoch,
        request.SourceAssetKey.Trim(), request.DestinationHash.Trim(),
        request.RequestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

    private static string DispatchSnapshotHash(
        AdminWithdrawalRun run,
        IReadOnlyCollection<ValueFragmentReservation> reservations,
        TreasuryCustodyReport custody,
        DateTimeOffset requestedAt) => Hash(string.Join('|',
        run.TenantId.ToString("N"), run.Id.ToString("N"), run.Version, run.FencingToken, run.ExecutionEpoch,
        run.Amount.Units, run.SourceAssetKey, run.DestinationHash,
        run.ReserveVersion.Value, run.ReserveAuthorizationEpoch,
        custody.EvidenceHash, custody.Signature,
        requestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        string.Join(';', reservations.OrderBy(item => item.Id).Select(item => string.Join(',',
            item.Id.ToString("N"), item.LotId.Value.ToString("N"), item.Amount.Units)))));

    private static string ProviderReceiptHash(AdminWithdrawalProviderReceipt receipt) => Hash(string.Join('|',
        receipt.TenantId.ToString("N"), receipt.RunId.ToString("N"), (int)receipt.Outcome, receipt.ProviderTransferId,
        receipt.FencingToken, receipt.ExecutionEpoch, receipt.Amount.Currency, receipt.Amount.Units,
        receipt.SourceAssetKey, receipt.DestinationHash, receipt.EvidenceHash,
        receipt.Signature, receipt.ObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

    private static string ProviderEventHash(AdminWithdrawalProviderEvent providerEvent) => Hash(string.Join('|',
        providerEvent.EventId, providerEvent.TenantId.ToString("N"), providerEvent.RunId.ToString("N"), (int)providerEvent.Outcome,
        providerEvent.ProviderTransferId, providerEvent.FencingToken, providerEvent.ExecutionEpoch,
        providerEvent.Amount.Currency, providerEvent.Amount.Units, providerEvent.SourceAssetKey,
        providerEvent.DestinationHash, providerEvent.EvidenceHash, providerEvent.Signature,
        providerEvent.ObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

    private static PostingId DeterministicPostingId(Guid runId, string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{runId:N}:{suffix}"));
        return new PostingId(new Guid(bytes.AsSpan(0, 16)));
    }

    private static string Hash(string canonical) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}

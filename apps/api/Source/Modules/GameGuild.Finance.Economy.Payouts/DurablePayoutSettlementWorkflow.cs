using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Payouts;

public sealed record DurablePayoutDispatchRequest(
    Guid OperationId,
    Guid ActorId,
    long ExpectedVersion,
    long FencingToken,
    long KillSwitchEpoch,
    string JurisdictionCode,
    string ReauthenticationEvidenceHash,
    string ProviderHash,
    IReadOnlyList<SourceStampId> SourceRoots,
    DateTimeOffset OccurredAt);

public sealed record DurablePayoutProviderEventRequest(PayoutProviderEvent ProviderEvent);

public interface IDurablePayoutSettlementWorkflow
{
    Task<PayoutOperation> BeginDispatchAsync(
        DurablePayoutDispatchRequest request,
        CancellationToken cancellationToken = default);

    Task<PayoutOperation> ApplyProviderEventAsync(
        DurablePayoutProviderEventRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists payout transitions after FIFO reservation. Provider callbacks cannot move value by
/// themselves: terminal settlement still requires an independently issued, single-use authority.
/// </summary>
public sealed class PostgreSqlDurablePayoutSettlementWorkflow(
    IApplicationDbContext dbContext,
    IPayoutOperationStore operations,
    IFifoFragmentReservationGateway reservations,
    IEconomyProtectedOperationOrchestrator orchestrator,
    IPayoutAuthorizationEvidenceWriter authorizationEvidence,
    IRegisteredPostingGateway postings,
    IProviderEvidencePostingAuthorityIssuer providerAuthority,
    IPayoutProviderEvidenceVerifier providerEvidence,
    IPayoutDispatchOutboxWriter dispatchOutbox) : IDurablePayoutSettlementWorkflow
{
    public async Task<PayoutOperation> BeginDispatchAsync(
        DurablePayoutDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateDispatchRequest(request);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            dbContext, IsolationLevel.Serializable, async transactionToken =>
        {
            var operation = operations.Get(request.OperationId);
            if (operation.State == PayoutOperationState.Dispatching &&
                operation.Version == checked(request.ExpectedVersion + 1))
                return operation;

            ValidateDispatchTransition(operation, request);
            var intent = new EconomyProtectedOperationIntent(
                EconomyValueMovementCapability.PayoutExecution,
                PostingTemplateKind.PayoutSuccess,
                operation.WalletId,
                operation.WalletId,
                operation.Amount,
                [new RiskCurrencyLeg(operation.Amount.Currency, operation.Amount.Units)],
                request.SourceRoots,
                request.ProviderHash.Trim(),
                operation.DestinationHash,
                new IdempotencyKey(operation.IdempotencyKey.Value + ":dispatch"),
                request.OccurredAt,
                ProtectedSubjectId: operation.PayeeId);
            return await orchestrator.ExecuteAsync(intent, async (authorization, operationToken) =>
            {
                ValidateDispatchReceipt(operation, request, authorization);
                var rootHashes = request.SourceRoots
                    .Select(root => Hash(root.Value.ToString("N")))
                    .ToArray();
                var dispatchSnapshotHash = Hash(string.Join('|',
                    operation.TenantId.ToString("N"),
                    operation.Id.ToString("N"),
                    operation.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    operation.FencingToken.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    operation.KillSwitchEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    operation.ProviderBindingHash,
                    operation.EligibilityHash,
                    authorization.RiskDecisionId.ToString("N"),
                    request.ReauthenticationEvidenceHash.Trim(),
                    authorization.OperationFingerprint,
                    request.ProviderHash.Trim(),
                    string.Join(',', rootHashes)));
                var dispatching = operation.Transition(
                    PayoutOperationState.Dispatching,
                    request.OccurredAt,
                    dispatchSnapshotHash: dispatchSnapshotHash);
                var persisted = operations.Update(dispatching, operation.Version);
                await authorizationEvidence.AppendAsync(
                    new PayoutAuthorizationEvidence(
                        operation.Id,
                        operation.TenantId,
                        request.ActorId,
                        PayoutAuthorizationPhase.Dispatch,
                        authorization.RiskDecisionId,
                        request.ReauthenticationEvidenceHash.Trim(),
                        Hash(authorization.OperationFingerprint),
                        authorization.Receipt.Id,
                        authorization.Receipt.ReceiptHash,
                        request.OccurredAt),
                    operationToken).ConfigureAwait(false);
                var transitioned = reservations.Transition(
                    operation.Id,
                    PersistedFragmentReservationStatus.Reserved,
                    PersistedFragmentReservationStatus.Dispatching,
                    request.OccurredAt);
                if (transitioned <= 0)
                    throw new PayoutStaleCommandException(
                        "Payout fragments are no longer reserved for dispatch.");
                var command = new PayoutDispatchCommand(
                    operation.Id,
                    dispatching.Version,
                    operation.FencingToken,
                    operation.KillSwitchEpoch,
                    operation.ProviderAccountId,
                    operation.DestinationHash,
                    operation.Amount,
                    dispatchSnapshotHash,
                    operation.IdempotencyKey.Value + ":dispatch",
                    request.OccurredAt);
                var payload = JsonSerializer.Serialize(command);
                await dispatchOutbox.AddAsync(new PayoutDispatchOutboxRow
                {
                    Id = DeterministicGuid(operation.Id, "dispatch-outbox"),
                    OperationId = operation.Id,
                    IdempotencyKey = command.IdempotencyKey,
                    Payload = payload,
                    PayloadHash = Hash(payload),
                    CreatedAt = request.OccurredAt,
                    AvailableAt = request.OccurredAt
                }, operationToken).ConfigureAwait(false);
                return persisted;
            }, transactionToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PayoutOperation> ApplyProviderEventAsync(
        DurablePayoutProviderEventRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProviderEvent(request.ProviderEvent);
        var eventHash = ProviderEventHash(request.ProviderEvent);

        var replay = operations.FindProviderEvent(request.ProviderEvent.EventId, eventHash);
        if (replay is not null)
            return operations.Get(replay.OperationId);

        if (!providerEvidence.Verify(request.ProviderEvent))
            throw new PayoutEvidenceException("Provider payout event signature is invalid.");
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            dbContext, IsolationLevel.ReadCommitted, async _ =>
        {
            replay = operations.FindProviderEvent(request.ProviderEvent.EventId, eventHash);
            if (replay is not null)
                return operations.Get(replay.OperationId);

            var operation = operations.Get(request.ProviderEvent.OperationId);
            ValidateTerminalTransition(operation, request.ProviderEvent);
            var nextState = request.ProviderEvent.Outcome == PayoutProviderOutcome.Succeeded
                ? PayoutOperationState.Succeeded
                : PayoutOperationState.Failed;
            var nextReservationStatus = nextState == PayoutOperationState.Succeeded
                ? PersistedFragmentReservationStatus.Consumed
                : PersistedFragmentReservationStatus.Released;
            var postingKind = nextState == PayoutOperationState.Succeeded
                ? PostingTemplateKind.PayoutSuccess
                : PostingTemplateKind.PayoutFailure;
            var providerOperationFingerprint = Hash(string.Join('|',
                operation.TenantId.ToString("N"),
                operation.Id.ToString("N"),
                request.ProviderEvent.EventId.Trim(),
                eventHash,
                (int)postingKind));
            var authority = await providerAuthority.IssueAsync(
                new ProviderEvidencePostingAuthorityRequest(
                    "payout-provider-terminal",
                    operation.TenantId,
                    operation.ActorId,
                    operation.WalletId,
                    postingKind,
                    operation.Amount,
                    operation.PolicyVersion,
                    operation.ReserveVersion,
                    operation.ReserveAuthorizationEpoch,
                    operation.KillSwitchEpoch,
                    providerOperationFingerprint,
                    Hash(request.ProviderEvent.ProviderPayoutId.Trim()),
                    request.ProviderEvent.EvidenceHash,
                    request.ProviderEvent.ObservedAt,
                    request.ProviderEvent.ObservedAt.AddMinutes(5)),
                cancellationToken).ConfigureAwait(false);
            if (authority.TenantId != operation.TenantId || authority.ActorId != operation.ActorId ||
                authority.RiskDecisionId == operation.RiskDecisionId)
                throw new PayoutEvidenceException(
                    "Provider evidence posting authority is not bound to the payout actor and tenant.");
            var changed = operation.Transition(
                nextState,
                request.ProviderEvent.ObservedAt,
                providerPayoutId: request.ProviderEvent.ProviderPayoutId.Trim());

            postings.Post(new RegisteredPostingRequest(
                authority,
                CreateTerminalPosting(operation, postingKind, request.ProviderEvent.ObservedAt)));
            await providerAuthority.ConsumeAsync(
                authority,
                request.ProviderEvent.ObservedAt,
                cancellationToken).ConfigureAwait(false);
            var transitioned = reservations.Transition(
                operation.Id,
                PersistedFragmentReservationStatus.Dispatching,
                nextReservationStatus,
                request.ProviderEvent.ObservedAt);
            if (transitioned <= 0)
                throw new PayoutStaleCommandException("Payout fragments are no longer reserved for terminal settlement.");

            operations.RecordProviderEvent(
                request.ProviderEvent.EventId,
                eventHash,
                changed,
                operation.Version,
                request.ProviderEvent.ObservedAt);
            return changed;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static PostingRequest CreateTerminalPosting(
        PayoutOperation operation,
        PostingTemplateKind kind,
        DateTimeOffset occurredAt) => new(
        DeterministicPostingId(operation.Id, kind == PostingTemplateKind.PayoutSuccess ? "success" : "failure"),
        new PostingTemplate(kind, PostingTemplate.CurrentVersion),
        new IdempotencyKey($"{operation.IdempotencyKey.Value}:{kind}"),
        PostingAuthority.PayoutCoordinator,
        operation.ReserveVersion,
        operation.PolicyVersion,
        null,
        occurredAt,
        kind == PostingTemplateKind.PayoutSuccess
            ?
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PayoutPayableHard, operation.Amount, null, null, null),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, operation.Amount, null, null, null)
            ]
            :
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PayoutPayableHard, operation.Amount, null, null, null),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.EarnedHardLiability, operation.Amount, operation.WalletId, null, ProvenanceKind.EarnedHard)
            ]);

    private static void ValidateDispatchRequest(DurablePayoutDispatchRequest request)
    {
        if (request.OperationId == Guid.Empty)
            throw new ArgumentException("Payout operation ID is required.", nameof(request));
        if (request.ActorId == Guid.Empty)
            throw new ArgumentException("Payout dispatch actor ID is required.", nameof(request));
        if (request.ExpectedVersion <= 0 || request.FencingToken <= 0 || request.KillSwitchEpoch < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Payout dispatch control versions must be positive.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.JurisdictionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ReauthenticationEvidenceHash);
        if (request.ReauthenticationEvidenceHash.Trim().Length != 64)
            throw new ArgumentException(
                "Payout reauthentication evidence hashes must contain 64 characters.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProviderHash);
        ArgumentNullException.ThrowIfNull(request.SourceRoots);
        if (request.SourceRoots.Count == 0 || request.SourceRoots.Any(root => root.Value == Guid.Empty))
            throw new ArgumentException("Payout dispatch requires immutable source roots.", nameof(request));
    }

    private static void ValidateDispatchTransition(PayoutOperation operation, DurablePayoutDispatchRequest request)
    {
        if (operation.State != PayoutOperationState.Reserved || operation.Version != request.ExpectedVersion ||
            operation.FencingToken != request.FencingToken || operation.KillSwitchEpoch != request.KillSwitchEpoch)
            throw new PayoutStaleCommandException("Payout dispatch command is stale or fenced.");
    }

    private static void ValidateProviderEvent(PayoutProviderEvent providerEvent)
    {
        ArgumentNullException.ThrowIfNull(providerEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.EventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.ProviderPayoutId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.ProviderAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.DestinationHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.EvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.Signature);
        if (providerEvent.OperationId == Guid.Empty)
            throw new ArgumentException("Provider payout events require an operation ID.", nameof(providerEvent));
        if (providerEvent.Outcome is not (PayoutProviderOutcome.Succeeded or PayoutProviderOutcome.Failed))
            throw new PayoutEvidenceException("Only terminal provider events can settle a payout.");
    }

    private static void ValidateTerminalTransition(
        PayoutOperation operation,
        PayoutProviderEvent providerEvent)
    {
        if (operation.State is not (PayoutOperationState.Dispatching or PayoutOperationState.Ambiguous))
            throw new PayoutStaleCommandException("Provider terminal event is out of order.");
        if (!string.Equals(providerEvent.ProviderAccountId, operation.ProviderAccountId, StringComparison.Ordinal) ||
            !string.Equals(providerEvent.DestinationHash, operation.DestinationHash, StringComparison.Ordinal))
            throw new PayoutProviderBindingException("Provider payout event is not bound to this operation.");
        if (providerEvent.ObservedAt < operation.CreatedAt)
            throw new PayoutEvidenceException("Provider payout event predates the payout operation.");
    }

    private static string ProviderEventHash(PayoutProviderEvent providerEvent)
    {
        var payload = string.Join('|',
            providerEvent.EventId.Trim(),
            providerEvent.OperationId.ToString("N"),
            ((int)providerEvent.Outcome).ToString(System.Globalization.CultureInfo.InvariantCulture),
            providerEvent.ProviderPayoutId.Trim(),
            providerEvent.ProviderAccountId.Trim(),
            providerEvent.DestinationHash.Trim(),
            providerEvent.EvidenceHash.Trim(),
            providerEvent.Signature.Trim(),
            providerEvent.ObservedAt.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static PostingId DeterministicPostingId(Guid operationId, string suffix)
    {
        return new PostingId(DeterministicGuid(operationId, suffix));
    }

    private static void ValidateDispatchReceipt(
        PayoutOperation operation,
        DurablePayoutDispatchRequest request,
        EconomyProtectedOperationAuthorization authorization)
    {
        var receipt = authorization.Receipt;
        var rootHashes = request.SourceRoots.Select(root => Hash(root.Value.ToString("N"))).ToArray();
        if (authorization.TenantId != operation.TenantId || authorization.ActorId != request.ActorId ||
            !string.Equals(authorization.JurisdictionCode, request.JurisdictionCode.Trim(),
                StringComparison.Ordinal) ||
            receipt.TenantId != operation.TenantId || receipt.ActorId != request.ActorId ||
            !string.Equals(receipt.SubjectReference,
                EconomySubjectReference.ForUser(operation.TenantId, operation.PayeeId),
                StringComparison.Ordinal) ||
            !string.Equals(receipt.JurisdictionCode, authorization.JurisdictionCode,
                StringComparison.Ordinal) ||
            receipt.RiskDecisionId != authorization.RiskDecisionId ||
            receipt.PolicyVersion != operation.PolicyVersion.Value ||
            receipt.ReserveVersion != operation.ReserveVersion.Value ||
            receipt.KillSwitchEpoch != operation.KillSwitchEpoch ||
            !string.Equals(receipt.ProviderHash, request.ProviderHash, StringComparison.Ordinal) ||
            !string.Equals(receipt.DestinationHash, operation.DestinationHash, StringComparison.Ordinal) ||
            !receipt.SourceRootHashes.SequenceEqual(rootHashes, StringComparer.Ordinal))
            throw new PayoutStaleCommandException(
                "The dispatch capability receipt does not match the reserved payout snapshot.");
    }

    private static Guid DeterministicGuid(Guid operationId, string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId:N}:payout:{suffix}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

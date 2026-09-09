using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Treasury;

public sealed record DurableAdminWithdrawalReservationRequest(
    AdminWithdrawalRun Run,
    string JurisdictionCode,
    string ReauthenticationEvidenceHash,
    string ProviderHash);

public sealed record DurableAdminWithdrawalApprovalRequest(
    Guid TenantId,
    Guid RunId,
    long ExpectedVersion,
    Guid ApprovedBy,
    DateTimeOffset ApprovedAt);

public sealed record DurableAdminWithdrawalDispatchRequest(
    Guid TenantId,
    Guid RunId,
    long ExpectedVersion,
    long FencingToken,
    long ExecutionEpoch,
    DateTimeOffset OccurredAt,
    Guid DispatchedBy,
    string JurisdictionCode,
    string ReauthenticationEvidenceHash,
    string ProviderHash,
    IReadOnlyList<SourceStampId> SourceRoots);

public sealed record DurableAdminWithdrawalProviderEventRequest(
    AdminWithdrawalProviderEvent ProviderEvent);

public sealed record AdminWithdrawalAuthorizationSnapshot(
    string RequestHash,
    string SubjectReference,
    string JurisdictionCode,
    string ProviderHash,
    string DestinationHash,
    long PolicyVersion,
    long ReserveVersion,
    long KillSwitchEpoch,
    Guid RiskDecisionId,
    string OperationFingerprintHash,
    string ReauthenticationEvidenceHash,
    string ReceiptHash,
    IReadOnlyList<string> SourceRootHashes,
    IReadOnlyList<string> EvidenceHashes);

public interface IDurableAdminWithdrawalWorkflow
{
    Task<AdminWithdrawalRun> ReserveAsync(DurableAdminWithdrawalReservationRequest request, CancellationToken cancellationToken = default);
    Task<AdminWithdrawalRun> ApproveAsync(DurableAdminWithdrawalApprovalRequest request, CancellationToken cancellationToken = default);
    Task<AdminWithdrawalRun> BeginDispatchAsync(DurableAdminWithdrawalDispatchRequest request, CancellationToken cancellationToken = default);
    Task<AdminWithdrawalRun> ApplyProviderEventAsync(DurableAdminWithdrawalProviderEventRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// PostgreSQL-backed two-person platform treasury withdrawal lifecycle. Every movement remains
/// protected by a single-use registered-posting authority and terminal provider evidence.
/// </summary>
public sealed class PostgreSqlDurableAdminWithdrawalWorkflow(
    IApplicationDbContext dbContext,
    IAdminWithdrawalStore operations,
    IAdminWithdrawalAuditTrail audit,
    IFifoFragmentReservationGateway reservations,
    IEconomyProtectedOperationOrchestrator orchestrator,
    IRegisteredPostingCapabilityResolver capabilityResolver,
    IRegisteredPostingGateway postings,
    IProviderEvidencePostingAuthorityIssuer providerAuthority,
    IAdminWithdrawalProviderEvidenceVerifier providerEvidence,
    IAdminWithdrawalDispatchOutboxWriter dispatchOutbox) : IDurableAdminWithdrawalWorkflow
{
    private const string ReservationCapabilityName = "admin-withdrawal-reservation";

    public async Task<AdminWithdrawalRun> ReserveAsync(
        DurableAdminWithdrawalReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateNewRun(request.Run);
        ValidateReservationAuthorization(request);
        var replay = operations.FindReplay(request.Run.TenantId, request.Run.IdempotencyKey.Value, request.Run.RequestHash);
        if (replay is not null)
            return replay;
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            dbContext, IsolationLevel.Serializable, async transactionToken =>
        {
            replay = operations.FindReplay(request.Run.TenantId, request.Run.IdempotencyKey.Value, request.Run.RequestHash);
            if (replay is not null)
                return replay;
            if (operations.FindPeriod(request.Run.TenantId, request.Run.PeriodStart) is not null)
                throw new AdminWithdrawalOverlapException("A withdrawal run already owns this monthly period.");

            operations.Add(request.Run);
            var fragments = reservations.Reserve(new FifoFragmentReservationRequest(
                request.Run.Id,
                request.Run.PlatformFeeWalletId,
                CurrencyCode.HardCoin,
                ProvenanceKind.EarnedHard,
                request.Run.Amount,
                PersistedFragmentReservationPurpose.AdminWithdrawal,
                request.Run.CreatedAt));
            if (fragments.Sum(fragment => fragment.Amount.Units) != request.Run.Amount.Units)
                throw new AdminWithdrawalEligibilityException("Administrative withdrawal FIFO reservations do not match the requested amount.");

            var sourceRoots = fragments.Select(fragment => fragment.RootSourceStampId)
                .Distinct().OrderBy(root => root.Value).ToArray();
            var intent = new EconomyProtectedOperationIntent(
                EconomyValueMovementCapability.AdminWithdrawalExecution,
                PostingTemplateKind.AdminWithdrawalReservation,
                request.Run.PlatformFeeWalletId,
                request.Run.PlatformFeeWalletId,
                request.Run.Amount,
                [new RiskCurrencyLeg(request.Run.Amount.Currency, request.Run.Amount.Units)],
                sourceRoots,
                request.ProviderHash.Trim(),
                request.Run.DestinationHash,
                request.Run.IdempotencyKey,
                request.Run.CreatedAt);
            return await orchestrator.ExecuteAsync(intent, async (authorization, operationToken) =>
            {
                var receipt = authorization.Receipt;
                ValidateReservationAuthorization(request, authorization, sourceRoots);
                var authority = await capabilityResolver.ResolveAuthorityAsync(
                    ReservationCapabilityName,
                    PostingTemplateKind.AdminWithdrawalReservation,
                    receipt,
                    operationToken).ConfigureAwait(false);
                if (authority.TenantId != request.Run.TenantId || authority.ActorId != request.Run.RequestedBy)
                    throw new AdminWithdrawalEligibilityException(
                        "The registered posting authority does not match the Treasury withdrawal actor and tenant.");

                postings.Post(new RegisteredPostingRequest(
                    authority,
                    CreateReservationPosting(request.Run),
                    fragments.Select(fragment => new RegisteredPostingAllocation(
                        1,
                        fragment.ParentLotId,
                        fragment.Amount.Units,
                        [fragment.Range]))
                        .ToArray()));
                audit.Append(
                    request.Run.TenantId,
                    request.Run.Id,
                    "reserved",
                    request.Run.RequestedBy,
                    JsonSerializer.Serialize(new AdminWithdrawalAuthorizationSnapshot(
                        request.Run.RequestHash,
                        receipt.SubjectReference,
                        receipt.JurisdictionCode,
                        receipt.ProviderHash,
                        receipt.DestinationHash,
                        receipt.PolicyVersion,
                        receipt.ReserveVersion,
                        receipt.KillSwitchEpoch,
                        authorization.RiskDecisionId,
                        Hash(authorization.OperationFingerprint),
                        request.ReauthenticationEvidenceHash.Trim(),
                        receipt.ReceiptHash,
                        receipt.SourceRootHashes,
                        receipt.EvidenceHashes)),
                    request.Run.CreatedAt);
                return request.Run;
            }, transactionToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdminWithdrawalRun> ApproveAsync(
        DurableAdminWithdrawalApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateApprovalRequest(request);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            dbContext, IsolationLevel.Serializable, async transactionToken =>
        {
            var run = operations.Get(request.TenantId, request.RunId);
            if (run.State == AdminWithdrawalRunState.Approved &&
                run.Version == checked(request.ExpectedVersion + 1) &&
                run.ApprovedBy.HasValue &&
                run.ApprovedBy.Value == request.ApprovedBy)
                return run;
            if (run.State != AdminWithdrawalRunState.PendingApproval || run.Version != request.ExpectedVersion)
                throw new AdminWithdrawalStaleCommandException("The withdrawal approval command is stale.");
            if (run.RequestedBy == request.ApprovedBy)
                throw new AdminWithdrawalApprovalException("The withdrawal requester cannot approve the same run.");

            var approved = run with
            {
                ApprovedBy = request.ApprovedBy,
                State = AdminWithdrawalRunState.Approved,
                Version = checked(run.Version + 1),
                UpdatedAt = request.ApprovedAt
            };
            operations.Update(approved, run.Version);
            audit.Append(run.TenantId, run.Id, "approved", request.ApprovedBy, run.RequestHash, request.ApprovedAt);
            await Task.CompletedTask;
            return approved;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdminWithdrawalRun> BeginDispatchAsync(
        DurableAdminWithdrawalDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateDispatchRequest(request);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            dbContext, IsolationLevel.Serializable, async transactionToken =>
        {
            var run = operations.Get(request.TenantId, request.RunId);
            if (run.State == AdminWithdrawalRunState.Dispatching &&
                run.Version == checked(request.ExpectedVersion + 1))
                return run;
            if (run.State != AdminWithdrawalRunState.Approved || run.Version != request.ExpectedVersion ||
                run.FencingToken != request.FencingToken || run.ExecutionEpoch != request.ExecutionEpoch ||
                !run.ApprovedBy.HasValue)
                throw new AdminWithdrawalStaleCommandException("Admin withdrawal dispatch command is stale, unapproved, or fenced.");
            if (run.ApprovedBy.Value == run.RequestedBy)
                throw new AdminWithdrawalStaleCommandException("Admin withdrawal dispatch command is stale, unapproved, or fenced.");

            var intent = new EconomyProtectedOperationIntent(
                EconomyValueMovementCapability.AdminWithdrawalExecution,
                PostingTemplateKind.AdminWithdrawalSuccess,
                run.PlatformFeeWalletId,
                run.PlatformFeeWalletId,
                run.Amount,
                [new RiskCurrencyLeg(run.Amount.Currency, run.Amount.Units)],
                request.SourceRoots,
                request.ProviderHash.Trim(),
                run.DestinationHash,
                new IdempotencyKey(run.IdempotencyKey.Value + ":dispatch"),
                request.OccurredAt);
            return await orchestrator.ExecuteAsync(intent, async (authorization, operationToken) =>
            {
                var receipt = authorization.Receipt;
                ValidateDispatchAuthorization(run, request, authorization);
                var rootHashes = request.SourceRoots
                    .Select(root => Hash(root.Value.ToString("N"))).ToArray();
                var dispatchSnapshotHash = Hash(string.Join('|',
                    run.TenantId.ToString("N"),
                    run.Id.ToString("N"),
                    run.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    run.FencingToken.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    run.ExecutionEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    run.RequestHash,
                    run.DestinationHash,
                    authorization.RiskDecisionId.ToString("N"),
                    authorization.OperationFingerprint,
                    request.ReauthenticationEvidenceHash.Trim(),
                    request.ProviderHash.Trim(),
                    string.Join(',', rootHashes)));
                var dispatching = run with
                {
                    State = AdminWithdrawalRunState.Dispatching,
                    Version = checked(run.Version + 1),
                    DispatchSnapshotHash = dispatchSnapshotHash,
                    UpdatedAt = request.OccurredAt
                };
                operations.Update(dispatching, run.Version);
                var transitioned = reservations.Transition(
                    run.Id,
                    PersistedFragmentReservationStatus.Reserved,
                    PersistedFragmentReservationStatus.Dispatching,
                    request.OccurredAt);
                if (transitioned <= 0)
                    throw new AdminWithdrawalStaleCommandException(
                        "Administrative withdrawal fragments are no longer reserved for dispatch.");
                var command = new AdminWithdrawalDispatchCommand(
                    run.Id,
                    run.TenantId,
                    dispatching.Version,
                    run.FencingToken,
                    run.ExecutionEpoch,
                    run.Amount,
                    run.SourceAssetKey,
                    run.DestinationHash,
                    dispatchSnapshotHash,
                    run.IdempotencyKey.Value + ":dispatch",
                    request.OccurredAt);
                var payload = JsonSerializer.Serialize(command);
                await dispatchOutbox.AddAsync(new AdminWithdrawalDispatchOutboxRow
                {
                    Id = DeterministicGuid(run.Id, "dispatch-outbox"),
                    RunId = run.Id,
                    TenantId = run.TenantId,
                    IdempotencyKey = command.IdempotencyKey,
                    Payload = payload,
                    PayloadHash = Hash(payload),
                    CreatedAt = request.OccurredAt,
                    AvailableAt = request.OccurredAt
                }, operationToken).ConfigureAwait(false);
                audit.Append(
                    run.TenantId,
                    run.Id,
                    "dispatching",
                    request.DispatchedBy,
                    Hash(string.Join('|', dispatchSnapshotHash, receipt.ReceiptHash)),
                    request.OccurredAt);
                return dispatching;
            }, transactionToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdminWithdrawalRun> ApplyProviderEventAsync(
        DurableAdminWithdrawalProviderEventRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProviderEventShape(request.ProviderEvent);
        var eventHash = ProviderEventHash(request.ProviderEvent);
        var replayRunId = operations.FindProviderEvent(request.ProviderEvent.TenantId, request.ProviderEvent.EventId, eventHash);
        if (replayRunId.HasValue)
            return operations.Get(request.ProviderEvent.TenantId, replayRunId.Value);
        if (!providerEvidence.Verify(request.ProviderEvent))
            throw new AdminWithdrawalEvidenceException("Provider withdrawal event signature is invalid.");

        var authorityRun = operations.Get(request.ProviderEvent.TenantId, request.ProviderEvent.RunId);
        ValidateTerminalEvent(authorityRun, request.ProviderEvent);
        var postingKind = request.ProviderEvent.Outcome == AdminWithdrawalProviderOutcome.Succeeded
            ? PostingTemplateKind.AdminWithdrawalSuccess
            : PostingTemplateKind.AdminWithdrawalFailure;
        var providerOperationFingerprint = Hash(string.Join('|',
            request.ProviderEvent.TenantId.ToString("N"),
            request.ProviderEvent.RunId.ToString("N"),
            request.ProviderEvent.EventId.Trim(),
            eventHash,
            (int)postingKind));
        var authority = await providerAuthority.IssueAsync(
            new ProviderEvidencePostingAuthorityRequest(
                "admin-withdrawal-provider-terminal",
                authorityRun.TenantId,
                authorityRun.RequestedBy,
                authorityRun.PlatformFeeWalletId,
                postingKind,
                authorityRun.Amount,
                authorityRun.PolicyVersion,
                authorityRun.ReserveVersion,
                authorityRun.ReserveAuthorizationEpoch,
                authorityRun.ExecutionEpoch,
                providerOperationFingerprint,
                Hash(request.ProviderEvent.ProviderTransferId.Trim()),
                request.ProviderEvent.EvidenceHash,
                request.ProviderEvent.ObservedAt,
                request.ProviderEvent.ObservedAt.AddMinutes(5)),
            cancellationToken).ConfigureAwait(false);
        if (authority.TenantId != authorityRun.TenantId || authority.ActorId != authorityRun.RequestedBy)
            throw new AdminWithdrawalEvidenceException(
                "Provider evidence posting authority is not bound to the withdrawal actor and tenant.");
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            dbContext, IsolationLevel.ReadCommitted, async _ =>
        {
            replayRunId = operations.FindProviderEvent(request.ProviderEvent.TenantId, request.ProviderEvent.EventId, eventHash);
            if (replayRunId.HasValue)
                return operations.Get(request.ProviderEvent.TenantId, replayRunId.Value);

            var run = operations.Get(request.ProviderEvent.TenantId, request.ProviderEvent.RunId);
            ValidateTerminalEvent(run, request.ProviderEvent);
            var succeeded = request.ProviderEvent.Outcome == AdminWithdrawalProviderOutcome.Succeeded;
            var terminal = run with
            {
                State = succeeded ? AdminWithdrawalRunState.Succeeded : AdminWithdrawalRunState.Failed,
                ProviderTransferId = request.ProviderEvent.ProviderTransferId.Trim(),
                Version = checked(run.Version + 1),
                UpdatedAt = request.ProviderEvent.ObservedAt
            };

            postings.Post(new RegisteredPostingRequest(
                authority,
                CreateTerminalPosting(run, postingKind, request.ProviderEvent.ObservedAt)));
            await providerAuthority.ConsumeAsync(
                authority,
                request.ProviderEvent.ObservedAt,
                cancellationToken).ConfigureAwait(false);
            var changedReservations = reservations.Transition(
                run.Id,
                PersistedFragmentReservationStatus.Dispatching,
                succeeded ? PersistedFragmentReservationStatus.Consumed : PersistedFragmentReservationStatus.Released,
                request.ProviderEvent.ObservedAt);
            if (changedReservations <= 0)
                throw new AdminWithdrawalStaleCommandException("Administrative withdrawal fragments are no longer reserved.");

            operations.RecordProviderEvent(run.TenantId, request.ProviderEvent.EventId, eventHash, terminal, run.Version);
            audit.Append(run.TenantId, run.Id, succeeded ? "succeeded" : "failed", null, eventHash, request.ProviderEvent.ObservedAt);
            return terminal;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static PostingRequest CreateReservationPosting(AdminWithdrawalRun run) => new(
        new PostingId(run.Id),
        new PostingTemplate(PostingTemplateKind.AdminWithdrawalReservation, PostingTemplate.CurrentVersion),
        run.IdempotencyKey,
        PostingAuthority.Administrator,
        run.ReserveVersion,
        run.PolicyVersion,
        null,
        run.CreatedAt,
        [
            new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PlatformHardTreasury, run.Amount, null, null, null),
            new PostingLine(2, EntrySide.Credit, EconomyAccountCode.AdminWithdrawalPayableHard, run.Amount, null, null, null)
        ]);

    private static PostingRequest CreateTerminalPosting(AdminWithdrawalRun run, PostingTemplateKind kind, DateTimeOffset occurredAt) => new(
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
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.AdminWithdrawalPayableHard, run.Amount, null, null, null),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, run.Amount, null, null, null)
            ]
            :
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.AdminWithdrawalPayableHard, run.Amount, null, null, null),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.PlatformHardTreasury, run.Amount, null, null, null)
            ]);

    private static void ValidateNewRun(AdminWithdrawalRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Id == Guid.Empty || run.TenantId == Guid.Empty || run.RequestedBy == Guid.Empty || run.PlatformFeeWalletId.Value == Guid.Empty)
            throw new ArgumentException("Run, tenant, requester, and platform fee wallet identities are required.", nameof(run));
        if (run.PeriodStart.Day != 1)
            throw new ArgumentException("Withdrawal period must start on the first day of a month.", nameof(run));
        if (run.State != AdminWithdrawalRunState.PendingApproval || run.Version != 1 || run.ApprovedBy.HasValue)
            throw new InvalidOperationException("Only a new, unapproved withdrawal run can reserve platform treasury value.");
        if (run.Amount.Currency != CurrencyCode.HardCoin || run.Amount.Units <= 0)
            throw new AdminWithdrawalEligibilityException("Administrative withdrawals require a positive hard-coin amount.");
        if (run.FencingToken <= 0 || run.ExecutionEpoch <= 0 || run.ReserveAuthorizationEpoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(run), "Administrative withdrawal control versions must be positive.");
        ArgumentException.ThrowIfNullOrWhiteSpace(run.RequestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(run.SourceAssetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(run.DestinationHash);
    }

    private static void ValidateReservationAuthorization(DurableAdminWithdrawalReservationRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.JurisdictionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ReauthenticationEvidenceHash);
        if (request.ReauthenticationEvidenceHash.Trim().Length != 64)
            throw new ArgumentException(
                "Treasury reauthentication evidence hashes must contain 64 characters.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProviderHash);
    }

    private static void ValidateReservationAuthorization(
        DurableAdminWithdrawalReservationRequest request,
        EconomyProtectedOperationAuthorization authorization,
        IReadOnlyList<SourceStampId> sourceRoots)
    {
        var run = request.Run;
        var receipt = authorization.Receipt;
        var rootHashes = sourceRoots.Select(root => Hash(root.Value.ToString("N"))).ToArray();
        if (authorization.TenantId != run.TenantId || authorization.ActorId != run.RequestedBy ||
            !string.Equals(authorization.JurisdictionCode, request.JurisdictionCode.Trim(),
                StringComparison.Ordinal) ||
            receipt.TenantId != run.TenantId || receipt.ActorId != run.RequestedBy ||
            !string.Equals(receipt.SubjectReference,
                EconomySubjectReference.ForUser(run.TenantId, run.RequestedBy),
                StringComparison.Ordinal) ||
            !string.Equals(receipt.JurisdictionCode, authorization.JurisdictionCode,
                StringComparison.Ordinal) ||
            receipt.RiskDecisionId != authorization.RiskDecisionId ||
            receipt.PolicyVersion != run.PolicyVersion.Value ||
            receipt.ReserveVersion != run.ReserveVersion.Value ||
            !string.Equals(receipt.ProviderHash, request.ProviderHash.Trim(), StringComparison.Ordinal) ||
            !string.Equals(receipt.DestinationHash, run.DestinationHash, StringComparison.Ordinal) ||
            !receipt.SourceRootHashes.SequenceEqual(rootHashes, StringComparer.Ordinal))
            throw new AdminWithdrawalEligibilityException(
                "The Treasury capability receipt does not match the durable withdrawal snapshot.");
    }

    private static void ValidateApprovalRequest(DurableAdminWithdrawalApprovalRequest request)
    {
        if (request.TenantId == Guid.Empty || request.RunId == Guid.Empty || request.ApprovedBy == Guid.Empty)
            throw new ArgumentException("Withdrawal tenant, run, and approver identities are required.", nameof(request));
        if (request.ExpectedVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "The withdrawal version must be positive.");
    }

    private static void ValidateDispatchRequest(DurableAdminWithdrawalDispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty || request.RunId == Guid.Empty || request.DispatchedBy == Guid.Empty)
            throw new ArgumentException(
                "Withdrawal tenant, run, and dispatcher IDs are required.", nameof(request));
        if (request.ExpectedVersion <= 0 || request.FencingToken <= 0 || request.ExecutionEpoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Administrative withdrawal control versions must be positive.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.JurisdictionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ReauthenticationEvidenceHash);
        if (request.ReauthenticationEvidenceHash.Trim().Length != 64)
            throw new ArgumentException(
                "Treasury reauthentication evidence hashes must contain 64 characters.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProviderHash);
        ArgumentNullException.ThrowIfNull(request.SourceRoots);
        if (request.SourceRoots.Count == 0 || request.SourceRoots.Distinct().Count() != request.SourceRoots.Count)
            throw new ArgumentException("Dispatch requires distinct source roots.", nameof(request));
    }

    private static void ValidateDispatchAuthorization(
        AdminWithdrawalRun run,
        DurableAdminWithdrawalDispatchRequest request,
        EconomyProtectedOperationAuthorization authorization)
    {
        var receipt = authorization.Receipt;
        var rootHashes = request.SourceRoots.Select(root => Hash(root.Value.ToString("N"))).ToArray();
        if (authorization.TenantId != run.TenantId || authorization.ActorId != request.DispatchedBy ||
            !string.Equals(authorization.JurisdictionCode, request.JurisdictionCode.Trim(),
                StringComparison.Ordinal) ||
            receipt.TenantId != run.TenantId || receipt.ActorId != request.DispatchedBy ||
            !string.Equals(receipt.SubjectReference,
                EconomySubjectReference.ForUser(run.TenantId, request.DispatchedBy),
                StringComparison.Ordinal) ||
            !string.Equals(receipt.JurisdictionCode, authorization.JurisdictionCode,
                StringComparison.Ordinal) ||
            receipt.PolicyVersion != run.PolicyVersion.Value ||
            receipt.ReserveVersion != run.ReserveVersion.Value ||
            receipt.RiskDecisionId != authorization.RiskDecisionId ||
            !string.Equals(receipt.ProviderHash, request.ProviderHash.Trim(), StringComparison.Ordinal) ||
            !string.Equals(receipt.DestinationHash, run.DestinationHash, StringComparison.Ordinal) ||
            !receipt.SourceRootHashes.SequenceEqual(rootHashes, StringComparer.Ordinal))
            throw new AdminWithdrawalStaleCommandException(
                "The dispatch capability receipt is not bound to the durable withdrawal snapshot.");
    }

    private static void ValidateProviderEventShape(AdminWithdrawalProviderEvent providerEvent)
    {
        ArgumentNullException.ThrowIfNull(providerEvent);
        if (providerEvent.TenantId == Guid.Empty || providerEvent.RunId == Guid.Empty)
            throw new ArgumentException("Provider withdrawal events require tenant and run IDs.", nameof(providerEvent));
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.EventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.ProviderTransferId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.SourceAssetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.DestinationHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.EvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.Signature);
        if (providerEvent.Outcome is not (AdminWithdrawalProviderOutcome.Succeeded or AdminWithdrawalProviderOutcome.Failed))
            throw new AdminWithdrawalEvidenceException("Only a terminal provider event can complete an administrative withdrawal.");
    }

    private static void ValidateTerminalEvent(
        AdminWithdrawalRun run,
        AdminWithdrawalProviderEvent providerEvent)
    {
        if (run.State is not (AdminWithdrawalRunState.Dispatching or AdminWithdrawalRunState.Ambiguous))
            throw new AdminWithdrawalStaleCommandException("Provider terminal evidence is out of order.");
        if (providerEvent.TenantId != run.TenantId ||
            providerEvent.FencingToken != run.FencingToken || providerEvent.ExecutionEpoch != run.ExecutionEpoch ||
            providerEvent.Amount != run.Amount ||
            !string.Equals(providerEvent.SourceAssetKey, run.SourceAssetKey, StringComparison.Ordinal) ||
            !string.Equals(providerEvent.DestinationHash, run.DestinationHash, StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(run.ProviderTransferId) &&
             !string.Equals(providerEvent.ProviderTransferId, run.ProviderTransferId, StringComparison.Ordinal)))
            throw new AdminWithdrawalEvidenceException("Provider withdrawal event is not bound to the fenced run.");
        if (providerEvent.ObservedAt < run.CreatedAt)
            throw new AdminWithdrawalEvidenceException("Provider withdrawal event predates the run.");
    }

    private static string ProviderEventHash(AdminWithdrawalProviderEvent providerEvent)
    {
        var payload = string.Join('|',
            providerEvent.EventId.Trim(), providerEvent.RunId.ToString("N"), providerEvent.TenantId.ToString("N"),
            ((int)providerEvent.Outcome).ToString(System.Globalization.CultureInfo.InvariantCulture),
            providerEvent.ProviderTransferId.Trim(), providerEvent.FencingToken.ToString(System.Globalization.CultureInfo.InvariantCulture),
            providerEvent.ExecutionEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ((int)providerEvent.Amount.Currency).ToString(System.Globalization.CultureInfo.InvariantCulture),
            providerEvent.Amount.Units.ToString(System.Globalization.CultureInfo.InvariantCulture),
            providerEvent.SourceAssetKey.Trim(), providerEvent.DestinationHash.Trim(), providerEvent.EvidenceHash.Trim(),
            providerEvent.Signature.Trim(), providerEvent.ObservedAt.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static PostingId DeterministicPostingId(Guid runId, string suffix)
    {
        return new PostingId(DeterministicGuid(runId, suffix));
    }

    private static Guid DeterministicGuid(Guid runId, string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{runId:N}:admin-withdrawal:{suffix}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

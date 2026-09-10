using System.Data;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Payouts;

public sealed record DurablePayoutReservationRequest(
    PayoutOperation Operation,
    string JurisdictionCode,
    string ReauthenticationEvidenceHash,
    string ProviderHash);

public interface IDurablePayoutReservationWorkflow
{
    Task<PayoutOperation> ReserveAsync(
        DurablePayoutReservationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlDurablePayoutReservationWorkflow(
    IApplicationDbContext dbContext,
    IPayoutOperationStore operations,
    IFifoFragmentReservationGateway reservations,
    IEconomyProtectedOperationOrchestrator orchestrator,
    IPayoutAuthorizationEvidenceWriter authorizationEvidence,
    IRegisteredPostingCapabilityResolver capabilityResolver,
    IRegisteredPostingGateway postings) : IDurablePayoutReservationWorkflow
{
    private const string ReservationCapabilityName = "payout-reservation";

    public async Task<PayoutOperation> ReserveAsync(
        DurablePayoutReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        var operation = request.Operation;
        var replay = operations.FindReplay(operation.TenantId, operation.IdempotencyKey.Value, operation.RequestHash);
        if (replay is not null)
            return replay;
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            dbContext, IsolationLevel.Serializable, async transactionToken =>
        {
            replay = operations.FindReplay(operation.TenantId, operation.IdempotencyKey.Value, operation.RequestHash);
            if (replay is not null)
                return replay;

            var fragments = reservations.Reserve(new FifoFragmentReservationRequest(
                operation.Id,
                operation.WalletId,
                CurrencyCode.HardCoin,
                ProvenanceKind.EarnedHard,
                operation.Amount,
                PersistedFragmentReservationPurpose.Payout,
                operation.CreatedAt));
            if (fragments.Sum(fragment => fragment.Amount.Units) != operation.Amount.Units)
                throw new RegisteredPostingRejectedException("Payout FIFO reservations do not match the requested hard-coin amount.");

            var sourceRoots = fragments
                .Select(fragment => fragment.RootSourceStampId)
                .Distinct()
                .OrderBy(root => root.Value)
                .ToArray();
            var intent = new EconomyProtectedOperationIntent(
                EconomyValueMovementCapability.PayoutExecution,
                PostingTemplateKind.PayoutReservation,
                operation.WalletId,
                operation.WalletId,
                operation.Amount,
                [new RiskCurrencyLeg(operation.Amount.Currency, operation.Amount.Units)],
                sourceRoots,
                request.ProviderHash.Trim(),
                operation.DestinationHash,
                operation.IdempotencyKey,
                operation.CreatedAt,
                ProtectedSubjectId: operation.PayeeId);
            return await orchestrator.ExecuteAsync(intent, async (authorization, operationToken) =>
            {
                var receipt = authorization.Receipt;
                EnsureAuthorization(operation, request, sourceRoots, authorization);
                var authority = await capabilityResolver.ResolveAuthorityAsync(
                    ReservationCapabilityName,
                    PostingTemplateKind.PayoutReservation,
                    receipt,
                    operationToken).ConfigureAwait(false);
                if (authority.TenantId != operation.TenantId || authority.ActorId != operation.ActorId ||
                    authority.RiskDecisionId != receipt.RiskDecisionId)
                    throw new InvalidOperationException(
                        "The registered posting authority does not match the payout actor and tenant.");
                var authorizedOperation = operation with
                {
                    RiskDecisionId = authorization.RiskDecisionId,
                    KillSwitchEpoch = receipt.KillSwitchEpoch
                };
                operations.Add(authorizedOperation);
                await authorizationEvidence.AppendAsync(
                    new PayoutAuthorizationEvidence(
                        authorizedOperation.Id,
                        authorizedOperation.TenantId,
                        authorizedOperation.ActorId,
                        PayoutAuthorizationPhase.Reservation,
                        authorization.RiskDecisionId,
                        request.ReauthenticationEvidenceHash.Trim(),
                        Hash(authorization.OperationFingerprint),
                        receipt.Id,
                        receipt.ReceiptHash,
                        authorizedOperation.CreatedAt),
                    operationToken).ConfigureAwait(false);

                postings.Post(new RegisteredPostingRequest(
                    authority,
                    CreateReservationPosting(authorizedOperation),
                    fragments.Select(fragment => new RegisteredPostingAllocation(
                        1,
                        fragment.ParentLotId,
                        fragment.Amount.Units,
                        [fragment.Range]))
                        .ToArray()));

                return authorizedOperation;
            }, transactionToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureAuthorization(
        PayoutOperation operation,
        DurablePayoutReservationRequest request,
        IReadOnlyList<SourceStampId> sourceRoots,
        EconomyProtectedOperationAuthorization authorization)
    {
        var receipt = authorization.Receipt;
        var rootHashes = sourceRoots.Select(root => Hash(root.Value.ToString("N"))).ToArray();
        if (authorization.TenantId != operation.TenantId || authorization.ActorId != operation.ActorId ||
            !string.Equals(authorization.JurisdictionCode, request.JurisdictionCode.Trim(),
                StringComparison.Ordinal) ||
            receipt.TenantId != operation.TenantId || receipt.ActorId != operation.ActorId ||
            !string.Equals(receipt.SubjectReference,
                EconomySubjectReference.ForUser(operation.TenantId, operation.PayeeId),
                StringComparison.Ordinal) ||
            !string.Equals(receipt.JurisdictionCode, authorization.JurisdictionCode,
                StringComparison.Ordinal) ||
            receipt.RiskDecisionId != authorization.RiskDecisionId ||
            receipt.PolicyVersion != operation.PolicyVersion.Value ||
            receipt.ReserveVersion != operation.ReserveVersion.Value ||
            !string.Equals(receipt.ProviderHash, request.ProviderHash.Trim(), StringComparison.Ordinal) ||
            !string.Equals(receipt.DestinationHash, operation.DestinationHash, StringComparison.Ordinal) ||
            !receipt.SourceRootHashes.SequenceEqual(rootHashes, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "The payout capability receipt does not match the durable reservation snapshot.");
    }

    private static PostingRequest CreateReservationPosting(PayoutOperation operation) => new(
        new PostingId(operation.Id),
        new PostingTemplate(PostingTemplateKind.PayoutReservation, PostingTemplate.CurrentVersion),
        operation.IdempotencyKey,
        PostingAuthority.PayoutCoordinator,
        operation.ReserveVersion,
        operation.PolicyVersion,
        null,
        operation.CreatedAt,
        [
            new PostingLine(
                1,
                EntrySide.Debit,
                EconomyAccountCode.EarnedHardLiability,
                operation.Amount,
                operation.WalletId,
                null,
                ProvenanceKind.EarnedHard),
            new PostingLine(
                2,
                EntrySide.Credit,
                EconomyAccountCode.PayoutPayableHard,
                operation.Amount,
                null,
                null,
                null)
        ]);

    private static void Validate(DurablePayoutReservationRequest request)
    {
        var operation = request.Operation;
        if (operation.Id == Guid.Empty)
            throw new ArgumentException("Payout operation ID is required.", nameof(request));
        if (operation.TenantId == Guid.Empty)
            throw new ArgumentException("Payout operation tenant ID is required.", nameof(request));
        if (operation.State != PayoutOperationState.Reserved || operation.Version != 1)
            throw new InvalidOperationException("Only a new reserved payout operation can create an immutable reservation posting.");
        if (operation.Amount.Currency != CurrencyCode.HardCoin || operation.Amount.Units <= 0)
            throw new ArgumentException("Payout reservations require a positive hard-coin amount.", nameof(request));
        if (operation.RiskDecisionId != Guid.Empty)
            throw new InvalidOperationException("New payout reservations cannot carry a client-issued risk decision.");
        if (operation.KillSwitchEpoch < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Payout kill-switch epochs cannot be negative.");
        if (operation.FencingToken <= 0 || operation.ReserveAuthorizationEpoch <= 0)
            throw new ArgumentException("Payout reservation control epochs must be positive.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.JurisdictionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ReauthenticationEvidenceHash);
        if (request.ReauthenticationEvidenceHash.Trim().Length != 64)
            throw new ArgumentException(
                "Payout reauthentication evidence hashes must contain 64 characters.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProviderHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation.RequestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation.ProviderAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation.DestinationHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation.ProviderBindingHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation.EligibilityHash);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

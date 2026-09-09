using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Payouts;

public sealed record ReserveApprovedPayoutCommand(
    Guid TenantId,
    Guid ActorId,
    Guid RequestId,
    ReauthenticationEvidence Reauthentication);

public sealed record DispatchPayoutOperationCommand(
    Guid TenantId,
    Guid ActorId,
    Guid OperationId,
    long ExpectedVersion,
    ReauthenticationEvidence Reauthentication);

public sealed record ReconcilePayoutOperationCommand(
    Guid TenantId,
    Guid ActorId,
    Guid OperationId);

public interface IDurablePayoutApplicationService
{
    ValueTask<ConnectOnboardingResult> CreateOrRefreshAccountAsync(
        Guid tenantId,
        Guid payeeId,
        CancellationToken cancellationToken = default);

    ValueTask<ConnectAccountSnapshot> GetAccountAsync(
        Guid tenantId,
        Guid payeeId,
        CancellationToken cancellationToken = default);

    ValueTask<PayoutOperation> ReserveApprovedAsync(
        ReserveApprovedPayoutCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<PayoutOperation> DispatchAsync(
        DispatchPayoutOperationCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<PayoutOperation> ApplyProviderEventAsync(
        PayoutProviderEvent providerEvent,
        CancellationToken cancellationToken = default);

    ValueTask<PayoutOperation> ReconcileAsync(
        ReconcilePayoutOperationCommand command,
        CancellationToken cancellationToken = default);

    PayoutOperation Get(Guid tenantId, Guid operationId);
    IReadOnlyList<PayoutOperation> List(Guid tenantId, int take = 100);
}

public sealed class DurablePayoutApplicationService(
    IPayoutRequestStore requests,
    IPayoutOperationStore operations,
    IEconomyWalletDirectory wallets,
    IEconomyCapabilityPolicyStore policies,
    ICapabilityPolicySignatureVerifier policySignatureVerifier,
    IEconomyReserveCustodyControlPlane reserves,
    IPayoutFencingTokenAllocator fencingTokens,
    IFifoFragmentReservationReader reservationReader,
    IConnectPayoutProvider provider,
    IEconomyJurisdictionResolver jurisdictionResolver,
    IDurablePayoutReservationWorkflow reservationWorkflow,
    IDurablePayoutSettlementWorkflow settlementWorkflow,
    TimeProvider timeProvider) : IDurablePayoutApplicationService
{
    public async ValueTask<ConnectOnboardingResult> CreateOrRefreshAccountAsync(
        Guid tenantId,
        Guid payeeId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, payeeId);
        _ = await wallets.GetOwnerWalletAsync(tenantId, payeeId, cancellationToken).ConfigureAwait(false);
        var result = await provider.CreateOrRefreshAccountAsync(payeeId, cancellationToken).ConfigureAwait(false);
        ValidateAccountIdentity(result.Account, payeeId);
        return result;
    }

    public async ValueTask<ConnectAccountSnapshot> GetAccountAsync(
        Guid tenantId,
        Guid payeeId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(tenantId, payeeId);
        _ = await wallets.GetOwnerWalletAsync(tenantId, payeeId, cancellationToken).ConfigureAwait(false);
        var account = await provider.GetAccountAsync(payeeId, cancellationToken).ConfigureAwait(false);
        ValidateAccountIdentity(account, payeeId);
        return account;
    }

    public async ValueTask<PayoutOperation> ReserveApprovedAsync(
        ReserveApprovedPayoutCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateReservationCommand(command);
        var payoutRequest = requests.GetForReview(command.RequestId, command.TenantId);
        if (payoutRequest.State != PayoutRequestState.Approved)
            throw new PayoutEligibilityException("Only a fully approved payout request can reserve value.");

        var reviewEvents = requests.ListReviewAudit(payoutRequest.Id, payoutRequest.TenantId);
        ValidateDualControlReview(payoutRequest, reviewEvents);
        var requestHash = RequestHash(payoutRequest, reviewEvents);
        var idempotencyKey = new IdempotencyKey($"payout-request:{payoutRequest.Id:N}:reservation");
        var replay = operations.FindReplay(command.TenantId, idempotencyKey.Value, requestHash);
        if (replay is not null) return replay;

        var now = timeProvider.GetUtcNow();
        var jurisdiction = await jurisdictionResolver.ResolveAsync(
            command.TenantId, payoutRequest.PayeeId, null, null, now, cancellationToken)
            .ConfigureAwait(false);
        var transactionBinding = PayoutProtectedOperationBinding.Reservation(command.RequestId);
        var reauthentication = ReauthenticationEvidenceValidator.RequireFresh(
            command.Reauthentication,
            command.ActorId,
            ProtectedOperationKind.Payout,
            transactionBinding,
            ReauthenticationAssurance.MultiFactor,
            now);
        var wallet = await wallets.GetWalletAsync(
            command.TenantId, payoutRequest.WalletId, cancellationToken).ConfigureAwait(false);
        if (wallet.OwnerId != payoutRequest.PayeeId)
            throw new PayoutEligibilityException("The approved payout wallet is not owned by its payee.");

        var account = await provider.GetAccountAsync(payoutRequest.PayeeId, cancellationToken).ConfigureAwait(false);
        ValidateReadyAccount(account, payoutRequest.PayeeId, now);
        var (policy, settings) = await LoadPolicyAsync(
            command.TenantId, jurisdiction.JurisdictionCode, now, cancellationToken).ConfigureAwait(false);
        if (payoutRequest.Amount.Units < settings.MinimumAmountUnits ||
            payoutRequest.Amount.Units > settings.MaximumAmountUnits)
            throw new PayoutEligibilityException("Payout amount is outside the signed policy limits.");
        var reserve = await reserves.CurrentHeadAsync(now, cancellationToken).ConfigureAwait(false);
        var fencingToken = await fencingTokens.AllocateAsync(cancellationToken).ConfigureAwait(false);
        var providerBindingHash = Hash(string.Join('|',
            settings.ProviderHash,
            account.ProviderAccountId,
            account.DestinationHash,
            account.Version.ToString(CultureInfo.InvariantCulture),
            account.EvidenceHash,
            policy.PayloadHash,
            reserve.Version.Value.ToString(CultureInfo.InvariantCulture),
            reserve.AuthorizationEpoch.ToString(CultureInfo.InvariantCulture),
            reserve.EvidenceHash));
        var eligibilityHash = Hash(string.Join('|',
            requestHash,
            string.Join(',', reviewEvents.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id)
                .Select(ReviewEventHash)),
            reauthentication.EvidenceHash,
            providerBindingHash));
        var operation = new PayoutOperation(
            payoutRequest.Id,
            idempotencyKey,
            requestHash,
            command.ActorId,
            payoutRequest.PayeeId,
            payoutRequest.WalletId,
            payoutRequest.Amount,
            account.ProviderAccountId,
            account.DestinationHash,
            providerBindingHash,
            eligibilityHash,
            null,
            null,
            PayoutOperationState.Reserved,
            1,
            fencingToken,
            0,
            reserve.Version,
            reserve.AuthorizationEpoch,
            new PolicyVersion(policy.Version),
            Guid.Empty,
            now,
            now,
            command.TenantId);
        return await reservationWorkflow.ReserveAsync(
            new DurablePayoutReservationRequest(
                operation,
                jurisdiction.JurisdictionCode,
                reauthentication.EvidenceHash,
                settings.ProviderHash),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<PayoutOperation> DispatchAsync(
        DispatchPayoutOperationCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateDispatchCommand(command);
        var operation = operations.GetForTenant(command.TenantId, command.OperationId);
        if (operation.State != PayoutOperationState.Reserved || operation.Version != command.ExpectedVersion)
            throw new PayoutStaleCommandException("Payout is not reserved at the requested version.");

        var now = timeProvider.GetUtcNow();
        var transactionBinding = PayoutProtectedOperationBinding.Dispatch(
            command.OperationId, command.ExpectedVersion);
        var reauthentication = ReauthenticationEvidenceValidator.RequireFresh(
            command.Reauthentication,
            command.ActorId,
            ProtectedOperationKind.Payout,
            transactionBinding,
            ReauthenticationAssurance.MultiFactor,
            now);
        var jurisdiction = await jurisdictionResolver.ResolveAsync(
            operation.TenantId, operation.PayeeId, null, null, now, cancellationToken)
            .ConfigureAwait(false);
        var (policy, settings) = await LoadPolicyAsync(
            operation.TenantId, jurisdiction.JurisdictionCode, now, cancellationToken).ConfigureAwait(false);
        if (policy.Version != operation.PolicyVersion.Value)
            throw new PayoutStaleCommandException("The signed payout policy changed before dispatch.");
        var reserve = await reserves.CurrentHeadAsync(now, cancellationToken).ConfigureAwait(false);
        if (reserve.Version != operation.ReserveVersion ||
            reserve.AuthorizationEpoch != operation.ReserveAuthorizationEpoch)
            throw new PayoutStaleCommandException("The reserve authorization changed before payout dispatch.");
        var account = await provider.GetAccountAsync(operation.PayeeId, cancellationToken).ConfigureAwait(false);
        ValidateReadyAccount(account, operation.PayeeId, now);
        if (!string.Equals(account.ProviderAccountId, operation.ProviderAccountId, StringComparison.Ordinal) ||
            !string.Equals(account.DestinationHash, operation.DestinationHash, StringComparison.Ordinal))
            throw new PayoutProviderBindingException("The payout destination changed before dispatch.");

        var fragments = reservationReader.Read(
            operation.Id, PersistedFragmentReservationStatus.Reserved);
        if (fragments.Count == 0 || fragments.Sum(item => item.Amount.Units) != operation.Amount.Units)
            throw new PayoutStaleCommandException("Payout FIFO reservations are missing or incomplete.");
        var sourceRoots = fragments.Select(item => item.RootSourceStampId)
            .Distinct().OrderBy(item => item.Value).ToArray();
        return await settlementWorkflow.BeginDispatchAsync(
            new DurablePayoutDispatchRequest(
                operation.Id,
                command.ActorId,
                operation.Version,
                operation.FencingToken,
                operation.KillSwitchEpoch,
                jurisdiction.JurisdictionCode,
                reauthentication.EvidenceHash,
                settings.ProviderHash,
                sourceRoots,
                now),
            cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<PayoutOperation> ApplyProviderEventAsync(
        PayoutProviderEvent providerEvent,
        CancellationToken cancellationToken = default) =>
        new(settlementWorkflow.ApplyProviderEventAsync(
            new DurablePayoutProviderEventRequest(providerEvent), cancellationToken));

    public async ValueTask<PayoutOperation> ReconcileAsync(
        ReconcilePayoutOperationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateIdentity(command.TenantId, command.ActorId);
        if (command.OperationId == Guid.Empty)
            throw new ArgumentException("Payout operation ID is required.", nameof(command));
        var operation = operations.GetForTenant(command.TenantId, command.OperationId);
        if (operation.State is not (PayoutOperationState.Dispatching or PayoutOperationState.Ambiguous))
            throw new PayoutStaleCommandException("Only an in-flight payout can be reconciled.");
        if (string.IsNullOrWhiteSpace(operation.ProviderPayoutId))
            throw new PayoutStaleCommandException("Provider payout identity is unavailable for reconciliation.");
        var providerEvent = await provider.ReconcileAsync(
            operation.Id, operation.ProviderPayoutId, cancellationToken).ConfigureAwait(false);
        if (providerEvent.Outcome is not (PayoutProviderOutcome.Succeeded or PayoutProviderOutcome.Failed))
            return operations.GetForTenant(operation.TenantId, operation.Id);
        return await settlementWorkflow.ApplyProviderEventAsync(
            new DurablePayoutProviderEventRequest(providerEvent), cancellationToken).ConfigureAwait(false);
    }

    public PayoutOperation Get(Guid tenantId, Guid operationId) =>
        operations.GetForTenant(tenantId, operationId);

    public IReadOnlyList<PayoutOperation> List(Guid tenantId, int take = 100) =>
        operations.ListForTenant(tenantId, take);

    private async ValueTask<(EconomyCapabilityPolicy Policy, PayoutExecutionPolicy Settings)> LoadPolicyAsync(
        Guid tenantId,
        string jurisdiction,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var policy = await policies.CurrentAsync(
                tenantId, EconomyValueMovementCapability.PayoutExecution,
                jurisdiction, cancellationToken).ConfigureAwait(false)
            ?? await policies.CurrentAsync(
                null, EconomyValueMovementCapability.PayoutExecution,
                jurisdiction, cancellationToken).ConfigureAwait(false)
            ?? throw new PayoutExecutionDisabledException(
                "No signed payout policy is active for the jurisdiction.");
        if (policy.State != EconomyCapabilityPolicyState.Active ||
            policy.EffectiveAt > now || policy.ExpiresAt <= now || !policy.ProviderReady)
            throw new PayoutExecutionDisabledException("Payout policy or provider readiness is unavailable.");
        if (!string.Equals(Hash(policy.CanonicalPayload), policy.PayloadHash, StringComparison.Ordinal) ||
            !await policySignatureVerifier.VerifyAsync(
                policy.CanonicalPayload, policy.KeyId, policy.Signature, cancellationToken).ConfigureAwait(false))
            throw new PayoutExecutionDisabledException("Payout policy signature is invalid.");
        return (policy, ParsePolicy(policy.CanonicalPayload));
    }

    private static PayoutExecutionPolicy ParsePolicy(string canonicalPayload)
    {
        try
        {
            using var document = JsonDocument.Parse(canonicalPayload);
            var root = document.RootElement;
            var providerHash = NormalizeBinding(root.GetProperty("providerHash").GetString()!, "providerHash");
            var minimum = root.GetProperty("minimumAmountUnits").GetInt64();
            var maximum = root.GetProperty("maximumAmountUnits").GetInt64();
            if (minimum <= 0 || maximum < minimum)
                throw new PayoutExecutionDisabledException("The active payout policy limits are invalid.");
            return new PayoutExecutionPolicy(providerHash, minimum, maximum);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or
                                           FormatException or OverflowException or JsonException or ArgumentException)
        {
            throw new PayoutExecutionDisabledException(
                "The active payout policy payload is invalid.", exception);
        }
    }

    private static void ValidateReservationCommand(ReserveApprovedPayoutCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateIdentity(command.TenantId, command.ActorId);
        if (command.RequestId == Guid.Empty)
            throw new ArgumentException("Payout request ID is required.", nameof(command));
        ArgumentNullException.ThrowIfNull(command.Reauthentication);
    }

    private static void ValidateDispatchCommand(DispatchPayoutOperationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateIdentity(command.TenantId, command.ActorId);
        if (command.OperationId == Guid.Empty)
            throw new ArgumentException("Payout operation ID is required.", nameof(command));
        if (command.ExpectedVersion <= 0) throw new ArgumentOutOfRangeException(nameof(command));
        ArgumentNullException.ThrowIfNull(command.Reauthentication);
    }

    private static void ValidateDualControlReview(
        PayoutRequest request,
        IReadOnlyList<PayoutRequestReviewAuditEvent> events)
    {
        var approvals = events.Where(item => item.Outcome == PayoutRequestState.Approved)
            .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToArray();
        if (approvals.Length != 2 || approvals.Select(item => item.ActorId).Distinct().Count() != 2 ||
            approvals.Any(item => item.TenantId != request.TenantId || item.RequestId != request.Id ||
                                  item.ActorId == request.PayeeId || string.IsNullOrWhiteSpace(item.Reason)))
            throw new PayoutEligibilityException("Payout approval audit does not prove independent dual control.");
    }

    private static void ValidateReadyAccount(
        ConnectAccountSnapshot account,
        Guid payeeId,
        DateTimeOffset now)
    {
        ValidateAccountIdentity(account, payeeId);
        if (account.State != ConnectAccountState.Ready || !account.ChargesEnabled || !account.PayoutsEnabled ||
            account.ObservedAt > now || account.ExpiresAt <= now || string.IsNullOrWhiteSpace(account.EvidenceHash))
            throw new PayoutExecutionDisabledException("The connected payout account is not ready or current.");
    }

    private static void ValidateAccountIdentity(ConnectAccountSnapshot account, Guid payeeId)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.PayeeId != payeeId || string.IsNullOrWhiteSpace(account.ProviderAccountId) ||
            string.IsNullOrWhiteSpace(account.DestinationHash))
            throw new PayoutProviderBindingException("Connected payout account is not bound to the payee.");
    }

    private static string RequestHash(
        PayoutRequest request,
        IReadOnlyList<PayoutRequestReviewAuditEvent> events) => Hash(string.Join('|',
        request.TenantId.ToString("N"),
        request.Id.ToString("N"),
        request.PayeeId.ToString("N"),
        request.WalletId.Value.ToString("N"),
        request.Amount.Units.ToString(CultureInfo.InvariantCulture),
        request.Version.ToString(CultureInfo.InvariantCulture),
        request.RequestHash,
        string.Join(',', events.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).Select(ReviewEventHash))));

    private static string ReviewEventHash(PayoutRequestReviewAuditEvent item) => Hash(string.Join('|',
        item.Id.ToString("N"),
        item.RequestId.ToString("N"),
        item.TenantId.ToString("N"),
        item.ActorId.ToString("N"),
        ((int)item.Outcome).ToString(CultureInfo.InvariantCulture),
        item.Reason,
        item.OccurredAt.UtcTicks.ToString(CultureInfo.InvariantCulture)));

    private static string NormalizeBinding(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 128)
            throw new ArgumentOutOfRangeException(parameterName, "Payout bindings cannot exceed 128 characters.");
        return normalized;
    }

    private static void ValidateIdentity(Guid tenantId, Guid actorId)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Payout tenant and actor IDs are required.");
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record PayoutExecutionPolicy(
        string ProviderHash,
        long MinimumAmountUnits,
        long MaximumAmountUnits);
}

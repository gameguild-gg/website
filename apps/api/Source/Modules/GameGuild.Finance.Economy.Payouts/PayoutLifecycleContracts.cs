using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Payouts;

public enum ConnectAccountState
{
    Pending = 1,
    Restricted = 2,
    Ready = 3,
    Disabled = 4
}

public enum PayoutOperationState
{
    Reserved = 1,
    Dispatching = 2,
    Ambiguous = 3,
    Succeeded = 4,
    Failed = 5,
    Cancelled = 6
}

public enum PayoutProviderOutcome
{
    Submitted = 1,
    Ambiguous = 2,
    Succeeded = 3,
    Failed = 4
}

public sealed record ConnectAccountSnapshot(
    Guid PayeeId,
    string ProviderAccountId,
    string DestinationHash,
    ConnectAccountState State,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    long Version,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string EvidenceHash);

public sealed record ConnectOnboardingResult(
    ConnectAccountSnapshot Account,
    Uri? OnboardingUri);

public sealed record PayoutKycSnapshot(
    Guid PayeeId,
    long Version,
    bool IsApproved,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string EvidenceHash);

public sealed record PayoutRollingReserveSnapshot(
    long Version,
    long EligibleHardUnits,
    long ReservedHardUnits,
    int ReserveBasisPoints,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string EvidenceHash)
{
    public long ReleasableHardUnits
    {
        get
        {
            var policyReserve = checked((EligibleHardUnits * ReserveBasisPoints + 9_999L) / 10_000L);
            return Math.Max(0, checked(EligibleHardUnits - policyReserve - ReservedHardUnits));
        }
    }
}

public sealed record PayoutReservationRequest(
    Guid OperationId,
    IdempotencyKey IdempotencyKey,
    Guid ActorId,
    Guid PayeeId,
    WalletId WalletId,
    CoinAmount Amount,
    WalletLifecycleState WalletState,
    PolicyVersion PolicyVersion,
    ReserveVersion ReserveVersion,
    long ReserveAuthorizationEpoch,
    long FeatureVersion,
    string ExpectedProviderAccountId,
    string DestinationHash,
    RiskEntityNode AccountNode,
    RiskEntityNode DestinationNode,
    DateTimeOffset RequestedAt,
    Guid TenantId = default);

public sealed record PayoutDispatchCommand(
    Guid OperationId,
    long ExpectedVersion,
    long FencingToken,
    long KillSwitchEpoch,
    string ProviderAccountId,
    string DestinationHash,
    CoinAmount Amount,
    string DispatchSnapshotHash,
    string IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record PayoutDispatchReceipt(
    Guid OperationId,
    PayoutProviderOutcome Outcome,
    string ProviderPayoutId,
    string ProviderAccountId,
    string DestinationHash,
    string EvidenceHash,
    string Signature,
    DateTimeOffset ObservedAt);

public sealed record PayoutProviderEvent(
    string EventId,
    Guid OperationId,
    PayoutProviderOutcome Outcome,
    string ProviderPayoutId,
    string ProviderAccountId,
    string DestinationHash,
    string EvidenceHash,
    string Signature,
    DateTimeOffset ObservedAt);

public sealed record PayoutRiskRequest(
    ProtectedOperationContext Context,
    PayoutKycSnapshot Kyc,
    IReadOnlyList<ExternalRiskEvidence> ExternalEvidence,
    PayoutRollingReserveSnapshot RollingReserve,
    ConnectAccountSnapshot Account,
    EntityRiskCluster EntityCluster,
    string EligibilityHash,
    DateTimeOffset RequestedAt);

public sealed record PayoutOperation(
    Guid Id,
    IdempotencyKey IdempotencyKey,
    string RequestHash,
    Guid ActorId,
    Guid PayeeId,
    WalletId WalletId,
    CoinAmount Amount,
    string ProviderAccountId,
    string DestinationHash,
    string ProviderBindingHash,
    string EligibilityHash,
    string? DispatchSnapshotHash,
    string? ProviderPayoutId,
    PayoutOperationState State,
    long Version,
    long FencingToken,
    long KillSwitchEpoch,
    ReserveVersion ReserveVersion,
    long ReserveAuthorizationEpoch,
    PolicyVersion PolicyVersion,
    Guid RiskDecisionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid TenantId = default)
{
    public PayoutOperation BindProviderDispatch(string providerPayoutId, DateTimeOffset occurredAt)
    {
        if (State is not (PayoutOperationState.Dispatching or PayoutOperationState.Ambiguous))
            throw new InvalidOperationException("Provider dispatch can only bind an in-flight payout.");
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPayoutId);
        return this with
        {
            ProviderPayoutId = providerPayoutId.Trim(),
            Version = checked(Version + 1),
            UpdatedAt = occurredAt
        };
    }

    public PayoutOperation Transition(
        PayoutOperationState next,
        DateTimeOffset occurredAt,
        string? dispatchSnapshotHash = null,
        string? providerPayoutId = null)
    {
        var allowed = (State, next) is
            (PayoutOperationState.Reserved, PayoutOperationState.Dispatching) or
            (PayoutOperationState.Reserved, PayoutOperationState.Cancelled) or
            (PayoutOperationState.Dispatching, PayoutOperationState.Ambiguous) or
            (PayoutOperationState.Dispatching, PayoutOperationState.Succeeded) or
            (PayoutOperationState.Dispatching, PayoutOperationState.Failed) or
            (PayoutOperationState.Ambiguous, PayoutOperationState.Succeeded) or
            (PayoutOperationState.Ambiguous, PayoutOperationState.Failed);
        if (!allowed)
            throw new InvalidOperationException($"Payout cannot transition from {State} to {next}.");
        return this with
        {
            State = next,
            Version = checked(Version + 1),
            DispatchSnapshotHash = dispatchSnapshotHash ?? DispatchSnapshotHash,
            ProviderPayoutId = providerPayoutId ?? ProviderPayoutId,
            UpdatedAt = occurredAt
        };
    }
}

public interface IConnectPayoutProvider
{
    ValueTask<ConnectOnboardingResult> CreateOrRefreshAccountAsync(
        Guid payeeId,
        CancellationToken cancellationToken = default);

    ValueTask<ConnectAccountSnapshot> GetAccountAsync(
        Guid payeeId,
        CancellationToken cancellationToken = default);

    ValueTask<PayoutDispatchReceipt> DispatchAsync(
        PayoutDispatchCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<PayoutProviderEvent> ReconcileAsync(
        Guid operationId,
        string providerPayoutId,
        CancellationToken cancellationToken = default);
}

public interface IPayoutKycEligibilitySource
{
    ValueTask<PayoutKycSnapshot> ReadAsync(
        Guid payeeId,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);
}

public interface IPayoutRollingReserveSource
{
    ValueTask<PayoutRollingReserveSnapshot> ReadAsync(
        WalletId walletId,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);
}

public interface IPayoutRiskDecisionSource
{
    ValueTask<RiskDecisionSnapshot> DecideAsync(
        PayoutRiskRequest request,
        CancellationToken cancellationToken = default);
}

public interface IPayoutReauthenticationSource
{
    ValueTask<ReauthenticationEvidence> ReadAsync(
        Guid actorId,
        string transactionBinding,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);
}

public interface IPayoutProviderEvidenceVerifier
{
    bool Verify(PayoutDispatchReceipt receipt);
    bool Verify(PayoutProviderEvent providerEvent);
}

public interface IIndependentAnchorVerifier
{
    bool Verify(ChainAnchor anchor);
}

public sealed class PayoutExecutionGate
{
    private readonly object _gate = new();
    private bool _enabled;
    private long _epoch;

    public PayoutExecutionGate(bool enabled = false, long epoch = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(epoch);
        _enabled = enabled;
        _epoch = epoch;
    }

    public bool IsEnabled
    {
        get { lock (_gate) return _enabled; }
    }

    public long Epoch
    {
        get { lock (_gate) return _epoch; }
    }

    public void EnsureEnabled()
    {
        lock (_gate)
            if (!_enabled)
                throw new PayoutExecutionDisabledException("Payout execution is disabled pending external approval.");
    }

    public long Stop()
    {
        lock (_gate)
        {
            _enabled = false;
            return _epoch = checked(_epoch + 1);
        }
    }
}

public sealed class PayoutExecutionDisabledException : InvalidOperationException
{
    public PayoutExecutionDisabledException(string message) : base(message) { }
    public PayoutExecutionDisabledException(string message, Exception innerException) : base(message, innerException) { }
}
public sealed class PayoutEligibilityException(string message) : InvalidOperationException(message);
public sealed class PayoutProviderBindingException(string message) : InvalidOperationException(message);
public sealed class PayoutStaleCommandException(string message) : InvalidOperationException(message);
public sealed class PayoutEvidenceException(string message) : InvalidOperationException(message);
public sealed class PayoutReplayConflictException(string message) : InvalidOperationException(message);

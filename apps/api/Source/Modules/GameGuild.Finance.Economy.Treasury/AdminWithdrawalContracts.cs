using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury;

public enum AdminWithdrawalRunState
{
    PendingApproval = 1,
    Approved = 2,
    Dispatching = 3,
    Ambiguous = 4,
    Succeeded = 5,
    Failed = 6,
    Cancelled = 7
}

public enum AdminWithdrawalProviderOutcome
{
    Submitted = 1,
    Ambiguous = 2,
    Succeeded = 3,
    Failed = 4
}

public sealed record AdminWithdrawalReservationRequest(
    Guid RunId,
    Guid TenantId,
    IdempotencyKey IdempotencyKey,
    Guid RequestedBy,
    WalletId PlatformFeeWalletId,
    DateOnly PeriodStart,
    PolicyVersion PolicyVersion,
    ReserveVersion ReserveVersion,
    long ReserveAuthorizationEpoch,
    string SourceAssetKey,
    string DestinationHash,
    DateTimeOffset RequestedAt);

public sealed record AdminWithdrawalDispatchCommand(
    Guid RunId,
    Guid TenantId,
    long ExpectedVersion,
    long FencingToken,
    long ExecutionEpoch,
    CoinAmount Amount,
    string SourceAssetKey,
    string DestinationHash,
    string DispatchSnapshotHash,
    string IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record AdminWithdrawalProviderReceipt(
    Guid RunId,
    Guid TenantId,
    AdminWithdrawalProviderOutcome Outcome,
    string ProviderTransferId,
    long FencingToken,
    long ExecutionEpoch,
    CoinAmount Amount,
    string SourceAssetKey,
    string DestinationHash,
    string EvidenceHash,
    string Signature,
    DateTimeOffset ObservedAt);

public sealed record AdminWithdrawalProviderEvent(
    string EventId,
    Guid RunId,
    Guid TenantId,
    AdminWithdrawalProviderOutcome Outcome,
    string ProviderTransferId,
    long FencingToken,
    long ExecutionEpoch,
    CoinAmount Amount,
    string SourceAssetKey,
    string DestinationHash,
    string EvidenceHash,
    string Signature,
    DateTimeOffset ObservedAt);

public sealed record AdminWithdrawalRun(
    Guid Id,
    Guid TenantId,
    IdempotencyKey IdempotencyKey,
    string RequestHash,
    DateOnly PeriodStart,
    Guid RequestedBy,
    Guid? ApprovedBy,
    WalletId PlatformFeeWalletId,
    CoinAmount Amount,
    string SourceAssetKey,
    string DestinationHash,
    AdminWithdrawalRunState State,
    long Version,
    long FencingToken,
    long ExecutionEpoch,
    ReserveVersion ReserveVersion,
    long ReserveAuthorizationEpoch,
    PolicyVersion PolicyVersion,
    string? DispatchSnapshotHash,
    string? ProviderTransferId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IAdminWithdrawalProvider
{
    ValueTask<AdminWithdrawalProviderReceipt> DispatchAsync(
        AdminWithdrawalDispatchCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<AdminWithdrawalProviderEvent> ReconcileAsync(
        Guid tenantId,
        Guid runId,
        string idempotencyKey,
        string? providerTransferId,
        CancellationToken cancellationToken = default);
}

public interface IAdminWithdrawalProviderEvidenceVerifier
{
    bool Verify(AdminWithdrawalProviderReceipt receipt);
    bool Verify(AdminWithdrawalProviderEvent providerEvent);
}

public sealed class AdminWithdrawalExecutionGate
{
    private readonly object _gate = new();
    private bool _enabled;
    private long _epoch;

    public AdminWithdrawalExecutionGate(bool enabled = false, long epoch = 1)
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
                throw new AdminWithdrawalExecutionDisabledException(
                    "Admin withdrawal execution is disabled pending legal and operational approval.");
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

public static class AdminWithdrawalReservationSnapshotGuard
{
    public static void EnsureUnchanged(long activeHoldUnits, string expectedSelectionHash, string currentSelectionHash)
    {
        if (activeHoldUnits > 0)
            throw new AdminWithdrawalEligibilityException("An active hold blocks platform fee withdrawal.");
        if (!string.Equals(expectedSelectionHash, currentSelectionHash, StringComparison.Ordinal))
            throw new AdminWithdrawalStaleCommandException(
                "Eligible platform fee fragments changed before reservation.");
    }
}
public sealed class AdminWithdrawalOverlapException(string message) : InvalidOperationException(message);
public sealed class AdminWithdrawalEligibilityException(string message) : InvalidOperationException(message);
public sealed class AdminWithdrawalApprovalException(string message) : InvalidOperationException(message);
public sealed class AdminWithdrawalStaleCommandException(string message) : InvalidOperationException(message);
public sealed class AdminWithdrawalEvidenceException(string message) : InvalidOperationException(message);
public sealed class AdminWithdrawalExecutionDisabledException(string message) : InvalidOperationException(message);

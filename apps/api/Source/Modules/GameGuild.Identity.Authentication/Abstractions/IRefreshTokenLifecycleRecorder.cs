namespace GameGuild.Identity.Authentication;

/// <summary>Persists credential-free lifecycle evidence at the operation's transaction boundary.</summary>
public interface IRefreshTokenLifecycleRecorder
{
    /// <summary>Enlists evidence in the transaction that owns the token mutation.</summary>
    Task RecordMutationAsync(RefreshTokenLifecycleEvent lifecycleEvent, CancellationToken cancellationToken);

    /// <summary>Persists a denial independently so the command's rollback does not erase it.</summary>
    Task RecordRejectionAsync(RefreshTokenLifecycleEvent lifecycleEvent, CancellationToken cancellationToken);
}

/// <summary>Only opaque database identifiers and bounded classifications; never credentials or hashes.</summary>
public sealed record RefreshTokenLifecycleEvent(
    RefreshTokenLifecycleOperation Operation,
    Guid? UserId = null,
    Guid? TokenId = null,
    Guid? SessionId = null,
    Guid? TenantId = null,
    Guid? ParentTokenId = null,
    RefreshTokenLifecycleReason Reason = RefreshTokenLifecycleReason.None);

public enum RefreshTokenLifecycleOperation
{
    Issued,
    Rotated,
    Rejected,
    ReplayContained,
    Revoked,
    AllRevoked
}

public enum RefreshTokenLifecycleReason
{
    None,
    Missing,
    Unknown,
    Expired,
    UserUnavailable,
    TenantDenied,
    Revoked,
    SessionInactive,
    ConcurrentRotation
}

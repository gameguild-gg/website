using GameGuild.CQRS.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Entity for Just-in-Time (JIT) permission elevation requests
///     Enables time-bound temporary permission grants with approval workflow
/// </summary>
public class JitElevationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RequesterId { get; set; }

    public TenantId? TenantId { get; set; }

    public string Permission { get; set; } = string.Empty;

    public string? ResourceType { get; set; }

    public Guid? ResourceId { get; set; }

    public string Justification { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public ElevationRequestStatus Status { get; set; } = ElevationRequestStatus.Pending;

    public Guid? ReviewerId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewerComments { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? RevokedBy { get; set; }

    public string? RevocationReason { get; set; }

    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     Check if request is currently active
    /// </summary>
    public bool IsActive()
    {
        if (Status != ElevationRequestStatus.Active)
        {
            return false;
        }

        var now = SystemClock.UtcNow;
        var startTime = StartsAt ?? CreatedAt;

        return now >= startTime && now < ExpiresAt;
    }

    /// <summary>
    ///     Check if the elevation currently confers its permission (enforcement window).
    ///     An elevation is in force when it is <see cref="ElevationRequestStatus.Active"/>,
    ///     or <see cref="ElevationRequestStatus.Approved"/> with a start time that has
    ///     already arrived (lazy window entry — approval grants the window, no separate
    ///     activation step is required), and the current time is within [start, expiry).
    ///     This is the predicate used by permission evaluation to honor JIT grants.
    /// </summary>
    public bool IsGrantInForce()
    {
        var now = SystemClock.UtcNow;

        var statusInForce = Status == ElevationRequestStatus.Active
            || (Status == ElevationRequestStatus.Approved
                && StartsAt.HasValue
                && StartsAt.Value <= now);
        if (!statusInForce) return false;

        var startTime = StartsAt ?? CreatedAt;
        return now >= startTime && now < ExpiresAt;
    }

    /// <summary>
    ///     Check if request has expired
    /// </summary>
    public bool IsExpired() => Status == ElevationRequestStatus.Active && SystemClock.UtcNow >= ExpiresAt;

    /// <summary>
    ///     Approve the elevation request
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the request is not pending, or when the reviewer is the requester
    ///     (self-approval is prohibited — elevation requires approval by a different user).
    /// </exception>
    public void Approve(Guid reviewerId, string? comments = null)
    {
        if (Status != ElevationRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending requests can be approved");
        }

        if (reviewerId == RequesterId)
            throw new InvalidOperationException("Self-approval of elevation requests is not allowed");

        Status = ElevationRequestStatus.Approved;
        ReviewerId = reviewerId;
        ReviewedAt = SystemClock.UtcNow;
        ReviewerComments = comments;
        UpdatedAt = SystemClock.UtcNow;

        // Auto-activate if no start time specified
        if (StartsAt is null)
        {
            Activate();
            return;
        }

        if (StartsAt.Value <= SystemClock.UtcNow)
        {
            Activate();
        }
    }

    /// <summary>
    ///     Deny the elevation request
    /// </summary>
    public void Deny(Guid reviewerId, string comments)
    {
        if (Status != ElevationRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending requests can be denied");
        }

        Status = ElevationRequestStatus.Denied;
        ReviewerId = reviewerId;
        ReviewedAt = SystemClock.UtcNow;
        ReviewerComments = comments;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Activate the approved elevation
    /// </summary>
    public void Activate()
    {
        if (Status != ElevationRequestStatus.Approved)
        {
            throw new InvalidOperationException("Only approved requests can be activated");
        }

        Status = ElevationRequestStatus.Active;
        ActivatedAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Revoke the active elevation
    /// </summary>
    public void Revoke(Guid revokedBy, string reason)
    {
        if (Status != ElevationRequestStatus.Active && Status != ElevationRequestStatus.Approved)
        {
            throw new InvalidOperationException("Only active or approved requests can be revoked");
        }

        Status = ElevationRequestStatus.Revoked;
        RevokedBy = revokedBy;
        RevokedAt = SystemClock.UtcNow;
        RevocationReason = reason;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Mark as expired
    /// </summary>
    public void MarkExpired()
    {
        if (Status == ElevationRequestStatus.Active || Status == ElevationRequestStatus.Approved)
        {
            Status = ElevationRequestStatus.Expired;
            UpdatedAt = SystemClock.UtcNow;
        }
    }

    /// <summary>
    ///     Calculate remaining time in minutes
    /// </summary>
    public int GetRemainingMinutes()
    {
        if (!IsActive())
        {
            return 0;
        }

        var remaining = ExpiresAt - SystemClock.UtcNow;
        return (int)Math.Max(0, remaining.TotalMinutes);
    }
}

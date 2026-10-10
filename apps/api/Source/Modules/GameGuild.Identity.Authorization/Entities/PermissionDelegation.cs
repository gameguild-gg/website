using GameGuild.CQRS.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Permission delegation allows users to delegate their permissions to other users
/// </summary>
public class PermissionDelegation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DelegatorUserId { get; set; }

    public Guid DelegateUserId { get; set; }

    public TenantId? TenantId { get; set; }

    public Guid? ResourceId { get; set; }

    public string[] DelegatedPermissions { get; set; } = Array.Empty<string>();

    public DateTime StartsAt { get; set; } = SystemClock.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool CanSubDelegate { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public string? Reason { get; set; }

    public string? Conditions { get; set; } // JSON serialized conditions

    public int? UsageLimit { get; set; }

    public int UsageCount { get; set; }

    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     Check if delegation is currently valid and active
    /// </summary>
    public bool IsValidNow()
    {
        if (!IsActive)
        {
            return false;
        }

        var now = SystemClock.UtcNow;
        if (StartsAt > now)
        {
            return false;
        }

        if (ExpiresAt is not null)
        {
            if (ExpiresAt.Value <= now)
            {
                return false;
            }
        }

        if (UsageLimit is not null)
        {
            if (UsageCount >= UsageLimit.Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Check if delegation allows a specific permission
    /// </summary>
    public bool AllowsPermission(string permission) =>
        IsValidNow() && DelegatedPermissions.Contains(permission);

    /// <summary>
    ///     Record usage of the delegation
    /// </summary>
    public void RecordUsage()
    {
        if (!IsValidNow())
        {
            throw new InvalidOperationException("Cannot record usage for invalid delegation");
        }

        UsageCount++;
        UpdatedAt = SystemClock.UtcNow;

        // Auto-deactivate if usage limit reached
        if (UsageLimit.HasValue && UsageCount >= UsageLimit.Value)
        {
            IsActive = false;
        }
    }

    /// <summary>
    ///     Activate the delegation
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Deactivate/revoke the delegation
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Check if delegation has expired
    /// </summary>
    public bool IsExpired() => ExpiresAt.HasValue && SystemClock.UtcNow > ExpiresAt.Value;

    /// <summary>
    ///     Extend the expiration date
    /// </summary>
    public void Extend(DateTime newExpiresAt)
    {
        if (newExpiresAt <= SystemClock.UtcNow)
        {
            throw new ArgumentException("New expiration must be in the future", nameof(newExpiresAt));
        }

        ExpiresAt = newExpiresAt;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Check if delegation has usage limit remaining
    /// </summary>
    public bool HasUsageRemaining() =>
        !UsageLimit.HasValue || UsageCount < UsageLimit.Value;

    /// <summary>
    ///     Get remaining usage count
    /// </summary>
    public int? GetRemainingUsage()
    {
        if (!UsageLimit.HasValue)
        {
            return null;
        }

        return Math.Max(0, UsageLimit.Value - UsageCount);
    }
}

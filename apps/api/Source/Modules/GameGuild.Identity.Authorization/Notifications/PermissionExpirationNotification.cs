using GameGuild.CQRS;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     When a permission lifecycle event crosses the expiration boundary.
/// </summary>
public enum PermissionExpirationKind
{
    /// <summary>The permission will expire within the configured upcoming window.</summary>
    Upcoming = 0,

    /// <summary>The permission expiration timestamp has passed and the grant is no longer honored.</summary>
    Expired = 1
}

/// <summary>
///     Notification published when a permission grant is about to expire or has expired.
///     Handlers can integrate with the Communication module's notification infrastructure
///     to deliver in-app or email notifications (same pattern as <see cref="AccessReviewReminderNotification"/>).
/// </summary>
public record PermissionExpirationNotification(
    Guid PermissionId,
    Guid? UserId,
    Guid? TenantId,
    string[] Permissions,
    DateTime ExpiresAt,
    PermissionExpirationKind Kind
) : INotification;

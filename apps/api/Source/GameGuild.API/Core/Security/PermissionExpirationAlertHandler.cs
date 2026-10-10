using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Options;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;

namespace GameGuild.API.Core.Security;

/// <summary>
///     Delivers in-app and email alerts when a permission grant approaches or crosses its
///     expiration boundary (issue #331). Consumes the <see cref="PermissionExpirationNotification"/>
///     published by <c>PermissionExpirationService</c> and routes it through the Notifications
///     module, so the recipient's per-channel preferences (drop/digest/send) are honored.
/// </summary>
/// <remarks>
///     <para>
///         Resides in the API composition root (same placement as
///         <see cref="RefreshTokenReplayAlertHandler"/>) because the publishing module must stay
///         platform-pure and cannot reference the Notifications delivery stack. Registered by the
///         CQRS assembly scan as an <see cref="INotificationHandler{TNotification}"/>.
///     </para>
///     <para>
///         <b>Priority mapping:</b> expirations that already revoked access are delivered as
///         <see cref="NotificationPriority.Urgent"/>; upcoming-expiration reminders as
///         <see cref="NotificationPriority.Normal"/>.
///     </para>
///     <para>
///         <b>Config gate:</b> delivery is skipped when <c>Authorization:PermissionExpiration:Enabled</c>
///         is false. The worker that publishes these notifications honors the same switch, but this
///         second gate also covers notifications published by administrative triggers while the
///         feature is disabled.
///     </para>
///     <para>
///         <b>Failure semantics:</b> queueing failures are logged, not thrown. The publisher stamps
///         reminder dedup metadata before dispatch and has no redelivery mechanism, so rethrowing
///         would only add noise without retrying the alert.
///     </para>
/// </remarks>
internal sealed class PermissionExpirationAlertHandler(
    IUserRepository users,
    INotificationService notifications,
    IOptions<PermissionExpirationOptions> expirationOptions,
    ILogger<PermissionExpirationAlertHandler> logger) : INotificationHandler<PermissionExpirationNotification>
{
    private const int MaxDisplayedPermissions = 5;

    public async Task Handle(PermissionExpirationNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!expirationOptions.Value.Enabled)
        {
            logger.LogDebug("Permission expiration alerts are disabled by configuration; dropping notification {PermissionId}.",
                notification.PermissionId);
            return;
        }

        if (notification.UserId is not { } userId)
        {
            // Grant without a subject (e.g. a tenant-wide grant); nothing user-facing to deliver.
            return;
        }

        var user = await users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDeleted)
        {
            return;
        }

        var isExpired = notification.Kind == PermissionExpirationKind.Expired;
        var title = isExpired ? "Permissions expired" : "Permissions expiring soon";
        var message = isExpired
            ? $"Access granted by {DescribePermissions(notification.Permissions)} expired on {FormatTimestamp(notification.ExpiresAt)} " +
              "and no longer grants access. Contact your administrator if you still need it."
            : $"Access granted by {DescribePermissions(notification.Permissions)} expires on {FormatTimestamp(notification.ExpiresAt)}. " +
              "Contact your administrator to extend the grant before it lapses.";

        await QueueAsync(NotificationChannel.InApp, null).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await QueueAsync(NotificationChannel.Email, user.Email).ConfigureAwait(false);
        }

        async Task QueueAsync(NotificationChannel channel, string? recipientEmail)
        {
            var result = await notifications.SendAsync(user.Id, NotificationType.Security, title, message, channel,
                notification.TenantId, priority: isExpired ? NotificationPriority.Urgent : NotificationPriority.Normal,
                referenceEntityId: notification.PermissionId,
                referenceEntityType: nameof(PermissionExpirationNotification),
                recipientEmail: recipientEmail, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result is null || result.IsFailure)
            {
                logger.LogWarning(
                    "Failed to queue {Kind} permission-expiration alert for user {UserId} on channel {Channel}: {Error}.",
                    notification.Kind, userId, channel, result?.Error.Description ?? "unknown error");
            }
        }
    }

    private static string DescribePermissions(IReadOnlyList<string> permissions)
    {
        if (permissions.Count == 0)
        {
            return "your permission grant";
        }

        var displayed = permissions.Count > MaxDisplayedPermissions
            ? string.Join(", ", permissions.Take(MaxDisplayedPermissions)) + $" and {permissions.Count - MaxDisplayedPermissions} more"
            : string.Join(", ", permissions);

        return $"'{displayed}'";
    }

    private static string FormatTimestamp(DateTime expiresAt)
    {
        return expiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
    }
}

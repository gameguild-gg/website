using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GameGuild.Notifications.Services.Email;

namespace GameGuild.Notifications.Services;

/// <summary>
/// Handles notification sending, scheduling, querying, and lifecycle management
/// </summary>
public class NotificationDeliveryService(
    IApplicationDbContext context,
    INotificationPreferenceService preferenceService,
    INotificationTemplateService templateService,
    ILogger<NotificationDeliveryService> logger,
    NotificationMetadataProtector metadataProtector) : INotificationDeliveryService
{
    public async Task<Result<Notification>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken).ConfigureAwait(false);

        if (notification == null)
        {
            return Result.Failure<Notification>(Error.NotFound("Notification.NotFound", $"Notification with ID {id} not found"));
        }

        return Result.Success(notification);
    }

    public async Task<Result<IEnumerable<Notification>>> GetUserNotificationsAsync(
        Guid userId,
        int skip = 0,
        int take = 20,
        bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Set<Notification>()
            .Where(n => n.RecipientId == userId && n.Channel == NotificationChannel.InApp);

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<Notification>>(notifications);
    }

    public async Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var count = await context.Set<Notification>()
            .CountAsync(n => n.RecipientId == userId && !n.IsRead && n.Channel == NotificationChannel.InApp, cancellationToken).ConfigureAwait(false);

        return Result.Success(count);
    }

    public async Task<Result<Notification>> SendAsync(
        Guid? recipientId,
        NotificationType type,
        string title,
        string message,
        NotificationChannel channel = NotificationChannel.InApp,
        Guid? tenantId = null,
        string? actionUrl = null,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? referenceEntityId = null,
        string? referenceEntityType = null,
        string? metadata = null,
        string? recipientEmail = null,
        CancellationToken cancellationToken = default)
    {
        NotificationDeliveryDecision decision;
        if (recipientId is { } userId)
        {
            decision = await preferenceService.DecideDeliveryAsync(userId, type, channel, priority, cancellationToken).ConfigureAwait(false);
            if (decision.Action == NotificationDeliveryAction.Drop)
            {
                logger.LogDebug("Notification dropped due to user preferences. UserId: {UserId}, Type: {Type}, Reason: {Reason}", LogRedaction.Sanitize(userId.ToString()), type, decision.Reason);
                return Result.Failure<Notification>(Error.Failure("Notification.Skipped", $"Notification skipped due to user preferences ({decision.Reason})"));
            }
        }
        else
        {
            // Email-only recipient (no account/prefs): transactional types bypass prefs; always deliver.
            decision = NotificationDeliveryDecision.Send();
        }

        var notification = Notification.Create(
            recipientId,
            type,
            channel,
            title,
            message,
            tenantId,
            actionUrl,
            null,
            priority,
            decision.Action == NotificationDeliveryAction.HoldUntil ? decision.HeldUntil : null,
            referenceEntityId,
            referenceEntityType,
            metadata,
            recipientEmail: recipientEmail);

        metadataProtector.ProtectForStorage(notification);

        if (decision.Action == NotificationDeliveryAction.Digest)
        {
            notification.MarkHeldForDigest();
        }

        context.Set<Notification>().Add(notification);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (channel == NotificationChannel.InApp)
        {
            notification.MarkAsSent();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Notification sent. Id: {NotificationId}, Recipient: {RecipientId}, Type: {Type}",
            notification.Id, LogRedaction.Sanitize(recipientId.ToString()), notification.Type);

        return Result.Success(notification);
    }

    public async Task<Result<Notification>> SendFromTemplateAsync(
        Guid recipientId,
        string templateCode,
        Dictionary<string, string> placeholders,
        Guid? tenantId = null,
        Guid? referenceEntityId = null,
        string? referenceEntityType = null,
        CancellationToken cancellationToken = default)
    {
        var template = await context.Set<NotificationTemplate>()
            .FirstOrDefaultAsync(t => t.Code == templateCode && t.IsActive, cancellationToken).ConfigureAwait(false);

        if (template == null)
        {
            return Result.Failure<Notification>(Error.NotFound("Template.NotFound", $"Active template with code '{templateCode}' not found"));
        }

        var title = templateService.ReplacePlaceholders(template.TitleTemplate, placeholders);
        var message = templateService.ReplacePlaceholders(template.MessageTemplate, placeholders);
        var actionUrl = template.ActionUrlTemplate != null
            ? templateService.ReplacePlaceholders(template.ActionUrlTemplate, placeholders)
            : null;

        return await SendAsync(
            recipientId,
            template.Type,
            title,
            message,
            template.Channel,
            tenantId,
            actionUrl,
            template.DefaultPriority,
            referenceEntityId,
            referenceEntityType,
            null,
            null,
            cancellationToken);
    }

    public async Task<Result<IEnumerable<Notification>>> SendBulkAsync(
        IEnumerable<Guid> recipientIds,
        NotificationType type,
        string title,
        string message,
        NotificationChannel channel = NotificationChannel.InApp,
        Guid? tenantId = null,
        string? actionUrl = null,
        NotificationPriority priority = NotificationPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        var notifications = new List<Notification>();

        foreach (var recipientId in recipientIds)
        {
            var decision = await preferenceService.DecideDeliveryAsync(recipientId, type, channel, priority, cancellationToken).ConfigureAwait(false);
            if (decision.Action == NotificationDeliveryAction.Drop)
            {
                continue;
            }

            var notification = Notification.Create(
                recipientId,
                type,
                channel,
                title,
                message,
                tenantId,
                actionUrl,
                null,
                priority,
                decision.Action == NotificationDeliveryAction.HoldUntil ? decision.HeldUntil : null);

            metadataProtector.ProtectForStorage(notification);

            if (decision.Action == NotificationDeliveryAction.Digest)
            {
                notification.MarkHeldForDigest();
            }

            notifications.Add(notification);
        }

        if (notifications.Count > 0)
        {
            context.Set<Notification>().AddRange(notifications);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (channel == NotificationChannel.InApp)
            {
                foreach (var notification in notifications)
                {
                    notification.MarkAsSent();
                }
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogInformation("Bulk notification sent. Count: {Count}, Type: {Type}", notifications.Count, type);

        return Result.Success<IEnumerable<Notification>>(notifications);
    }

    public async Task<Result<Notification>> ScheduleAsync(
        Guid recipientId,
        NotificationType type,
        string title,
        string message,
        DateTime scheduledAt,
        NotificationChannel channel = NotificationChannel.InApp,
        Guid? tenantId = null,
        string? actionUrl = null,
        NotificationPriority priority = NotificationPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        if (scheduledAt <= SystemClock.UtcNow)
        {
            return Result.Failure<Notification>(Error.Validation("Notification.InvalidSchedule", "Scheduled time must be in the future"));
        }

        var notification = Notification.Create(
            recipientId,
            type,
            channel,
            title,
            message,
            tenantId,
            actionUrl,
            null,
            priority,
            scheduledAt);

        metadataProtector.ProtectForStorage(notification);
        context.Set<Notification>().Add(notification);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Notification scheduled. Id: {NotificationId}, ScheduledAt: {ScheduledAt}",
            notification.Id, scheduledAt);

        return Result.Success(notification);
    }

    public async Task<Result> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken).ConfigureAwait(false);

        if (notification == null)
        {
            return Result.Failure(Error.NotFound("Notification.NotFound", $"Notification with ID {notificationId} not found"));
        }

        notification.MarkAsRead();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await context.Set<Notification>()
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var notification in unreadNotifications)
        {
            notification.MarkAsRead();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Marked {Count} notifications as read for user {UserId}", unreadNotifications.Count, userId);

        return Result.Success();
    }

    public async Task<Result> MarkAsUnreadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken).ConfigureAwait(false);

        if (notification == null)
        {
            return Result.Failure(Error.NotFound("Notification.NotFound", $"Notification with ID {notificationId} not found"));
        }

        notification.MarkAsUnread();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken).ConfigureAwait(false);

        if (notification == null)
        {
            return Result.Failure(Error.NotFound("Notification.NotFound", $"Notification with ID {notificationId} not found"));
        }

        notification.Delete();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<int>> DeleteReadNotificationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var readNotifications = await context.Set<Notification>()
            .Where(n => n.RecipientId == userId && n.IsRead)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var notification in readNotifications)
        {
            notification.Delete();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(readNotifications.Count);
    }
}

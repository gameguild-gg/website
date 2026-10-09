using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Notifications.Services.Email;

/// <summary>
/// <see cref="IEmailDeliveryAdminService"/> implementation over the shared application
/// context. Every email comparison passes through <see cref="EmailAddressNormalizer"/>.
/// </summary>
public class EmailDeliveryAdminService(
    IApplicationDbContext context,
    IRecipientEmailResolver recipientEmailResolver,
    ILogger<EmailDeliveryAdminService> logger) : IEmailDeliveryAdminService
{
    public async Task<Result<PagedResult<EmailDeliveryEvent>>> GetEventsAsync(
        int skip,
        int take,
        string? eventType = null,
        string? email = null,
        string? providerMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Set<EmailDeliveryEvent>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            if (!Enum.TryParse<EmailDeliveryEventType>(eventType, ignoreCase: true, out var parsedType))
            {
                return Result.Failure<PagedResult<EmailDeliveryEvent>>(Error.Validation(
                    "Notifications.EmailEvents.InvalidEventType",
                    $"Unknown event type '{eventType}'."));
            }

            query = query.Where(e => e.EventType == parsedType);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            // Events store RecipientEmail normalized at ingest, so the filter input is
            // normalized the same way and compared for exact equality.
            var normalized = EmailAddressNormalizer.Normalize(email);
            query = query.Where(e => e.RecipientEmail == normalized);
        }

        if (!string.IsNullOrWhiteSpace(providerMessageId))
        {
            query = query.Where(e => e.ProviderMessageId == providerMessageId);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new PagedResult<EmailDeliveryEvent>(items, totalCount, skip, take));
    }

    public async Task<Result<PagedResult<EmailSuppression>>> GetSuppressionsAsync(
        int skip,
        int take,
        bool includeReleased = false,
        CancellationToken cancellationToken = default)
    {
        var query = context.Set<EmailSuppression>().AsQueryable();
        if (!includeReleased)
        {
            query = query.Where(s => s.ReleasedAt == null);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(s => s.SuppressedAt)
            .ThenByDescending(s => s.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new PagedResult<EmailSuppression>(items, totalCount, skip, take));
    }

    public async Task<Result<bool>> ReleaseSuppressionAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = EmailAddressNormalizer.Normalize(email);
        var suppression = await context.Set<EmailSuppression>()
            .FirstOrDefaultAsync(s => s.EmailAddress == normalized, cancellationToken).ConfigureAwait(false);

        if (suppression is null || !suppression.IsActive)
        {
            // Idempotent: nothing active to release.
            return Result.Success(false);
        }

        suppression.Release();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Suppression released by admin. EmailAddress: {EmailAddress}", LogRedaction.MaskEmail(normalized));
        return Result.Success(true);
    }

    public async Task<Result<PagedResult<Notification>>> GetDeadLettersAsync(
        int skip,
        int take,
        string? type = null,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Set<Notification>()
            .Where(n => n.DeliveryStatus == NotificationDeliveryStatus.DeadLettered);

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<NotificationType>(type, ignoreCase: true, out var parsedType))
            {
                return Result.Failure<PagedResult<Notification>>(Error.Validation(
                    "Notifications.DeadLetters.InvalidType",
                    $"Unknown notification type '{type}'."));
            }

            query = query.Where(n => n.Type == parsedType);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            // RecipientEmail stores raw values and EmailAddressNormalizer is not EF-translatable,
            // so the row side is lowered in SQL — equivalent to a Normalize comparison for both sides.
            var normalized = EmailAddressNormalizer.Normalize(email);
            query = query.Where(n => n.RecipientEmail != null && n.RecipientEmail.ToLower() == normalized);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(n => n.UpdatedAt)
            .ThenByDescending(n => n.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new PagedResult<Notification>(items, totalCount, skip, take));
    }

    public async Task<Result<Notification>> RequeueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken).ConfigureAwait(false);

        if (notification is null)
        {
            return Result.Failure<Notification>(Error.NotFound(
                "Notification.NotFound",
                $"Notification with ID {id} not found"));
        }

        if (notification.DeliveryStatus != NotificationDeliveryStatus.DeadLettered)
        {
            return Result.Failure<Notification>(Error.Conflict(
                "Notifications.Requeue.NotDeadLettered",
                $"Only dead-lettered notifications can be requeued (current status: {notification.DeliveryStatus})."));
        }

        var resolvedEmail = await recipientEmailResolver.ResolveAsync(notification, cancellationToken).ConfigureAwait(false);
        if (resolvedEmail is not null)
        {
            var activeSuppression = await EmailDispatcherService.FindActiveSuppressionAsync(context, resolvedEmail, cancellationToken)
                .ConfigureAwait(false);

            if (activeSuppression is not null)
            {
                return Result.Failure<Notification>(Error.Conflict(
                    "Notifications.Requeue.Suppressed",
                    $"Recipient address is actively suppressed ({activeSuppression.Reason}); release the suppression before requeueing."));
            }
        }

        notification.MarkRequeued();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Dead-lettered notification requeued by admin. NotificationId: {NotificationId}, RequeueCount: {RequeueCount}",
            notification.Id, notification.RequeueCount);
        return Result.Success(notification);
    }

    public async Task<Result<EmailDeliveryTimeline>> GetTimelineAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken).ConfigureAwait(false);

        if (notification is null)
        {
            return Result.Failure<EmailDeliveryTimeline>(Error.NotFound(
                "Notification.NotFound",
                $"Notification with ID {id} not found"));
        }

        if (notification.ProviderMessageId is null)
        {
            // Digest bundles / rows that never reached a provider have no correlation id.
            return Result.Success(new EmailDeliveryTimeline(null, []));
        }

        var events = await context.Set<EmailDeliveryEvent>()
            .Where(e => e.ProviderMessageId == notification.ProviderMessageId)
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new EmailDeliveryTimeline(notification.ProviderMessageId, events));
    }
}

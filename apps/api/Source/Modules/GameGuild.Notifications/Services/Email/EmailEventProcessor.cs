using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Notifications.Services.Email;

/// <summary>
/// Default <see cref="IEmailEventProcessor"/>. Runs inline within the webhook request by design —
/// row counts per address are small; move to a queue if bounce volume ever demands it.
/// </summary>
public sealed class EmailEventProcessor(
    IApplicationDbContext context,
    ILogger<EmailEventProcessor> logger) : IEmailEventProcessor
{
    private const string HardBounceDeadLetterReason = "suppressed: hard bounce";
    private const string ComplaintDeadLetterReason = "suppressed: complaint";

    /// <inheritdoc />
    /// <remarks>
    /// Race note: a bounce arriving while a send to the same address is mid-flight leaves that row
    /// in Sending — outside this sweep. The second bounce lands later and the idempotent upsert
    /// absorbs it; the dispatcher's pre-send suppression check is the safety net for in-flight rows.
    /// </remarks>
    public async Task ProcessAsync(EmailDeliveryEvent deliveryEvent, CancellationToken cancellationToken = default)
    {
        var (reason, deadLetterReason) = deliveryEvent.EventType switch
        {
            EmailDeliveryEventType.Bounce when IsHardBounce(deliveryEvent.BounceType)
                => (EmailSuppressionReason.HardBounce, HardBounceDeadLetterReason),
            EmailDeliveryEventType.Complaint
                => (EmailSuppressionReason.Complaint, ComplaintDeadLetterReason),
            // Transient bounces (ContentRejected, MailboxFull, null BounceType, ...) and
            // Send/Delivery/Open events carry no suppression side effects — the event row is the record.
            _ => ((EmailSuppressionReason?)null, (string?)null)
        };

        if (reason is null)
        {
            return;
        }

        var normalized = EmailAddressNormalizer.Normalize(deliveryEvent.RecipientEmail);

        await UpsertSuppressionAsync(deliveryEvent, normalized, reason.Value, cancellationToken).ConfigureAwait(false);
        var deadLettered = await DeadLetterPendingSendsAsync(normalized, deadLetterReason!, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Processed {EventType} delivery event. Recipient: {RecipientEmail}, SuppressionReason: {Reason}, DeadLettered: {DeadLettered}",
            deliveryEvent.EventType, LogRedaction.MaskEmail(normalized), reason, deadLettered);
    }

    private static bool IsHardBounce(string? bounceType) =>
        string.Equals(bounceType, "Permanent", StringComparison.OrdinalIgnoreCase)
        || string.Equals(bounceType, "Undetermined", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Idempotent upsert keyed on the normalized address: missing row → create; active row →
    /// refresh BounceType/SourceEventId; released row → re-suppress (clear ReleasedAt).
    /// </summary>
    private async Task UpsertSuppressionAsync(
        EmailDeliveryEvent deliveryEvent,
        string normalized,
        EmailSuppressionReason reason,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<EmailSuppression>()
            .SingleOrDefaultAsync(s => s.EmailAddress == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.Set<EmailSuppression>().Add(EmailSuppression.Create(
                normalized, reason, deliveryEvent.BounceType, deliveryEvent.Id));
            return;
        }

        if (context is not DbContext dbContext)
        {
            // Matches the repo-wide `context is DbContext` escape hatch; production context always is one.
            return;
        }

        var entry = dbContext.Entry(existing);
        if (existing.IsActive)
        {
            // ponytail/interim: the schema task's entity (frozen for this lane) exposes no refresh
            // transition, so an active row is refreshed through the EF change API — same pattern as
            // EmailDispatcherService.Hold. Reason is deliberately NOT overwritten on an active row.
            // Replace with a domain method when the entity is next open for edits.
            entry.Property(s => s.BounceType).CurrentValue = deliveryEvent.BounceType;
            entry.Property(s => s.SourceEventId).CurrentValue = deliveryEvent.Id;
            entry.Property(s => s.UpdatedAt).CurrentValue = SystemClock.UtcNow;
            return;
        }

        // Released row → re-suppress under the new event.
        entry.Property(s => s.Reason).CurrentValue = reason;
        entry.Property(s => s.BounceType).CurrentValue = deliveryEvent.BounceType;
        entry.Property(s => s.SourceEventId).CurrentValue = deliveryEvent.Id;
        entry.Property(s => s.SuppressedAt).CurrentValue = SystemClock.UtcNow;
        entry.Property(s => s.ReleasedAt).CurrentValue = null;
        entry.Property(s => s.UpdatedAt).CurrentValue = SystemClock.UtcNow;
    }

    /// <summary>
    /// Deadletters every Pending email send to the suppressed address. EmailAddressNormalizer.Normalize
    /// is not SQL-translatable on the row side, so candidate rows are fetched and compared client-side —
    /// normalized on BOTH sides.
    /// </summary>
    private async Task<int> DeadLetterPendingSendsAsync(
        string normalized,
        string deadLetterReason,
        CancellationToken cancellationToken)
    {
        var candidates = await context.Set<Notification>()
            .Where(n => n.Channel == NotificationChannel.Email
                && n.DeliveryStatus == NotificationDeliveryStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var deadLettered = 0;
        foreach (var notification in candidates)
        {
            try
            {
                // RecipientId-only rows (no RecipientEmail) are skipped: their address is resolved at
                // dispatch time, and the dispatcher's pre-send suppression check is the safety net.
                if (notification.RecipientEmail is null
                    || EmailAddressNormalizer.Normalize(notification.RecipientEmail) != normalized)
                {
                    continue;
                }

                notification.MarkDeadLettered(deadLetterReason);
                deadLettered++;
            }
            catch (Exception ex)
            {
                // One bad row must not abort the sweep — log and skip; a later event redelivery
                // (or the dispatcher pre-send check) re-covers the address.
                logger.LogWarning(ex, "Failed to deadletter pending send during suppression sweep. NotificationId: {NotificationId}",
                    notification.Id);
            }
        }

        return deadLettered;
    }
}

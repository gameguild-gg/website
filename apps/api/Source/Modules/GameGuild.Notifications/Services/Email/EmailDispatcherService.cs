using GameGuild.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Notifications.Services.Email;

/// <summary>
/// Core email dispatch pipeline, unit-testable without a BackgroundService host.
/// Sweeps Pending email rows, claims them (Sending), renders, sends, and applies
/// retry/backoff, staleness TTL, quiet-hours hold, and deadlettering.
/// </summary>
public sealed class EmailDispatcherService(
    IApplicationDbContext context,
    IEmailRendererRegistry rendererRegistry,
    IRecipientEmailResolver recipientResolver,
    INotificationPreferenceService preferenceService,
    IEmailSender emailSender,
    IOptions<EmailDispatcherOptions> options,
    ILogger<EmailDispatcherService> logger,
    NotificationMetadataProtector metadataProtector)
{
    /// <summary>Rows stuck in Sending longer than this are reclaimed by the sweep (crash between claim and send).</summary>
    private static readonly TimeSpan SendingReclaimWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// INTERIM quiet-hours rule (until the preference rework lands): the legacy boolean gate cannot
    /// distinguish "drop" from "hold", so any false holds the row for a 30-minute recheck.
    /// Never drop, never send. Reconcile when the decision-based preference API replaces the boolean.
    /// </summary>
    private static readonly TimeSpan PreferenceHoldRecheckInterval = TimeSpan.FromMinutes(30);

    /// <summary>Transactional types: expire via TransactionalStalenessTtl and never render once stale.</summary>
    private static readonly HashSet<NotificationType> TransactionalTypes =
        [NotificationType.EmailVerification, NotificationType.PasswordReset, NotificationType.MagicLink, NotificationType.EmailCode, NotificationType.TenantInvite];

    /// <summary>
    /// Runs one sweep pass and returns the number of rows processed (sent, held, skipped, or deadlettered by design).
    /// A row whose processing throws counts as failed and is retried per the backoff schedule.
    /// </summary>
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        await ProtectLegacyMetadataAsync(opts.SweepBatchSize, cancellationToken).ConfigureAwait(false);
        var now = SystemClock.UtcNow;
        var reclaimCutoff = now.Subtract(SendingReclaimWindow);

        var batch = await context.Set<Notification>()
            .Where(n => n.Channel == NotificationChannel.Email
                && (n.DeliveryStatus == NotificationDeliveryStatus.Pending
                        && (n.NextAttemptAt == null || n.NextAttemptAt <= now)
                        && (n.ScheduledAt == null || n.ScheduledAt <= now)
                    // Reclaim arm: rows stuck in Sending (crashed between claim and send) re-enter the sweep.
                    || (n.DeliveryStatus == NotificationDeliveryStatus.Sending && n.UpdatedAt <= reclaimCutoff)))
            .OrderBy(n => n.CreatedAt)
            .Take(opts.SweepBatchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var processed = 0;
        foreach (var notification in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await DispatchAsync(notification, opts, cancellationToken).ConfigureAwait(false);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Email delivery failed. NotificationId: {NotificationId}, Type: {Type}",
                    notification.Id, notification.Type);
                try
                {
                    HandleFailure(notification, ex, opts);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception saveEx)
                {
                    // Row stays Sending and is reclaimed by a later sweep.
                    logger.LogError(saveEx, "Failed to persist delivery failure. NotificationId: {NotificationId}",
                        notification.Id);
                }
            }
        }

        return processed;
    }

    private async Task DispatchAsync(Notification notification, EmailDispatcherOptions opts, CancellationToken cancellationToken)
    {
        // A due row can fall outside this sweep's history backfill batch.
        // Protect it before any lifecycle write, including staleness and preference holds.
        metadataProtector.ProtectForStorage(notification);

        // 1. Staleness: transactional emails older than the TTL are deadlettered without rendering.
        if (TransactionalTypes.Contains(notification.Type)
            && notification.CreatedAt < SystemClock.UtcNow.Subtract(opts.TransactionalStalenessTtl))
        {
            notification.MarkDeadLettered($"stale: transactional email unsent after {opts.TransactionalStalenessTtl.TotalHours}h");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Deadlettered stale transactional email. NotificationId: {NotificationId}, Type: {Type}",
                notification.Id, notification.Type);
            return;
        }

        // 2. Quiet-hours re-check (only possible when a registered recipient exists).
        // INTERIM: see PreferenceHoldRecheckInterval — any "false" is a hold, never a drop.
        if (notification.RecipientId is { } userId)
        {
#pragma warning disable CS0618 // Interim: legacy boolean gate kept for the dispatcher; replaced by the decision-based preference API.
            var shouldSend = await preferenceService.ShouldSendNotificationAsync(
#pragma warning restore CS0618
                userId, notification.Type, notification.Channel, notification.Priority, cancellationToken)
                .ConfigureAwait(false);
            if (!shouldSend)
            {
                Hold(notification, SystemClock.UtcNow.Add(PreferenceHoldRecheckInterval));
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Email held by user preferences; recheck scheduled. NotificationId: {NotificationId}, NextAttemptAt: {NextAttemptAt}",
                    notification.Id, notification.NextAttemptAt);
                return;
            }
        }

        // 3. Claim (persisted before any external call so a crash mid-send is reclaimed, not lost).
        if (notification.DeliveryStatus == NotificationDeliveryStatus.Pending)
        {
            notification.ClaimForSending();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // 4. Renderer: missing renderer is a permanent configuration error — deadletter, do not retry.
        var renderer = rendererRegistry.Resolve(notification.Type);
        if (renderer is null)
        {
            notification.MarkDeadLettered($"No email renderer registered for notification type '{notification.Type}'");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogError("Deadlettered notification with no renderer. NotificationId: {NotificationId}, Type: {Type}",
                notification.Id, notification.Type);
            return;
        }

        // 5. Recipient: unresolvable address is a permanent error — deadletter, do not retry.
        var toEmail = await recipientResolver.ResolveAsync(notification, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            notification.MarkDeadLettered("Recipient email could not be resolved (no RecipientEmail and user lookup yielded no address)");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogError("Deadlettered notification without recipient. NotificationId: {NotificationId}", notification.Id);
            return;
        }

        // 5b. Suppression: actively suppressed address (hard bounce / complaint) — deadletter without
        // sending and without consuming a retry attempt. Rows are already claimed (Sending) here;
        // MarkDeadLettered is an unguarded state write, so Sending → DeadLettered is legal.
        // ponytail: per-row indexed lookup is fine at the 50-row/30s sweep; batch-prefetch the
        // suppression set per sweep if profiling ever shows the roundtrips matter.
        var activeSuppression = await FindActiveSuppressionAsync(context, toEmail, cancellationToken).ConfigureAwait(false);
        if (activeSuppression is not null)
        {
            notification.MarkDeadLettered($"suppressed: {activeSuppression.Reason}");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Deadlettered notification to suppressed address. NotificationId: {NotificationId}, Recipient: {RecipientEmail}, Reason: {Reason}",
                notification.Id, LogRedaction.MaskEmail(toEmail), activeSuppression.Reason);
            return;
        }

        // 6. Render: null means "nothing to send" — treat as delivered.
        var message = await renderer.RenderAsync(notification, cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            if (notification.Type == NotificationType.Security)
            {
                throw new InvalidOperationException("A security email requires a rendered message.");
            }
            notification.MarkDeliverySent();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Email renderer returned no message; marked sent. NotificationId: {NotificationId}, Type: {Type}",
                notification.Id, notification.Type);
            return;
        }

        // Security alerts need explicit acceptance; a nullable message ID cannot distinguish a skip from acceptance.
        string? providerMessageId;
        if (notification.Type == NotificationType.Security)
        {
            if (emailSender is not IConfirmedEmailSender confirmedSender)
            {
                throw new InvalidOperationException("The security email sender does not expose provider acceptance.");
            }
            var receipt = await confirmedSender.SendWithReceiptAsync(message with { ToEmail = toEmail }, cancellationToken).ConfigureAwait(false);
            if (!receipt.Accepted)
            {
                throw new InvalidOperationException("The security email was not accepted for delivery.");
            }
            providerMessageId = receipt.ProviderMessageId;
        }
        else
        {
            providerMessageId = await emailSender.SendAsync(message with { ToEmail = toEmail }, cancellationToken).ConfigureAwait(false);
        }
        notification.MarkDeliverySent(providerMessageId);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Email delivered. NotificationId: {NotificationId}, Type: {Type}, Recipient: {RecipientEmail}",
            notification.Id, notification.Type, LogRedaction.MaskEmail(toEmail));
    }

    private async Task ProtectLegacyMetadataAsync(int batchSize, CancellationToken cancellationToken)
    {
        // Hosted maintenance includes sent, dead-lettered and soft-deleted history in every tenant.
        // It changes metadata only; delivery state, timestamps and retry counters remain intact.
        var legacy = await context.Set<Notification>()
            .IgnoreQueryFilters()
            .Where(n => (n.Type == NotificationType.EmailVerification
                    || n.Type == NotificationType.PasswordReset
                    || n.Type == NotificationType.MagicLink
                    || n.Type == NotificationType.EmailCode)
                && n.Metadata != null && n.Metadata.Trim() != ""
                && !n.Metadata.StartsWith(NotificationMetadataProtector.ProtectedPrefix))
            .OrderBy(n => n.CreatedAt)
            .ThenBy(n => n.Id)
            .Take(Math.Clamp(batchSize, 1, 1000))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var changed = false;
        foreach (var notification in legacy)
        {
            cancellationToken.ThrowIfCancellationRequested();
            changed |= metadataProtector.ProtectForStorage(notification);
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Active suppression (ReleasedAt null) for the given recipient, matched on the canonical
    /// normalized form via <see cref="EmailAddressNormalizer"/>; null when the address is clear.
    /// </summary>
    internal static async Task<EmailSuppression?> FindActiveSuppressionAsync(
        IApplicationDbContext context, string toEmail, CancellationToken cancellationToken)
    {
        var normalized = EmailAddressNormalizer.Normalize(toEmail);
        return await context.Set<EmailSuppression>()
            .FirstOrDefaultAsync(s => s.EmailAddress == normalized && s.ReleasedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    private void HandleFailure(Notification notification, Exception ex, EmailDispatcherOptions opts)
    {
        if (notification.AttemptCount + 1 >= opts.MaxAttempts)
        {
            notification.MarkDeadLettered($"delivery failed after {notification.AttemptCount + 1} attempts: {Truncate(ex.Message)}");
            return;
        }

        var backoff = opts.BackoffSchedule.Length == 0
            ? TimeSpan.FromMinutes(1)
            : opts.BackoffSchedule[Math.Min(notification.AttemptCount, opts.BackoffSchedule.Length - 1)];
        notification.MarkDeliveryAttemptFailed(Truncate(ex.Message), SystemClock.UtcNow.Add(backoff));
    }

    /// <summary>
    /// ponytail/interim: the Notification entity (delivered by the schema task, currently frozen for this lane)
    /// has no Hold transition, so the hold writes NextAttemptAt and the Pending status through the EF change API
    /// instead of incrementing AttemptCount via MarkDeliveryAttemptFailed (holds must not consume retry attempts).
    /// Replace with a domain Hold(nextAttemptAt) method when the entity is next open for edits.
    /// </summary>
    private void Hold(Notification notification, DateTime recheckAt)
    {
        if (context is not DbContext dbContext)
        {
            // Matches the repo-wide `context is DbContext` escape hatch; production context always is one.
            return;
        }

        dbContext.Entry(notification).Property(n => n.NextAttemptAt).CurrentValue = recheckAt;
        dbContext.Entry(notification).Property(n => n.DeliveryStatus).CurrentValue = NotificationDeliveryStatus.Pending;
    }

    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];
}

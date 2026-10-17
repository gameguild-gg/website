using System.Text.Json;
using GameGuild;
using GameGuild.Notifications.Services.Email;
using GameGuild.Sms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Notifications.Services.Sms;

/// <summary>
///     Core SMS dispatch pipeline, unit-testable without a BackgroundService host. Sweeps Pending
///     SMS rows, claims them (Sending), resolves the destination phone from protected metadata,
///     sends through <see cref="ISmsSender"/>, and applies retry/backoff, staleness TTL and
///     deadlettering — the out-of-band twin of <see cref="Email.EmailDispatcherService"/>.
/// </summary>
public sealed class SmsDispatcherService(
    IApplicationDbContext context,
    ISmsSender smsSender,
    NotificationMetadataProtector metadataProtector,
    IOptions<SmsDispatcherOptions> options,
    ILogger<SmsDispatcherService> logger)
{
    private static readonly TimeSpan SendingReclaimWindow = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Sweeps one batch of due SMS rows and returns how many were processed.</summary>
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        await ProtectLegacyMetadataAsync(opts.SweepBatchSize, cancellationToken).ConfigureAwait(false);
        var now = SystemClock.UtcNow;
        var reclaimCutoff = now.Subtract(SendingReclaimWindow);

        var batch = await context.Set<Notification>()
            .Where(n => n.Channel == NotificationChannel.Sms
                && (n.DeliveryStatus == NotificationDeliveryStatus.Pending
                        && (n.NextAttemptAt == null || n.NextAttemptAt <= now)
                        && (n.ScheduledAt == null || n.ScheduledAt <= now)
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
                logger.LogWarning(ex, "SMS delivery failed. NotificationId: {NotificationId}, Type: {Type}",
                    notification.Id, notification.Type);
                try
                {
                    HandleFailure(notification, ex, opts);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception saveEx)
                {
                    // Row stays Sending and is reclaimed by a later sweep.
                    logger.LogError(saveEx, "Failed to persist SMS delivery failure. NotificationId: {NotificationId}",
                        notification.Id);
                }
            }
        }

        return processed;
    }

    private async Task DispatchAsync(Notification notification, SmsDispatcherOptions opts, CancellationToken cancellationToken)
    {
        // A due row can fall outside this sweep's history backfill batch. Protect it before any
        // lifecycle write, including staleness.
        metadataProtector.ProtectForStorage(notification);

        // 1. Staleness: transactional SMS older than the TTL is deadlettered without sending.
        if (NotificationCategories.Transactional.Contains(notification.Type)
            && notification.CreatedAt < SystemClock.UtcNow.Subtract(opts.TransactionalStalenessTtl))
        {
            notification.MarkDeadLettered($"stale: transactional SMS unsent after {opts.TransactionalStalenessTtl.TotalMinutes:0.#}m");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Deadlettered stale transactional SMS. NotificationId: {NotificationId}, Type: {Type}",
                notification.Id, notification.Type);
            return;
        }

        // 2. Claim (persisted before the external call so a crash mid-send is reclaimed, not lost).
        if (notification.DeliveryStatus == NotificationDeliveryStatus.Pending)
        {
            notification.ClaimForSending();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // 3. Recipient: unresolvable phone is a permanent error — deadletter, do not retry.
        var toPhone = ResolveDestinationPhone(notification);
        if (string.IsNullOrWhiteSpace(toPhone))
        {
            notification.MarkDeadLettered("Recipient phone could not be resolved from notification metadata");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogError("Deadlettered SMS without recipient. NotificationId: {NotificationId}", notification.Id);
            return;
        }

        // 4. Send: body is the notification message text (SMS has no template pipeline).
        var receipt = await smsSender.SendAsync(new SmsMessage(toPhone, notification.Message), cancellationToken).ConfigureAwait(false);
        if (!receipt.Accepted)
        {
            throw new InvalidOperationException("The SMS provider declined the message for delivery.");
        }

        notification.MarkDeliverySent(receipt.ProviderMessageId);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("SMS delivered. NotificationId: {NotificationId}, Type: {Type}",
            notification.Id, notification.Type);
    }

    /// <summary>Reads the destination phone from notification metadata (protected for transactional types).</summary>
    internal string? ResolveDestinationPhone(Notification notification)
    {
        string? metadata = null;
        try
        {
            metadata = NotificationMetadataProtector.RequiresProtection(notification.Type)
                ? metadataProtector.GetForRendering(notification, notification.Type)
                : notification.Metadata;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or FormatException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<SmsNotificationMetadata>(metadata, MetadataJsonOptions);
            return string.IsNullOrWhiteSpace(payload?.PhoneNumber) ? null : payload.PhoneNumber.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Backfills protection onto legacy plaintext metadata rows for SMS types.</summary>
    private async Task ProtectLegacyMetadataAsync(int batchSize, CancellationToken cancellationToken)
    {
        var legacy = await context.Set<Notification>()
            .IgnoreQueryFilters()
            .Where(n => (n.Type == NotificationType.SmsMfaCode)
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

    private void HandleFailure(Notification notification, Exception ex, SmsDispatcherOptions opts)
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

    private static string Truncate(string value) =>
        value.Length <= 200 ? value : value[..200];

    private sealed record SmsNotificationMetadata(string? PhoneNumber);
}

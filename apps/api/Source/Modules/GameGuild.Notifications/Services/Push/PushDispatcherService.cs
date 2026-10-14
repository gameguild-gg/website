using System.Text.Json;
using GameGuild;
using GameGuild.Notifications.Services.Email;
using GameGuild.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Notifications.Services.Push;

/// <summary>
///     Core mobile-push dispatch pipeline, unit-testable without a BackgroundService host. Sweeps
///     Pending push rows, claims them (Sending), resolves the device token and data payload from
///     protected metadata, sends through <see cref="IPushSender"/>, and applies retry/backoff,
///     staleness TTL and deadlettering — the out-of-band twin of
///     <see cref="Email.EmailDispatcherService"/>.
/// </summary>
public sealed class PushDispatcherService(
    IApplicationDbContext context,
    IPushSender pushSender,
    NotificationMetadataProtector metadataProtector,
    IOptions<PushDispatcherOptions> options,
    ILogger<PushDispatcherService> logger)
{
    private static readonly TimeSpan SendingReclaimWindow = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Sweeps one batch of due push rows and returns how many were processed.</summary>
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var now = SystemClock.UtcNow;
        var reclaimCutoff = now.Subtract(SendingReclaimWindow);

        var batch = await context.Set<Notification>()
            .Where(n => n.Channel == NotificationChannel.Push
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
                logger.LogWarning(ex, "Push delivery failed. NotificationId: {NotificationId}, Type: {Type}",
                    notification.Id, notification.Type);
                try
                {
                    HandleFailure(notification, ex, opts);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception saveEx)
                {
                    // Row stays Sending and is reclaimed by a later sweep.
                    logger.LogError(saveEx, "Failed to persist push delivery failure. NotificationId: {NotificationId}",
                        notification.Id);
                }
            }
        }

        return processed;
    }

    private async Task DispatchAsync(Notification notification, PushDispatcherOptions opts, CancellationToken cancellationToken)
    {
        metadataProtector.ProtectForStorage(notification);

        // 1. Staleness: transactional push older than the TTL is deadlettered without sending.
        if (NotificationCategories.Transactional.Contains(notification.Type)
            && notification.CreatedAt < SystemClock.UtcNow.Subtract(opts.TransactionalStalenessTtl))
        {
            notification.MarkDeadLettered($"stale: transactional push unsent after {opts.TransactionalStalenessTtl.TotalMinutes:0.#}m");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Deadlettered stale transactional push. NotificationId: {NotificationId}, Type: {Type}",
                notification.Id, notification.Type);
            return;
        }

        // 2. Claim (persisted before the external call so a crash mid-send is reclaimed, not lost).
        if (notification.DeliveryStatus == NotificationDeliveryStatus.Pending)
        {
            notification.ClaimForSending();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // 3. Recipient: unresolvable device token is a permanent error — deadletter, do not retry.
        var payload = ResolvePayload(notification);
        if (payload is null || string.IsNullOrWhiteSpace(payload.DeviceToken))
        {
            notification.MarkDeadLettered("Device token could not be resolved from notification metadata");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogError("Deadlettered push without device token. NotificationId: {NotificationId}", notification.Id);
            return;
        }

        // 4. Send: title/body come from the notification; structured data rides in metadata.
        var receipt = await pushSender.SendAsync(
            new PushMessage(payload.DeviceToken.Trim(), notification.Title, notification.Message, payload.Data),
            cancellationToken).ConfigureAwait(false);
        if (!receipt.Accepted)
        {
            throw new InvalidOperationException("The push provider declined the message for delivery.");
        }

        notification.MarkDeliverySent(receipt.ProviderMessageId);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Push delivered. NotificationId: {NotificationId}, Type: {Type}",
            notification.Id, notification.Type);
    }

    /// <summary>Reads the device token and data payload from notification metadata (protected for transactional types).</summary>
    internal PushPayload? ResolvePayload(Notification notification)
    {
        string? metadata;
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
            var payload = JsonSerializer.Deserialize<PushNotificationMetadata>(metadata, MetadataJsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.DeviceToken))
            {
                return null;
            }

            return new PushPayload(payload.DeviceToken, payload.Data);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void HandleFailure(Notification notification, Exception ex, PushDispatcherOptions opts)
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

    internal sealed record PushPayload(string DeviceToken, IReadOnlyDictionary<string, string>? Data);

    private sealed record PushNotificationMetadata(
        string? DeviceToken,
        Dictionary<string, string>? Data);
}

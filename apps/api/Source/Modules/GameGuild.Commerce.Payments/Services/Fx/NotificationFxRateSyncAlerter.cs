using System.Text.Json;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Raises FX sync failure/staleness alerts (issue #412 spec B) through the platform
///     system-alert notification pattern, mirroring
/// <c>NotificationGatewayAvailabilityAlerter</c>. Implementations must be failure-tolerant:
/// an alerting problem is logged, never propagated into the sync path.
/// </summary>
public interface IFxRateSyncAlerter
{
    /// <summary>Alerts operators that FX rate sync has failed <paramref name="consecutiveFailures"/> times in a row.</summary>
    Task RaiseSyncFailureAlertAsync(string providerName, int consecutiveFailures, string lastError, CancellationToken cancellationToken = default);

    /// <summary>Alerts operators that the newest persisted rates are older than the staleness threshold.</summary>
    Task RaiseStalenessAlertAsync(DateTime newestEffectiveDateUtc, int thresholdDays, CancellationToken cancellationToken = default);
}

/// <summary>No-op alerter for tests and hosts without the notification pipeline.</summary>
public sealed class NullFxRateSyncAlerter : IFxRateSyncAlerter
{
    /// <inheritdoc />
    public Task RaiseSyncFailureAlertAsync(string providerName, int consecutiveFailures, string lastError, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task RaiseStalenessAlertAsync(DateTime newestEffectiveDateUtc, int thresholdDays, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
///     Notification-backed <see cref="IFxRateSyncAlerter"/>: in-app <see cref="NotificationType.System"/>
/// notifications to the system channel (recipient <c>Guid.Empty</c>), throttled so a sustained
/// outage raises at most one alert per window per kind.
/// </summary>
public sealed class NotificationFxRateSyncAlerter(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationFxRateSyncAlerter> logger) : IFxRateSyncAlerter
{
    /// <summary>Minimum interval between two alerts of the same kind.</summary>
    public static readonly TimeSpan ThrottleWindow = TimeSpan.FromMinutes(15);

    private readonly object _throttleSyncRoot = new();
    private DateTime _lastFailureAlertAtUtc = DateTime.MinValue;
    private DateTime _lastStalenessAlertAtUtc = DateTime.MinValue;

    /// <inheritdoc />
    public async Task RaiseSyncFailureAlertAsync(
        string providerName,
        int consecutiveFailures,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        if (!TryBeginAlert(ref _lastFailureAlertAtUtc))
        {
            logger.LogWarning(
                "FX rate sync failed again (provider {Provider}, {Failures} consecutive failures; alert throttled).",
                providerName,
                consecutiveFailures);
            return;
        }

        var metadata = JsonSerializer.Serialize(new
        {
            provider = providerName,
            consecutiveFailures,
            lastError = lastError.Length <= 1000 ? lastError : lastError[..1000],
            raisedAtUtc = SystemClock.UtcNow,
        });

        await SendAsync(
            title: "FX rate sync failing",
            message:
                $"FX rate sync has failed {consecutiveFailures} consecutive time(s); provider '{providerName}' returned no usable rate set. "
                + "Cross-currency conversions keep using the last persisted rates (or fail closed when none exist). "
                + $"Last error: {lastError}",
            referenceEntityType: "FxRateSyncRuns",
            metadata: metadata,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RaiseStalenessAlertAsync(DateTime newestEffectiveDateUtc, int thresholdDays, CancellationToken cancellationToken = default)
    {
        if (!TryBeginAlert(ref _lastStalenessAlertAtUtc))
        {
            logger.LogWarning(
                "FX rates are stale (newest effective date {Effective:yyyy-MM-dd}, threshold {Days} days; alert throttled).",
                newestEffectiveDateUtc,
                thresholdDays);
            return;
        }

        var metadata = JsonSerializer.Serialize(new
        {
            newestEffectiveDateUtc,
            thresholdDays,
            raisedAtUtc = SystemClock.UtcNow,
        });

        await SendAsync(
            title: "FX rates are stale",
            message:
                $"The newest persisted FX rates are effective {newestEffectiveDateUtc:yyyy-MM-dd} UTC, older than the "
                + $"{thresholdDays}-day staleness threshold. Conversions served from stale quotations may misprice; "
                + "check the FX sync worker and provider configuration.",
            referenceEntityType: "FxRateSnapshots",
            metadata: metadata,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private bool TryBeginAlert(ref DateTime lastRaisedAtUtc)
    {
        lock (_throttleSyncRoot)
        {
            if (SystemClock.UtcNow - lastRaisedAtUtc < ThrottleWindow)
            {
                return false;
            }

            lastRaisedAtUtc = SystemClock.UtcNow;
            return true;
        }
    }

    private async Task SendAsync(
        string title,
        string message,
        string referenceEntityType,
        string metadata,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var result = await notificationService
                .SendAsync(
                    recipientId: Guid.Empty, // System-level alert — routed to the admin channel
                    type: NotificationType.System,
                    title: title,
                    message: message,
                    channel: NotificationChannel.InApp,
                    tenantId: null,
                    actionUrl: null,
                    priority: NotificationPriority.High,
                    referenceEntityId: null,
                    referenceEntityType: referenceEntityType,
                    metadata: metadata,
                    recipientEmail: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                logger.LogError("{Title} alert sent.", title);
            }
            else
            {
                logger.LogError("{Title} alert was not delivered: {Reason}.", title, result.Error?.Description ?? "unknown notification failure");
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Title} alert raised an exception; alerting must never break the sync path.", title);
        }
    }
}

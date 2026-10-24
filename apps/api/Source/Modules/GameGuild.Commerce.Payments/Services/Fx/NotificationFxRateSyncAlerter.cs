using System.Text.Json;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Raises FX sync failure/staleness alerts (issue #412 spec B) through the platform
///     system notification service. Implementations must be failure-tolerant:
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
/// notifications to configured operator accounts. Successful deliveries are throttled per
/// recipient and kind; a failed delivery remains eligible for the next attempt.
/// </summary>
public sealed class NotificationFxRateSyncAlerter(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationFxRateSyncAlerter> logger,
    IOptions<FxSyncOptions> options) : IFxRateSyncAlerter
{
    /// <summary>Minimum interval between two alerts of the same kind.</summary>
    public static readonly TimeSpan ThrottleWindow = TimeSpan.FromMinutes(15);

    private readonly SemaphoreSlim _deliveryGate = new(1, 1);
    private readonly Dictionary<(string Kind, Guid Recipient), DateTime> _lastDelivered = new();

    /// <inheritdoc />
    public async Task RaiseSyncFailureAlertAsync(
        string providerName,
        int consecutiveFailures,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var boundedError = lastError.Length <= 1000 ? lastError : lastError[..1000];

        var metadata = JsonSerializer.Serialize(new
        {
            provider = providerName,
            consecutiveFailures,
            lastError = boundedError,
            raisedAtUtc = SystemClock.UtcNow,
        });

        await SendAsync(
            title: "FX rate sync failing",
            message:
                $"FX rate sync has failed {consecutiveFailures} consecutive time(s); provider '{providerName}' returned no usable rate set. "
                + "Cross-currency conversions keep using the last persisted rates (or fail closed when none exist). "
                + $"Last error: {boundedError}",
            referenceEntityType: "FxRateSyncRuns",
            metadata: metadata,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RaiseStalenessAlertAsync(DateTime newestEffectiveDateUtc, int thresholdDays, CancellationToken cancellationToken = default)
    {
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

    private async Task SendAsync(
        string title,
        string message,
        string referenceEntityType,
        string metadata,
        CancellationToken cancellationToken)
    {
        var recipients = options.Value.AlertRecipientIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (recipients.Length == 0)
        {
            logger.LogError("{Title} alert has no configured operator recipients.", title);
            return;
        }

        await _deliveryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var recipient in recipients)
            {
                var key = (referenceEntityType, recipient);
                if (_lastDelivered.TryGetValue(key, out var lastDelivered)
                    && SystemClock.UtcNow - lastDelivered < ThrottleWindow)
                {
                    continue;
                }

                try
                {
                    // Separate scopes prevent one failed persistence operation from poisoning
                    // delivery to the remaining operators.
                    using var scope = scopeFactory.CreateScope();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var result = await notificationService
                        .SendAsync(
                            recipientId: recipient,
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
                        _lastDelivered[key] = SystemClock.UtcNow;
                        logger.LogInformation("{Title} alert delivered to configured operator.", title);
                    }
                    else
                    {
                        logger.LogError("{Title} alert was not delivered: {Reason}.", title, result.Error?.Description ?? "unknown notification failure");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "{Title} alert delivery failed; remaining operators will still be attempted.", title);
                }
            }
        }
        finally
        {
            _deliveryGate.Release();
        }
    }
}

using System.Text.Json;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Sends the all-gateways-down alert (issue #413) through the existing system-alert
///     notification pattern — a <see cref="NotificationType.System" /> in-app notification
///     addressed to the system channel (recipient <c>Guid.Empty</c>), mirroring
/// <c>GameGuild.Monitoring.SLA.AlertManager</c>. Registered as a singleton so the throttle
/// window holds across requests; the scoped <see cref="INotificationService" /> is resolved
/// through a scope per alert, matching the background-service notification pattern. Repeats
/// within the throttle window are suppressed so a sustained outage raises one alert per window.
/// </summary>
public sealed class NotificationGatewayAvailabilityAlerter(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationGatewayAvailabilityAlerter> logger) : IGatewayAvailabilityAlerter
{
    /// <summary>Minimum interval between two all-gateways-down alerts.</summary>
    public static readonly TimeSpan ThrottleWindow = TimeSpan.FromMinutes(5);

    private readonly object _throttleSyncRoot = new();
    private DateTime _lastRaisedAtUtc = DateTime.MinValue;

    /// <inheritdoc />
    public async Task RaiseAllGatewaysUnavailableAsync(
        IReadOnlyList<GatewayCircuitSnapshot> circuitStates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circuitStates);

        if (!TryBeginAlert())
        {
            logger.LogWarning(
                "All payment gateways unavailable (circuit states suppressed; alert throttled after a recent raise)");
            return;
        }

        var circuitSummary = circuitStates.Count == 0
            ? "no gateway circuits recorded"
            : string.Join(
                ", ",
                circuitStates.Select(state =>
                    $"{state.ProviderId}={state.State}({state.ConsecutiveFailures} failures)"));

        var metadata = JsonSerializer.Serialize(new
        {
            circuitStates = circuitStates.Select(state => new
            {
                providerId = state.ProviderId,
                state = state.State.ToString(),
                consecutiveFailures = state.ConsecutiveFailures,
                openedAtUtc = state.OpenedAtUtc
            }),
            raisedAtUtc = SystemClock.UtcNow
        });

        using var scope = scopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var result = await notificationService
            .SendAsync(
                recipientId: Guid.Empty, // System-level alert — routed to the admin channel
                type: NotificationType.System,
                title: "Payment gateways unavailable: all circuits open",
                message:
                    "No payment gateway could be attempted: every eligible gateway circuit is open. " +
                    $"Circuit states: {circuitSummary}. Charges are failing closed until a gateway recovers.",
                channel: NotificationChannel.InApp,
                tenantId: null,
                actionUrl: null,
                priority: NotificationPriority.High,
                referenceEntityId: null,
                referenceEntityType: "PaymentGatewayCircuits",
                metadata: metadata,
                recipientEmail: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            logger.LogError(
                "All-gateways-down alert sent: {CircuitSummary}",
                circuitSummary);
        }
        else
        {
            logger.LogError(
                "All-gateways-down alert was not delivered: {Reason}",
                result.Error?.Description ?? "unknown notification failure");
        }
    }

    private bool TryBeginAlert()
    {
        lock (_throttleSyncRoot)
        {
            if (SystemClock.UtcNow - _lastRaisedAtUtc < ThrottleWindow)
            {
                return false;
            }

            _lastRaisedAtUtc = SystemClock.UtcNow;
            return true;
        }
    }
}

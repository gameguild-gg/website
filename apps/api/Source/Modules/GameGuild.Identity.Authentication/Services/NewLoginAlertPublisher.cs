using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Records <see cref="NewLoginDetectedV1"/> durable events for the host-side notification
///     bridge, gated by <c>Authentication:NewLoginNotifications:Enabled</c> (default on — the
///     per-user opt-in preference is evaluated by the consumer, and defaults to off).
///     Mirrors <see cref="SuspiciousLoginAlertPublisher"/>: recording runs in an independent
///     service scope so the outbox entry commits independently of the sign-in transaction, and
///     any failure is swallowed because the notification is defense-in-depth.
/// </summary>
public sealed class NewLoginAlertPublisher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<NewLoginAlertPublisher> logger) : INewLoginAlertPublisher
{
    private const string EnabledPath = "Authentication:NewLoginNotifications:Enabled";

    public async Task RecordAsync(
        Guid userId,
        Guid? tenantId,
        Guid sessionId,
        string? deviceSummary,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return;
        }

        if (!configuration.GetValue(EnabledPath, true))
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var events = scope.ServiceProvider.GetRequiredService<IDurableEventProducer>();
            await events.RecordAsync(new NewLoginDetectedV1(
                userId,
                sessionId,
                string.IsNullOrWhiteSpace(deviceSummary) ? string.Empty : deviceSummary)
            {
                TenantId = tenantId ?? DurableIntegrationEventTenants.Platform,
                ActorId = DurableIntegrationEventActors.System,
                AggregateType = "User",
                AggregateId = userId.ToString(),
                CorrelationId = Guid.NewGuid()
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Informational notification: a failure here must never break authentication.
            logger.LogError(exception,
                "Could not record the new-login event for user {UserId} (session {SessionId})",
                userId, sessionId);
        }
    }
}

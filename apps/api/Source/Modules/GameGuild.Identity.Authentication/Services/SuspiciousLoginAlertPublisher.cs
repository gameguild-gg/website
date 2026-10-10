using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Records <see cref="SuspiciousLoginDetectedV1" /> durable events for the host-side
///     notification bridge, gated by <c>Authentication:SecurityNotifications</c>.
///     Gated by <c>Enabled</c> (default on) and <c>MinimumRiskLevel</c> (default High).
/// </summary>
/// <remarks>
///     <para>
///         Suspicious sign-ins must alert the owner even when the sign-in command itself fails:
///         a step-up challenge returns a failure outcome and a denied attempt throws, and the
///         use-case pipeline rolls back the request transaction in both cases. Recording therefore
///         runs in an independent service scope with its own database context, mirroring the
///         independent-scope persistence used for refresh-token rejection audits, so the alert
///         outbox entry commits independently of the sign-in transaction.
///     </para>
/// </remarks>
public sealed class SuspiciousLoginAlertPublisher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SuspiciousLoginAlertPublisher> logger) : ISuspiciousLoginAlertPublisher
{
    private const string EnabledPath = "Authentication:SecurityNotifications:Enabled";
    private const string MinimumRiskLevelPath = "Authentication:SecurityNotifications:MinimumRiskLevel";

    public async Task RecordAsync(
        Guid userId,
        Guid? tenantId,
        string alertKind,
        RiskLevel assessedRiskLevel,
        int riskScore,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(alertKind))
        {
            return;
        }

        var enabled = configuration.GetValue(EnabledPath, true);
        var minimumRiskLevel = configuration.GetValue(MinimumRiskLevelPath, RiskLevel.High);
        // The named signals are on the notify-by-default list, so their effective severity is at
        // least High; the minimum can only be raised (e.g. Critical) to silence High-level alerts.
        var effectiveRiskLevel = assessedRiskLevel > RiskLevel.High ? assessedRiskLevel : RiskLevel.High;
        if (!enabled || effectiveRiskLevel < minimumRiskLevel)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var events = scope.ServiceProvider.GetRequiredService<IDurableEventProducer>();
            await events.RecordAsync(new SuspiciousLoginDetectedV1(
                userId,
                alertKind,
                assessedRiskLevel.ToString(),
                riskScore)
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
            // Security alert recording is defense-in-depth: a failure here must never break
            // authentication. The durable transport retries recorded events, and the analysis
            // itself is already persisted, audited, and forwarded to the SIEM.
            logger.LogError(exception,
                "Could not record the suspicious-login alert event for user {UserId} ({AlertKind})",
                userId, alertKind);
        }
    }
}

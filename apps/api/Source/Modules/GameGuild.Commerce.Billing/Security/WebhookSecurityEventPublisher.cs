using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>Kind of webhook security event published to the central security event pipeline.</summary>
public enum WebhookSecurityEventKind
{
    /// <summary>Provider signature or timestamp verification failed.</summary>
    SignatureFailed = 1,

    /// <summary>Source rejected by the configured IP allowlist.</summary>
    SourceIpRejected = 2,

    /// <summary>Duplicate/replayed webhook detected by idempotency checks.</summary>
    ReplayDetected = 3,

    /// <summary>Webhook endpoint rate limit exceeded.</summary>
    RateLimitExceeded = 4,

    /// <summary>Source temporarily blocked for repeated security failures.</summary>
    SourceBlocked = 5
}

/// <summary>
///     Publishes billing webhook security events to the Compliance.Audit security event
///     pipeline. Publishing is best-effort from the request path: a pipeline failure is
///     logged, never propagated to the webhook caller.
/// </summary>
public interface IWebhookSecurityEventPublisher
{
    Task PublishAsync(
        WebhookSecurityEventKind kind,
        string provider,
        string? sourceIpAddress,
        string detail,
        string? eventId = null,
        CancellationToken cancellationToken = default);
}

public sealed class WebhookSecurityEventPublisher(
    ISecurityEventLogger securityEventLogger,
    ILogger<WebhookSecurityEventPublisher> logger) : IWebhookSecurityEventPublisher
{
    private static readonly IReadOnlyDictionary<WebhookSecurityEventKind, string> ActionTypes =
        new Dictionary<WebhookSecurityEventKind, string>
        {
            [WebhookSecurityEventKind.SignatureFailed] = AuditActionTypes.WebhookSignatureFailed,
            [WebhookSecurityEventKind.SourceIpRejected] = AuditActionTypes.WebhookSourceIpRejected,
            [WebhookSecurityEventKind.ReplayDetected] = AuditActionTypes.WebhookReplayDetected,
            [WebhookSecurityEventKind.RateLimitExceeded] = AuditActionTypes.WebhookRateLimitExceeded,
            [WebhookSecurityEventKind.SourceBlocked] = AuditActionTypes.WebhookSourceBlocked
        };

    public async Task PublishAsync(
        WebhookSecurityEventKind kind,
        string provider,
        string? sourceIpAddress,
        string detail,
        string? eventId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        var request = new CreateAuditLogRequest
        {
            ActionType = ActionTypes[kind],
            ResourceType = "BillingWebhook",
            ResourceId = provider,
            IpAddress = sourceIpAddress,
            Category = AuditCategory.Security,
            RiskLevel = RiskLevelFor(kind),
            Success = false,
            ErrorMessage = detail,
            Description = $"Billing webhook security event ({kind}) for provider '{provider}'.",
            Metadata = new Dictionary<string, object?>
            {
                ["provider"] = provider,
                ["webhookSecurityEvent"] = kind.ToString(),
                ["sourceIpAddress"] = sourceIpAddress,
                ["eventId"] = eventId
            }
        };

        try
        {
            await securityEventLogger.RecordAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Best effort: security telemetry must never change the webhook response.
            logger.LogError(
                exception,
                "Failed to publish webhook security event {Kind} for provider {Provider}",
                kind,
                provider);
        }
    }

    private static AuditRiskLevel RiskLevelFor(WebhookSecurityEventKind kind) => kind switch
    {
        WebhookSecurityEventKind.SignatureFailed => AuditRiskLevel.High,
        WebhookSecurityEventKind.SourceIpRejected => AuditRiskLevel.High,
        WebhookSecurityEventKind.ReplayDetected => AuditRiskLevel.Low,
        WebhookSecurityEventKind.RateLimitExceeded => AuditRiskLevel.Medium,
        WebhookSecurityEventKind.SourceBlocked => AuditRiskLevel.High,
        _ => AuditRiskLevel.Medium
    };
}

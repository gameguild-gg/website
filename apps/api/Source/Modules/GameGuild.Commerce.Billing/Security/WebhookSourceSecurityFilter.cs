using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Endpoint filter enforcing the webhook source security controls on the anonymous provider
///     callback endpoints: temporary suspicious-activity blocks first, then the configured CIDR
///     allowlist (fail closed with 403). Both checks run before the request payload is read.
/// </summary>
public sealed class WebhookSourceSecurityFilter(
    WebhookSourceIpAllowlist allowlist,
    IWebhookSuspiciousActivityMonitor suspiciousActivityMonitor,
    IWebhookSecurityEventPublisher securityEventPublisher,
    ILogger<WebhookSourceSecurityFilter> logger) : IAsyncAuthorizationFilter
{

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        var provider = ResolveProvider(context);

        // Blocked sources are rejected first, before the allowlist, with a security event.
        if (!string.IsNullOrEmpty(remoteIp) &&
            suspiciousActivityMonitor.IsBlocked(remoteIp, SystemClock.UtcNow))
        {
            logger.LogWarning(
                "Billing webhook from blocked source {SourceIp} rejected (provider {Provider}).",
                remoteIp,
                provider);

            await PublishSafe(
                WebhookSecurityEventKind.SourceBlocked,
                provider,
                remoteIp,
                "Request from a source currently blocked for suspicious webhook activity.");

            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        if (allowlist.IsAllowed(context.HttpContext.Connection.RemoteIpAddress))
        {
            return;
        }

        logger.LogWarning(
            "Billing webhook from disallowed source {SourceIp} rejected by allowlist (provider {Provider}).",
            remoteIp ?? "<unknown>",
            provider);

        await PublishSafe(
            WebhookSecurityEventKind.SourceIpRejected,
            provider,
            remoteIp,
            "Source address is not in the configured billing webhook allowlist.");

        if (suspiciousActivityMonitor.RegisterFailure(remoteIp ?? "unknown", SystemClock.UtcNow))
        {
            await PublishSafe(
                WebhookSecurityEventKind.SourceBlocked,
                provider,
                remoteIp,
                "Source exceeded the suspicious-activity failure threshold and was temporarily blocked.");
        }

        context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
    }

    private static string ResolveProvider(AuthorizationFilterContext context) =>
        context.HttpContext.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) switch
        {
            { Length: > 3 } segments => segments[^1].TrimEnd(':'),
            _ => "unknown"
        };

    private async Task PublishSafe(WebhookSecurityEventKind kind, string provider, string? sourceIp, string detail)
    {
        try
        {
            await securityEventPublisher.PublishAsync(kind, provider, sourceIp, detail).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to publish webhook source security event {Kind}", kind);
        }
    }
}

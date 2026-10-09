using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Bridges identity authentication events to the durable security event pipeline. Authentication
/// events are security events: they are classified by the security event taxonomy, persisted with
/// bounded retries and local spool fallback, and evaluated against the security alert rules.
/// </summary>
public sealed class CentralAuthenticationAuditEventSink(
    ISecurityEventLogger securityEventLogger,
    ILogger<CentralAuthenticationAuditEventSink> logger) : IAuthenticationAuditEventSink
{
    public async Task RecordAsync(AuthenticationAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        try
        {
            var result = await securityEventLogger.RecordAsync(new CreateAuditLogRequest
            {
                ActionType = auditEvent.ActionType,
                ResourceType = "User",
                ResourceId = auditEvent.UserId?.ToString(),
                UserId = auditEvent.UserId,
                TenantId = auditEvent.TenantId,
                IpAddress = auditEvent.IpAddress,
                UserAgent = auditEvent.UserAgent,
                SessionId = auditEvent.SessionId,
                Description = $"Authentication event using {auditEvent.Method}",
                Metadata = new { AuthenticationMethod = auditEvent.Method, Details = auditEvent.Metadata },
                Success = auditEvent.Success,
                ErrorMessage = auditEvent.ErrorMessage,
                RiskLevel = auditEvent.AssessedRiskLevel switch
                {
                    RiskLevel.Low => AuditRiskLevel.Low,
                    RiskLevel.Medium => AuditRiskLevel.Medium,
                    RiskLevel.High => AuditRiskLevel.High,
                    RiskLevel.Critical => AuditRiskLevel.Critical,
                    _ => auditEvent.Success ? AuditRiskLevel.Low : AuditRiskLevel.High
                },
                Category = AuditCategory.Authentication
            }, cancellationToken).ConfigureAwait(false);

            if (result.Outcome is SecurityEventCaptureOutcome.SpooledLocally or SecurityEventCaptureOutcome.SpoolingDisabled)
            {
                logger.LogWarning(
                    "Authentication event {ActionType} was not persisted directly (outcome {Outcome}); capture error: {CaptureError}",
                    auditEvent.ActionType,
                    result.Outcome,
                    result.CaptureError);
            }
        }
        catch (Exception exception)
        {
            // The audit transport must not make a login, MFA, or session operation fail.
            logger.LogError(exception, "Could not forward authentication event {ActionType} to the security event pipeline", auditEvent.ActionType);
        }
    }
}

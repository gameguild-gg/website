using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Bridges API-key lifecycle events (create/rotate/revoke) to the central compliance audit log,
/// keyed by the API-key id. Key material is never written to the audit trail.
/// </summary>
public sealed class CentralApiKeyAuditEventSink(
    IAuditService auditService,
    ILogger<CentralApiKeyAuditEventSink> logger) : IApiKeyAuditEventSink
{
    public async Task RecordAsync(ApiKeyAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        try
        {
            await auditService.LogAsync(new CreateAuditLogRequest
            {
                ActionType = auditEvent.ActionType,
                ResourceType = "ApiKey",
                ResourceId = auditEvent.ApiKeyId.ToString(),
                UserId = auditEvent.UserId,
                TenantId = auditEvent.TenantId,
                Description = auditEvent.Description,
                Metadata = auditEvent.Metadata,
                Success = auditEvent.Success,
                ErrorMessage = auditEvent.ErrorMessage,
                RiskLevel = AuditRiskLevel.Medium,
                Category = AuditCategory.Security
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Audit transport must not make an API-key lifecycle operation fail.
            logger.LogError(exception, "Could not forward API-key event {ActionType} for key {ApiKeyId} to the audit log",
                auditEvent.ActionType, auditEvent.ApiKeyId);
        }
    }
}

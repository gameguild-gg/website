using GameGuild.Identity.Provisioning;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Bridges SCIM provisioning events (user/group mutations and token lifecycle) to
///     the central compliance audit log. Token material is never written to the audit
///     trail; only the actor subject and identifiers are recorded.
/// </summary>
public sealed class CentralScimProvisioningAuditEventSink(
    IAuditService auditService,
    ILogger<CentralScimProvisioningAuditEventSink> logger) : IScimProvisioningAuditSink
{
    public async Task RecordAsync(ScimProvisioningAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        try
        {
            await auditService.LogAsync(new CreateAuditLogRequest
            {
                ActionType = auditEvent.Action,
                ResourceType = auditEvent.TargetRoleId is { } roleId ? "ScimGroup" : "ScimUser",
                ResourceId = (auditEvent.TargetUserId ?? auditEvent.TargetRoleId)?.ToString() ?? auditEvent.Actor,
                UserId = auditEvent.TargetUserId,
                TenantId = auditEvent.TenantId,
                Description = auditEvent.Detail ?? auditEvent.Action,
                Metadata = auditEvent.Metadata,
                Success = true,
                ErrorMessage = null,
                RiskLevel = AuditRiskLevel.Medium,
                Category = AuditCategory.Security
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Audit transport must not make a provisioning operation fail.
            logger.LogError(exception, "Could not forward SCIM provisioning event {Action} to the audit log",
                auditEvent.Action);
        }
    }
}

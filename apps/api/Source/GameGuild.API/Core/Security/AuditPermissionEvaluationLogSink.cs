using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authorization;

namespace GameGuild.API.Core.Security;

/// <summary>
///     Durable permission evaluation log sink that persists every evaluation
///     (user, tenant, resource, permission set, outcome) to the shared audit trail.
///     Delivery failures are reported instead of swallowed: <see cref="TryRecordAsync"/>
///     returns <see langword="false"/> when the audit write was not durable, while the
///     authorization decision itself stays unaffected.
/// </summary>
internal sealed class AuditPermissionEvaluationLogSink(IAuditService auditService) : IPermissionEvaluationLogSink
{
    public async Task<bool> TryRecordAsync(
        PermissionEvaluationRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        cancellationToken.ThrowIfCancellationRequested();

        var granted = record.Outcome == PermissionEvaluationOutcome.Allow;
        var permissions = record.RequiredPermissions as IReadOnlyCollection<string> ?? record.RequiredPermissions.ToArray();
        // Role attribution is user-context audit data (issue #359): normalized so the durable
        // metadata always carries a non-null, capped role list even for records built directly.
        var roles = PermissionEvaluationRoles.Normalize(record.Roles);

        return await auditService.TryLogAsync(new CreateAuditLogRequest
        {
            ActionType = granted ? AuditActionTypes.PermissionGranted : AuditActionTypes.PermissionDenied,
            ResourceType = record.ResourceType,
            ResourceId = record.ResourceId,
            UserId = record.UserId,
            TenantId = record.TenantId,
            Description = granted
                ? $"Permission evaluation allowed '{string.Join("', '", permissions)}' on {record.ResourceType} '{record.ResourceId ?? "-"}'."
                : $"Permission evaluation denied '{string.Join("', '", permissions)}' on {record.ResourceType} '{record.ResourceId ?? "-"}'.",
            Metadata = new
            {
                // Serialize as the outcome name; enums would serialize as numbers
                // and make the durable audit metadata harder to query.
                Outcome = record.Outcome.ToString(),
                record.Operation,
                record.Source,
                record.Reason,
                RequiredPermissions = permissions,
                Roles = roles,
            },
            Success = granted,
            ErrorMessage = granted ? null : record.Reason,
            RiskLevel = granted ? AuditRiskLevel.Medium : AuditRiskLevel.High,
            Category = AuditCategory.Permission,
        }).ConfigureAwait(false);
    }
}

using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record ExportAuditLogsCommand(
    Guid AdminUserId,
    AuditExportRequest Request) : ICommand<List<AuditLog>>;
public sealed record ExportSecurityAuditLogsCommand(
    Guid AdminUserId,
    UnifiedSecurityAuditRequest Request) : ICommand<byte[]>;

public sealed class AuditExportCommandHandler(
    IAuditService auditService,
    ISecurityAuditAggregator auditAggregator) :
    ICommandHandler<ExportAuditLogsCommand, List<AuditLog>>,
    ICommandHandler<ExportSecurityAuditLogsCommand, byte[]>
{
    public async Task<List<AuditLog>> Handle(
        ExportAuditLogsCommand command,
        CancellationToken cancellationToken)
    {
        await auditService.LogAdminActionAsync(
            command.AdminUserId,
            "ExportAuditLogs",
            "Admin exported audit logs",
            new { ExportRequest = command.Request, RequestedBy = command.AdminUserId }).ConfigureAwait(false);

        return await auditService.GetAuditLogsAsync(new AuditLogQuery
        {
            UserId = command.Request.UserId,
            TenantId = command.Request.TenantId,
            ActionType = command.Request.ActionType,
            ResourceType = command.Request.ResourceType,
            Category = command.Request.Category,
            RiskLevel = command.Request.RiskLevel,
            Success = command.Request.Success,
            StartDate = command.Request.StartDate,
            EndDate = command.Request.EndDate,
            IpAddress = command.Request.IpAddress,
            Take = 0
        }).ConfigureAwait(false);
    }

    public async Task<byte[]> Handle(
        ExportSecurityAuditLogsCommand command,
        CancellationToken cancellationToken)
    {
        await auditService.LogAdminActionAsync(
            command.AdminUserId,
            "ExportSecurityAuditLogs",
            "Admin exported unified security audit logs",
            new { Filters = command.Request }).ConfigureAwait(false);
        return await auditAggregator.ExportAuditLogsAsync(command.Request, cancellationToken).ConfigureAwait(false);
    }
}

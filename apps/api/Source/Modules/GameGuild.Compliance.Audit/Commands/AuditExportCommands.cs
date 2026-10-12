using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record ExportAuditLogsCommand(
    Guid AdminUserId,
    Guid ExportId,
    AuditExportRequest Request) : ICommand<AuditLogExportData>;

public sealed record AuditLogExportData(
    Guid ExportId,
    int TotalCount,
    int? PageNumber,
    int? PageSize,
    IAsyncEnumerable<AuditLog> Records);

public sealed record ExportSecurityAuditLogsCommand(
    Guid AdminUserId,
    UnifiedSecurityAuditRequest Request) : ICommand<byte[]>;

public sealed class AuditExportCommandHandler(
    IAuditService auditService,
    ISecurityAuditAggregator auditAggregator) :
    ICommandHandler<ExportAuditLogsCommand, AuditLogExportData>,
    ICommandHandler<ExportSecurityAuditLogsCommand, byte[]>
{
    public async Task<AuditLogExportData> Handle(
        ExportAuditLogsCommand command,
        CancellationToken cancellationToken)
    {
        await auditService.LogAdminActionAsync(
            command.AdminUserId,
            "ExportAuditLogs",
            "Admin exported audit logs",
            new
            {
                ExportId = command.ExportId,
                RequestedBy = command.AdminUserId,
                HasCompletionWebhook = !string.IsNullOrWhiteSpace(command.Request.WebhookUrl)
            }).ConfigureAwait(false);

        var query = new AuditLogQuery
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
            Skip = command.Request.PageNumber.HasValue && command.Request.PageSize.HasValue
                ? (command.Request.PageNumber.Value - 1) * command.Request.PageSize.Value
                : 0,
            Take = command.Request.PageSize ?? 0
        };

        var totalCount = await auditService.GetAuditLogCountAsync(query).ConfigureAwait(false);
        var records = auditService.StreamAuditLogsAsync(query, cancellationToken);

        return new AuditLogExportData(
            command.ExportId,
            totalCount,
            command.Request.PageNumber,
            command.Request.PageSize,
            records);
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

using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record SearchAuditLogsWithConditionsCommand(
    Guid AdminUserId,
    AuditConditionSearchRequest Request) : ICommand<SearchAuditLogsWithConditionsResult>;

public sealed record SearchAuditLogsWithConditionsResult(
    IReadOnlyList<AuditLog> Logs,
    int TotalCount);

public sealed class SearchAuditLogsWithConditionsCommandHandler(IAuditService auditService) :
    ICommandHandler<SearchAuditLogsWithConditionsCommand, SearchAuditLogsWithConditionsResult>
{
    public async Task<SearchAuditLogsWithConditionsResult> Handle(
        SearchAuditLogsWithConditionsCommand command,
        CancellationToken cancellationToken)
    {
        await auditService.LogAdminActionAsync(
            command.AdminUserId,
            "SearchAuditLogsWithConditions",
            "Admin searched audit logs with condition filters",
            new { Filters = command.Request, RequestedBy = command.AdminUserId }).ConfigureAwait(false);

        var query = new AuditLogQuery
        {
            Condition = command.Request.Condition,
            Skip = command.Request.Skip,
            Take = Math.Min(command.Request.Take, 1000) // Cap at 1000 records
        };

        var logs = await auditService.GetAuditLogsAsync(query).ConfigureAwait(false);
        var totalCount = await auditService.GetAuditLogCountAsync(query).ConfigureAwait(false);

        return new SearchAuditLogsWithConditionsResult(logs, totalCount);
    }
}

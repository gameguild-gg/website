using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record CreateScheduledAuditExportCommand(
    CreateScheduledAuditExportRequest Request,
    Guid AdminUserId) : ICommand<ScheduledAuditExportResponse>;

public sealed record DisableScheduledAuditExportCommand(
    Guid ExportId,
    Guid TenantId,
    Guid AdminUserId) : ICommand<bool>;

public sealed class ScheduledAuditExportCommandHandler(IScheduledAuditExportService scheduledExportService) :
    ICommandHandler<CreateScheduledAuditExportCommand, ScheduledAuditExportResponse>,
    ICommandHandler<DisableScheduledAuditExportCommand, bool>
{
    public Task<ScheduledAuditExportResponse> Handle(
        CreateScheduledAuditExportCommand command,
        CancellationToken cancellationToken) =>
        scheduledExportService.CreateAsync(command.Request, command.AdminUserId, cancellationToken);

    public Task<bool> Handle(DisableScheduledAuditExportCommand command, CancellationToken cancellationToken) =>
        scheduledExportService.DisableAsync(
            command.ExportId,
            command.TenantId,
            command.AdminUserId,
            cancellationToken);
}

using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record CaptureAuditReplayCommand(AuditReplayCaptureRequest Request) : ICommand<AuditReplaySnapshot>;

public sealed class CaptureAuditReplayCommandHandler(IAuditReplayCaptureService service) :
    ICommandHandler<CaptureAuditReplayCommand, AuditReplaySnapshot>
{
    public Task<AuditReplaySnapshot> Handle(CaptureAuditReplayCommand command, CancellationToken cancellationToken) =>
        service.CaptureAsync(command.Request, cancellationToken);
}

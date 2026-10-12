using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record ConfigureAuditRetentionCommand(ConfigureAuditRetentionRequest Request) : ICommand<AuditRetentionConfigurationResponse>;
public sealed record RunAuditRetentionSimulationCommand(RunAuditRetentionSimulationRequest Request) : ICommand<AuditRetentionSimulationResponse?>;

public sealed class AuditRetentionSimulationCommandHandler(IAuditRetentionSimulationService service) :
    ICommandHandler<ConfigureAuditRetentionCommand, AuditRetentionConfigurationResponse>,
    ICommandHandler<RunAuditRetentionSimulationCommand, AuditRetentionSimulationResponse?>
{
    public Task<AuditRetentionConfigurationResponse> Handle(ConfigureAuditRetentionCommand command, CancellationToken cancellationToken) =>
        service.ConfigureAsync(command.Request, cancellationToken);
    public Task<AuditRetentionSimulationResponse?> Handle(RunAuditRetentionSimulationCommand command, CancellationToken cancellationToken) =>
        service.RunAsync(command.Request, cancellationToken);
}

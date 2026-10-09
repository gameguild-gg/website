using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record ConfigureSecurityLogRetentionCommand(ConfigureSecurityLogRetentionRequest Request) : ICommand<SecurityLogRetentionPolicyResponse>;

public sealed record EnforceSecurityLogRetentionCommand(EnforceSecurityLogRetentionRequest Request) : ICommand<SecurityLogRetentionExecutionResponse?>;

public sealed record AcknowledgeSecurityAlertCommand(Guid AlertId, string? Notes) : ICommand<SecurityAlertResponse?>;

public sealed class SecurityEventPipelineCommandHandler(
    ISecurityLogRetentionService retentionService,
    ISecurityEventQueryService queryService) :
    ICommandHandler<ConfigureSecurityLogRetentionCommand, SecurityLogRetentionPolicyResponse>,
    ICommandHandler<EnforceSecurityLogRetentionCommand, SecurityLogRetentionExecutionResponse?>,
    ICommandHandler<AcknowledgeSecurityAlertCommand, SecurityAlertResponse?>
{
    public Task<SecurityLogRetentionPolicyResponse> Handle(ConfigureSecurityLogRetentionCommand command, CancellationToken cancellationToken) =>
        retentionService.ConfigureAsync(command.Request, cancellationToken);

    public Task<SecurityLogRetentionExecutionResponse?> Handle(EnforceSecurityLogRetentionCommand command, CancellationToken cancellationToken) =>
        retentionService.EnforceForCurrentTenantAsync(command.Request, cancellationToken);

    public async Task<SecurityAlertResponse?> Handle(AcknowledgeSecurityAlertCommand command, CancellationToken cancellationToken) =>
        await queryService.AcknowledgeAlertAsync(command.AlertId, command.Notes, cancellationToken).ConfigureAwait(false);
}

using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record ConfigureValueSnapshotRetentionCommand(ConfigureValueSnapshotRetentionRequest Request) : ICommand<ValueSnapshotRetentionPolicyResponse>;

public sealed record EnforceValueSnapshotRetentionCommand(EnforceValueSnapshotRetentionRequest Request) : ICommand<ValueSnapshotRetentionExecutionResponse>;

public sealed record CreateValueSnapshotRollbackPlanCommand(CreateValueSnapshotRollbackPlanRequest Request) : ICommand<ValueSnapshotRollbackPlanResponse>;

public sealed record ExecuteValueSnapshotRollbackCommand(Guid PlanId, ExecuteValueSnapshotRollbackRequest Request) : ICommand<ValueSnapshotRollbackPlanResponse>;

public sealed class ValueSnapshotCommandHandler(
    IValueSnapshotRetentionService retention,
    IValueSnapshotRollbackService rollback) :
    ICommandHandler<ConfigureValueSnapshotRetentionCommand, ValueSnapshotRetentionPolicyResponse>,
    ICommandHandler<EnforceValueSnapshotRetentionCommand, ValueSnapshotRetentionExecutionResponse>,
    ICommandHandler<CreateValueSnapshotRollbackPlanCommand, ValueSnapshotRollbackPlanResponse>,
    ICommandHandler<ExecuteValueSnapshotRollbackCommand, ValueSnapshotRollbackPlanResponse>
{
    public Task<ValueSnapshotRetentionPolicyResponse> Handle(ConfigureValueSnapshotRetentionCommand command, CancellationToken cancellationToken) =>
        retention.ConfigurePolicyAsync(command.Request, cancellationToken);

    public Task<ValueSnapshotRetentionExecutionResponse> Handle(EnforceValueSnapshotRetentionCommand command, CancellationToken cancellationToken) =>
        retention.EnforceAsync(command.Request ?? new EnforceValueSnapshotRetentionRequest(), cancellationToken);

    public Task<ValueSnapshotRollbackPlanResponse> Handle(CreateValueSnapshotRollbackPlanCommand command, CancellationToken cancellationToken) =>
        rollback.CreatePlanAsync(command.Request, cancellationToken);

    public async Task<ValueSnapshotRollbackPlanResponse> Handle(ExecuteValueSnapshotRollbackCommand command, CancellationToken cancellationToken) =>
        await rollback.ExecuteAsync(command.PlanId, command.Request ?? new ExecuteValueSnapshotRollbackRequest(), cancellationToken).ConfigureAwait(false);
}

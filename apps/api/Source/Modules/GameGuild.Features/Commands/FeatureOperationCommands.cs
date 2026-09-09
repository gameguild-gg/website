using Microsoft.Extensions.Logging;
using GameGuild.CQRS;

namespace GameGuild.Features;

public sealed record EvaluateFeatureOperationCommand(
    string FeatureKey,
    FeatureContext Context) : ICommand<FeatureEvaluationResult>;
public sealed record BulkEvaluateFeatureOperationCommand(
    IReadOnlyCollection<string> FeatureKeys,
    FeatureContext Context) : ICommand<BulkEvaluateFeaturesResponse>;
public sealed record SetCapabilityOverrideCommand(
    Guid TenantId,
    string Capability,
    bool IsEnabled,
    string Source,
    Guid? UserId,
    string? Reason,
    DateTimeOffset? ExpiresAt) : ICommand;
public sealed record RemoveCapabilityOverrideCommand(
    Guid TenantId,
    string Capability,
    Guid? UserId,
    string? Reason) : ICommand;
public sealed record SyncCapabilitiesFromPlanCommand(Guid TenantId) : ICommand;

public sealed class FeatureOperationCommandHandler(
    IFeatureFlagEvaluationService evaluationService,
    ICapabilityService capabilityService,
    ILogger<FeatureOperationCommandHandler> logger) :
    ICommandHandler<EvaluateFeatureOperationCommand, FeatureEvaluationResult>,
    ICommandHandler<BulkEvaluateFeatureOperationCommand, BulkEvaluateFeaturesResponse>,
    ICommandHandler<SetCapabilityOverrideCommand>,
    ICommandHandler<RemoveCapabilityOverrideCommand>,
    ICommandHandler<SyncCapabilitiesFromPlanCommand>
{
    public Task<FeatureEvaluationResult> Handle(
        EvaluateFeatureOperationCommand command,
        CancellationToken cancellationToken) =>
        evaluationService.EvaluateAsync(command.FeatureKey, command.Context, cancellationToken);

    public async Task<BulkEvaluateFeaturesResponse> Handle(
        BulkEvaluateFeatureOperationCommand command,
        CancellationToken cancellationToken)
    {
        var results = new List<FeatureEvaluationResult>();
        foreach (var featureKey in command.FeatureKeys)
        {
            try
            {
                results.Add(await evaluationService.EvaluateAsync(
                    featureKey,
                    command.Context,
                    cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to evaluate feature '{FeatureKey}' in bulk request", featureKey);
                throw;
            }
        }

        return new BulkEvaluateFeaturesResponse
        {
            Results = results.ToDictionary(result => result.FeatureKey, result => result)
        };
    }

    public async Task<Unit> Handle(SetCapabilityOverrideCommand command, CancellationToken cancellationToken)
    {
        await capabilityService.SetCapabilityOverrideAsync(
            command.TenantId,
            command.Capability,
            command.IsEnabled,
            command.Source,
            command.UserId,
            command.Reason,
            command.ExpiresAt,
            cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(RemoveCapabilityOverrideCommand command, CancellationToken cancellationToken)
    {
        await capabilityService.RemoveCapabilityOverrideAsync(
            command.TenantId,
            command.Capability,
            command.UserId,
            command.Reason,
            cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(SyncCapabilitiesFromPlanCommand command, CancellationToken cancellationToken)
    {
        await capabilityService.SyncCapabilitiesFromPlanAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

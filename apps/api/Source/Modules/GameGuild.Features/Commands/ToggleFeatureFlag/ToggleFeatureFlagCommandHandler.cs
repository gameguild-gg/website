using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Features;

/// <summary>
///     Handler for ToggleFeatureFlagCommand
/// </summary>
public sealed class ToggleFeatureFlagCommandHandler(
    IFeatureFlagQueryRepository repository,
    ILogger<ToggleFeatureFlagCommandHandler> logger
) : ICommandHandler<ToggleFeatureFlagCommand>
{
    public async Task<Unit> Handle(ToggleFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Toggling feature flag {Id} to {State}", request.Id, request.IsEnabled);

        var flag = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException($"Feature flag '{request.Id}' not found");

        flag.IsEnabled = request.IsEnabled;
        await repository.UpdateAsync(flag, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Feature flag {Id} toggled to {State}", request.Id, request.IsEnabled);

        return Unit.Value;
    }
}

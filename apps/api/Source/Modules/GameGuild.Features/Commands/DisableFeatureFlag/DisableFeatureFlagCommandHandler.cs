using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Features;

/// <summary>
///     Handler for DisableFeatureFlagCommand
/// </summary>
public sealed class DisableFeatureFlagCommandHandler(
    IFeatureFlagQueryRepository repository,
    ILogger<DisableFeatureFlagCommandHandler> logger
) : ICommandHandler<DisableFeatureFlagCommand>
{
    public async Task<Unit> Handle(DisableFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Disabling feature flag {Id}", request.Id);

        var flag = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException($"Feature flag '{request.Id}' not found");

        flag.IsEnabled = false;
        await repository.UpdateAsync(flag, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Feature flag {Id} disabled", request.Id);

        return Unit.Value;
    }
}

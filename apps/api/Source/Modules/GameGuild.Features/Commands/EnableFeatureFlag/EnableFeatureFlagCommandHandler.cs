using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Features;

/// <summary>
///     Handler for EnableFeatureFlagCommand
/// </summary>
public sealed class EnableFeatureFlagCommandHandler(
    IFeatureFlagQueryRepository repository,
    ILogger<EnableFeatureFlagCommandHandler> logger
) : ICommandHandler<EnableFeatureFlagCommand>
{
    public async Task<Unit> Handle(EnableFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Enabling feature flag {Id}", request.Id);

        var flag = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException($"Feature flag '{request.Id}' not found");

        flag.IsEnabled = true;
        await repository.UpdateAsync(flag, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Feature flag {Id} enabled", request.Id);

        return Unit.Value;
    }
}

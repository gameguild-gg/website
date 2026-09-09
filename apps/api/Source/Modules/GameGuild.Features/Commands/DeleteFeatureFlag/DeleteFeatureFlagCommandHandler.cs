using GameGuild.CQRS;

namespace GameGuild.Features;

public sealed class DeleteFeatureFlagCommandHandler(IFeatureFlagManagementService management)
    : ICommandHandler<DeleteFeatureFlagCommand>
{
    public async Task<Unit> Handle(DeleteFeatureFlagCommand command, CancellationToken cancellationToken)
    {
        await management.DeleteFeatureFlagAsync(command.Id, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

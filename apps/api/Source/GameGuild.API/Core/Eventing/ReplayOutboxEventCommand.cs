using GameGuild.CQRS;

namespace GameGuild.API.Eventing;

public sealed record ReplayOutboxEventCommand(Guid EventId, string? ConsumerName) : ICommand<bool>;

public sealed class ReplayOutboxEventCommandHandler(IEventReplayService replayService)
    : ICommandHandler<ReplayOutboxEventCommand, bool>
{
    public Task<bool> Handle(ReplayOutboxEventCommand command, CancellationToken cancellationToken) =>
        replayService.ReplayAsync(command.EventId, command.ConsumerName, cancellationToken);
}

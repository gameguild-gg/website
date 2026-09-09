using GameGuild.CQRS;

namespace GameGuild.Features;

public sealed record DeleteFeatureFlagCommand(Guid Id) : ICommand;

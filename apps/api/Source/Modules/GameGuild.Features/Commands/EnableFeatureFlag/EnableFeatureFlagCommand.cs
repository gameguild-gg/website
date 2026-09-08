using GameGuild.CQRS;

namespace GameGuild.Features;

/// <summary>
///     Command to enable a feature flag
/// </summary>
public sealed record EnableFeatureFlagCommand(Guid Id) : ICommand;

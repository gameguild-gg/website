using GameGuild.CQRS;

namespace GameGuild.Features;

/// <summary>
///     Command to disable a feature flag
/// </summary>
public sealed record DisableFeatureFlagCommand(Guid Id) : ICommand;

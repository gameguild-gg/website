using GameGuild.CQRS;

namespace GameGuild.Features;

/// <summary>
///     Command to toggle a feature flag state
/// </summary>
public sealed record ToggleFeatureFlagCommand(Guid Id, bool IsEnabled) : ICommand;

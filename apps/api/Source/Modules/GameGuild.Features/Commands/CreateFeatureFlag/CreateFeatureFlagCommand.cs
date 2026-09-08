using GameGuild.CQRS;
using GameGuild.Resources;

namespace GameGuild.Features;

/// <summary>
///     Command to create a new feature flag
/// </summary>
[RequiresQuota(ResourceUsageType.FeatureFlags, Source = "CreateFeatureFlag")]
public sealed record CreateFeatureFlagCommand(string Key, string Name, string? Description, bool IsEnabled = false, Guid? TenantId = null) : ICommand<Guid>;

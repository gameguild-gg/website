using System.Text.Json;

namespace GameGuild.Features;

/// <summary>
///     Utility class for mapping between Entity and Model types
///     Provides manual mapping without AutoMapper dependency
/// </summary>
public static class EntityModelMapper
{
    /// <summary>
    ///     Converts Entity FeatureFlagType to Model FeatureFlagType
    /// </summary>
    public static FeatureFlagType ToModel(FeatureFlagType entityType) { return (FeatureFlagType) (int) entityType; }

    /// <summary>
    ///     Converts Model FeatureFlagType to Entity FeatureFlagType
    /// </summary>
    public static FeatureFlagType ToEntity(FeatureFlagType modelType) { return (FeatureFlagType) (int) modelType; }

    /// <summary>
    ///     Maps Entity FeatureFlag to Model FeatureFlagConfig
    ///     Used for SDK configuration and API responses
    /// </summary>
    public static FeatureFlagConfig ToConfig(FeatureFlag entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new FeatureFlagConfig
        {
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            Type = ToModel(entity.Type),
            DefaultValue = entity.DefaultValue,
            EnabledValue = entity.EnabledValue,
            RolloutPercentage = entity.RolloutPercentage,
            Environment = entity.Environment,
            IsEnabled = entity.IsEnabled
        };
    }

    /// <summary>
    ///     Maps collection of Entity FeatureFlags to Model FeatureFlagConfigs
    /// </summary>
    public static IEnumerable<FeatureFlagConfig> ToConfigs(IEnumerable<FeatureFlag> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        return entities.Select(ToConfig);
    }

    /// <summary>
    ///     Maps Entity FeatureFlagTarget to Model TargetingRule
    /// </summary>
    public static TargetingRule ToTargetingRule(FeatureFlagTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new TargetingRule
        {
            TargetType = target.TargetType,
            TargetIdentifier = target.TargetIdentifier,
            IsEnabled = target.IsEnabled,
            RolloutPercentage = target.RolloutPercentage,
            CustomValue = target.CustomValue,
            Priority = target.Priority,
            Conditions = target.Metadata != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(target.Metadata) ?? new Dictionary<string, object>() : new Dictionary<string, object>()
        };
    }

    /// <summary>
    ///     Creates an Entity FeatureFlagTarget from a FeatureFlagTargetingRequest
    ///     Used for CRUD operations
    /// </summary>
    public static FeatureFlagTarget ToTargetEntity(FeatureFlagTargetingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new FeatureFlagTarget
        {
            FeatureFlagId = request.FeatureFlagId,
            TargetType = request.TargetType ?? string.Empty,
            TargetIdentifier = request.TargetIdentifier ?? string.Empty,
            IsEnabled = request.IsEnabled,
            RolloutPercentage = request.RolloutPercentage ?? 100,
            CustomValue = request.CustomValue,
            Priority = request.Priority,
            Metadata = request.Metadata != null && request.Metadata.Count > 0 ? JsonSerializer.Serialize(request.Metadata) : null
        };
    }

    /// <summary>
    ///     Updates an existing Entity FeatureFlagTarget from a FeatureFlagTargetingRequest
    ///     Only updates provided fields
    /// </summary>
    public static void UpdateTargetEntity(FeatureFlagTarget target, FeatureFlagTargetingRequest request)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(request);

        target.TargetType = request.TargetType ?? string.Empty;
        target.TargetIdentifier = request.TargetIdentifier ?? string.Empty;
        target.IsEnabled = request.IsEnabled;
        target.RolloutPercentage = request.RolloutPercentage ?? 100;
        target.Priority = request.Priority;

        if (request.CustomValue != null)
        {
            target.CustomValue = request.CustomValue;
        }

        if (request.Metadata != null && request.Metadata.Count > 0)
        {
            target.Metadata = JsonSerializer.Serialize(request.Metadata);
        }
    }

    /// <summary>
    ///     Converts a FeatureFlag entity to a DTO
    /// </summary>
    public static FeatureFlagDto ToDto(FeatureFlag featureFlag)
    {
        ArgumentNullException.ThrowIfNull(featureFlag);

        return new FeatureFlagDto
        {
            Id = featureFlag.Id,
            Key = featureFlag.Key,
            Name = featureFlag.Name,
            Description = featureFlag.Description,
            Type = featureFlag.Type,
            IsEnabled = featureFlag.IsEnabled,
            DefaultValue = featureFlag.DefaultValue,
            Environment = featureFlag.Environment,
            CreatedAt = featureFlag.CreatedAt,
            UpdatedAt = featureFlag.UpdatedAt
            // Add other properties as needed
        };
    }
}

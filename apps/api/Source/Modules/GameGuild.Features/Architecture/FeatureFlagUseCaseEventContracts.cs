using GameGuild;
using GameGuild.Features;

[assembly: UseCaseEventContract(typeof(CreateFeatureFlagCommand), "features.create-feature-flag", QuotaImpact = "+1 FeatureFlags", NoDomainEventReason = "Feature-flag creation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(DeleteFeatureFlagCommand), "features.delete-feature-flag", QuotaImpact = "-1 FeatureFlags", NoDomainEventReason = "Feature-flag deletion is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(EnableFeatureFlagCommand), "features.enable-feature-flag", NoDomainEventReason = "Feature-flag activation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(DisableFeatureFlagCommand), "features.disable-feature-flag", NoDomainEventReason = "Feature-flag deactivation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ToggleFeatureFlagCommand), "features.toggle-feature-flag", NoDomainEventReason = "Feature-flag state changes are observed through the durable generic operation event.")]

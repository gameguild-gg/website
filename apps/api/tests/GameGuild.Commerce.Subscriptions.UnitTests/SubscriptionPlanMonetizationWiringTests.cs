using System.Reflection;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests;

/// <summary>
///     Issue #346 (Configure): subscription plan tiers and pricing models are monetization
///     settings. Every plan-configuration command must be gated on the CQRS pipeline (all
///     dispatch paths) and every mutating plan endpoint must be gated at the controller.
/// </summary>
public sealed class SubscriptionPlanMonetizationWiringTests
{
    private static readonly Type[] PlanConfigurationCommands =
    [
        typeof(CreateSubscriptionPlanCommand),
        typeof(FullUpdateSubscriptionPlanCommand),
        typeof(UpdateSubscriptionPlanCommand),
        typeof(UpdateSubscriptionPlanPricingCommand),
        typeof(UpdateSubscriptionPlanLimitsCommand),
        typeof(UpdateSubscriptionPlanFeaturesCommand),
        typeof(ActivateSubscriptionPlanCommand),
        typeof(DeactivateSubscriptionPlanCommand),
        typeof(ArchiveSubscriptionPlanCommand),
        typeof(CloneSubscriptionPlanCommand),
        typeof(DeleteSubscriptionPlanCommand),
        typeof(SetSubscriptionPlanFeaturedCommand),
        typeof(SetSubscriptionPlanExternalIdCommand)
    ];

    [Fact]
    public void PlanConfigurationCommands_RequireConfigurePermission()
    {
        foreach (var command in PlanConfigurationCommands)
        {
            var attribute = command.GetCustomAttribute<AuthorizeRequestAttribute>();
            attribute.Should().NotBeNull(
                "{0} must carry [AuthorizeRequest] so plan configuration is gated on every dispatch path",
                command.Name);
            attribute!.Permission.Should().Be(MonetizationPermission.Keys.Configure, $"for {command.Name}");
        }
    }

    [Theory]
    [InlineData(typeof(SubscriptionPlansCrudController), nameof(SubscriptionPlansCrudController.CreateSubscriptionPlan))]
    [InlineData(typeof(SubscriptionPlansCrudController), nameof(SubscriptionPlansCrudController.DeleteSubscriptionPlan))]
    [InlineData(typeof(SubscriptionPlansCrudController), nameof(SubscriptionPlansCrudController.PutSubscriptionPlan))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.UpdateSubscriptionPlanDetails))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.UpdateSubscriptionPlanPricing))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.UpdateSubscriptionPlanLimits))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.UpdateSubscriptionPlanFeatures))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.ActivateSubscriptionPlan))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.DeactivateSubscriptionPlan))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.ArchiveSubscriptionPlan))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.CloneSubscriptionPlan))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.SetSubscriptionPlanFeatured))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.SetSubscriptionPlanExternalId))]
    public void MutatingPlanEndpoints_RequireConfigurePermission(Type controller, string actionName)
    {
        var method = controller.GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();

        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();
        attribute.Should().NotBeNull(
            "{0}.{1} mutates monetization settings and must be permission-gated", controller.Name, actionName);
        attribute!.PermissionName.Should().Be(MonetizationPermission.Keys.Configure);
    }

    [Theory]
    [InlineData(typeof(SubscriptionPlansCrudController), nameof(SubscriptionPlansCrudController.GetSubscriptionPlans))]
    [InlineData(typeof(SubscriptionPlansCrudController), nameof(SubscriptionPlansCrudController.GetSubscriptionPlanById))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.GetSubscriptionPlanUsage))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.CalculateSubscriptionPlanPricing))]
    [InlineData(typeof(SubscriptionPlanOperationsController), nameof(SubscriptionPlanOperationsController.ValidateSubscriptionPlanLimits))]
    public void ReadOnlyPlanEndpoints_AreNotConfigureGated(Type controller, string actionName)
    {
        var method = controller.GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();

        method!.GetCustomAttribute<RequirePermissionAttribute>().Should().BeNull(
            "{0}.{1} is read-only plan discovery/validation and must remain accessible without the configure permission",
            controller.Name, actionName);
    }

    [Fact]
    public void SelfServiceSubscriptionCommands_AreNotConfigureGated()
    {
        // Changing a subscription's billing cycle is a self-service operation on the
        // subscriber's own subscription, not platform monetization configuration.
        typeof(ChangeSubscriptionBillingCycleCommand)
            .GetCustomAttribute<AuthorizeRequestAttribute>()
            .Should().BeNull();
    }
}

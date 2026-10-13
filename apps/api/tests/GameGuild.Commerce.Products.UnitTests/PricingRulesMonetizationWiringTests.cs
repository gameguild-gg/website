using System.Reflection;
using Asp.Versioning;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests;

/// <summary>
/// Issue #395: pricing-rule management must be permission-guarded at the controller level
/// (products:pricing:manage, the same monetization gate as SetProductPricing) and on the
/// CQRS pipeline (monetization:monetize) so pricing changes are gated on every dispatch path.
/// </summary>
public sealed class PricingRulesMonetizationWiringTests
{
    public static TheoryData<string> MutatingActions => new()
    {
        nameof(PricingRulesController.CreatePricingRule),
        nameof(PricingRulesController.UpdatePricingRule),
        nameof(PricingRulesController.ActivatePricingRule),
        nameof(PricingRulesController.DeactivatePricingRule),
        nameof(PricingRulesController.DeletePricingRule)
    };

    public static TheoryData<string> PricingRuleCommands => new()
    {
        nameof(CreatePricingRuleCommand),
        nameof(UpdatePricingRuleCommand),
        nameof(DeletePricingRuleCommand),
        nameof(ActivatePricingRuleCommand),
        nameof(DeactivatePricingRuleCommand)
    };

    [Theory]
    [MemberData(nameof(MutatingActions))]
    public void MutatingActions_RequirePricingManagePermission(string actionName)
    {
        var method = typeof(PricingRulesController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();

        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();
        attribute.Should().NotBeNull(
            $"{actionName} mutates pricing configuration and must carry the pricing-manage gate");
        attribute!.PermissionName.Should().Be(ProductsPermission.Keys.PricingManage);
    }

    [Theory]
    [MemberData(nameof(PricingRuleCommands))]
    public void PricingRuleCommands_RequireMonetizePermission(string commandName)
    {
        var commandType = typeof(CreatePricingRuleCommand).Assembly
            .GetType($"GameGuild.Commerce.Products.{commandName}");
        commandType.Should().NotBeNull();

        var attribute = commandType!.GetCustomAttribute<AuthorizeRequestAttribute>();
        attribute.Should().NotBeNull(
            $"{commandName} must carry [AuthorizeRequest] so pricing changes are gated on every dispatch path");
        attribute!.Permission.Should().Be(MonetizationPermission.Keys.Monetize);
    }

    [Fact]
    public void Controller_IsAuthorized_Versioned_AndReadGated()
    {
        typeof(PricingRulesController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(PricingRulesController).GetCustomAttribute<ApiVersionAttribute>().Should().NotBeNull();

        var permission = typeof(PricingRulesController).GetCustomAttribute<RequirePermissionAttribute>();
        permission.Should().NotBeNull();
        permission!.PermissionName.Should().Be(ProductsPermission.Keys.Read);
    }
}

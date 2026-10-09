using System.Reflection;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests;

/// <summary>
///     Issue #346 (Monetize): setting product pricing is a revenue-generation action.
///     The command itself must be gated on the CQRS pipeline (covers every dispatch path,
///     including GraphQL), in addition to the existing controller-level pricing permission.
/// </summary>
public sealed class SetProductPricingMonetizationWiringTests
{
    [Fact]
    public void SetProductPricingCommand_RequiresMonetizePermission()
    {
        var attribute = typeof(SetProductPricingCommand).GetCustomAttribute<AuthorizeRequestAttribute>();
        attribute.Should().NotBeNull(
            "SetProductPricingCommand must carry [AuthorizeRequest] so pricing changes are gated on every dispatch path");
        attribute!.Permission.Should().Be(MonetizationPermission.Keys.Monetize);
    }

    [Fact]
    public void SetProductPricingControllerAction_KeepsItsControllerLevelPricingGate()
    {
        var method = typeof(ProductsController)
            .GetMethod(nameof(ProductsController.SetProductPricing), BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();

        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();
        attribute.Should().NotBeNull();
        attribute!.PermissionName.Should().Be(ProductsPermission.Keys.PricingManage);
    }
}

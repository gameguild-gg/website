using System.Reflection;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Xunit;

namespace GameGuild.Analytics.UnitTests;

/// <summary>
///     Issue #346 (ViewAnalytics): product revenue/subscription metrics are business-intelligence
///     data and must be gated on EVERY transport — REST controller actions and the CQRS queries
///     themselves (covers GraphQL and in-process dispatch).
/// </summary>
public sealed class MonetizationAnalyticsWiringTests
{
    [Fact]
    public void ProductMetricsQueries_RequireViewAnalyticsPermission_OnEveryDispatchPath()
    {
        AssertAuthorizeRequest(typeof(GetProductMetricsQuery), MonetizationPermission.Keys.ViewAnalytics);
        AssertAuthorizeRequest(typeof(ExportProductMetricsQuery), MonetizationPermission.Keys.ViewAnalytics);
    }

    [Fact]
    public void ProductMetricsController_Actions_RequireViewAnalyticsPermission()
    {
        var controller = typeof(ProductMetricsController);
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var attribute = action.GetCustomAttribute<RequirePermissionAttribute>();
            attribute.Should().NotBeNull(
                "product metrics action '{0}' must declare a permission requirement", action.Name);
            attribute!.PermissionName.Should().Be(MonetizationPermission.Keys.ViewAnalytics);
        }
    }

    private static void AssertAuthorizeRequest(Type requestType, string expectedPermission)
    {
        var attribute = requestType.GetCustomAttribute<AuthorizeRequestAttribute>();
        attribute.Should().NotBeNull(
            "{0} must carry [AuthorizeRequest] so non-HTTP dispatch paths are gated", requestType.Name);
        attribute!.Permission.Should().Be(expectedPermission);
    }
}

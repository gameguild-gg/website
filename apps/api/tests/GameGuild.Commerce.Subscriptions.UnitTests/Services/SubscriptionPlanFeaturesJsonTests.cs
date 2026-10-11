using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

/// <summary>
///     Issue #416: "Features and limits stored as JSON with schema validation".
/// </summary>
public class SubscriptionPlanFeaturesJsonTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_AcceptsEmptyPayloads(string? features)
    {
        SubscriptionPlanFeaturesJson.IsValid(features, out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("""{"customDomain":true,"apiAccess":true}""")]
    [InlineData("""{"basicFeatures":false}""")]
    [InlineData("""{"maxUsers":25,"storageGB":500,"apiRequestsPerDay":100000}""")]
    [InlineData("""{"supportTier":"gold"}""")] // unknown keys stay allowed with scalar values
    [InlineData("""{"maxUsers":25,"customDomain":true,"note":"anything"}""")]
    [InlineData("""{}""")]
    public void IsValid_AcceptsFlatObjectsWithScalarLeaves(string features)
    {
        SubscriptionPlanFeaturesJson.IsValid(features, out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"unclosed": """)]
    [InlineData("""[1,2,3]""")] // root must be an object
    [InlineData("""42""")]
    [InlineData("""{"nested":{"a":1}}""")] // nested objects rejected
    [InlineData("""{"list":[1,2]}""")] // arrays rejected
    [InlineData("""{"":"value"}""")] // empty key rejected
    public void IsValid_RejectsMalformedOrNonFlatPayloads(string features)
    {
        SubscriptionPlanFeaturesJson.IsValid(features, out var error).Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IsValid_TypeChecksWellKnownKeys()
    {
        // "customDomain" is a boolean feature key — a number must be rejected.
        SubscriptionPlanFeaturesJson.IsValid("""{"customDomain":5}""", out var error).Should().BeFalse();
        error.Should().Contain("customDomain").And.Contain("boolean");

        // "maxUsers" is a numeric entitlement key — a string must be rejected.
        SubscriptionPlanFeaturesJson.IsValid("""{"maxUsers":"many"}""", out error).Should().BeFalse();
        error.Should().Contain("maxUsers").And.Contain("number");
    }

    [Fact]
    public void IsValid_RejectsPayloadsOverTheColumnWidth()
    {
        var oversized = "{\"pad\":\"" + new string('x', 2100) + "\"}";

        SubscriptionPlanFeaturesJson.IsValid(oversized, out var error).Should().BeFalse();
        error.Should().Contain("2000");
    }

    [Fact]
    public void IsValid_RejectsExcessiveKeyCounts()
    {
        var keys = string.Join(",", Enumerable.Range(0, 51).Select(i => $"\"k{i}\":1"));
        var payload = "{" + keys + "}";

        SubscriptionPlanFeaturesJson.IsValid(payload, out var error).Should().BeFalse();
        error.Should().Contain("50");
    }
}

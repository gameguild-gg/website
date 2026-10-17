using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Validators;

/// <summary>
///     Issue #416: the pricing PATCH validator previously only checked the id — negative
///     prices were accepted on this path.
/// </summary>
public class UpdateSubscriptionPlanPricingCommandValidatorTests
{
    private readonly UpdateSubscriptionPlanPricingCommandValidator _validator = new();

    [Fact]
    public void Validate_Passes_ForNonNegativePrices()
    {
        var result = _validator.Validate(new UpdateSubscriptionPlanPricingCommand(Guid.NewGuid(), 0, 0));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenMonthlyPriceIsNegative()
    {
        // -2 is below the enterprise custom-quote sentinel (-1) and must be rejected;
        // the sentinel itself is valid and tested separately below.
        var result = _validator.Validate(new UpdateSubscriptionPlanPricingCommand(Guid.NewGuid(), -2, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateSubscriptionPlanPricingCommand.MonthlyPriceInCents) &&
            e.ErrorMessage.Contains("custom-quote sentinel"));
    }

    [Fact]
    public void Validate_Passes_ForEnterpriseSentinelPrice()
    {
        var result = _validator.Validate(new UpdateSubscriptionPlanPricingCommand(Guid.NewGuid(), -1, null));

        result.IsValid.Should().BeTrue("the -1 custom-quote sentinel is a valid enterprise plan price");
    }

    [Fact]
    public void Validate_Fails_WhenAnnualPriceIsNegative()
    {
        var result = _validator.Validate(new UpdateSubscriptionPlanPricingCommand(Guid.NewGuid(), 999, -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateSubscriptionPlanPricingCommand.AnnualPriceInCents) &&
            e.ErrorMessage.Contains("greater than or equal to 0"));
    }

    [Fact]
    public void Validate_Fails_WhenIdIsEmpty()
    {
        var result = _validator.Validate(new UpdateSubscriptionPlanPricingCommand(Guid.Empty, 999, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSubscriptionPlanPricingCommand.Id));
    }
}

/// <summary>
///     Issue #416: limits columns must stay non-negative on the limits PATCH path.
/// </summary>
public class UpdateSubscriptionPlanLimitsCommandValidatorTests
{
    private readonly UpdateSubscriptionPlanLimitsCommandValidator _validator = new();

    [Fact]
    public void Validate_Passes_ForNonNegativeLimits()
    {
        var result = _validator.Validate(new UpdateSubscriptionPlanLimitsCommand(Guid.NewGuid(), 0, 0, 0));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_ForNegativeLimits()
    {
        var result = _validator.Validate(new UpdateSubscriptionPlanLimitsCommand(Guid.NewGuid(), -1, -2, -3));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSubscriptionPlanLimitsCommand.MaxUsers));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSubscriptionPlanLimitsCommand.MaxStorageMb));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSubscriptionPlanLimitsCommand.MaxApiCallsPerMonth));
    }
}

/// <summary>
///     Issue #416: the features PATCH payload must satisfy the features JSON schema.
/// </summary>
public class UpdateSubscriptionPlanFeaturesCommandValidatorTests
{
    private readonly UpdateSubscriptionPlanFeaturesCommandValidator _validator = new();

    [Fact]
    public void Validate_Passes_ForAFlatScalarPayload()
    {
        var result = _validator.Validate(new UpdateSubscriptionPlanFeaturesCommand(
            Guid.NewGuid(), true, false, null, """{"customDomain":true,"maxUsers":10}"""));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Passes_WhenFeaturesIsOmitted()
    {
        var result = _validator.Validate(new UpdateSubscriptionPlanFeaturesCommand(Guid.NewGuid(), null, null, null, null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("""{"nested":{"a":1}}""")]
    [InlineData("""{"customDomain":42}""")]
    public void Validate_Fails_ForInvalidPayloads(string features)
    {
        var result = _validator.Validate(new UpdateSubscriptionPlanFeaturesCommand(
            Guid.NewGuid(), null, null, null, features));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateSubscriptionPlanFeaturesCommand.Features));
    }
}

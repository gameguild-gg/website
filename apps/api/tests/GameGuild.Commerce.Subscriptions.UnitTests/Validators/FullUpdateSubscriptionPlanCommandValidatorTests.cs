using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Validators;

/// <summary>
///     Issue #416: the full-update path previously had no validator at all.
/// </summary>
public class FullUpdateSubscriptionPlanCommandValidatorTests
{
    private readonly FullUpdateSubscriptionPlanCommandValidator _validator;

    public FullUpdateSubscriptionPlanCommandValidatorTests()
    {
        _validator = new FullUpdateSubscriptionPlanCommandValidator(new SubscriptionPlanSlugGenerator());
    }

    private static FullUpdateSubscriptionPlanCommand ValidCommand(
        string? slug = "pro",
        long? annualPriceInCents = 49990,
        int? maxUsers = 10,
        string? features = null,
        int? trialPeriodDays = null) => new(
        PlanId: Guid.NewGuid(),
        Name: "Professional",
        Slug: slug,
        Description: null,
        MonthlyPriceInCents: 4999,
        AnnualPriceInCents: annualPriceInCents,
        MaxUsers: maxUsers,
        MaxStorageMb: null,
        MaxApiCallsPerMonth: null,
        HasPrioritySupport: false,
        HasAdvancedAnalytics: false,
        HasCustomBranding: false,
        Features: features,
        SortOrder: null,
        TrialPeriodDays: trialPeriodDays);

    [Fact]
    public void Validate_Passes_ForAValidCommand()
    {
        _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Passes_WithGeneratedSlugAndNoOptionalValues()
    {
        _validator.Validate(ValidCommand(slug: null, annualPriceInCents: null, maxUsers: null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenNameIsTooShort()
    {
        var result = _validator.Validate(ValidCommand() with { Name = "ab" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(FullUpdateSubscriptionPlanCommand.Name) &&
            e.ErrorMessage.Contains("between 3 and 100"));
    }

    [Fact]
    public void Validate_Fails_WhenExplicitSlugIsMalformed()
    {
        var result = _validator.Validate(ValidCommand(slug: "Not A Slug"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(FullUpdateSubscriptionPlanCommand.Slug) &&
            e.ErrorMessage.Contains("lowercase letters, numbers, and hyphens"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-9999)]
    public void Validate_Fails_WhenMonthlyPriceIsNegative(long monthlyPriceInCents)
    {
        var result = _validator.Validate(ValidCommand() with { MonthlyPriceInCents = monthlyPriceInCents });

        result.IsValid.Should().BeFalse();
        // The sentinel rule is a command-level Must() — the error carries the sentinel message,
        // not a per-property name. Assert on the message text matching the pricing invariants.
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("custom-quote sentinel") ||
            e.ErrorMessage.Contains("non-negative"));
    }

    [Fact]
    public void Validate_Fails_WhenAnnualPriceIsNegative()
    {
        var result = _validator.Validate(ValidCommand(annualPriceInCents: -1));

        result.IsValid.Should().BeFalse();
        // The sentinel rule is a command-level Must() — assert on the invariant message.
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("custom-quote sentinel"));
    }

    [Fact]
    public void Validate_Fails_WhenLimitsAreNegative()
    {
        var result = _validator.Validate(ValidCommand(maxUsers: -5) with { MaxStorageMb = -10, MaxApiCallsPerMonth = -15 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(FullUpdateSubscriptionPlanCommand.MaxUsers));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(FullUpdateSubscriptionPlanCommand.MaxStorageMb));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(FullUpdateSubscriptionPlanCommand.MaxApiCallsPerMonth));
    }

    [Fact]
    public void Validate_Fails_WhenFeaturesIsNotAFlatJsonObject()
    {
        var result = _validator.Validate(ValidCommand(features: """{"nested":{"a":1}}"""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(FullUpdateSubscriptionPlanCommand.Features) &&
            e.ErrorMessage.Contains("scalar"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(91)]
    public void Validate_Fails_WhenTrialPeriodDaysIsOutsideZeroToNinety(int trialPeriodDays)
    {
        var result = _validator.Validate(ValidCommand(trialPeriodDays: trialPeriodDays));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(FullUpdateSubscriptionPlanCommand.TrialPeriodDays) &&
            e.ErrorMessage.Contains("between 0 and 90"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    public void Validate_Passes_WhenTrialPeriodDaysIsWithinZeroToNinety(int trialPeriodDays)
    {
        _validator.Validate(ValidCommand(trialPeriodDays: trialPeriodDays)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenSlugOmittedAndNameCannotProduceOne()
    {
        var result = _validator.Validate(ValidCommand(slug: null) with { Name = "---" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("slug can be generated"));
    }
}

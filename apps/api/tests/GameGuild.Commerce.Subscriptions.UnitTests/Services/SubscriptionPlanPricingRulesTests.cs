using FluentAssertions;
using GameGuild.Commerce.Subscriptions;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

/// <summary>
///     Plan-type pricing contracts (issue #416): free plans are priced at zero, the -1
///     custom-quote sentinel is enterprise-only, the annual discount is bounded, and the
///     annual derivation applies the discount with stable rounding.
/// </summary>
public class SubscriptionPlanPricingRulesTests
{
    [Fact]
    public void FreePlansMustBePricedAtExactlyZero()
    {
        var act = () => SubscriptionPlanPricingRules.Validate(SubscriptionPlanType.Free, 1, null);

        act.Should().Throw<ArgumentException>().WithMessage("*Free plans must have a monthly price of exactly zero*");
    }

    [Fact]
    public void FreePlansRejectNonZeroAnnualPrices()
    {
        var act = () => SubscriptionPlanPricingRules.Validate(SubscriptionPlanType.Free, 0, 1);

        act.Should().Throw<ArgumentException>().WithMessage("*Free plans must have an annual price of exactly zero*");
    }

    [Fact]
    public void EnterprisePlansMayCarryTheCustomQuoteSentinel()
    {
        var act = () => SubscriptionPlanPricingRules.Validate(SubscriptionPlanType.Enterprise,
            SubscriptionPlanPricingRules.CustomQuotePriceInCents, null);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(-100)]
    public void EnterprisePlansRejectOtherNegativePrices(long monthlyCents)
    {
        var act = () => SubscriptionPlanPricingRules.Validate(SubscriptionPlanType.Enterprise, monthlyCents, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PaidPlansRejectTheCustomQuoteSentinel()
    {
        var act = () => SubscriptionPlanPricingRules.Validate(SubscriptionPlanType.Paid,
            SubscriptionPlanPricingRules.CustomQuotePriceInCents, null);

        act.Should().Throw<ArgumentException>().WithMessage("*only enterprise plans may use the -1 custom-quote sentinel*");
    }

    [Fact]
    public void PaidPlansAcceptConcreteNonNegativePrices()
    {
        var act = () => SubscriptionPlanPricingRules.Validate(SubscriptionPlanType.Paid, 1_999, 19_990);

        act.Should().NotThrow();
    }

    [Fact]
    public void AnnualPricesMustBeNonNegativeOnEveryType()
    {
        var act = () => SubscriptionPlanPricingRules.Validate(SubscriptionPlanType.Paid, 1_999, -1);

        act.Should().Throw<ArgumentException>().WithMessage("*Annual prices must be non-negative*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void AnnualDiscountPercentIsBounded(double percent)
    {
        var act = () => SubscriptionPlanPricingRules.ValidateAnnualDiscountPercent((decimal) percent);

        act.Should().Throw<ArgumentException>().WithMessage("*between 0 and 100*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void AnnualDiscountPercentAcceptsItsBounds(double percent)
    {
        var act = () => SubscriptionPlanPricingRules.ValidateAnnualDiscountPercent((decimal) percent);

        act.Should().NotThrow();
    }

    [Fact]
    public void RequiresCustomQuoteIsTrueOnlyForEnterpriseSentinelPlans()
    {
        SubscriptionPlanPricingRules.RequiresCustomQuote(SubscriptionPlanType.Enterprise,
            SubscriptionPlanPricingRules.CustomQuotePriceInCents).Should().BeTrue();
        SubscriptionPlanPricingRules.RequiresCustomQuote(SubscriptionPlanType.Enterprise, 99_999).Should().BeFalse();
        SubscriptionPlanPricingRules.RequiresCustomQuote(SubscriptionPlanType.Paid,
            SubscriptionPlanPricingRules.CustomQuotePriceInCents).Should().BeFalse();
        SubscriptionPlanPricingRules.RequiresCustomQuote(SubscriptionPlanType.Free, 0).Should().BeFalse();
    }

    [Theory]
    [InlineData(1_000, null, 0, 12_000)]      // default discount: monthly x 12 unchanged
    [InlineData(1_000, null, 20, 9_600)]      // 20% annual discount
    [InlineData(1_000, 11_000, 20, 11_000)]   // explicit annual price always wins
    [InlineData(0, null, 50, 0)]              // zero stays zero
    [InlineData(-1, null, 50, -1)]            // the enterprise sentinel propagates unchanged
    public void DeriveAnnualPriceAppliesDiscountWithStablePrecedence(
        long monthlyCents, long? annualCents, decimal discountPercent, long expectedCents)
    {
        SubscriptionPlanPricingRules.DeriveAnnualPriceInCents(monthlyCents, annualCents, discountPercent)
            .Should().Be(expectedCents);
    }

    [Fact]
    public void ReclassifyEnforcesTheTargetTypeInvariants()
    {
        var plan = new SubscriptionPlan("Pro", "pro", 1_999, "USD");

        // A priced plan cannot be reclassified to free without zeroing its prices first...
        var toFree = () => plan.Reclassify(SubscriptionPlanType.Free);
        toFree.Should().Throw<ArgumentException>().WithMessage("*Free plans must have a monthly price of exactly zero*");
        plan.Type.Should().Be(SubscriptionPlanType.Paid);

        // ...while a concrete price remains valid for the enterprise type.
        var toEnterprise = () => plan.Reclassify(SubscriptionPlanType.Enterprise);
        toEnterprise.Should().NotThrow();
        plan.Type.Should().Be(SubscriptionPlanType.Enterprise);
    }

    [Fact]
    public void UpdatePricingValidatesTypeRulesAndDiscount()
    {
        var plan = new SubscriptionPlan("Pro", "pro", 1_999, "USD");

        plan.UpdatePricing(2_499, 24_990, 15m);

        plan.MonthlyPriceInCents.Should().Be(2_499);
        plan.AnnualPriceInCents.Should().Be(24_990);
        plan.AnnualDiscountPercent.Should().Be(15m);

        var invalid = () => plan.UpdatePricing(-1, null, null);
        invalid.Should().Throw<ArgumentException>().WithMessage("*custom-quote sentinel*");
        var invalidDiscount = () => plan.UpdatePricing(2_499, null, 150m);
        invalidDiscount.Should().Throw<ArgumentException>().WithMessage("*between 0 and 100*");
    }

    [Fact]
    public void UpdatePricingKeepsTheStoredDiscountWhenOmitted()
    {
        var plan = new SubscriptionPlan("Pro", "pro", 1_999, "USD") { AnnualDiscountPercent = 25m };

        plan.UpdatePricing(2_999, null);

        plan.AnnualDiscountPercent.Should().Be(25m);
    }
}

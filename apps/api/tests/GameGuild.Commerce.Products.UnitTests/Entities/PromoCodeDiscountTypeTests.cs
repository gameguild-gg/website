using FluentAssertions;
using GameGuild.Commerce.Products;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Entities;

/// <summary>
/// Coverage for all five discount types in <see cref="PromoCode.CalculateDiscount"/>,
/// including the <c>MaxDiscountAmount</c> cap for percentage discounts.
/// </summary>
public class PromoCodeDiscountTypeTests
{
    private static PromoCode CreateCode(
        PromoCodeType type,
        Action<PromoCode>? configure = null)
    {
        var promo = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "TEST-" + type.ToString().ToUpperInvariant(),
            Name = type.ToString(),
            Type = type,
            Currency = "USD",
            IsActive = true
        };
        configure?.Invoke(promo);
        return promo;
    }

    [Fact]
    public void PercentageOff_ComputesPercentageOfAmount()
    {
        var promo = CreateCode(PromoCodeType.PercentageOff, p => p.DiscountPercentage = 20m);

        promo.CalculateDiscount(100m).Should().Be(20m);
    }

    [Fact]
    public void PercentageOff_CappedByMaxDiscountAmount()
    {
        var promo = CreateCode(PromoCodeType.PercentageOff, p =>
        {
            p.DiscountPercentage = 50m;
            p.MaxDiscountAmount = 30m;
        });

        // 50% of 100 = 50, capped at 30
        promo.CalculateDiscount(100m).Should().Be(30m);
    }

    [Fact]
    public void PercentageOff_CapNeverExceedsOrderAmount()
    {
        var promo = CreateCode(PromoCodeType.PercentageOff, p =>
        {
            p.DiscountPercentage = 90m;
            p.MaxDiscountAmount = 500m;
        });

        promo.CalculateDiscount(80m).Should().Be(72m);
    }

    [Fact]
    public void PercentageOff_WithoutCap_ReturnsFullPercentage()
    {
        var promo = CreateCode(PromoCodeType.PercentageOff, p => p.DiscountPercentage = 100m);

        promo.CalculateDiscount(250m).Should().Be(250m);
    }

    [Fact]
    public void FixedAmountOff_NeverExceedsOrderAmount()
    {
        var promo = CreateCode(PromoCodeType.FixedAmountOff, p => p.DiscountAmount = 50m);

        promo.CalculateDiscount(30m).Should().Be(30m);
        promo.CalculateDiscount(100m).Should().Be(50m);
    }

    [Fact]
    public void FreeTrialExtension_WaivesCurrentCharge_WhenDaysConfigured()
    {
        var promo = CreateCode(PromoCodeType.FreeTrialExtension, p => p.FreeTrialExtensionDays = 14);

        var discount = promo.CalculateDiscount(100m);
        discount.Should().Be(100m, "the current charge is deferred by the granted trial days");
    }

    [Fact]
    public void FreeTrialExtension_WithNoDays_AppliesNoDiscount()
    {
        var promo = CreateCode(PromoCodeType.FreeTrialExtension);

        promo.CalculateDiscount(100m).Should().Be(0m);
    }

    [Fact]
    public void FreeMonths_WaivesCurrentCharge_WhenMonthsConfigured()
    {
        var promo = CreateCode(PromoCodeType.FreeMonths, p => p.DurationMonths = 2);

        promo.CalculateDiscount(100m).Should().Be(100m, "this billing period is skipped");
    }

    [Fact]
    public void FreeMonths_WithNoMonths_AppliesNoDiscount()
    {
        var promo = CreateCode(PromoCodeType.FreeMonths);

        promo.CalculateDiscount(100m).Should().Be(0m);
    }

    [Fact]
    public void SetPrice_DiscountsDifferenceToOverridePrice()
    {
        var promo = CreateCode(PromoCodeType.SetPrice, p => p.DiscountAmount = 60m);

        promo.CalculateDiscount(100m).Should().Be(40m);
    }

    [Fact]
    public void SetPrice_NeverProducesNegativeDiscount_WhenPriceAboveAmount()
    {
        var promo = CreateCode(PromoCodeType.SetPrice, p => p.DiscountAmount = 150m);

        promo.CalculateDiscount(100m).Should().Be(0m);
    }

    [Fact]
    public void MinimumOrderAmount_BelowMinimum_AppliesNoDiscount()
    {
        var promo = CreateCode(PromoCodeType.PercentageOff, p =>
        {
            p.DiscountPercentage = 10m;
            p.MinimumOrderAmount = 50m;
        });

        promo.CalculateDiscount(49.99m).Should().Be(0m);
        promo.CalculateDiscount(50m).Should().Be(5m);
    }

    [Fact]
    public void InactiveCode_AppliesNoDiscount_RegardlessOfType()
    {
        var promo = CreateCode(PromoCodeType.SetPrice, p =>
        {
            p.DiscountAmount = 60m;
            p.IsActive = false;
        });

        promo.CalculateDiscount(100m).Should().Be(0m);
    }

    [Fact]
    public void ExpiredCode_AppliesNoDiscount()
    {
        var promo = CreateCode(PromoCodeType.FixedAmountOff, p =>
        {
            p.DiscountAmount = 10m;
            p.ValidFrom = SystemClock.UtcNow.AddDays(-10);
            p.ValidUntil = SystemClock.UtcNow.AddDays(-1);
        });

        promo.CalculateDiscount(100m).Should().Be(0m);
    }

    [Theory]
    [InlineData(PromoCodeType.PercentageOff)]
    [InlineData(PromoCodeType.FixedAmountOff)]
    [InlineData(PromoCodeType.FreeTrialExtension)]
    [InlineData(PromoCodeType.FreeMonths)]
    [InlineData(PromoCodeType.SetPrice)]
    public void AllFiveSpecTypes_ProduceNonZeroDiscount_WhenConfigured(PromoCodeType type)
    {
        var promo = CreateCode(type, p =>
        {
            p.DiscountPercentage = 25m;
            p.DiscountAmount = type is PromoCodeType.FixedAmountOff or PromoCodeType.SetPrice ? 60m : null;
            p.MaxDiscountAmount = null;
            p.FreeTrialExtensionDays = type == PromoCodeType.FreeTrialExtension ? 7 : null;
            p.DurationMonths = type == PromoCodeType.FreeMonths ? 1 : null;
        });

        promo.CalculateDiscount(100m).Should().BePositive($"type {type} must produce a monetary outcome");
    }
}

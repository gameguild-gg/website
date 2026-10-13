using FluentAssertions;
using GameGuild.Commerce.Products;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests.Entities;

/// <summary>
/// Unit tests for PromoCode entity
/// </summary>
public class PromoCodeEntityTests
{
    #region Constructor Tests

    [Fact]
    public void DefaultConstructor_ShouldSetDefaultValues()
    {
        // Arrange & Act
        var promoCode = new PromoCode();

        // Assert
        promoCode.Code.Should().BeEmpty();
        promoCode.Name.Should().BeEmpty();
        promoCode.Description.Should().BeNull();
        promoCode.Type.Should().Be(PromoCodeType.PercentageOff);
        promoCode.Currency.Should().Be("USD");
        promoCode.IsActive.Should().BeTrue();
        promoCode.IsExclusive.Should().BeFalse();
        promoCode.StackingPriority.Should().Be(0);
        promoCode.PromoCodeUses.Should().NotBeNull().And.BeEmpty();
    }

    #endregion

    #region IsCurrentlyValid Tests

    [Fact]
    public void IsCurrentlyValid_WhenActiveAndNoDateConstraints_ShouldReturnTrue()
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = true,
            ValidFrom = null,
            ValidUntil = null
        };

        // Act
        var result = promoCode.IsCurrentlyValid();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCurrentlyValid_WhenInactive_ShouldReturnFalse()
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = false,
            ValidFrom = null,
            ValidUntil = null
        };

        // Act
        var result = promoCode.IsCurrentlyValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyValid_WhenBeforeValidFrom_ShouldReturnFalse()
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = true,
            ValidFrom = DateTime.UtcNow.AddDays(1),
            ValidUntil = null
        };

        // Act
        var result = promoCode.IsCurrentlyValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyValid_WhenAfterValidUntil_ShouldReturnFalse()
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = true,
            ValidFrom = null,
            ValidUntil = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var result = promoCode.IsCurrentlyValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyValid_WhenWithinValidPeriod_ShouldReturnTrue()
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = true,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidUntil = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = promoCode.IsCurrentlyValid();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region CalculateDiscount Tests

    [Fact]
    public void CalculateDiscount_WhenInvalid_ShouldReturnZero()
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = false,
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 10
        };

        // Act
        var result = promoCode.CalculateDiscount(100m);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateDiscount_WhenBelowMinimumOrderAmount_ShouldReturnZero()
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = true,
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 10,
            MinimumOrderAmount = 100m
        };

        // Act
        var result = promoCode.CalculateDiscount(50m);

        // Assert
        result.Should().Be(0);
    }

    [Theory]
    [InlineData(100, 10, 10)]    // 10% of 100 = 10
    [InlineData(200, 25, 50)]    // 25% of 200 = 50
    [InlineData(50, 50, 25)]     // 50% of 50 = 25
    [InlineData(1000, 15, 150)]  // 15% of 1000 = 150
    public void CalculateDiscount_ForPercentageOff_ShouldCalculateCorrectly(
        decimal orderAmount, decimal percentage, decimal expectedDiscount)
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = true,
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = percentage
        };

        // Act
        var result = promoCode.CalculateDiscount(orderAmount);

        // Assert
        result.Should().Be(expectedDiscount);
    }

    [Theory]
    [InlineData(100, 20, 20)]    // Fixed $20 off $100 order
    [InlineData(200, 50, 50)]    // Fixed $50 off $200 order
    [InlineData(30, 50, 30)]     // Fixed $50 off but order is only $30, cap at order amount
    public void CalculateDiscount_ForFixedAmountOff_ShouldCalculateCorrectly(
        decimal orderAmount, decimal fixedAmount, decimal expectedDiscount)
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = true,
            Type = PromoCodeType.FixedAmountOff,
            DiscountAmount = fixedAmount
        };

        // Act
        var result = promoCode.CalculateDiscount(orderAmount);

        // Assert
        result.Should().Be(expectedDiscount);
    }

    [Fact]
    public void CalculateDiscount_WhenPercentageNotSet_ShouldReturnZero()
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = true,
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = null
        };

        // Act
        var result = promoCode.CalculateDiscount(100m);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateDiscount_WhenFixedAmountNotSet_ShouldReturnZero()
    {
        // Arrange
        var promoCode = new PromoCode
        {
            IsActive = true,
            Type = PromoCodeType.FixedAmountOff,
            DiscountAmount = null
        };

        // Act
        var result = promoCode.CalculateDiscount(100m);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region GetIsExclusive Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetIsExclusive_ShouldReturnIsExclusiveValue(bool isExclusive)
    {
        // Arrange
        var promoCode = new PromoCode { IsExclusive = isExclusive };

        // Act
        var result = promoCode.GetIsExclusive();

        // Assert
        result.Should().Be(isExclusive);
    }

    #endregion

    #region Property Tests

    [Theory]
    [InlineData("SUMMER20")]
    [InlineData("WELCOME10")]
    [InlineData("BLACKFRIDAY2024")]
    public void Code_ShouldAcceptValidCodes(string code)
    {
        // Arrange
        var promoCode = new PromoCode();

        // Act
        promoCode.Code = code;

        // Assert
        promoCode.Code.Should().Be(code);
    }

    [Theory]
    [InlineData(PromoCodeType.PercentageOff)]
    [InlineData(PromoCodeType.FixedAmountOff)]
    public void Type_ShouldAcceptAllPromoCodeTypes(PromoCodeType type)
    {
        // Arrange
        var promoCode = new PromoCode();

        // Act
        promoCode.Type = type;

        // Assert
        promoCode.Type.Should().Be(type);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void MaxUses_ShouldAcceptValidValues(int maxUses)
    {
        // Arrange
        var promoCode = new PromoCode();

        // Act
        promoCode.MaxUses = maxUses;

        // Assert
        promoCode.MaxUses.Should().Be(maxUses);
    }

    [Fact]
    public void ProductId_WhenSet_ShouldRetainValue()
    {
        // Arrange
        var promoCode = new PromoCode();
        var productId = Guid.NewGuid();

        // Act
        promoCode.ProductId = productId;

        // Assert
        promoCode.ProductId.Should().Be(productId);
    }

    #endregion
}

/// <summary>
/// Unit tests for PromoCodeType enum
/// </summary>
public class PromoCodeTypeEnumTests
{
    [Theory]
    [InlineData(PromoCodeType.PercentageOff, 0)]
    [InlineData(PromoCodeType.FixedAmountOff, 1)]
    [InlineData(PromoCodeType.FreeTrial, 2)]
    [InlineData(PromoCodeType.BuyOneGetOne, 3)]
    [InlineData(PromoCodeType.FreeShipping, 4)]
    public void PromoCodeType_ShouldHaveExpectedIntValues(PromoCodeType type, int expectedValue)
    {
        // Assert
        ((int)type).Should().Be(expectedValue);
    }

    [Fact]
    public void PromoCodeType_ShouldHaveCorrectCount()
    {
        // Five legacy types plus FreeTrialExtension, FreeMonths and SetPrice (issue #418).
        Enum.GetValues<PromoCodeType>().Should().HaveCount(8);
    }
}

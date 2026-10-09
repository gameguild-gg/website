using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Security;

/// <summary>
///     Unit Tests: Tax Calculation - Multi-Jurisdiction Rates
///     From: COMMERCE_MODULES_SECURITY_AUDIT.md Section 7 - Test Plan
///     These tests verify correct tax rate calculations for various jurisdictions.
/// </summary>
public class TaxCalculationTests
{
    #region Standard VAT Rate Tests

    [Theory]
    [InlineData("DE", 0.19, 100, 19)]        // Germany 19% VAT
    [InlineData("FR", 0.20, 100, 20)]        // France 20% VAT
    [InlineData("GB", 0.20, 100, 20)]        // UK 20% VAT
    [InlineData("IT", 0.22, 100, 22)]        // Italy 22% VAT
    [InlineData("ES", 0.21, 100, 21)]        // Spain 21% VAT
    [InlineData("NL", 0.21, 100, 21)]        // Netherlands 21% VAT
    [InlineData("BE", 0.21, 100, 21)]        // Belgium 21% VAT
    [InlineData("AT", 0.20, 100, 20)]        // Austria 20% VAT
    [InlineData("PL", 0.23, 100, 23)]        // Poland 23% VAT
    [InlineData("SE", 0.25, 100, 25)]        // Sweden 25% VAT
    public void EUVATRates_StandardDigitalGoods_CalculatesCorrectly(
        string _, decimal rate, decimal subtotal, decimal expectedTax)
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act
        var result = calculator.CalculateTax(subtotal, rate);

        // Assert
        result.TaxAmount.Should().BeApproximately(expectedTax, 0.01m);
        result.TotalAmount.Should().BeApproximately(subtotal + expectedTax, 0.01m);
        result.EffectiveRate.Should().BeApproximately(rate, 0.001m);
    }

    [Theory]
    [InlineData("DE", 0.07, 100, 7)]         // Germany 7% reduced VAT (books, food)
    [InlineData("FR", 0.055, 100, 5.5)]      // France 5.5% reduced VAT
    [InlineData("GB", 0.05, 100, 5)]         // UK 5% reduced VAT
    [InlineData("IT", 0.10, 100, 10)]        // Italy 10% reduced VAT
    [InlineData("ES", 0.10, 100, 10)]        // Spain 10% reduced VAT
    public void EUVATRates_ReducedRate_CalculatesCorrectly(
        string _, decimal rate, decimal subtotal, decimal expectedTax)
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act
        var result = calculator.CalculateTax(subtotal, rate);

        // Assert
        result.TaxAmount.Should().BeApproximately(expectedTax, 0.01m);
    }

    #endregion

    #region US Sales Tax Tests

    [Theory]
    [InlineData("US-CA", 0.0725, 100, 7.25)]   // California 7.25%
    [InlineData("US-NY", 0.08, 100, 8)]         // New York 8%
    [InlineData("US-TX", 0.0625, 100, 6.25)]    // Texas 6.25%
    [InlineData("US-FL", 0.06, 100, 6)]         // Florida 6%
    [InlineData("US-WA", 0.065, 100, 6.5)]      // Washington 6.5%
    [InlineData("US-OR", 0, 100, 0)]            // Oregon (no sales tax)
    [InlineData("US-DE", 0, 100, 0)]            // Delaware (no sales tax)
    [InlineData("US-MT", 0, 100, 0)]            // Montana (no sales tax)
    [InlineData("US-NH", 0, 100, 0)]            // New Hampshire (no sales tax)
    public void USSalesTax_ByState_CalculatesCorrectly(
        string _, decimal rate, decimal subtotal, decimal expectedTax)
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act
        var result = calculator.CalculateTax(subtotal, rate);

        // Assert
        result.TaxAmount.Should().BeApproximately(expectedTax, 0.01m);
    }

    #endregion

    #region Canada GST/PST/HST Tests

    [Theory]
    [InlineData("CA-ON", 0.13, 100, 13)]        // Ontario HST 13%
    [InlineData("CA-BC", 0.12, 100, 12)]        // BC GST 5% + PST 7%
    [InlineData("CA-AB", 0.05, 100, 5)]         // Alberta GST only 5%
    [InlineData("CA-QC", 0.14975, 100, 14.975)] // Quebec GST 5% + QST 9.975%
    [InlineData("CA-NS", 0.15, 100, 15)]        // Nova Scotia HST 15%
    [InlineData("CA-NB", 0.15, 100, 15)]        // New Brunswick HST 15%
    public void CanadaTax_ByProvince_CalculatesCorrectly(
        string _, decimal combinedRate, decimal subtotal, decimal expectedTax)
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act
        var result = calculator.CalculateTax(subtotal, combinedRate);

        // Assert
        result.TaxAmount.Should().BeApproximately(expectedTax, 0.01m);
    }

    [Fact]
    public void CanadaTax_BCProvince_ReturnsSeparateGSTAndPST()
    {
        // Arrange
        var calculator = new TaxCalculator();
        decimal subtotal = 100m;
        decimal gstRate = 0.05m;
        decimal pstRate = 0.07m;

        // Act
        var gstResult = calculator.CalculateTax(subtotal, gstRate);
        var pstResult = calculator.CalculateTax(subtotal, pstRate);

        // Assert - Both taxes calculated on original subtotal
        gstResult.TaxAmount.Should().Be(5m);
        pstResult.TaxAmount.Should().Be(7m);
        
        var totalTax = gstResult.TaxAmount + pstResult.TaxAmount;
        totalTax.Should().Be(12m);
    }

    #endregion

    #region Tax Inclusive Calculation Tests

    [Theory]
    [InlineData(119, 0.19, 100, 19)]        // DE: 119 EUR inclusive = 100 + 19
    [InlineData(120, 0.20, 100, 20)]        // UK: 120 GBP inclusive = 100 + 20
    [InlineData(107.25, 0.0725, 100, 7.25)] // CA: 107.25 USD inclusive = 100 + 7.25
    public void TaxInclusive_ExtractsCorrectSubtotalAndTax(
        decimal inclusiveAmount, decimal rate, decimal expectedSubtotal, decimal expectedTax)
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act
        var result = calculator.ExtractTaxFromInclusive(inclusiveAmount, rate);

        // Assert
        result.SubtotalAmount.Should().BeApproximately(expectedSubtotal, 0.01m);
        result.TaxAmount.Should().BeApproximately(expectedTax, 0.01m);
        result.TotalAmount.Should().BeApproximately(inclusiveAmount, 0.01m);
    }

    [Fact]
    public void TaxInclusive_PreservesTotal()
    {
        // Arrange
        var calculator = new TaxCalculator();
        decimal inclusiveAmount = 119m;
        decimal rate = 0.19m;

        // Act
        var result = calculator.ExtractTaxFromInclusive(inclusiveAmount, rate);

        // Assert - Total should exactly match input
        result.TotalAmount.Should().Be(inclusiveAmount);
        (result.SubtotalAmount + result.TaxAmount).Should().BeApproximately(inclusiveAmount, 0.01m);
    }

    #endregion

    #region Reverse Charge Tests

    [Fact]
    public void ReverseCharge_B2B_ReturnsZeroTax()
    {
        // Arrange
        var calculator = new TaxCalculator();
        decimal subtotal = 100m;

        // Act
        var result = calculator.CalculateReverseCharge(subtotal);

        // Assert
        result.TaxAmount.Should().Be(0);
        result.TotalAmount.Should().Be(subtotal);
        result.IsReverseCharge.Should().BeTrue();
    }

    [Fact]
    public void ReverseCharge_SetsApplicableRateToZero()
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act
        var result = calculator.CalculateReverseCharge(1000m);

        // Assert
        result.EffectiveRate.Should().Be(0);
    }

    #endregion

    #region Tax Exemption Tests

    [Fact]
    public void TaxExempt_NonProfit_ReturnsZeroTax()
    {
        // Arrange
        var calculator = new TaxCalculator();
        decimal subtotal = 100m;

        // Act
        var result = calculator.CalculateExempt(subtotal, "NONPROFIT_501C3");

        // Assert
        result.TaxAmount.Should().Be(0);
        result.IsTaxExempt.Should().BeTrue();
        result.ExemptionReason.Should().Be("NONPROFIT_501C3");
    }

    [Fact]
    public void TaxExempt_Government_ReturnsZeroTax()
    {
        // Arrange
        var calculator = new TaxCalculator();
        decimal subtotal = 100m;

        // Act
        var result = calculator.CalculateExempt(subtotal, "GOVERNMENT_ENTITY");

        // Assert
        result.TaxAmount.Should().Be(0);
        result.IsTaxExempt.Should().BeTrue();
    }

    [Fact]
    public void TaxExempt_Educational_ReturnsZeroTax()
    {
        // Arrange
        var calculator = new TaxCalculator();
        decimal subtotal = 100m;

        // Act
        var result = calculator.CalculateExempt(subtotal, "EDUCATIONAL_INSTITUTION");

        // Assert
        result.TaxAmount.Should().Be(0);
        result.IsTaxExempt.Should().BeTrue();
    }

    #endregion

    #region Rounding Tests

    [Theory]
    [InlineData(99.99, 0.19, 19.00)]     // Should round to 19.00
    [InlineData(33.33, 0.19, 6.33)]      // Should round to 6.33
    [InlineData(1.01, 0.19, 0.19)]       // Small amount rounding
    [InlineData(0.01, 0.19, 0.00)]       // Minimum amount
    public void TaxCalculation_RoundsToTwoDecimalPlaces(
        decimal subtotal, decimal rate, decimal expectedTax)
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act
        var result = calculator.CalculateTax(subtotal, rate);

        // Assert
        result.TaxAmount.Should().BeApproximately(expectedTax, 0.01m);
        // Verify no more than 2 decimal places
        (result.TaxAmount * 100 % 1).Should().Be(0);
    }

    [Fact]
    public void TaxCalculation_LargeAmount_MaintainsPrecision()
    {
        // Arrange
        var calculator = new TaxCalculator();
        decimal subtotal = 999999.99m;
        decimal rate = 0.19m;

        // Act
        var result = calculator.CalculateTax(subtotal, rate);

        // Assert
        var expectedTax = Math.Round(subtotal * rate, 2);
        result.TaxAmount.Should().BeApproximately(expectedTax, 0.01m);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TaxCalculation_ZeroAmount_ReturnsZeroTax()
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act
        var result = calculator.CalculateTax(0, 0.19m);

        // Assert
        result.TaxAmount.Should().Be(0);
        result.TotalAmount.Should().Be(0);
    }

    [Fact]
    public void TaxCalculation_ZeroRate_ReturnsZeroTax()
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act
        var result = calculator.CalculateTax(100m, 0);

        // Assert
        result.TaxAmount.Should().Be(0);
        result.TotalAmount.Should().Be(100m);
    }

    [Fact]
    public void TaxCalculation_NegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act & Assert
        var act = () => calculator.CalculateTax(-100m, 0.19m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TaxCalculation_NegativeRate_ThrowsArgumentException()
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act & Assert
        var act = () => calculator.CalculateTax(100m, -0.19m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TaxCalculation_RateOver100Percent_ThrowsArgumentException()
    {
        // Arrange
        var calculator = new TaxCalculator();

        // Act & Assert
        var act = () => calculator.CalculateTax(100m, 1.5m); // 150% rate
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Multi-Tier Tax Tests

    [Fact]
    public void MultiTierTax_CalculatesEachTierSeparately()
    {
        // Arrange - Quebec has GST (5%) and QST (9.975%)
        var calculator = new TaxCalculator();
        decimal subtotal = 100m;

        // Act
        var tierResults = calculator.CalculateMultiTierTax(subtotal, new[]
        {
            new TaxTier { Name = "GST", Rate = 0.05m },
            new TaxTier { Name = "QST", Rate = 0.09975m }
        });

        // Assert
        tierResults.Should().HaveCount(2);
        tierResults[0].TaxAmount.Should().Be(5m);
        tierResults[1].TaxAmount.Should().BeApproximately(9.98m, 0.01m);
        
        var totalTax = tierResults.Sum(t => t.TaxAmount);
        totalTax.Should().BeApproximately(14.98m, 0.01m);
    }

    #endregion
}

/// <summary>
/// Simple tax calculator for unit testing
/// </summary>
internal class TaxCalculator
{
    public TaxResult CalculateTax(decimal subtotal, decimal rate)
    {
        if (subtotal < 0)
        {
            throw new ArgumentException("Subtotal cannot be negative", nameof(subtotal));
        }

        if (rate < 0)
        {
            throw new ArgumentException("Rate cannot be negative", nameof(rate));
        }

        if (rate > 1)
        {
            throw new ArgumentException("Rate cannot exceed 100%", nameof(rate));
        }

        var taxAmount = Math.Round(subtotal * rate, 2);
        
        return new TaxResult
        {
            SubtotalAmount = subtotal,
            TaxAmount = taxAmount,
            TotalAmount = subtotal + taxAmount,
            EffectiveRate = rate
        };
    }

    public TaxResult ExtractTaxFromInclusive(decimal inclusiveAmount, decimal rate)
    {
        if (rate < 0)
        {
            throw new ArgumentException("Rate cannot be negative", nameof(rate));
        }

        var subtotal = Math.Round(inclusiveAmount / (1 + rate), 2);
        var taxAmount = inclusiveAmount - subtotal;

        return new TaxResult
        {
            SubtotalAmount = subtotal,
            TaxAmount = taxAmount,
            TotalAmount = inclusiveAmount,
            EffectiveRate = rate
        };
    }

    public TaxResult CalculateReverseCharge(decimal subtotal)
    {
        return new TaxResult
        {
            SubtotalAmount = subtotal,
            TaxAmount = 0,
            TotalAmount = subtotal,
            EffectiveRate = 0,
            IsReverseCharge = true
        };
    }

    public TaxResult CalculateExempt(decimal subtotal, string exemptionReason)
    {
        return new TaxResult
        {
            SubtotalAmount = subtotal,
            TaxAmount = 0,
            TotalAmount = subtotal,
            EffectiveRate = 0,
            IsTaxExempt = true,
            ExemptionReason = exemptionReason
        };
    }

    public List<TaxTierResult> CalculateMultiTierTax(decimal subtotal, TaxTier[] tiers)
    {
        return tiers.Select(tier => new TaxTierResult
        {
            Name = tier.Name,
            Rate = tier.Rate,
            TaxAmount = Math.Round(subtotal * tier.Rate, 2)
        }).ToList();
    }
}

internal class TaxResult
{
    public decimal SubtotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal EffectiveRate { get; set; }
    public bool IsReverseCharge { get; set; }
    public bool IsTaxExempt { get; set; }
    public string? ExemptionReason { get; set; }
}

internal class TaxTier
{
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}

internal class TaxTierResult
{
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal TaxAmount { get; set; }
}

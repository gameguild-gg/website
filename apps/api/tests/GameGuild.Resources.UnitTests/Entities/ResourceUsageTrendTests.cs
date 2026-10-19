using System;
using FluentAssertions;
using Xunit;

namespace GameGuild.Resources.UnitTests.Entities;

public class ResourceUsageTrendTests
{
    [Fact]
    public void IsAnomaly_WithZeroStandardDeviation_ReturnsFalse()
    {
        var trend = new ResourceUsageTrend { AverageUsage = 100, StandardDeviation = 0 };

        trend.IsAnomaly(200).Should().BeFalse();
    }

    [Theory]
    [InlineData("positive-zero", 0L)]
    [InlineData("negative-zero", long.MinValue)]
    public void IsAnomaly_SignedZeroDeviationUsesTheDegenerateBaseline(string scenario, long zeroBits)
    {
        var deviation = BitConverter.Int64BitsToDouble(zeroBits);
        var trend = new ResourceUsageTrend { AverageUsage = 0, StandardDeviation = deviation };

        trend.IsAnomaly(1).Should().BeFalse($"{scenario} is a zero denominator");
    }

    [Theory]
    [InlineData("small-normal", 1e-12)]
    [InlineData("smallest-subnormal", double.Epsilon)]
    public void IsAnomaly_SmallNonzeroDeviationStillScoresTheObservation(string scenario, double deviation)
    {
        var trend = new ResourceUsageTrend { AverageUsage = 0, StandardDeviation = deviation };

        trend.IsAnomaly(1).Should().BeTrue($"{scenario} must not be treated as zero");
        trend.IsAnomaly(0).Should().BeFalse($"{scenario} must still distinguish a matching observation");
    }

    [Fact]
    public void IsAnomaly_WithNormalUsage_ReturnsFalse()
    {
        var trend = new ResourceUsageTrend { AverageUsage = 100, StandardDeviation = 10 };

        trend.IsAnomaly(105).Should().BeFalse();
    }

    [Fact]
    public void IsAnomaly_WithAnomalousUsage_ReturnsTrue()
    {
        var trend = new ResourceUsageTrend { AverageUsage = 100, StandardDeviation = 10 };

        trend.IsAnomaly(130).Should().BeTrue();
    }

    [Fact]
    public void ForecastNextPeriod_WithPositiveGrowth_ReturnsIncreasedForecast()
    {
        var trend = new ResourceUsageTrend { AverageUsage = 100, GrowthRate = 10 };

        trend.ForecastNextPeriod().Should().BeApproximately(110, 0.01);
    }

    [Fact]
    public void ForecastNextPeriod_WithNegativeGrowth_ReturnsDecreasedForecast()
    {
        var trend = new ResourceUsageTrend { AverageUsage = 100, GrowthRate = -10 };

        trend.ForecastNextPeriod().Should().BeApproximately(90, 0.01);
    }

    [Fact]
    public void GetTrendDirection_WithHighPositiveGrowth_ReturnsGrowing()
    {
        var trend = new ResourceUsageTrend { GrowthRate = 10 };

        trend.GetTrendDirection().Should().Be("Growing");
    }

    [Fact]
    public void GetTrendDirection_WithHighNegativeGrowth_ReturnsDeclining()
    {
        var trend = new ResourceUsageTrend { GrowthRate = -10 };

        trend.GetTrendDirection().Should().Be("Declining");
    }

    [Fact]
    public void GetTrendDirection_WithSmallGrowth_ReturnsSteady()
    {
        var trend = new ResourceUsageTrend { GrowthRate = 2 };

        trend.GetTrendDirection().Should().Be("Steady");
    }

    [Fact]
    public void IsVolatile_WithZeroAverageUsage_ReturnsFalse()
    {
        var trend = new ResourceUsageTrend { AverageUsage = 0, StandardDeviation = 10 };

        trend.IsVolatile().Should().BeFalse();
    }

    [Theory]
    [InlineData("positive-zero", 0L)]
    [InlineData("negative-zero", long.MinValue)]
    public void IsVolatile_SignedZeroAverageUsesTheDegenerateBaseline(string scenario, long zeroBits)
    {
        var average = BitConverter.Int64BitsToDouble(zeroBits);
        var trend = new ResourceUsageTrend { AverageUsage = average, StandardDeviation = 1 };

        trend.IsVolatile().Should().BeFalse($"{scenario} is a zero denominator");
    }

    [Theory]
    [InlineData("small-normal", 1e-12)]
    [InlineData("smallest-subnormal", double.Epsilon)]
    public void IsVolatile_SmallNonzeroAverageStillScoresTheVariation(string scenario, double average)
    {
        var trend = new ResourceUsageTrend { AverageUsage = average, StandardDeviation = average };

        trend.IsVolatile().Should().BeTrue($"{scenario} retains a coefficient of variation of one");
        trend.StandardDeviation = 0;
        trend.IsVolatile().Should().BeFalse($"{scenario} must still distinguish a constant observation");
    }

    [Fact]
    public void IsVolatile_WithHighVariation_ReturnsTrue()
    {
        var trend = new ResourceUsageTrend { AverageUsage = 100, StandardDeviation = 50 };

        trend.IsVolatile().Should().BeTrue();
    }

    [Fact]
    public void IsVolatile_WithLowVariation_ReturnsFalse()
    {
        var trend = new ResourceUsageTrend { AverageUsage = 100, StandardDeviation = 10 };

        trend.IsVolatile().Should().BeFalse();
    }

    [Fact]
    public void Type_AliasSetsResourceType()
    {
        var trend = new ResourceUsageTrend();

        trend.Type = ResourceUsageType.ApiCalls;

        trend.ResourceType.Should().Be(ResourceUsageType.ApiCalls);
    }
}

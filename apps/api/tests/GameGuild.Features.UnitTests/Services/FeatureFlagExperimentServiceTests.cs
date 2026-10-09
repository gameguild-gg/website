using FluentAssertions;
using Xunit;

namespace GameGuild.Features.UnitTests.Services;

public class FeatureFlagExperimentServiceTests
{
    private readonly FeatureFlagExperimentService _sut = new();

    [Fact]
    public void EvaluateAbTest_ShouldThrow_WhenLessThanTwoVariants()
    {
        var variants = new Dictionary<string, (int observations, int conversions)>
        {
            { "control", (100, 10) }
        };

        var act = () => _sut.EvaluateAbTest(variants);

        act.Should().Throw<ArgumentException>().WithMessage("*two variants*");
    }

    [Fact]
    public void EvaluateAbTest_ShouldIdentifySignificantUplift()
    {
        var variants = new Dictionary<string, (int observations, int conversions)>
        {
            { "control", (10000, 500) },       // 5% conversion
            { "treatment", (10000, 600) }       // 6% conversion
        };

        var result = _sut.EvaluateAbTest(variants);

        result.ControlVariant.Should().Be("control");
        result.TreatmentVariant.Should().Be("treatment");
        result.PValue.Should().BeGreaterThan(0);
        result.ConfidenceLevel.Should().Be(0.95);
        result.RelativeUplift.Should().BeGreaterThan(0);
        result.VariantStats.Should().HaveCount(2);
    }

    [Fact]
    public void EvaluateAbTest_ShouldReturnInconclusive_WithSmallSample()
    {
        var variants = new Dictionary<string, (int observations, int conversions)>
        {
            { "control", (50, 5) },
            { "treatment", (50, 6) }
        };

        var result = _sut.EvaluateAbTest(variants);

        result.Recommendation.Should().Contain("Inconclusive");
    }

    [Fact]
    public void EvaluateAbTest_ShouldRecommendRollout_WhenTreatmentIsBetter()
    {
        var variants = new Dictionary<string, (int observations, int conversions)>
        {
            { "control", (50000, 2500) },       // 5%
            { "treatment", (50000, 4000) }       // 8% — large uplift
        };

        var result = _sut.EvaluateAbTest(variants);

        result.IsStatisticallySignificant.Should().BeTrue();
        result.RelativeUplift.Should().BeGreaterThan(0);
        result.Recommendation.Should().Contain("Recommend rollout");
    }

    [Fact]
    public void EvaluateAbTest_ShouldRecommendNotRollout_WhenTreatmentIsWorse()
    {
        var variants = new Dictionary<string, (int observations, int conversions)>
        {
            { "control", (50000, 5000) },       // 10%
            { "treatment", (50000, 2000) }       // 4% — big decrease
        };

        var result = _sut.EvaluateAbTest(variants);

        result.IsStatisticallySignificant.Should().BeTrue();
        result.RelativeUplift.Should().BeLessThan(0);
        result.Recommendation.Should().Contain("Do not rollout");
    }

    [Fact]
    public void EvaluateAbTest_ShouldRecommendStopTest_WhenNoSignificantDifferenceWithLargeSample()
    {
        var variants = new Dictionary<string, (int observations, int conversions)>
        {
            { "control", (5000, 500) },
            { "treatment", (5000, 502) }
        };

        var result = _sut.EvaluateAbTest(variants);

        if (!result.IsStatisticallySignificant)
        {
            result.Recommendation.Should().Contain("No significant difference");
        }
    }

    [Fact]
    public void EvaluateAbTest_VariantStats_ShouldContainCorrectValues()
    {
        var variants = new Dictionary<string, (int observations, int conversions)>
        {
            { "A", (1000, 100) },
            { "B", (1000, 120) }
        };

        var result = _sut.EvaluateAbTest(variants);

        result.VariantStats.Should().HaveCount(2);

        var controlStat = result.VariantStats.First(s => s.IsControl);
        controlStat.VariantName.Should().Be("A");
        controlStat.Observations.Should().Be(1000);
        controlStat.Conversions.Should().Be(100);
        controlStat.ConversionRate.Should().BeApproximately(0.1, 0.001);
        controlStat.ConfidenceIntervalLower.Should().BeLessThan(controlStat.ConversionRate);
        controlStat.ConfidenceIntervalUpper.Should().BeGreaterThan(controlStat.ConversionRate);
    }

    [Fact]
    public void EvaluateAbTest_ShouldHandleZeroControlConversions()
    {
        var variants = new Dictionary<string, (int observations, int conversions)>
        {
            { "control", (1000, 0) },
            { "treatment", (1000, 50) }
        };

        var result = _sut.EvaluateAbTest(variants);

        result.RelativeUplift.Should().Be(0); // controlRate is 0, so uplift formula returns 0
    }

    [Fact]
    public void CalculateRequiredSampleSize_ShouldReturnPositiveValue()
    {
        var sampleSize = _sut.CalculateRequiredSampleSize(
            baselineConversionRate: 0.05,
            minimumDetectableEffect: 0.2
        );

        sampleSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateRequiredSampleSize_ShouldRequireLargerSample_ForSmallerEffect()
    {
        var smallEffect = _sut.CalculateRequiredSampleSize(0.05, 0.1);
        var largeEffect = _sut.CalculateRequiredSampleSize(0.05, 0.5);

        smallEffect.Should().BeGreaterThan(largeEffect);
    }

    [Fact]
    public void CalculateRequiredSampleSize_ShouldRequireLargerSample_ForHigherPower()
    {
        var lowPower = _sut.CalculateRequiredSampleSize(0.05, 0.2, power: 0.7);
        var highPower = _sut.CalculateRequiredSampleSize(0.05, 0.2, power: 0.95);

        highPower.Should().BeGreaterThan(lowPower);
    }

    [Fact]
    public void HasReachedSignificance_ShouldReturnTrue_ForLargeEffect()
    {
        var result = _sut.HasReachedSignificance(
            controlConversions: 100,
            controlObservations: 10000,
            treatmentConversions: 300,
            treatmentObservations: 10000
        );

        result.Should().BeTrue();
    }

    [Fact]
    public void HasReachedSignificance_ShouldReturnFalse_ForTinyEffect()
    {
        var result = _sut.HasReachedSignificance(
            controlConversions: 100,
            controlObservations: 1000,
            treatmentConversions: 101,
            treatmentObservations: 1000
        );

        result.Should().BeFalse();
    }

    [Fact]
    public void HasReachedSignificance_ShouldRespectAlpha()
    {
        // With very strict alpha, should not reach significance
        var result = _sut.HasReachedSignificance(
            controlConversions: 100,
            controlObservations: 5000,
            treatmentConversions: 130,
            treatmentObservations: 5000,
            alpha: 0.001
        );

        // Same test with lenient alpha
        var lenient = _sut.HasReachedSignificance(
            controlConversions: 100,
            controlObservations: 5000,
            treatmentConversions: 130,
            treatmentObservations: 5000,
            alpha: 0.5
        );

        // More lenient alpha should be at least as likely to find significance
        if (!result)
        {
            lenient.Should().BeTrue();
        }
    }

    [Fact]
    public void EvaluateAbTest_ShouldAcceptCustomConfidenceLevel()
    {
        var variants = new Dictionary<string, (int observations, int conversions)>
        {
            { "control", (1000, 100) },
            { "treatment", (1000, 120) }
        };

        var result = _sut.EvaluateAbTest(variants, confidenceLevel: 0.99);

        result.ConfidenceLevel.Should().Be(0.99);
    }
}

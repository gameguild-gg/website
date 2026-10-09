namespace GameGuild.Features;

/// <summary>
///     Implementation of A/B test statistical evaluation
/// </summary>
public class FeatureFlagExperimentService : IFeatureFlagExperimentService
{
    public AbTestResult EvaluateAbTest(Dictionary<string, (int observations, int conversions)> variants, double confidenceLevel = 0.95)
    {
        if (variants.Count < 2)
        {
            throw new ArgumentException("At least two variants required for A/B test");
        }

        var control = variants.First();
        var treatment = variants.Skip(1).First();

        var controlRate = (double) control.Value.conversions / control.Value.observations;
        var treatmentRate = (double) treatment.Value.conversions / treatment.Value.observations;

        // Calculate pooled proportion and standard error
        var pooledProportion = (double) (control.Value.conversions + treatment.Value.conversions) / (control.Value.observations + treatment.Value.observations);

        var standardError = Math.Sqrt(pooledProportion * (1 - pooledProportion) * (1.0 / control.Value.observations + 1.0 / treatment.Value.observations));

        // Calculate Z-score
        var zScore = (treatmentRate - controlRate) / standardError;

        // Calculate P-value (two-tailed test)
        var pValue = 2 * (1 - StandardNormalCdf(Math.Abs(zScore)));

        // Determine significance
        var alpha = 1 - confidenceLevel;
        var isSignificant = pValue < alpha;

        // Calculate relative uplift
        var relativeUplift = controlRate > 0 ? (treatmentRate - controlRate) / controlRate * 100 : 0;

        // Generate recommendation
        var recommendation = GenerateRecommendation(isSignificant, relativeUplift, pValue, control.Value.observations + treatment.Value.observations);

        var result = new AbTestResult
        {
            ControlVariant = control.Key,
            TreatmentVariant = treatment.Key,
            PValue = pValue,
            ZScore = zScore,
            IsStatisticallySignificant = isSignificant,
            ConfidenceLevel = confidenceLevel,
            RelativeUplift = relativeUplift,
            Recommendation = recommendation,
            VariantStats = variants.Select(v => CalculateVariantStatistics(v.Key, v.Value.observations, v.Value.conversions, v.Key == control.Key, confidenceLevel)).ToList()
        };

        return result;
    }

    public int CalculateRequiredSampleSize(double baselineConversionRate, double minimumDetectableEffect, double power = 0.8, double alpha = 0.05)
    {
        // Using formula for two-proportion z-test
        var zAlpha = InverseStandardNormalCdf(1 - alpha / 2);
        var zBeta = InverseStandardNormalCdf(power);

        var p1 = baselineConversionRate;
        var p2 = baselineConversionRate * (1 + minimumDetectableEffect);

        var numerator = Math.Pow(zAlpha * Math.Sqrt(2 * p1 * (1 - p1)) + zBeta * Math.Sqrt(p1 * (1 - p1) + p2 * (1 - p2)), 2);
        var denominator = Math.Pow(p2 - p1, 2);

        var sampleSize = (int) Math.Ceiling(numerator / denominator);

        return sampleSize;
    }

    public bool HasReachedSignificance(int controlConversions, int controlObservations, int treatmentConversions, int treatmentObservations, double alpha = 0.05)
    {
        var controlRate = (double) controlConversions / controlObservations;
        var treatmentRate = (double) treatmentConversions / treatmentObservations;

        var pooledProportion = (double) (controlConversions + treatmentConversions) / (controlObservations + treatmentObservations);
        var standardError = Math.Sqrt(pooledProportion * (1 - pooledProportion) * (1.0 / controlObservations + 1.0 / treatmentObservations));

        var zScore = (treatmentRate - controlRate) / standardError;
        var pValue = 2 * (1 - StandardNormalCdf(Math.Abs(zScore)));

        return pValue < alpha;
    }

    private ExperimentStatistics CalculateVariantStatistics(string variantName, int observations, int conversions, bool isControl, double confidenceLevel)
    {
        var conversionRate = (double) conversions / observations;
        var standardError = Math.Sqrt(conversionRate * (1 - conversionRate) / observations);

        var zScore = InverseStandardNormalCdf((1 + confidenceLevel) / 2);
        var marginOfError = zScore * standardError;

        return new ExperimentStatistics
        {
            VariantName = variantName,
            Observations = observations,
            Conversions = conversions,
            ConversionRate = conversionRate,
            StandardError = standardError,
            ConfidenceIntervalLower = Math.Max(0, conversionRate - marginOfError),
            ConfidenceIntervalUpper = Math.Min(1, conversionRate + marginOfError),
            IsControl = isControl
        };
    }

    private string GenerateRecommendation(bool isSignificant, double uplift, double pValue, int totalSampleSize)
    {
        if (!isSignificant && totalSampleSize < 1000) { return $"Inconclusive - Need more data. Current sample size: {totalSampleSize}. Continue test."; }

        if (!isSignificant) { return $"No significant difference detected (p={pValue:F4}). Consider stopping test."; }

        if (uplift > 0) { return $"Treatment variant shows {uplift:F2}% improvement (p={pValue:F4}). Recommend rollout."; }

        return $"Treatment variant shows {Math.Abs(uplift):F2}% decrease (p={pValue:F4}). Do not rollout.";
    }

    // Standard normal cumulative distribution function approximation
    private double StandardNormalCdf(double z)
    {
        // Approximation using error function
        return 0.5 * (1 + Erf(z / Math.Sqrt(2)));
    }

    // Error function approximation
    private double Erf(double x)
    {
        // Abramowitz and Stegun approximation
        var sign = x >= 0 ? 1 : -1;
        x = Math.Abs(x);

        var a1 = 0.254829592;
        var a2 = -0.284496736;
        var a3 = 1.421413741;
        var a4 = -1.453152027;
        var a5 = 1.061405429;
        var p = 0.3275911;

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - ((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }

    // Inverse standard normal CDF approximation
    private double InverseStandardNormalCdf(double p)
    {
        if (p <= 0 || p >= 1)
        {
            throw new ArgumentException("Probability must be between 0 and 1");
        }

        // Beasley-Springer-Moro approximation
        double[ ] a = [2.50662823884, -18.61500062529, 41.39119773534, -25.44106049637];
        double[ ] b = [-8.47351093090, 23.08336743743, -21.06224101826, 3.13082909833];
        var c = new[ ] { 0.3374754822726147, 0.9761690190917186, 0.1607979714918209, 0.0276438810333863, 0.0038405729373609, 0.0003951896511919, 0.0000321767881768, 0.0000002888167364, 0.0000003960315187 };

        var y = p - 0.5;
        double x;

        if (Math.Abs(y) < 0.42)
        {
            var r = y * y;
            x = y * (((a[3] * r + a[2]) * r + a[1]) * r + a[0]) / ((((b[3] * r + b[2]) * r + b[1]) * r + b[0]) * r + 1);
        }
        else
        {
            var r = p < 0.5 ? p : 1 - p;
            r = Math.Log(-Math.Log(r));
            x = c[0] + r * (c[1] + r * (c[2] + r * (c[3] + r * (c[4] + r * (c[5] + r * (c[6] + r * (c[7] + r * c[8])))))));
            if (y < 0)
            {
                x = -x;
            }
        }

        return x;
    }
}

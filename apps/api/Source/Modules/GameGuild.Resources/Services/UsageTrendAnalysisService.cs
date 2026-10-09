using Microsoft.Extensions.Logging;

namespace GameGuild.Resources;

/// <summary>
///     Implementation of usage trend analysis and forecasting
/// </summary>
public class UsageTrendAnalysisService(IResourceUsageTrendRepository trendRepository, IUsageRecordRepository usageRepository, ILogger<UsageTrendAnalysisService> logger) : IUsageTrendAnalysisService
{
    public async Task<ResourceUsageTrend> AnalyzeTrendAsync(Guid tenantId, ResourceUsageType type, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        var usageRecords = await usageRepository.GetByTenantAsync(tenantId, type, periodStart, periodEnd, cancellationToken).ConfigureAwait(false);

        var recordsList = usageRecords.ToList();

        if (recordsList.Count == 0)
        {
            logger.LogWarning("No usage records found for tenant {TenantId}, type {Type} in period {Start} to {End}", tenantId, type, periodStart, periodEnd);

            var emptyTrend = new ResourceUsageTrend
            {
                Type = type, PeriodStart = periodStart, PeriodEnd = periodEnd, AverageUsage = 0, MinUsage = 0, MaxUsage = 0, StandardDeviation = 0, GrowthRate = 0, AnomalyCount = 0, Pattern = "Insufficient Data"
            };
            emptyTrend.SetProperties(new Dictionary<string, object?> { ["TenantId"] = tenantId });

            return emptyTrend;
        }

        var usageAmounts = recordsList.Select(r => r.UsageAmount).ToList();

        var averageUsage = usageAmounts.Average();
        var minUsage = usageAmounts.Min();
        var maxUsage = usageAmounts.Max();
        var stdDev = CalculateStandardDeviation(usageAmounts);

        // Calculate growth rate (compare first half vs second half)
        var growthRate = CalculateGrowthRate(recordsList, periodStart, periodEnd);

        // Detect anomalies
        var anomalyCount = DetectAnomalyCount(usageAmounts, averageUsage, stdDev);

        // Classify pattern
        var pattern = ClassifyPattern(growthRate, stdDev, averageUsage);

        var trend = new ResourceUsageTrend
        {
            Type = type,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AverageUsage = (long) averageUsage,
            MinUsage = minUsage,
            MaxUsage = maxUsage,
            StandardDeviation = (double) stdDev,
            GrowthRate = (double) growthRate,
            AnomalyCount = anomalyCount,
            Pattern = pattern
        };
        trend.SetProperties(new Dictionary<string, object?> { ["TenantId"] = tenantId });

        var savedTrend = await trendRepository.AddAsync(trend, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Analyzed trend for tenant {TenantId}, type {Type}: Pattern={Pattern}, Growth={Growth:P}", tenantId, type, pattern, growthRate);

        return savedTrend;
    }

    public async Task<IEnumerable<ResourceUsageTrend>> GetTenantTrendsAsync(Guid tenantId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        return await trendRepository.GetByTenantAsync(tenantId, null, fromDate, toDate, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ResourceUsageTrend>> DetectAnomaliesAsync(Guid tenantId, ResourceUsageType? type = null, int lookbackDays = 30, CancellationToken cancellationToken = default)
    {
        var fromDate = SystemClock.UtcNow.AddDays(-lookbackDays);

        var trends = await trendRepository.GetByTenantAsync(tenantId, type, fromDate, null, cancellationToken).ConfigureAwait(false);

        return trends.Where(t => t.AnomalyCount > 0 || t.IsAnomaly((long) t.AverageUsage, t.StandardDeviation));
    }

    public async Task<long> ForecastUsageAsync(Guid tenantId, ResourceUsageType type, DateTime targetDate, CancellationToken cancellationToken = default)
    {
        // Get recent usage records (last 90 days) for linear regression
        var fromDate = SystemClock.UtcNow.AddDays(-90);

        var usageRecords = await usageRepository.GetByTenantAsync(tenantId, type, fromDate, null, cancellationToken).ConfigureAwait(false);
        var recordsList = usageRecords.OrderBy(r => r.PeriodStart).ToList();

        if (recordsList.Count == 0)
        {
            logger.LogWarning("No usage records found for forecasting tenant {TenantId}, type {Type}", tenantId, type);
        }

        // Use linear regression for forecasting
        var forecast = CalculateLinearRegressionForecast(recordsList, targetDate);

        // Ensure non-negative forecast
        forecast = Math.Max(0, forecast);

        logger.LogInformation(
            "Forecast for tenant {TenantId}, type {Type} on {Date}: {Forecast} (based on {RecordCount} records)",
            tenantId, type, targetDate, forecast, recordsList.Count);

        return forecast;
    }

    /// <summary>
    ///     Calculates a usage forecast using simple linear regression (least squares method).
    ///     This is a basic ML approach that fits a line y = mx + b to historical data.
    /// </summary>
    private static long CalculateLinearRegressionForecast(List<UsageRecord> records, DateTime targetDate)
    {
        if (records.Count < 2)
        {
            return records.Count == 1 ? records[0].UsageAmount : 0;
        }

        // Convert dates to numeric values (days since first record)
        var baseDate = records.First().PeriodStart;
        var dataPoints = records
            .Select(r => new
            {
                X = (r.PeriodStart - baseDate).TotalDays,
                Y = (double)r.UsageAmount
            })
            .ToList();

        var n = dataPoints.Count;

        // Calculate sums for linear regression
        var sumX = dataPoints.Sum(p => p.X);
        var sumY = dataPoints.Sum(p => p.Y);
        var sumXY = dataPoints.Sum(p => p.X * p.Y);
        var sumX2 = dataPoints.Sum(p => p.X * p.X);

        // Calculate slope (m) and intercept (b) using least squares
        // m = (n * Σxy - Σx * Σy) / (n * Σx² - (Σx)²)
        // b = (Σy - m * Σx) / n
        var denominator = n * sumX2 - sumX * sumX;

        if (Math.Abs(denominator) < 0.0001)
        {
            // Essentially horizontal line - return average
            return (long)(sumY / n);
        }

        var slope = (n * sumXY - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / n;

        // Calculate forecast for target date
        var targetDays = (targetDate - baseDate).TotalDays;
        var forecastValue = slope * targetDays + intercept;

        return (long)Math.Round(forecastValue);
    }

    public async Task<Dictionary<string, int>> GetPatternDistributionAsync(Guid? tenantId = null, ResourceUsageType? type = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<ResourceUsageTrend> trends;

        if (tenantId.HasValue) { trends = await trendRepository.GetByTenantAsync(tenantId.Value, type, null, null, cancellationToken).ConfigureAwait(false); }
        else
        {
            // Get all trends - would need an additional repository method
            // For now, return empty result
            logger.LogWarning("GetPatternDistributionAsync called without tenant ID - not yet supported");

            return new Dictionary<string, int>();
        }

        return trends.GroupBy(t => t.Pattern).ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<decimal> CalculateGrowthRateAsync(Guid tenantId, ResourceUsageType type, int periodDays = 30, CancellationToken cancellationToken = default)
    {
        var endDate = SystemClock.UtcNow;
        var startDate = endDate.AddDays(-periodDays);

        var usageRecords = await usageRepository.GetByTenantAsync(tenantId, type, startDate, endDate, cancellationToken).ConfigureAwait(false);

        var recordsList = usageRecords.OrderBy(r => r.PeriodStart).ToList();

        if (recordsList.Count < 2)
        {
            return 0;
        }

        return CalculateGrowthRate(recordsList, startDate, endDate);
    }

    private static decimal CalculateStandardDeviation(List<long> values)
    {
        if (values.Count <= 1)
        {
            return 0;
        }

        var average = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));

        return (decimal) Math.Sqrt(sumOfSquares / values.Count);
    }

    private static decimal CalculateGrowthRate(List<UsageRecord> records, DateTime periodStart, DateTime periodEnd)
    {
        if (records.Count < 2)
        {
            return 0;
        }

        var midpoint = periodStart.AddTicks((periodEnd - periodStart).Ticks / 2);

        var firstHalf = records.Where(r => r.PeriodStart < midpoint).ToList();
        var secondHalf = records.Where(r => r.PeriodStart >= midpoint).ToList();

        if (firstHalf.Count == 0 || secondHalf.Count == 0)
        {
            return 0;
        }

        var firstAvg = firstHalf.Average(r => r.UsageAmount);
        var secondAvg = secondHalf.Average(r => r.UsageAmount);

        if (firstAvg == 0)
        {
            return 0;
        }

        return (decimal) ((secondAvg - firstAvg) / firstAvg);
    }

    private static int DetectAnomalyCount(List<long> values, double average, decimal stdDev)
    {
        if (stdDev == 0 || values.Count < 3)
        {
            return 0;
        }

        // Use Median Absolute Deviation (MAD) for robust anomaly detection
        // MAD is resistant to outlier contamination unlike mean/stddev
        var sorted = values.OrderBy(v => v).ToList();
        var median = sorted.Count % 2 == 0
            ? (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0
            : sorted[sorted.Count / 2];

        var absDeviations = values.Select(v => Math.Abs(v - median)).OrderBy(d => d).ToList();
        var mad = absDeviations.Count % 2 == 0
            ? (absDeviations[absDeviations.Count / 2 - 1] + absDeviations[absDeviations.Count / 2]) / 2.0
            : absDeviations[absDeviations.Count / 2];

        if (mad == 0)
        {
            // Fall back to mean/stddev when MAD is 0 (most values identical)
            var threshold = 2.0;
            return values.Count(v => Math.Abs(v - average) > (double)stdDev * threshold);
        }

        // 1.4826 converts MAD to standard deviation equivalent for normal distributions
        var madSigma = mad * 1.4826;
        var madThreshold = 2.0;
        return values.Count(v => Math.Abs(v - median) > madSigma * madThreshold);
    }

    private static string ClassifyPattern(decimal growthRate, decimal stdDev, double average)
    {
        // High variability
        if (average > 0 && stdDev / (decimal) average > 0.5m)
        {
            return "Volatile";
        }

        // Growth patterns
        if (growthRate > 0.2m)
        {
            return "Rapid Growth";
        }

        if (growthRate > 0.05m)
        {
            return "Growing";
        }

        if (growthRate < -0.2m)
        {
            return "Rapid Decline";
        }

        if (growthRate < -0.05m)
        {
            return "Declining";
        }

        // Stable pattern
        return "Stable";
    }
}

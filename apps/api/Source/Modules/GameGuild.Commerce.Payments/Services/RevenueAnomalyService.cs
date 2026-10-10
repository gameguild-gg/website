using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Default <see cref="IRevenueAnomalyService" /> (issue #404). Daily net revenue is
///     compared against the trailing baseline window: a day is anomalous when its net
///     total deviates from the baseline mean by at least the configured z-score threshold
///     and enough baseline days with activity exist. Baselines, means and z-scores are
///     computed per currency so amounts in different units are never mixed (a 100 USD
///     spike does not contaminate the EUR baseline). Persistence is idempotent per
///     (kind, day, currency, tenant).
/// </summary>
public sealed class RevenueAnomalyService(
    IRevenueEventRepository revenueEventRepository,
    IRevenueAnomalyAlertRepository alertRepository,
    IOptions<RevenueAuditingOptions> options,
    ILogger<RevenueAnomalyService> logger) : IRevenueAnomalyService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RevenueAnomalyCandidate>> DetectAsync(
        DateTime evaluationDateUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var auditingOptions = options.Value;
        var evaluationDate = DateTime.SpecifyKind(evaluationDateUtc, DateTimeKind.Utc).Date;

        var evaluationDays = Enumerable
            .Range(0, auditingOptions.AnomalyEvaluationDays)
            .Select(offset => evaluationDate.AddDays(-offset))
            .OrderBy(day => day)
            .ToList();
        var firstEvaluationDay = evaluationDays[0];
        var baselineStart = firstEvaluationDay.AddDays(-auditingOptions.AnomalyBaselineDays);
        var fetchEnd = evaluationDate.AddDays(1).AddTicks(-1);

        var dailyTotals = await revenueEventRepository
            .GetDailyTotalsAsync(baselineStart, fetchEnd, tenantId, cancellationToken)
            .ConfigureAwait(false);

        // One independent statistical series per currency: baselines, means and
        // z-scores must never aggregate across units.
        var currencies = dailyTotals
            .Select(total => total.Currency)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(currency => currency, StringComparer.Ordinal)
            .ToList();

        var candidates = new List<RevenueAnomalyCandidate>();
        foreach (var currency in currencies)
        {
            var currencyTotals = dailyTotals
                .Where(total => string.Equals(total.Currency, currency, StringComparison.Ordinal))
                .ToList();

            var totalsByDay = currencyTotals.ToDictionary(total => total.DateUtc, total => total.NetTotal);
            var baselineByDay = currencyTotals
                .Where(total => total.DateUtc < firstEvaluationDay)
                .ToDictionary(total => total.DateUtc, total => total.NetTotal);

            var activeBaselineDays = baselineByDay.Count;

            // Without a meaningful baseline, no deviation can be claimed.
            if (activeBaselineDays < auditingOptions.AnomalyMinBaselineDays)
            {
                logger.LogInformation(
                    "Revenue anomaly detection skipped for {Currency}: {ActiveDays} active baseline days is below the minimum of {MinBaselineDays}.",
                    currency,
                    activeBaselineDays,
                    auditingOptions.AnomalyMinBaselineDays);
                continue;
            }

            var baselineValues = baselineByDay.Values.Select(net => decimal.ToDouble(net)).ToList();
            var mean = baselineValues.Average();
            var variance = baselineValues.Sum(value => (value - mean) * (value - mean)) / baselineValues.Count;
            var standardDeviation = Math.Sqrt(variance);

            foreach (var day in evaluationDays)
            {
                var observed = totalsByDay.TryGetValue(day, out var netTotal) ? netTotal : 0m;
                var observedDouble = decimal.ToDouble(observed);
                var deviation = observedDouble - mean;
                var zScore = standardDeviation > 0d ? deviation / standardDeviation : (Math.Abs(deviation) > 0d ? double.PositiveInfinity : 0d);

                if (double.IsInfinity(zScore)
                    ? Math.Abs(deviation) > 0d
                    : Math.Abs(zScore) >= decimal.ToDouble(auditingOptions.AnomalyZScoreThreshold))
                {
                    candidates.Add(new RevenueAnomalyCandidate(
                        Kind: deviation > 0d ? RevenueAnomalyKind.Spike : RevenueAnomalyKind.Drop,
                        DetectedForDateUtc: day,
                        Currency: currency,
                        ObservedNetRevenue: decimal.Round(observed, 2),
                        ExpectedNetRevenue: decimal.Round(Convert.ToDecimal(mean), 2),
                        ZScore: double.IsInfinity(zScore)
                            ? 9999m
                            : decimal.Round(Convert.ToDecimal(zScore), 4),
                        BaselineDays: activeBaselineDays));
                }
            }
        }

        return candidates;
    }

    /// <inheritdoc />
    public async Task<int> DetectAndPersistAsync(
        DateTime evaluationDateUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var candidates = await DetectAsync(evaluationDateUtc, tenantId, cancellationToken).ConfigureAwait(false);

        var created = 0;
        foreach (var candidate in candidates)
        {
            if (await alertRepository
                .ExistsForDayAsync(candidate.Kind, candidate.DetectedForDateUtc, candidate.Currency, tenantId, cancellationToken)
                .ConfigureAwait(false))
            {
                continue;
            }

            var alert = new RevenueAnomalyAlert
            {
                TenantId = tenantId,
                Kind = candidate.Kind,
                DetectedForDateUtc = candidate.DetectedForDateUtc,
                Currency = candidate.Currency,
                DetectedAtUtc = SystemClock.UtcNow,
                ObservedNetRevenue = candidate.ObservedNetRevenue,
                ExpectedNetRevenue = candidate.ExpectedNetRevenue,
                ZScore = candidate.ZScore,
                BaselineDays = candidate.BaselineDays
            };

            await alertRepository.AddAsync(alert, cancellationToken).ConfigureAwait(false);
            created++;
        }

        if (created > 0)
        {
            await alertRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Revenue anomaly detection created {Created} alert(s) as of {EvaluationDate:yyyy-MM-dd}.",
                created,
                evaluationDateUtc);
        }

        return created;
    }
}

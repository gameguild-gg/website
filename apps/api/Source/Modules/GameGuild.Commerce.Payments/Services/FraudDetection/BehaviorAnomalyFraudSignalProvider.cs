using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Behavior-anomaly fraud signal provider (issue #409): flags charges that deviate
///     sharply from the customer's established baseline. The baseline is the average
///     amount of the customer's successful charges in the velocity window; with at least
///     <see cref="FraudAssessmentOptions.BehaviorAnomalyMinHistory"/> qualifying charges,
///     an attempt at or above <see cref="FraudAssessmentOptions.BehaviorAnomalyAmountMultiplier"/>
///     times the baseline fires the signal.
/// </summary>
public sealed class BehaviorAnomalyFraudSignalProvider(
    IApplicationDbContext context,
    ILogger<BehaviorAnomalyFraudSignalProvider> logger) : IFraudSignalProvider
{
    private const string SignalType = "Behavior.AmountAnomaly";

    /// <inheritdoc />
    public string Name => "BehaviorAnomaly";

    /// <inheritdoc />
    public async Task<IReadOnlyList<FraudSignal>> EvaluateAsync(
        FraudSignalContext signalContext,
        FraudAssessmentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signalContext);
        ArgumentNullException.ThrowIfNull(options);

        var windowStart = SystemClock.UtcNow - options.VelocityWindow;

        var baselineQuery = context.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.TenantId == signalContext.TenantId
                              && payment.Id != signalContext.PaymentId
                              && payment.CreatedAt >= windowStart
                              && payment.Status == PaymentStatus.Succeeded
                              && (payment.FraudDecision == null || payment.FraudDecision == FraudDecision.Allow));

        baselineQuery = signalContext.ExternalCustomerId is null
            ? baselineQuery.Where(payment => payment.SubscriptionId == signalContext.SubscriptionId)
            : baselineQuery.Where(payment => payment.ExternalCustomerId == signalContext.ExternalCustomerId);

        var priorSuccessfulAmounts = await baselineQuery
            .Select(payment => payment.Amount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var signals = new List<FraudSignal>();
        if (priorSuccessfulAmounts.Count < options.BehaviorAnomalyMinHistory)
        {
            // Not enough history to establish a meaningful baseline for this customer.
            return signals;
        }

        var baselineAverage = priorSuccessfulAmounts.Average();
        if (baselineAverage <= 0)
        {
            return signals;
        }

        var anomalyCeiling = baselineAverage * options.BehaviorAnomalyAmountMultiplier;
        if (signalContext.Amount >= anomalyCeiling)
        {
            signals.Add(new FraudSignal(
                SignalType,
                Math.Min(options.BehaviorAnomalyWeight, options.MaxSignalScore),
                JsonSerializer.Serialize(new
                {
                    amount = signalContext.Amount,
                    baselineAverage = Math.Round(baselineAverage, 2),
                    baselineSamples = priorSuccessfulAmounts.Count,
                    multiplier = options.BehaviorAnomalyAmountMultiplier,
                    anomalyCeiling = Math.Round(anomalyCeiling, 2),
                    windowMinutes = options.VelocityWindowMinutes
                })));
        }

        logger.LogDebug(
            "Fraud behavior-anomaly evaluation for tenant {TenantId} payment {PaymentId}: amount {Amount} against baseline {Baseline} over {Samples} sample(s)",
            signalContext.TenantId,
            signalContext.PaymentId,
            signalContext.Amount,
            baselineAverage,
            priorSuccessfulAmounts.Count);

        return signals;
    }
}

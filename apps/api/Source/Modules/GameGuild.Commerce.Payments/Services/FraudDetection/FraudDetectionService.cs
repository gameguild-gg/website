using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Aggregates every registered <see cref="IFraudSignalProvider"/> into a single
///     Allow / Review / Block decision for a payment attempt (issue #409). The decision is
///     evaluated before gateway execution; signal evidence stays bounded and internal
///     (no provider tokens ever enter the evidence bundle).
/// </summary>
public sealed class FraudDetectionService(
    IEnumerable<IFraudSignalProvider> signalProviders,
    IOptionsMonitor<FraudAssessmentOptions> optionsMonitor,
    ILogger<FraudDetectionService> logger) : IFraudDetectionService
{
    private const int MaxScore = 100;
    private const int MaxReasonLength = 400;

    /// <inheritdoc />
    public async Task<FraudAssessment> AssessPaymentAsync(
        FraudSignalContext signalContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signalContext);

        var options = optionsMonitor.CurrentValue.ResolveFor(signalContext.TenantId);

        if (!options.IsEnabled)
        {
            logger.LogDebug(
                "Fraud assessment disabled for tenant {TenantId}; allowing payment {PaymentId}",
                signalContext.TenantId,
                signalContext.PaymentId);

            return new FraudAssessment(
                signalContext.TenantId,
                signalContext.PaymentId,
                0,
                FraudRiskLevel.Low,
                FraudDecision.Allow,
                TruncateReason("Fraud assessment disabled for tenant"),
                [],
                SystemClock.UtcNow);
        }

        var signals = new List<FraudSignal>();
        foreach (var provider in signalProviders)
        {
            var providerSignals = await provider.EvaluateAsync(signalContext, options, cancellationToken)
                .ConfigureAwait(false);
            signals.AddRange(providerSignals);
        }

        var score = Math.Min(signals.Sum(signal => signal.Weight), MaxScore);
        var riskLevel = FraudAssessment.RiskLevelForScore(score);
        var decision = score switch
        {
            var s when s >= options.BlockThreshold => FraudDecision.Block,
            var s when s >= options.ReviewThreshold => FraudDecision.Review,
            _ => FraudDecision.Allow
        };
        var reason = BuildReason(decision, score, options, signals);

        logger.LogInformation(
            "Fraud assessment for tenant {TenantId} payment {PaymentId}: decision {Decision}, score {Score}, risk {RiskLevel}, {SignalCount} signal(s)",
            signalContext.TenantId,
            signalContext.PaymentId,
            decision,
            score,
            riskLevel,
            signals.Count);

        return new FraudAssessment(
            signalContext.TenantId,
            signalContext.PaymentId,
            score,
            riskLevel,
            decision,
            reason,
            signals,
            SystemClock.UtcNow);
    }

    private static string BuildReason(
        FraudDecision decision,
        int score,
        FraudAssessmentOptions options,
        IReadOnlyList<FraudSignal> signals)
    {
        if (decision == FraudDecision.Allow)
        {
            return TruncateReason($"Score {score} below review threshold {options.ReviewThreshold}");
        }

        var topSignals = signals
            .OrderByDescending(signal => signal.Weight)
            .Take(3)
            .Select(signal => $"{signal.SignalType}({signal.Weight})");

        return TruncateReason(
            $"Score {score} reached the {decision.ToString().ToLowerInvariant()} threshold " +
            $"{(decision == FraudDecision.Block ? options.BlockThreshold : options.ReviewThreshold)}: {string.Join(", ", topSignals)}");
    }

    private static string TruncateReason(string reason) =>
        reason.Length <= MaxReasonLength ? reason : reason[..MaxReasonLength];
}

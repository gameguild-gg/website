using GameGuild.Commerce.Payments;

namespace GameGuild.Commerce.Payments.UnitTests;

/// <summary>Shared fakes for fraud-gate tests.</summary>
public static class FraudTestHelpers
{
    /// <summary>A detection service that always returns the given decision (Allow by default).</summary>
    public static IFraudDetectionService DetectionServiceReturning(
        FraudDecision decision = FraudDecision.Allow,
        int score = 0,
        string reason = "unit-test assessment")
        => new StaticDecisionDetectionService(decision, score, reason);

    /// <summary>A detection service that always Allows (gate transparent).</summary>
    public static IFraudDetectionService AllowAllDetectionService() => DetectionServiceReturning();

    private sealed class StaticDecisionDetectionService(
        FraudDecision decision,
        int score,
        string reason) : IFraudDetectionService
    {
        public Task<FraudAssessment> AssessPaymentAsync(
            FraudSignalContext signalContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new FraudAssessment(
                signalContext.TenantId,
                signalContext.PaymentId,
                score,
                FraudAssessment.RiskLevelForScore(score),
                decision,
                reason,
                [new FraudSignal("UnitTest.Static", score, """{"source":"unit-test"}""")],
                SystemClock.UtcNow));
        }
    }
}

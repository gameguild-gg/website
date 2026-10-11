namespace GameGuild.Commerce.Payments;

/// <summary>
///     Evaluates the aggregate fraud assessment for a payment attempt by aggregating all
///     registered <see cref="IFraudSignalProvider"/> contributions against the
///     tenant-resolved <see cref="FraudAssessmentOptions"/> thresholds (issue #409).
/// </summary>
public interface IFraudDetectionService
{
    /// <summary>
    ///     Assesses a payment attempt and returns the Allow / Review / Block decision with
    ///     its score, risk level, reason and bounded signal evidence.
    /// </summary>
    /// <param name="context">The payment attempt being assessed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<FraudAssessment> AssessPaymentAsync(FraudSignalContext context, CancellationToken cancellationToken = default);
}

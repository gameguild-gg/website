namespace GameGuild.Identity.Authentication;

/// <summary>
///     Records redacted suspicious-login alert events for the host-side notification bridge.
///     Publishing is fire-and-forget: a recording failure is logged and never fails authentication.
/// </summary>
public interface ISuspiciousLoginAlertPublisher
{
    /// <summary>
    ///     Records a security alert event when the assessed severity clears the configured bar.
    ///     The named security signals (step-up, brute force, impossible travel) are treated as at
    ///     least <see cref="RiskLevel.High"/> severity so the default minimum keeps notifying.
    /// </summary>
    Task RecordAsync(
        Guid userId,
        Guid? tenantId,
        string alertKind,
        RiskLevel assessedRiskLevel,
        int riskScore,
        CancellationToken cancellationToken = default);
}

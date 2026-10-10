namespace GameGuild.Identity.Authentication;

/// <summary>
///     Configuration for adaptive (online statistical) anomaly detection over login behavior.
///     Bound from <c>Authentication:AdaptiveAnomaly</c>.
/// </summary>
public sealed class AdaptiveAnomalyDetectionOptions
{
    public const string SectionName = "Authentication:AdaptiveAnomaly";

    /// <summary>
    ///     Master switch. When false, the learned model is not registered in the risk path and
    ///     only the fixed-weight heuristic analysis runs. Default true: the model has a built-in
    ///     cold-start abstention, so enabling it never produces learned verdicts for subjects
    ///     without baseline data.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Sensitivity: minimum z-score at which a feature deviation counts as a learned anomaly.
    ///     Higher values reduce false positives; lower values detect subtler deviations.
    /// </summary>
    public double ZScoreThreshold { get; set; } = 3.0;

    /// <summary>
    ///     Minimum accumulated observations before the learned model issues verdicts
    ///     (cold-start window). Below this count the model abstains.
    /// </summary>
    public int MinimumObservations { get; set; } = 20;

    /// <summary>
    ///     EWMA learning rate (0..1): each observation moves every baseline statistic by this
    ///     fraction of the distance to the new value. Smaller values adapt more slowly (more
    ///     stable baselines); larger values track behavior changes faster but are noisier.
    ///     The exponential decay is also the drift mechanism: gradual legitimate change fades
    ///     old behavior out of the baseline.
    /// </summary>
    public double EwmaAlpha { get; set; } = 0.05;

    /// <summary>Variance floor applied to every feature so a degenerate (near-zero) learned variance cannot produce infinite z-scores.</summary>
    public double VarianceFloor { get; set; } = 0.01;

    /// <summary>Maximum number of distinct IP hashes tracked per subject in the learned novelty distribution.</summary>
    public int MaxTrackedIps { get; set; } = 16;

    /// <summary>
    ///     Baselines whose last observation is older than this window are reset on the next attempt
    ///     (drift handling for returning subjects: months-old behavior is not evidence about today's).
    /// </summary>
    public int StaleBaselineResetDays { get; set; } = 30;

    /// <summary>Cap on the risk-score contribution the learned model can add to the heuristic score for a single attempt.</summary>
    public int MaxLearnedRiskScore { get; set; } = 30;

    /// <summary>Constant risk-score contribution once a learned deviation crosses the sensitivity threshold (before z scaling).</summary>
    public int BaseLearnedRiskScore { get; set; } = 5;
}

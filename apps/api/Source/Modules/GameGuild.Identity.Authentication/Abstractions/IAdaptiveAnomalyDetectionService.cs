namespace GameGuild.Identity.Authentication;

/// <summary>
///     Origin of an adaptive anomaly assessment.
/// </summary>
public enum AdaptiveAnomalyOrigin
{
    /// <summary>
    ///     The subject has enough learned baseline observations for statistically meaningful
    ///     deviation scoring; the assessment carries learned z-scores.
    /// </summary>
    LearnedBaseline = 0,

    /// <summary>
    ///     The subject has not accumulated enough observations yet (or the persisted baseline
    ///     was reset after prolonged inactivity). The learned model deliberately abstains and
    ///     the fixed-weight heuristic analysis remains the only signal.
    /// </summary>
    ColdStart = 1,

    /// <summary>
    ///     The assessment could not be produced (persisted state unreadable, repository failure).
    ///     Learning is defense-in-depth: the failure is logged and the heuristic analysis decides alone.
    /// </summary>
    AssessmentFailed = 2
}

/// <summary>
///     Result of scoring one authentication attempt against the subject's learned behavioral baseline.
/// </summary>
public sealed record AdaptiveAnomalyAssessment
{
    public required AdaptiveAnomalyOrigin Origin { get; init; }

    /// <summary>True when at least one feature deviates from the learned baseline beyond the configured sensitivity.</summary>
    public bool IsLearnedDeviation { get; init; }

    /// <summary>Largest per-feature z-score observed for this attempt (0 when not learned).</summary>
    public double CombinedZScore { get; init; }

    /// <summary>Labels such as <c>Learned:HourOfDayDeviation</c> for every feature that breached the sensitivity threshold.</summary>
    public IReadOnlyList<string> DeviationLabels { get; init; } = [];

    /// <summary>Risk-score contribution (0..100 scale points) the learned model adds to the heuristic score.</summary>
    public int LearnedRiskScoreContribution { get; init; }

    /// <summary>Number of observations the baseline had accumulated when this attempt was scored.</summary>
    public int BaselineSampleCount { get; init; }

    public static AdaptiveAnomalyAssessment ColdStartFallback(int sampleCount) => new()
    {
        Origin = AdaptiveAnomalyOrigin.ColdStart,
        IsLearnedDeviation = false,
        CombinedZScore = 0,
        DeviationLabels = [],
        LearnedRiskScoreContribution = 0,
        BaselineSampleCount = sampleCount
    };
}

/// <summary>
///     Online statistical anomaly detection over login behavior. The service maintains per
///     subject (tenant + user, or tenant + identifier hash when the user id is unknown)
///     exponentially weighted moving-average baselines of login features — hour-of-day,
///     IP novelty, and attempt cadence — and scores each attempt by z-score deviation from
///     those learned baselines. This is statistical online learning (EWMA estimators), not
///     deep learning: there is no neural network and no offline training phase.
/// </summary>
public interface IAdaptiveAnomalyDetectionService
{
    /// <summary>
    ///     Scores one authentication attempt against the subject's learned baseline and then
    ///     folds the attempt into the baseline (score-then-learn). Never throws: assessment
    ///     failures fail open as <see cref="AdaptiveAnomalyOrigin.ColdStart" />-equivalent results.
    /// </summary>
    Task<AdaptiveAnomalyAssessment> AssessAsync(AuthenticationAttemptContext context, CancellationToken cancellationToken = default);
}

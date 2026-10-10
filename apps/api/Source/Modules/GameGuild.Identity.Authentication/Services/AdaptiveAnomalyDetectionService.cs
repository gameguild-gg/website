using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Adaptive anomaly detection over login behavior using online statistical learning.
///     Per subject (tenant + user id, or tenant + identifier digest when the user id is
///     unknown) the service maintains exponentially weighted moving-average (EWMA)
///     baselines of three login features and scores every attempt by z-score deviation:
///     <list type="bullet">
///         <item><b>hour-of-day</b> — circular mean and dispersion of login hours (wraparound 24h clock);</item>
///         <item><b>IP novelty</b> — information content (−log2 probability) of the source IP under a
///         decayed per-subject IP distribution; a recurring IP is unsurprising, a novel IP on a stable
///         baseline carries several bits of surprise;</item>
///         <item><b>attempt cadence</b> — mean and dispersion of log inter-arrival seconds (log space so
///         both bursts and abnormal gaps are visible).</item>
///     </list>
///     Honest scope note: this is statistical online learning (EWMA estimators with z-score
///     deviation tests), not deep learning — there is no neural network, no embedding, and no
///     offline training phase. Subjects with fewer than the configured minimum observations
///     (or whose baseline was reset after prolonged inactivity) are in cold start: the model
///     abstains and the fixed-weight heuristic analysis remains the only signal. Baselines are
///     persisted per subject so learning survives restarts; the EWMA decay itself is the drift
///     mechanism (gradual legitimate behavior change fades old statistics out of the baseline).
/// </summary>
public class AdaptiveAnomalyDetectionService(
    IAdaptiveBehaviorBaselineRepository baselineRepository,
    AdaptiveAnomalyDetectionOptions options,
    ILogger<AdaptiveAnomalyDetectionService> logger) : IAdaptiveAnomalyDetectionService
{
    public const string HourOfDayDeviationLabel = "Learned:HourOfDayDeviation";
    public const string IpNoveltyDeviationLabel = "Learned:IpNovelty";
    public const string CadenceDeviationLabel = "Learned:CadenceDeviation";

    private static readonly JsonSerializerOptions WeightJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AdaptiveAnomalyDetectionOptions _options = options;

    public async Task<AdaptiveAnomalyAssessment> AssessAsync(AuthenticationAttemptContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sampleCount = 0;
        try
        {
            var subjectKey = BuildSubjectKey(context.TenantId, context.UserId, context.Identifier);
            var baseline = await baselineRepository.GetBySubjectKeyAsync(subjectKey, cancellationToken).ConfigureAwait(false);

            // Cold start spans the whole learning window: until the baseline reaches the minimum
            // observation count the learned model has too little support to score against, so the
            // assessment must stay on the cold-start fallback while observations keep accumulating.
            var isColdStart = baseline is null
                || IsStale(baseline)
                || baseline.ObservationCount < _options.MinimumObservations;
            var state = baseline is null || IsStale(baseline) ? null : LoadState(baseline!);
            sampleCount = state?.ObservationCount ?? baseline?.ObservationCount ?? 0;

            var observedAt = context.AttemptedAt == default ? SystemClock.UtcNow : context.AttemptedAt;
            var assessment = isColdStart
                ? ScoreColdStart(sampleCount)
                : ScoreLearned(state!, context, observedAt);

            var updatedState = Learn(state, context, observedAt);
            await PersistAsync(subjectKey, context, updatedState, isColdStart, cancellationToken).ConfigureAwait(false);

            return assessment;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Learning is defense-in-depth: it must never break (or block) the primary risk analysis.
            logger.LogWarning(exception, "Adaptive anomaly assessment failed; falling back to the heuristic-only analysis");
            return new AdaptiveAnomalyAssessment
            {
                Origin = AdaptiveAnomalyOrigin.AssessmentFailed,
                IsLearnedDeviation = false,
                CombinedZScore = 0,
                DeviationLabels = [],
                LearnedRiskScoreContribution = 0,
                BaselineSampleCount = sampleCount
            };
        }
    }

    // ── Scoring ─────────────────────────────────────────────────────────

    private AdaptiveAnomalyAssessment ScoreColdStart(int sampleCount) =>
        AdaptiveAnomalyAssessment.ColdStartFallback(sampleCount);

    private AdaptiveAnomalyAssessment ScoreLearned(AdaptiveBaselineState state, AuthenticationAttemptContext context, DateTime observedAt)
    {
        var labels = new List<string>(3);
        var maxZ = 0.0;

        // Hour-of-day deviation (circular statistics on the 24h clock).
        var meanHour = CircularMeanHour(state.HourMeanX, state.HourMeanY);
        var hourDelta = WraparoundHourDistance(context.AttemptedAt.Hour + context.AttemptedAt.Minute / 60.0, meanHour);
        var hourStd = Math.Sqrt(state.HourMeanSquaredDeviation + _options.VarianceFloor);
        var hourZ = hourDelta / hourStd;
        TrackFeature(hourZ, HourOfDayDeviationLabel, labels, ref maxZ);

        // IP novelty: information content of this source IP under the learned per-subject IP distribution.
        var weights = ParseWeights(state.IpWeightsJson);
        var probability = ProbabilityOf(weights, DigestIp(context.IpAddress));
        var surprise = -Math.Log2(probability + 1e-6);
        var surpriseStd = Math.Sqrt(state.IpSurpriseMeanSquaredDeviation + _options.VarianceFloor);
        var ipZ = Math.Abs(surprise - state.IpSurpriseMean) / surpriseStd;
        TrackFeature(ipZ, IpNoveltyDeviationLabel, labels, ref maxZ);

        // Attempt cadence: deviation of the log inter-arrival gap.
        if (state.CadenceObservationCount > 0 && state.LastObservedAtUtc.HasValue)
        {
            var gapSeconds = Math.Clamp((observedAt - state.LastObservedAtUtc.Value).TotalSeconds, 1, 31_536_000);
            var logGap = Math.Log(gapSeconds);
            var cadenceStd = Math.Sqrt(state.CadenceLogSecondsMeanSquaredDeviation + _options.VarianceFloor);
            var cadenceZ = Math.Abs(logGap - state.CadenceLogSecondsMean) / cadenceStd;
            TrackFeature(cadenceZ, CadenceDeviationLabel, labels, ref maxZ);
        }

        var isLearned = state.ObservationCount >= _options.MinimumObservations && labels.Count > 0;
        var contribution = 0;
        if (isLearned)
        {
            var scaled = _options.BaseLearnedRiskScore
                         + (int)Math.Round((maxZ - _options.ZScoreThreshold) * 5, MidpointRounding.AwayFromZero);
            contribution = Math.Clamp(scaled, _options.BaseLearnedRiskScore, Math.Max(_options.BaseLearnedRiskScore, _options.MaxLearnedRiskScore));
        }

        return new AdaptiveAnomalyAssessment
        {
            Origin = AdaptiveAnomalyOrigin.LearnedBaseline,
            IsLearnedDeviation = isLearned,
            CombinedZScore = isLearned ? maxZ : 0,
            DeviationLabels = isLearned ? labels : [],
            LearnedRiskScoreContribution = contribution,
            BaselineSampleCount = state.ObservationCount
        };
    }

    private void TrackFeature(double zScore, string label, List<string> labels, ref double maxZ)
    {
        if (zScore < _options.ZScoreThreshold)
        {
            return;
        }

        labels.Add(label);
        maxZ = Math.Max(maxZ, zScore);
    }

    // ── Learning (state update with the current observation) ────────────

    private AdaptiveBaselineState Learn(AdaptiveBaselineState? previous, AuthenticationAttemptContext context, DateTime observedAt)
    {
        var alpha = Math.Clamp(_options.EwmaAlpha, 0.001, 1.0);
        var hourAngle = 2.0 * Math.PI * (context.AttemptedAt.Hour + context.AttemptedAt.Minute / 60.0) / 24.0;
        var state = previous ?? new AdaptiveBaselineState();

        // Hour-of-day circular statistics.
        state.HourMeanX = (1 - alpha) * state.HourMeanX + alpha * Math.Sin(hourAngle);
        state.HourMeanY = (1 - alpha) * state.HourMeanY + alpha * Math.Cos(hourAngle);
        var meanHour = CircularMeanHour(state.HourMeanX, state.HourMeanY);
        var hourDelta = WraparoundHourDistance(context.AttemptedAt.Hour + context.AttemptedAt.Minute / 60.0, meanHour);
        state.HourMeanSquaredDeviation = (1 - alpha) * state.HourMeanSquaredDeviation + alpha * hourDelta * hourDelta;

        // IP novelty distribution (decayed weights over a bounded IP digest set).
        var weights = ParseWeights(state.IpWeightsJson);
        foreach (var key in weights.Keys.ToList())
        {
            weights[key] *= 1 - alpha;
        }

        var ipDigest = DigestIp(context.IpAddress);
        weights[ipDigest] = weights.TryGetValue(ipDigest, out var current) ? current + 1.0 : 1.0;
        if (weights.Count > _options.MaxTrackedIps)
        {
            foreach (var evict in weights.OrderBy(pair => pair.Value).Take(weights.Count - _options.MaxTrackedIps).Select(pair => pair.Key).ToList())
            {
                weights.Remove(evict);
            }
        }

        var probability = ProbabilityOf(weights, ipDigest);
        var surprise = -Math.Log2(probability + 1e-6);
        if (state.ObservationCount == 0)
        {
            // Seed the surprise estimators: a single-observation history carries no information content.
            state.IpSurpriseMean = 0;
            state.IpSurpriseMeanSquaredDeviation = _options.VarianceFloor;
        }
        else
        {
            state.IpSurpriseMean = (1 - alpha) * state.IpSurpriseMean + alpha * surprise;
            state.IpSurpriseMeanSquaredDeviation =
                (1 - alpha) * state.IpSurpriseMeanSquaredDeviation + alpha * (surprise - state.IpSurpriseMean) * (surprise - state.IpSurpriseMean);
        }

        state.IpWeightsJson = SerializeWeights(weights);

        // Attempt cadence in log-seconds; the first observable gap seeds the estimator.
        if (state.LastObservedAtUtc.HasValue)
        {
            var gapSeconds = Math.Clamp((observedAt - state.LastObservedAtUtc.Value).TotalSeconds, 1, 31_536_000);
            var logGap = Math.Log(gapSeconds);
            if (state.CadenceObservationCount == 0)
            {
                state.CadenceLogSecondsMean = logGap;
                state.CadenceLogSecondsMeanSquaredDeviation = _options.VarianceFloor;
            }
            else
            {
                state.CadenceLogSecondsMean = (1 - alpha) * state.CadenceLogSecondsMean + alpha * logGap;
                state.CadenceLogSecondsMeanSquaredDeviation =
                    (1 - alpha) * state.CadenceLogSecondsMeanSquaredDeviation
                    + alpha * (logGap - state.CadenceLogSecondsMean) * (logGap - state.CadenceLogSecondsMean);
            }

            state.CadenceObservationCount++;
        }

        state.ObservationCount++;
        state.LastObservedAtUtc = observedAt;
        return state;
    }

    private async Task PersistAsync(
        string subjectKey,
        AuthenticationAttemptContext context,
        AdaptiveBaselineState state,
        bool createdNew,
        CancellationToken cancellationToken)
    {
        var now = SystemClock.UtcNow;
        var baseline = createdNew || state.PersistentId is null
            ? new AdaptiveBehaviorBaseline { Id = Guid.NewGuid() }
            : new AdaptiveBehaviorBaseline { Id = state.PersistentId.Value };

        baseline.TenantId = context.TenantId;
        baseline.UserId = context.UserId;
        baseline.SubjectKey = subjectKey;
        baseline.HourMeanX = state.HourMeanX;
        baseline.HourMeanY = state.HourMeanY;
        baseline.HourMeanSquaredDeviation = state.HourMeanSquaredDeviation;
        baseline.IpSurpriseMean = state.IpSurpriseMean;
        baseline.IpSurpriseMeanSquaredDeviation = state.IpSurpriseMeanSquaredDeviation;
        baseline.IpWeightsJson = state.IpWeightsJson;
        baseline.CadenceLogSecondsMean = state.CadenceLogSecondsMean;
        baseline.CadenceLogSecondsMeanSquaredDeviation = state.CadenceLogSecondsMeanSquaredDeviation;
        baseline.CadenceObservationCount = state.CadenceObservationCount;
        baseline.ObservationCount = state.ObservationCount;
        baseline.LastObservedAtUtc = state.LastObservedAtUtc;
        baseline.CreatedAt = now;
        baseline.UpdatedAt = now;

        await baselineRepository.UpsertAsync(baseline, cancellationToken).ConfigureAwait(false);
    }

    // ── Subject identity ────────────────────────────────────────────────

    /// <summary>
    ///     Builds the privacy-preserving subject key: SHA-256 over the tenant id and either the
    ///     user id or (when unknown) a digest of the raw identifier, so no identifier material
    ///     is persisted in the learned state.
    /// </summary>
    internal static string BuildSubjectKey(Guid? tenantId, Guid? userId, string identifier)
    {
        var identity = userId.HasValue
            ? $"user:{userId.Value:N}"
            : $"identifier:{Sha256Hex(identifier.ToLowerInvariant())}";
        return Sha256Hex($"tenant:{tenantId?.ToString("N") ?? "-"}|{identity}");
    }

    private bool IsStale(AdaptiveBehaviorBaseline baseline) =>
        baseline.LastObservedAtUtc.HasValue
        && baseline.LastObservedAtUtc.Value < SystemClock.UtcNow.AddDays(-Math.Max(1, _options.StaleBaselineResetDays));

    // ── Feature math ────────────────────────────────────────────────────

    private static double CircularMeanHour(double meanX, double meanY)
    {
        if (meanX == 0 && meanY == 0)
        {
            return 0;
        }

        var angle = Math.Atan2(meanX, meanY);
        if (angle < 0)
        {
            angle += 2 * Math.PI;
        }

        return angle * 24.0 / (2.0 * Math.PI);
    }

    private static double WraparoundHourDistance(double a, double b)
    {
        var delta = Math.Abs(a - b) % 24.0;
        return Math.Min(delta, 24.0 - delta);
    }

    private static double ProbabilityOf(Dictionary<string, double> weights, string ipDigest)
    {
        var total = 0.0;
        foreach (var value in weights.Values)
        {
            total += value;
        }

        if (total <= 0)
        {
            return 0;
        }

        return weights.TryGetValue(ipDigest, out var weight) ? weight / total : 0;
    }

    private static Dictionary<string, double> ParseWeights(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json, WeightJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            // A corrupt weight table is treated as an empty one; the distribution relearns.
            return [];
        }
    }

    private static string SerializeWeights(Dictionary<string, double> weights) =>
        JsonSerializer.Serialize(weights, WeightJsonOptions);

    internal static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Truncated digest of an IP address (privacy-preserving key for the learned IP distribution).</summary>
    private static string DigestIp(string? ipAddress) =>
        string.IsNullOrEmpty(ipAddress) ? "unknown" : Sha256Hex(ipAddress)[..16];

    // ── In-memory state ─────────────────────────────────────────────────

    private sealed class AdaptiveBaselineState
    {
        public Guid? PersistentId { get; init; }

        public double HourMeanX { get; set; }

        public double HourMeanY { get; set; }

        public double HourMeanSquaredDeviation { get; set; }

        public double IpSurpriseMean { get; set; }

        public double IpSurpriseMeanSquaredDeviation { get; set; }

        public string? IpWeightsJson { get; set; }

        public double CadenceLogSecondsMean { get; set; }

        public double CadenceLogSecondsMeanSquaredDeviation { get; set; }

        public int CadenceObservationCount { get; set; }

        public int ObservationCount { get; set; }

        public DateTime? LastObservedAtUtc { get; set; }
    }

    private static AdaptiveBaselineState LoadState(AdaptiveBehaviorBaseline baseline) => new()
    {
        PersistentId = baseline.Id,
        HourMeanX = baseline.HourMeanX,
        HourMeanY = baseline.HourMeanY,
        HourMeanSquaredDeviation = baseline.HourMeanSquaredDeviation,
        IpSurpriseMean = baseline.IpSurpriseMean,
        IpSurpriseMeanSquaredDeviation = baseline.IpSurpriseMeanSquaredDeviation,
        IpWeightsJson = baseline.IpWeightsJson,
        CadenceLogSecondsMean = baseline.CadenceLogSecondsMean,
        CadenceLogSecondsMeanSquaredDeviation = baseline.CadenceLogSecondsMeanSquaredDeviation,
        CadenceObservationCount = baseline.CadenceObservationCount,
        ObservationCount = baseline.ObservationCount,
        LastObservedAtUtc = baseline.LastObservedAtUtc
    };
}

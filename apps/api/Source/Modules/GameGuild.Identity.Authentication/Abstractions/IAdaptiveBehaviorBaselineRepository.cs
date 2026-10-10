namespace GameGuild.Identity.Authentication;

/// <summary>
///     Persistence for the learned per-subject behavioral baselines used by adaptive
///     anomaly detection. Baselines are loaded by subject key and saved after each
///     scored attempt (score-then-learn).
/// </summary>
public interface IAdaptiveBehaviorBaselineRepository
{
    /// <summary>Returns the learned baseline for the subject, or null when no baseline exists yet.</summary>
    Task<AdaptiveBehaviorBaseline?> GetBySubjectKeyAsync(string subjectKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Inserts a new baseline or updates the existing one for its subject key.
    /// </summary>
    Task<AdaptiveBehaviorBaseline> UpsertAsync(AdaptiveBehaviorBaseline baseline, CancellationToken cancellationToken = default);
}

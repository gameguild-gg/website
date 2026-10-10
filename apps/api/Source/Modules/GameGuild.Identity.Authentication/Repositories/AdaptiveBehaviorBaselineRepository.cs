using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository implementation for learned behavioral baselines (adaptive anomaly detection).
/// </summary>
public class AdaptiveBehaviorBaselineRepository(IApplicationDbContext context) : IAdaptiveBehaviorBaselineRepository
{
    private DbSet<AdaptiveBehaviorBaseline> Baselines { get => context.Set<AdaptiveBehaviorBaseline>(); }

    public async Task<AdaptiveBehaviorBaseline?> GetBySubjectKeyAsync(string subjectKey, CancellationToken cancellationToken = default)
    {
        return await Baselines
            .FirstOrDefaultAsync(baseline => baseline.SubjectKey == subjectKey, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdaptiveBehaviorBaseline> UpsertAsync(AdaptiveBehaviorBaseline baseline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        var existing = await Baselines
            .FirstOrDefaultAsync(candidate => candidate.SubjectKey == baseline.SubjectKey, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            baseline.Id = Guid.NewGuid();
            baseline.CreatedAt = SystemClock.UtcNow;
            baseline.UpdatedAt = baseline.CreatedAt;
            Baselines.Add(baseline);
        }
        else
        {
            // Copy the learned estimators onto the tracked instance (subject identity never changes for a key).
            existing.TenantId = baseline.TenantId;
            existing.UserId = baseline.UserId;
            existing.HourMeanX = baseline.HourMeanX;
            existing.HourMeanY = baseline.HourMeanY;
            existing.HourMeanSquaredDeviation = baseline.HourMeanSquaredDeviation;
            existing.IpSurpriseMean = baseline.IpSurpriseMean;
            existing.IpSurpriseMeanSquaredDeviation = baseline.IpSurpriseMeanSquaredDeviation;
            existing.IpWeightsJson = baseline.IpWeightsJson;
            existing.CadenceLogSecondsMean = baseline.CadenceLogSecondsMean;
            existing.CadenceLogSecondsMeanSquaredDeviation = baseline.CadenceLogSecondsMeanSquaredDeviation;
            existing.CadenceObservationCount = baseline.CadenceObservationCount;
            existing.ObservationCount = baseline.ObservationCount;
            existing.LastObservedAtUtc = baseline.LastObservedAtUtc;
            existing.UpdatedAt = SystemClock.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing ?? baseline;
    }
}

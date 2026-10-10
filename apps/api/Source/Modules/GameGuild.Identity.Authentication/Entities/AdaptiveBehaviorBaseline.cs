using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Learned behavioral baseline for one authentication subject (tenant + user id, or
///     tenant + identifier hash when the user id is unknown). Stores online EWMA estimators
///     for the login features used by adaptive anomaly detection:
///     <list type="bullet">
///         <item>hour-of-day: circular mean components and mean squared circular distance;</item>
///         <item>IP novelty: EWMA mean/variance of per-IP information content (surprise) plus a decayed IP weight table;</item>
///         <item>attempt cadence: EWMA mean/variance of log inter-arrival seconds.</item>
///     </list>
///     The raw identifier is never stored — <see cref="SubjectKey" /> is a SHA-256 digest and
///     the IP table holds truncated IP digests only.
/// </summary>
public class AdaptiveBehaviorBaseline
{
    public Guid Id { get; set; }

    /// <summary>Tenant the baseline belongs to (null for tenant-less attempts).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>User id when known; null for attempts against unknown identifiers.</summary>
    public Guid? UserId { get; set; }

    /// <summary>SHA-256 hex digest of the composite subject identity (tenant + user id or identifier hash). Unique.</summary>
    [Required]
    [MaxLength(64)]
    public string SubjectKey { get; set; } = string.Empty;

    // ── Hour-of-day (circular statistics over the 24h clock) ────────────

    /// <summary>EWMA of sin(2π·hour/24) for the observed login hours.</summary>
    public double HourMeanX { get; set; }

    /// <summary>EWMA of cos(2π·hour/24) for the observed login hours.</summary>
    public double HourMeanY { get; set; }

    /// <summary>EWMA of the squared circular distance between observed hours and the circular mean.</summary>
    public double HourMeanSquaredDeviation { get; set; }

    // ── IP novelty (information content of the observed source IP) ──────

    /// <summary>EWMA mean of per-attempt IP surprise, −log2(p(ip)).</summary>
    public double IpSurpriseMean { get; set; }

    /// <summary>EWMA of the squared deviation of IP surprise from <see cref="IpSurpriseMean" />.</summary>
    public double IpSurpriseMeanSquaredDeviation { get; set; }

    /// <summary>Decayed weight table over tracked IP digests, serialized as JSON (digest hex → weight).</summary>
    [MaxLength(2000)]
    public string? IpWeightsJson { get; set; }

    // ── Attempt cadence (log inter-arrival seconds) ─────────────────────

    /// <summary>EWMA mean of ln(seconds since the previous attempt).</summary>
    public double CadenceLogSecondsMean { get; set; }

    /// <summary>EWMA of the squared deviation of ln inter-arrival seconds from <see cref="CadenceLogSecondsMean" />.</summary>
    public double CadenceLogSecondsMeanSquaredDeviation { get; set; }

    /// <summary>Number of inter-arrival gaps folded into the cadence estimators (0 until a second attempt is seen).</summary>
    public int CadenceObservationCount { get; set; }

    // ── Bookkeeping ─────────────────────────────────────────────────────

    /// <summary>Total observations folded into this baseline (cold-start gate).</summary>
    public int ObservationCount { get; set; }

    /// <summary>UTC instant of the last attempt folded into the baseline (cadence reference and staleness gate).</summary>
    public DateTime? LastObservedAtUtc { get; set; }

    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    public DateTime UpdatedAt { get; set; } = SystemClock.UtcNow;
}

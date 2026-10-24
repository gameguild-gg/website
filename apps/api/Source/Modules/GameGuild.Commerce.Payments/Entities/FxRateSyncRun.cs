using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>Outcome values for an FX sync run.</summary>
public enum FxRateSyncRunStatus
{
    /// <summary>At least one provider returned a rate set that was persisted.</summary>
    Succeeded = 0,

    /// <summary>Primary and fallback providers both failed (after retries); nothing persisted.</summary>
    Failed = 1,

    /// <summary>A rate set was persisted, but the newest effective date is older than the staleness threshold.</summary>
    SucceededStale = 2
}

/// <summary>
///     Audit/persistence row for one FX rate sync attempt (issue #412 spec B). Stores the raw
///     provider response (raw-response storage requirement), success/failure for the
///     consecutive-failure alerting counter, and the staleness assessment of the newest data.
///     Platform-global (TenantId stays null).
/// </summary>
[Table("fx_rate_sync_runs")]
[Index(nameof(StartedAtUtc))]
[Index(nameof(Status))]
[Index(nameof(ProviderName))]
public class FxRateSyncRun : EntityBase
{
    /// <summary>Default constructor</summary>
    public FxRateSyncRun() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial run data</param>
    public FxRateSyncRun(object partial) : base(partial) { }

    /// <summary>Provider that produced the persisted rate set (the fallback when the primary failed).</summary>
    [Required]
    [MaxLength(50)]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Primary provider configured for the run (may differ from <see cref="ProviderName"/> when failover kicked in).</summary>
    [Required]
    [MaxLength(50)]
    public string PrimaryProviderName { get; set; } = string.Empty;

    /// <summary>Run start (UTC).</summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>Run completion (UTC).</summary>
    public DateTime CompletedAtUtc { get; set; }

    /// <summary>Outcome of the run.</summary>
    public FxRateSyncRunStatus Status { get; set; }

    /// <summary>Number of quotations persisted (inserts + updates).</summary>
    public int RatesApplied { get; set; }

    /// <summary>Number of quotations whose value actually changed (rate-change audit reference).</summary>
    public int RatesChanged { get; set; }

    /// <summary>Effective date of the rate set that was persisted.</summary>
    public DateTime? EffectiveDateUtc { get; set; }

    /// <summary>Consecutive failed runs including this one at the time it completed (0 on success).</summary>
    public int ConsecutiveFailureCount { get; set; }

    /// <summary>Failure reason when <see cref="Status"/> is <see cref="FxRateSyncRunStatus.Failed"/>.</summary>
    [MaxLength(4000)]
    public string? ErrorMessage { get; set; }

    /// <summary>Verbatim provider response body (raw-response storage for audit and replays).</summary>
    public string RawResponse { get; set; } = string.Empty;

    /// <summary>Content type of <see cref="RawResponse"/> (e.g. "application/json", "application/xml").</summary>
    [MaxLength(100)]
    public string RawResponseContentType { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Persisted FX quotation (issue #412 spec B): one row per (pair, effective date, provider,
///     source). Platform-global reference data (<see cref="EntityBase{TKey}.TenantId"/> stays
///     null) because FX rates are market facts, not tenant decisions. Append-mostly history:
///     re-syncing the same provider/date updates the row in place (rate-change audit entries
///     record the old → new value), so historical/effective-date lookups are direct queries.
/// </summary>
[Table("fx_rate_snapshots")]
[Index(nameof(BaseCurrency))]
[Index(nameof(QuoteCurrency))]
[Index(nameof(EffectiveDateUtc))]
[Index(nameof(ProviderName))]
[Index(nameof(IsOverride))]
[Index(nameof(BaseCurrency), nameof(QuoteCurrency), nameof(EffectiveDateUtc), nameof(ProviderName), nameof(Source), IsUnique = true)]
public class FxRateSnapshot : EntityBase
{
    /// <summary>Default constructor</summary>
    public FxRateSnapshot() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial snapshot data</param>
    public FxRateSnapshot(object partial) : base(partial) { }

    /// <summary>ISO 4217 code the rate converts from (always upper-case).</summary>
    [Required]
    [MaxLength(3)]
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>ISO 4217 code the rate converts to (always upper-case).</summary>
    [Required]
    [MaxLength(3)]
    public string QuoteCurrency { get; set; } = string.Empty;

    /// <summary>Quote units per one base unit; strictly positive.</summary>
    [Column(TypeName = "decimal(28,12)")]
    public decimal Rate { get; set; }

    /// <summary>
    ///     Effective date of the quotation (the provider's data date, UTC, truncated to the day).
    ///     Historical lookups filter on this; it is deliberately distinct from
    ///     <see cref="EntityBase{TKey}.CreatedAt"/> (sync time).
    /// </summary>
    public DateTime EffectiveDateUtc { get; set; }

    /// <summary>Moment (UTC) the provider quoted the rate — used for freshness/staleness auditing.</summary>
    public DateTime AsOfUtc { get; set; }

    /// <summary>Provider that produced the rate (e.g. "openexchangerates", "ecb", "manual").</summary>
    [Required]
    [MaxLength(50)]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Provenance label within the provider (e.g. "latest", "historical", "manual-override").</summary>
    [Required]
    [MaxLength(50)]
    public string Source { get; set; } = string.Empty;

    /// <summary>Sync run that persisted the rate; null for rows written by the admin override command.</summary>
    public Guid? SyncRunId { get; set; }

    /// <summary>Whether this row is an operator-entered manual override rather than a market rate.</summary>
    public bool IsOverride { get; set; }

    /// <summary>Optional expiry for manual overrides; null means effective until replaced. Ignored for market rows.</summary>
    public DateTime? ExpiresAtUtc { get; set; }
}

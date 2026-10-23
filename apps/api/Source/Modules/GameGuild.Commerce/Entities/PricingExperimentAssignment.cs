using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce;

/// <summary>
///     Durable (sticky) assignment of one bucket key to one experiment arm (issue #395).
///     Created once by deterministic hash allocation; every later calculation for the same
///     key resolves to the same variant so customers never see a price flicker.
/// </summary>
[Table("pricing_experiment_assignments")]
[Index(nameof(ExperimentId))]
[Index(nameof(VariantId))]
[Index(nameof(BucketKey))]
[Index(nameof(ExperimentId), nameof(BucketKey), IsUnique = true)]
public class PricingExperimentAssignment : EntityBase
{
    /// <summary>Default constructor</summary>
    public PricingExperimentAssignment() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial assignment data</param>
    public PricingExperimentAssignment(object partial) : base(partial) { }

    /// <summary>Foreign key to the experiment</summary>
    [Required]
    public Guid ExperimentId { get; set; }

    /// <summary>Navigation property to the experiment</summary>
    [ForeignKey(nameof(ExperimentId))]
    public virtual PricingExperiment Experiment { get; set; } = null!;

    /// <summary>Foreign key to the assigned variant</summary>
    [Required]
    public Guid VariantId { get; set; }

    /// <summary>Navigation property to the assigned variant</summary>
    [ForeignKey(nameof(VariantId))]
    public virtual PricingExperimentVariant Variant { get; set; } = null!;

    /// <summary>
    ///     Stable bucketing key (usually the acting user id). Hashed with the experiment id
    ///     to pick the arm; uniqueness is enforced per experiment.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string BucketKey { get; set; } = string.Empty;

    /// <summary>Optional user id correlation for per-user analytics</summary>
    public Guid? UserId { get; set; }

    /// <summary>When the key was first enrolled (UTC)</summary>
    public DateTime AssignedAt { get; set; } = SystemClock.UtcNow;
}

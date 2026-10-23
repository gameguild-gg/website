using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce;

/// <summary>
///     One monetized conversion under an experiment arm (issue #395): a final priced
///     outcome (checkout, subscription start) attributed to the arm the bucket key was
///     assigned to. Outcomes feed the revenue-impact analytics for the experiment.
/// </summary>
[Table("pricing_experiment_outcomes")]
[Index(nameof(ExperimentId))]
[Index(nameof(VariantId))]
[Index(nameof(OccurredAt))]
public class PricingExperimentOutcome : EntityBase
{
    /// <summary>Default constructor</summary>
    public PricingExperimentOutcome() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial outcome data</param>
    public PricingExperimentOutcome(object partial) : base(partial) { }

    /// <summary>Foreign key to the experiment</summary>
    [Required]
    public Guid ExperimentId { get; set; }

    /// <summary>Navigation property to the experiment</summary>
    [ForeignKey(nameof(ExperimentId))]
    public virtual PricingExperiment Experiment { get; set; } = null!;

    /// <summary>Foreign key to the variant the conversion is attributed to</summary>
    [Required]
    public Guid VariantId { get; set; }

    /// <summary>Navigation property to the variant</summary>
    [ForeignKey(nameof(VariantId))]
    public virtual PricingExperimentVariant Variant { get; set; } = null!;

    /// <summary>Optional link to the assignment that produced the conversion</summary>
    public Guid? AssignmentId { get; set; }

    /// <summary>Final monetized amount of the conversion</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>Currency of the conversion (ISO 4217)</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>Optional order correlation for audit</summary>
    public Guid? OrderId { get; set; }

    /// <summary>When the conversion occurred (UTC)</summary>
    public DateTime OccurredAt { get; set; } = SystemClock.UtcNow;
}

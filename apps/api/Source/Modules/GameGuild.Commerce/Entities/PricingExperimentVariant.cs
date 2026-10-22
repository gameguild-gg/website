using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce;

/// <summary>
///     One arm of a pricing A/B experiment (issue #395). The arm either pins a fixed
///     per-unit price or applies a discount percentage to the effective price; the control
///     arm carries neither so it prices at the unadjusted amount.
/// </summary>
[Table("pricing_experiment_variants")]
[Index(nameof(ExperimentId))]
[Index(nameof(Name))]
public class PricingExperimentVariant : EntityBase
{
    /// <summary>Default constructor</summary>
    public PricingExperimentVariant() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial variant data</param>
    public PricingExperimentVariant(object partial) : base(partial) { }

    /// <summary>Foreign key to the owning <see cref="PricingExperiment"/></summary>
    [Required]
    public Guid ExperimentId { get; set; }

    /// <summary>Navigation property to the owning experiment</summary>
    [ForeignKey(nameof(ExperimentId))]
    public virtual PricingExperiment Experiment { get; set; } = null!;

    /// <summary>Variant name (unique inside the experiment, e.g. "control", "treatment-a")</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether this arm is the control (baseline) arm</summary>
    public bool IsControl { get; set; }

    /// <summary>
    ///     Relative allocation weight of this arm. Weights are normalized across the
    ///     experiment's arms when bucketing (they need not sum to 100).
    /// </summary>
    [Range(1, 100)]
    public int AllocationPercentage { get; set; } = 50;

    /// <summary>Discount percentage applied to the effective price for this arm (0–100)</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercentage { get; set; }

    /// <summary>Fixed per-unit price pinned by this arm (alternative to the discount)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? FixedPrice { get; set; }
}

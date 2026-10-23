using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce;

/// <summary>
///     A pricing A/B experiment (issue #395): splits traffic for a product (or globally)
///     across <see cref="PricingExperimentVariant"/> arms, sticks each bucket key to one
///     variant via deterministic hash allocation, and records conversion outcomes so
///     revenue impact per arm can be analyzed against the control.
/// </summary>
[Table("pricing_experiments")]
[Index(nameof(ProductId))]
[Index(nameof(Status))]
[Index(nameof(StartDate))]
[Index(nameof(EndDate))]
public class PricingExperiment : EntityBase
{
    /// <summary>Default constructor</summary>
    public PricingExperiment() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial experiment data</param>
    public PricingExperiment(object partial) : base(partial) { }

    /// <summary>
    ///     Product this experiment prices differently (null = global experiment that applies
    ///     to every product calculation that carries an experiment bucket key).
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>Experiment name</summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description of the hypothesis under test</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Lifecycle status of the experiment</summary>
    public PricingExperimentStatus Status { get; set; } = PricingExperimentStatus.Draft;

    /// <summary>When the experiment starts enrolling (null = starts when set to Running)</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>When the experiment stops enrolling (null = runs until paused/completed)</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    ///     Percentage of bucket-key traffic enrolled (0–100); keys hashing above this share
    ///     are held out and priced without any experiment adjustment.
    /// </summary>
    [Range(0, 100)]
    public int TrafficPercentage { get; set; } = 100;

    /// <summary>Experiment arms (one must be flagged <see cref="PricingExperimentVariant.IsControl"/>)</summary>
    public virtual ICollection<PricingExperimentVariant> Variants { get; } = new List<PricingExperimentVariant>();

    /// <summary>
    ///     Whether the experiment enrolls at <paramref name="at"/>: status Running and, when
    ///     date bounds are set, inside the [StartDate, EndDate) window.
    /// </summary>
    public bool IsEnrolling(DateTime? at = null)
    {
        if (Status != PricingExperimentStatus.Running)
        {
            return false;
        }

        var now = at ?? SystemClock.UtcNow;
        if (StartDate.HasValue && now < StartDate.Value)
        {
            return false;
        }

        if (EndDate.HasValue && now >= EndDate.Value)
        {
            return false;
        }

        return true;
    }
}

/// <summary>Lifecycle statuses for a pricing experiment (issue #395).</summary>
public enum PricingExperimentStatus
{
    /// <summary>Created but not yet enrolling; can be edited freely.</summary>
    Draft = 0,

    /// <summary>Actively enrolling bucket keys and applying variant adjustments.</summary>
    Running = 1,

    /// <summary>Temporarily not enrolling; existing assignments stay sticky.</summary>
    Paused = 2,

    /// <summary>Finished; assignments and outcomes are retained for analysis.</summary>
    Completed = 3
}

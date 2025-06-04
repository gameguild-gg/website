using cms.Common.Entities;

namespace cms.Modules.Reputation.Models;

/// <summary>
/// Tracks a user's reputation score and level
/// </summary>
public class UserReputation : ResourceBase, IReputation
{
    /// <summary>
    /// The user this reputation belongs to
    /// </summary>
    public required Modules.User.Models.User User { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Current reputation score
    /// </summary>
    public int Score { get; set; } = 0;

    /// <summary>
    /// Current reputation level (linked to configurable level)
    /// </summary>
    public ReputationLevel? CurrentLevel { get; set; }
    public Guid? CurrentLevelId { get; set; }

    /// <summary>
    /// When the reputation was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user's reputation level was last recalculated
    /// </summary>
    public DateTime? LastLevelCalculation { get; set; }

    /// <summary>
    /// Number of positive reputation changes
    /// </summary>
    public int PositiveChanges { get; set; } = 0;

    /// <summary>
    /// Number of negative reputation changes
    /// </summary>
    public int NegativeChanges { get; set; } = 0;

    /// <summary>
    /// History of reputation changes for this user
    /// </summary>
    public ICollection<UserReputationHistory> History { get; set; } = new List<UserReputationHistory>();
}

using cms.Common.Entities;

namespace cms.Modules.Reputation.Models;

/// <summary>
/// Interface for reputation entities that can track reputation scores and levels
/// Supports polymorphic reputation system for users, user-tenants, and future entities
/// </summary>
public interface IReputation : IEntity
{
    /// <summary>
    /// Current reputation score
    /// </summary>
    int Score { get; set; }

    /// <summary>
    /// Current reputation tier (linked to configurable tier)
    /// </summary>
    ReputationTier? CurrentLevel { get; set; }
    Guid? CurrentLevelId { get; set; }

    /// <summary>
    /// When the reputation was last updated
    /// </summary>
    DateTime LastUpdated { get; set; }

    /// <summary>
    /// When the reputation tier was last recalculated
    /// </summary>
    DateTime? LastLevelCalculation { get; set; }

    /// <summary>
    /// Number of positive reputation changes
    /// </summary>
    int PositiveChanges { get; set; }

    /// <summary>
    /// Number of negative reputation changes
    /// </summary>
    int NegativeChanges { get; set; }

    /// <summary>
    /// History of reputation changes for this entity
    /// </summary>
    ICollection<UserReputationHistory> History { get; set; }
}

using cms.Common.Entities;

namespace cms.Modules.Reputation.Models;

/// <summary>
/// Tracks a user's reputation score and level within a specific tenant context
/// Supports tenant-specific reputation that is separate from global user reputation
/// </summary>
public class UserTenantReputation : ResourceBase, IReputation
{
    /// <summary>
    /// The user-tenant relationship this reputation belongs to
    /// </summary>
    public required Modules.Tenant.Models.UserTenant UserTenant { get; set; }
    public Guid UserTenantId { get; set; }

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
    /// History of reputation changes for this user-tenant
    /// </summary>
    public ICollection<UserReputationHistory> History { get; set; } = new List<UserReputationHistory>();
}

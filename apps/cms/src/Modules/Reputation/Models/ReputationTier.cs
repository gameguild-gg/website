using cms.Common.Entities;

namespace cms.Modules.Reputation.Models;

/// <summary>
/// Configurable reputation tier definition stored in the database
/// Allows dynamic configuration of reputation thresholds and permissions
/// </summary>
public class ReputationTier : ResourceBase
{
    /// <summary>
    /// Unique name/identifier for this reputation tier
    /// </summary>
    public required string Name
    {
        get;
        set;
    }

    /// <summary>
    /// Display name for this reputation tier
    /// </summary>
    public required string DisplayName
    {
        get;
        set;
    }

    /// <summary>
    /// Minimum score required to achieve this tier
    /// </summary>
    public int MinimumScore
    {
        get;
        set;
    }

    /// <summary>
    /// Maximum score for this tier (null means no upper limit)
    /// </summary>
    public int? MaximumScore
    {
        get;
        set;
    }

    /// <summary>
    /// Color or visual identifier for this tier (hex color, CSS class, etc.)
    /// </summary>
    public string? Color
    {
        get;
        set;
    }

    /// <summary>
    /// Icon or badge identifier for this tier
    /// </summary>
    public string? Icon
    {
        get;
        set;
    }

    /// <summary>
    /// Order/priority of this tier (lower numbers = higher priority)
    /// </summary>
    public int SortOrder
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this tier is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Users who have achieved this reputation tier
    /// </summary>
    public ICollection<UserReputation> UserReputations
    {
        get;
        set;
    } = new List<UserReputation>();
}

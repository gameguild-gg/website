using cms.Common.Entities;

namespace cms.Modules.Reputation.Models;

/// <summary>
/// Configurable reputation level definition stored in the database
/// Allows dynamic configuration of reputation thresholds and permissions
/// </summary>
public class ReputationLevel : ResourceBase
{
    /// <summary>
    /// Unique name/identifier for this reputation level
    /// </summary>
    public required string Name
    {
        get;
        set;
    }

    /// <summary>
    /// Display name for this reputation level
    /// </summary>
    public required string DisplayName
    {
        get;
        set;
    }

    /// <summary>
    /// Minimum score required to achieve this level
    /// </summary>
    public int MinimumScore
    {
        get;
        set;
    }

    /// <summary>
    /// Maximum score for this level (null means no upper limit)
    /// </summary>
    public int? MaximumScore
    {
        get;
        set;
    }

    /// <summary>
    /// Color or visual identifier for this level (hex color, CSS class, etc.)
    /// </summary>
    public string? Color
    {
        get;
        set;
    }

    /// <summary>
    /// Icon or badge identifier for this level
    /// </summary>
    public string? Icon
    {
        get;
        set;
    }

    /// <summary>
    /// Order/priority of this level (lower numbers = higher priority)
    /// </summary>
    public int SortOrder
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this level is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Users who have achieved this reputation level
    /// </summary>
    public ICollection<UserReputation> UserReputations
    {
        get;
        set;
    } = new List<UserReputation>();
}

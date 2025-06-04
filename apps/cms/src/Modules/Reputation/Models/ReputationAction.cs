using cms.Common.Entities;

namespace cms.Modules.Reputation.Models;

/// <summary>
/// Defines actions that can affect user reputation
/// Allows configurable reputation changes for different activities
/// </summary>
public class ReputationAction : ResourceBase
{
    /// <summary>
    /// Unique identifier for this action type
    /// </summary>
    public required string ActionType
    {
        get;
        set;
    }

    /// <summary>
    /// Display name for this action
    /// </summary>
    public required string DisplayName
    {
        get;
        set;
    }

    /// <summary>
    /// Description of what this action represents
    /// </summary>
    public new string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Points gained/lost when this action is performed
    /// </summary>
    public int Points
    {
        get;
        set;
    }

    /// <summary>
    /// Maximum number of times this action can award points per day
    /// (null for no limit)
    /// </summary>
    public int? DailyLimit
    {
        get;
        set;
    }

    /// <summary>
    /// Maximum number of times this action can award points total
    /// (null for no limit)
    /// </summary>
    public int? TotalLimit
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this action is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Minimum reputation level required to perform this action
    /// (null for no requirement)
    /// </summary>
    public ReputationLevel? RequiredLevel
    {
        get;
        set;
    }

    public Guid? RequiredLevelId
    {
        get;
        set;
    }

    /// <summary>
    /// History of when this action was performed
    /// </summary>
    public ICollection<UserReputationHistory> ReputationHistory
    {
        get;
        set;
    } = new List<UserReputationHistory>();
}

using cms.Common.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace cms.Modules.Reputation.Models;

/// <summary>
/// Tracks the history of reputation changes for any reputation entity
/// Provides audit trail and analytics for reputation system
/// Supports polymorphic relationships with UserReputation, UserTenantReputation, and future reputation entities
/// </summary>
public class UserReputationHistory : ResourceBase
{
    /// <summary>
    /// The user whose reputation changed (for direct user reputation tracking)
    /// </summary>
    public Modules.User.Models.User? User
    {
        get;
        set;
    }

    public Guid? UserId
    {
        get;
        set;
    }

    /// <summary>
    /// The user-tenant whose reputation changed (for tenant-specific reputation tracking)
    /// </summary>
    public Modules.Tenant.Models.UserTenant? UserTenant
    {
        get;
        set;
    }

    public Guid? UserTenantId
    {
        get;
        set;
    }

    /// <summary>
    /// Polymorphic reference to the reputation entity that changed
    /// This can point to UserReputation, UserTenantReputation, or any future IReputation implementation
    /// Note: This is a computed property for convenience - the actual relationship is handled
    /// through UserId (for UserReputation) or UserTenantId (for UserTenantReputation)
    /// </summary>
    [NotMapped]
    public IReputation? Reputation
    {
        get;
        set;
    }

    /// <summary>
    /// The action that caused this reputation change
    /// </summary>
    public ReputationAction? ReputationAction
    {
        get;
        set;
    }

    public Guid? ReputationActionId
    {
        get;
        set;
    }

    /// <summary>
    /// Points gained or lost in this change
    /// </summary>
    public int PointsChange
    {
        get;
        set;
    }

    /// <summary>
    /// User's reputation score before this change
    /// </summary>
    public int PreviousScore
    {
        get;
        set;
    }

    /// <summary>
    /// User's reputation score after this change
    /// </summary>
    public int NewScore
    {
        get;
        set;
    }

    /// <summary>
    /// Previous reputation tier (if different)
    /// </summary>
    public ReputationTier? PreviousLevel
    {
        get;
        set;
    }

    public Guid? PreviousLevelId
    {
        get;
        set;
    }

    /// <summary>
    /// New reputation tier (if changed)
    /// </summary>
    public ReputationTier? NewLevel
    {
        get;
        set;
    }

    public Guid? NewLevelId
    {
        get;
        set;
    }

    /// <summary>
    /// Optional reason for this reputation change
    /// </summary>
    public string? Reason
    {
        get;
        set;
    }

    /// <summary>
    /// User who triggered this change (null for system actions)
    /// </summary>
    public Modules.User.Models.User? TriggeredByUser
    {
        get;
        set;
    }

    public Guid? TriggeredByUserId
    {
        get;
        set;
    }

    /// <summary>
    /// Related resource that triggered this change (polymorphic relationship)
    /// Entity Framework will create a shadow RelatedResourceId foreign key property automatically
    /// </summary>
    public ResourceBase? RelatedResource
    {
        get;
        set;
    }

    /// <summary>
    /// When this reputation change occurred
    /// </summary>
    public DateTime OccurredAt
    {
        get;
        set;
    } = DateTime.UtcNow;
}

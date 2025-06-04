namespace cms.Common.Entities;

/// <summary>
/// Represents a follower of a followable entity.
/// </summary>
public class Follower : BaseEntity, ITenantable
{
    /// <summary>
    /// The user who is following
    /// </summary>
    public virtual cms.Modules.User.Models.User User
    {
        get;
        set;
    } = null!;

    public Guid UserId
    {
        get;
        set;
    }

    // Optional: Tenant property for ITenantable
    public virtual cms.Modules.Tenant.Models.Tenant? Tenant
    {
        get;
        set;
    }

    public bool IsGlobal => Tenant == null;
}

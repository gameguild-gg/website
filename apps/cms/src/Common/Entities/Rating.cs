namespace cms.Common.Entities;

/// <summary>
/// Represents a rating (e.g., 1-5 stars) on a rateable entity.
/// </summary>
public class Rating : BaseEntity, ITenantable
{
    public int Value
    {
        get;
        set;
    } // e.g., 1-5

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

    public virtual cms.Modules.Tenant.Models.Tenant? Tenant
    {
        get;
        set;
    }

    public bool IsGlobal => Tenant == null;
}

namespace cms.Common.Entities;

/// <summary>
/// Represents a vote (upvote/downvote) on a voteable entity.
/// </summary>
public class Vote : BaseEntity, ITenantable
{
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

    public int Value
    {
        get;
        set;
    } // 1 for upvote, -1 for downvote

    public virtual cms.Modules.Tenant.Models.Tenant? Tenant
    {
        get;
        set;
    }

    public bool IsGlobal => Tenant == null;
}

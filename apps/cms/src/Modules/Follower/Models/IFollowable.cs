namespace cms.Common.Entities;

/// <summary>
/// Interface for entities that can be followed by users.
/// </summary>
public interface IFollowable
{
    /// <summary>
    /// Gets the collection of followers for this entity.
    /// </summary>
    ICollection<Follower> Followers
    {
        get;
        set;
    }
}

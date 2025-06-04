namespace cms.Common.Entities;

/// <summary>
/// Interface for entities that support ratings (e.g., 1-5 stars).
/// </summary>
public interface IRateable
{
    /// <summary>
    /// Gets the collection of ratings for this entity.
    /// </summary>
    ICollection<Rating> Ratings
    {
        get;
        set;
    }
}

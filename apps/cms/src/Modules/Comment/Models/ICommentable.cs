namespace GameGuild.Common.Entities;

/// <summary>
/// Interface for entities that support comments.
/// </summary>
public interface ICommentable
{
    /// <summary>
    /// Gets the collection of comments for this entity.
    /// </summary>
    ICollection<Comment> Comments
    {
        get;
        set;
    }
}

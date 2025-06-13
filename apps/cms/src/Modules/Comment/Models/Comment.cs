using GameGuild.Modules.Comment.Models;

namespace GameGuild.Common.Entities;

/// <summary>
/// Represents a comment on a commentable entity.
/// </summary>
public class Comment : ResourceBase
{
    public string Content
    {
        get;
        set;
    } = string.Empty;
    
    /// <summary>
    /// Navigation to comment permissions
    /// </summary>
    public virtual ICollection<CommentPermission> Permissions { get; set; } = new List<CommentPermission>();
}

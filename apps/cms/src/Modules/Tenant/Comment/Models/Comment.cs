namespace cms.Common.Entities;

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
}

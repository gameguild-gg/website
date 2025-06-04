namespace cms.Common.Entities;

/// <summary>
/// Content type discriminator for polymorphic queries
/// Uses string-based content type names for flexibility
/// </summary>
public static class ContentTypes
{
    // todo: change the names here. nameof should come from the actual class names not the variables from the left side
    public const string UserProfile = nameof(UserProfile);

    public const string Post = nameof(Post);

    public const string Comment = nameof(Comment);

    public const string Forum = nameof(Forum);

    public const string Document = nameof(Document);

    public const string Media = nameof(Media);

    public const string Article = nameof(Article);

    public const string Course = nameof(Course);

    public const string Project = nameof(Project);
}

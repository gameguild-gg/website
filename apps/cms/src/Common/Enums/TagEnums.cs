using System.ComponentModel;

namespace GameGuild.Common.Enums;

public enum TagType
{
    [Description("Tag represents a learnable or teachable skill")]
    Skill,

    [Description("General subject matter or area of knowledge")]
    Topic,

    [Description("Specific technology, software, framework, or tool")]
    Technology,

    [Description("Indicates content difficulty level")]
    Difficulty,

    [Description("High-level grouping of content")]
    Category,

    [Description("Specific industry or sector")]
    Industry,

    [Description("Professional or industry certification")]
    Certification
}

public enum TagRelationshipType
{
    [Description("Tags that are related or complementary")]
    Related,

    [Description("Parent tag in a hierarchical structure")]
    Parent,

    [Description("Child tag in a hierarchical structure")]
    Child,

    [Description("Required prerequisite tag")]
    Requires,

    [Description("Suggested prerequisite or related tag")]
    Suggested
}

public enum CertificateTagRelationshipType
{
    [Description("Tag is required for this certificate")]
    Required,

    [Description("Tag is optional but recommended for this certificate")]
    Optional,

    [Description("Tag indicates skill mastery demonstrated by this certificate")]
    Demonstrates
}

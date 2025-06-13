using System.ComponentModel;

namespace GameGuild.Common.Enums;

public enum CertificateStatus
{
    [Description("Certificate is valid and can be verified")]
    Active,

    [Description("Certificate has reached its expiration date")]
    Expired,

    [Description("Certificate has been manually invalidated")]
    Revoked,

    [Description("Certificate is in the process of being issued")]
    Pending
}

public enum VerificationMethod
{
    [Description("Certificate can be verified using a unique code")]
    Code,

    [Description("Certificate is verified through blockchain anchoring")]
    Blockchain,

    [Description("Certificate uses both code and blockchain verification")]
    Both
}

public enum CertificateType
{
    [Description("Standard certificate for completing a single program")]
    ProgramCompletion,

    [Description("Certificate for completing all programs in a product bundle/specialization")]
    ProductBundleCompletion,

    [Description("Certificate for achieving specific skill combinations across multiple programs/products")]
    LearningPathway,

    [Description("Certificate recognizing mastery of specific skills")]
    SkillMastery,

    [Description("Certificate for attending workshops, conferences, or events")]
    EventParticipation,

    [Description("Certificate from passing standardized tests or evaluations")]
    AssessmentPassed,

    [Description("Certificate for successfully completing projects")]
    ProjectCompletion,

    [Description("Certificate for completing a series of related programs")]
    Specialization,

    [Description("Industry-recognized professional certification")]
    Professional,

    [Description("Certificate for specific accomplishments or milestones")]
    Achievement,

    [Description("Certification to teach specific subjects")]
    Instructor,

    [Description("Certificate proving time spent learning a topic")]
    TimeInvestment,

    [Description("Certificate based on peer validation or community contribution")]
    PeerRecognition
}

public enum SkillProficiencyLevel
{
    [Description("Basic awareness and understanding of concepts")]
    Awareness,

    [Description("Can perform basic tasks with guidance")]
    Novice,

    [Description("Can perform routine tasks independently")]
    Beginner,

    [Description("Can handle most tasks and some complex scenarios")]
    Intermediate,

    [Description("Can handle complex tasks and mentor others")]
    Advanced,

    [Description("Deep expertise, can innovate and teach")]
    Expert,

    [Description("Recognized authority, can define best practices")]
    Master
}

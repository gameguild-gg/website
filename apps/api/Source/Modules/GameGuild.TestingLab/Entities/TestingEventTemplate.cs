using GameGuild.Identity.Users;

namespace GameGuild.TestingLab;

[Table("testing_event_templates")]
public sealed class TestingEventTemplate : EntityBase<Guid>
{
    [Required, MaxLength(255)]
    public string Name { get; private set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; private set; }

    public int CurrentRevisionNumber { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    public ICollection<TestingEventTemplateRevision> Revisions { get; private set; } = new List<TestingEventTemplateRevision>();

    public TestingEventTemplateRevision CurrentRevision => Revisions.Single(revision => revision.RevisionNumber == CurrentRevisionNumber);

    private TestingEventTemplate() { }

    public static TestingEventTemplate Create(
        Guid tenantId,
        string name,
        string generalRules,
        string candidateInstructions,
        string testerInstructions,
        QuestionnaireSchema projectApplicationSchema,
        QuestionnaireSchema testerRegistrationSchema,
        TestingEventMode defaultMode,
        TestingEventApprovalMode defaultApprovalMode,
        bool defaultRequiresFeedback,
        Guid createdByUserId,
        string? description = null)
    {
        if (tenantId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Tenant and creator are required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name is required.", nameof(name));
        }

        var template = new TestingEventTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = Normalize(description),
            CurrentRevisionNumber = 1
        };
        template.Revisions.Add(TestingEventTemplateRevision.Create(
            template.Id, tenantId, 1, generalRules, candidateInstructions, testerInstructions,
            projectApplicationSchema, testerRegistrationSchema, defaultMode, defaultApprovalMode,
            defaultRequiresFeedback, createdByUserId));
        return template;
    }

    public TestingEventTemplateRevision CreateRevision(
        string generalRules,
        string candidateInstructions,
        string testerInstructions,
        QuestionnaireSchema projectApplicationSchema,
        QuestionnaireSchema testerRegistrationSchema,
        TestingEventMode defaultMode,
        TestingEventApprovalMode defaultApprovalMode,
        bool defaultRequiresFeedback,
        Guid createdByUserId,
        string? name = null,
        string? description = null)
    {
        if (ArchivedAt.HasValue)
        {
            throw new InvalidOperationException("Archived templates cannot be revised.");
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (description != null)
        {
            Description = Normalize(description);
        }

        var revisionNumber = CurrentRevisionNumber + 1;
        var revision = TestingEventTemplateRevision.Create(
            Id, TenantId!.Value, revisionNumber, generalRules, candidateInstructions, testerInstructions,
            projectApplicationSchema, testerRegistrationSchema, defaultMode, defaultApprovalMode,
            defaultRequiresFeedback, createdByUserId);
        Revisions.Add(revision);
        CurrentRevisionNumber = revisionNumber;
        Touch();
        return revision;
    }

    public void Archive()
    {
        if (ArchivedAt.HasValue)
        {
            return;
        }

        ArchivedAt = SystemClock.UtcNow;
        Touch();
    }

    public void RestoreArchivedTemplate()
    {
        ArchivedAt = null;
        Touch();
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

[Table("testing_event_template_revisions")]
[Index(nameof(TemplateId), nameof(RevisionNumber), IsUnique = true)]
public sealed class TestingEventTemplateRevision : EntityBase<Guid>
{
    public Guid TemplateId { get; private set; }
    public TestingEventTemplate Template { get; private set; } = null!;
    public int RevisionNumber { get; private set; }
    [Required, MaxLength(20000)] public string GeneralRules { get; private set; } = string.Empty;
    [Required, MaxLength(20000)] public string CandidateInstructions { get; private set; } = string.Empty;
    [Required, MaxLength(20000)] public string TesterInstructions { get; private set; } = string.Empty;
    [Required] public string ProjectApplicationSchemaJson { get; private set; } = string.Empty;
    [Required] public string TesterRegistrationSchemaJson { get; private set; } = string.Empty;
    public TestingEventMode DefaultMode { get; private set; }
    public TestingEventApprovalMode DefaultApprovalMode { get; private set; }
    public bool DefaultRequiresFeedback { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public User CreatedByUser { get; private set; } = null!;

    public QuestionnaireSchema ProjectApplicationSchema => QuestionnaireSchema.FromJson(ProjectApplicationSchemaJson);
    public QuestionnaireSchema TesterRegistrationSchema => QuestionnaireSchema.FromJson(TesterRegistrationSchemaJson);

    private TestingEventTemplateRevision() { }

    internal static TestingEventTemplateRevision Create(
        Guid templateId,
        Guid tenantId,
        int revisionNumber,
        string generalRules,
        string candidateInstructions,
        string testerInstructions,
        QuestionnaireSchema projectApplicationSchema,
        QuestionnaireSchema testerRegistrationSchema,
        TestingEventMode defaultMode,
        TestingEventApprovalMode defaultApprovalMode,
        bool defaultRequiresFeedback,
        Guid createdByUserId)
    {
        if (new[] { templateId, tenantId, createdByUserId }.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Template, tenant and creator are required.");
        }

        if (revisionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        }

        EnsureContent(generalRules, candidateInstructions, testerInstructions);
        return new TestingEventTemplateRevision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateId = templateId,
            RevisionNumber = revisionNumber,
            GeneralRules = generalRules.Trim(),
            CandidateInstructions = candidateInstructions.Trim(),
            TesterInstructions = testerInstructions.Trim(),
            ProjectApplicationSchemaJson = projectApplicationSchema.ToJson(),
            TesterRegistrationSchemaJson = testerRegistrationSchema.ToJson(),
            DefaultMode = defaultMode,
            DefaultApprovalMode = defaultApprovalMode,
            DefaultRequiresFeedback = defaultRequiresFeedback,
            CreatedByUserId = createdByUserId
        };
    }

    internal static void EnsureContent(string generalRules, string candidateInstructions, string testerInstructions)
    {
        if (string.IsNullOrWhiteSpace(generalRules) ||
            string.IsNullOrWhiteSpace(candidateInstructions) ||
            string.IsNullOrWhiteSpace(testerInstructions))
        {
            throw new ArgumentException("General rules and candidate/tester instructions are required.");
        }
    }
}

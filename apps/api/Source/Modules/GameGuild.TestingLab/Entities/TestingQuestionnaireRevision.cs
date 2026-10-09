namespace GameGuild.TestingLab;

public sealed record TestingProjectBrief(
    string TestObjective,
    string InstallationAndAccess,
    IReadOnlyList<string> TestTasks,
    string Controls,
    string KnownLimitations,
    IReadOnlyList<string>? Links = null)
{
    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(TestObjective) ||
            string.IsNullOrWhiteSpace(InstallationAndAccess) ||
            string.IsNullOrWhiteSpace(Controls) ||
            string.IsNullOrWhiteSpace(KnownLimitations) ||
            TestTasks == null || TestTasks.Count == 0 || TestTasks.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Test objective, installation/access, tasks, controls, and known limitations are required.");
        }

        if (TestTasks.Count > 50)
        {
            throw new ArgumentException("A test brief cannot contain more than 50 tasks.");
        }

        if ((Links ?? []).Any(link => !Uri.TryCreate(link, UriKind.Absolute, out _)))
        {
            throw new ArgumentException("Every test brief link must be an absolute URL.");
        }
    }
}

[Table("testing_questionnaire_revisions")]
[Index(nameof(ApplicationId), nameof(RevisionNumber), IsUnique = true)]
public sealed class TestingQuestionnaireRevision : EntityBase<Guid>
{
    public Guid ApplicationId { get; private set; }
    public int RevisionNumber { get; private set; }
    [Required] public string SchemaJson { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }

    [NotMapped]
    public QuestionnaireSchema Schema => QuestionnaireSchema.FromJson(SchemaJson, ensureValid: false);

    private TestingQuestionnaireRevision() { }

    public static TestingQuestionnaireRevision Create(
        Guid applicationId,
        int revisionNumber,
        QuestionnaireSchema schema,
        Guid createdByUserId,
        Guid? tenantId,
        bool ensureValid = true)
    {
        if (applicationId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Application and creator are required.");
        }

        if (revisionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        }

        if (ensureValid)
        {
            schema.EnsureValid();
        }

        return new TestingQuestionnaireRevision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            RevisionNumber = revisionNumber,
            SchemaJson = schema.ToJson(ensureValid),
            CreatedByUserId = createdByUserId
        };
    }
}

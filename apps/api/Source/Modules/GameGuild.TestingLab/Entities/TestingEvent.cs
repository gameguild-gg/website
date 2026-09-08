using GameGuild.Identity.Users;

namespace GameGuild.TestingLab;

[Table("testing_events")]
public sealed class TestingEvent : EntityBase
{
    [Required, MaxLength(255)]
    public string Name { get; private set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; private set; }

    public TestingEventMode Mode { get; private set; }

    public TestingEventApprovalMode ApprovalMode { get; private set; }

    public TestingEventStatus Status { get; private set; } = TestingEventStatus.Draft;

    public Guid ManagerUserId { get; private set; }

    public User Manager { get; private set; } = null!;

    public DateTime ApplicationsOpenAt { get; private set; }

    public DateTime ApplicationsCloseAt { get; private set; }

    public DateTime StartsAt { get; private set; }

    public DateTime EndsAt { get; private set; }

    [Required, MaxLength(100)]
    public string TimeZoneId { get; private set; } = "UTC";

    public Guid? RecurrenceSeriesId { get; private set; }

    public int? RecurrenceOccurrence { get; private set; }

    public TestingEventRecurrenceFrequency? RecurrenceFrequency { get; private set; }

    public int? RecurrenceInterval { get; private set; }

    [MaxLength(64)]
    public string? RecurrenceDaysOfWeek { get; private set; }

    public DateTime? RecurrenceEndsAt { get; private set; }

    public int? RecurrenceOccurrenceCount { get; private set; }

    public bool RequiresFeedback { get; private set; }

    public Guid? SourceTemplateId { get; private set; }
    public Guid? SourceTemplateRevisionId { get; private set; }

    [MaxLength(20000)]
    public string? GeneralRules { get; private set; }

    [MaxLength(20000)]
    public string? CandidateInstructions { get; private set; }

    [MaxLength(20000)]
    public string? TesterInstructions { get; private set; }

    public string? ProjectApplicationSchemaJson { get; private set; }
    public string? TesterRegistrationSchemaJson { get; private set; }
    public DateTime? ConfigurationFrozenAt { get; private set; }

    [NotMapped]
    public QuestionnaireSchema? ProjectApplicationSchema => string.IsNullOrWhiteSpace(ProjectApplicationSchemaJson)
        ? null
        : QuestionnaireSchema.FromJson(ProjectApplicationSchemaJson);

    [NotMapped]
    public QuestionnaireSchema? TesterRegistrationSchema => string.IsNullOrWhiteSpace(TesterRegistrationSchemaJson)
        ? null
        : QuestionnaireSchema.FromJson(TesterRegistrationSchemaJson);

    public TestingLearningCompletionRequirement LearningCompletionRequirement { get; private set; }

    public Guid? CourseId { get; private set; }

    public Guid? CohortId { get; private set; }

    public Guid? LearningActivityId { get; private set; }

    [MaxLength(64)]
    public string? ReminderDaysBeforeOverride { get; private set; }

    [MaxLength(128)]
    public string SentReminderDays { get; private set; } = string.Empty;

    [MaxLength(1000)]
    public string? CancellationReason { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public ICollection<TestingEventSlot> Slots { get; private set; } = new List<TestingEventSlot>();

    public ICollection<TestingProjectApplication> Applications { get; private set; } = new List<TestingProjectApplication>();

    public ICollection<TestingCommitteeMember> CommitteeMembers { get; private set; } = new List<TestingCommitteeMember>();

    private TestingEvent()
    {
    }

    public static TestingEvent Create(
        string name,
        TestingEventMode mode,
        Guid managerUserId,
        DateTime applicationsOpenAt,
        DateTime applicationsCloseAt,
        DateTime startsAt,
        DateTime endsAt,
        bool requiresFeedback,
        TestingEventApprovalMode approvalMode,
        Guid? tenantId,
        string? description = null,
        Guid? recurrenceSeriesId = null,
        int? recurrenceOccurrence = null,
        TestingEventRecurrenceFrequency? recurrenceFrequency = null,
        int? recurrenceInterval = null,
        string? recurrenceDaysOfWeek = null,
        DateTime? recurrenceEndsAt = null,
        int? recurrenceOccurrenceCount = null,
        string timeZoneId = "UTC")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Event name is required.", nameof(name));
        if (managerUserId == Guid.Empty) throw new ArgumentException("Manager is required.", nameof(managerUserId));
        if (applicationsCloseAt <= applicationsOpenAt) throw new ArgumentException("Application window must end after it opens.");
        if (startsAt < applicationsCloseAt) throw new ArgumentException("Event must start after applications close.");
        if (endsAt <= startsAt) throw new ArgumentException("Event must end after it starts.");
        var validatedTimeZoneId = ValidateTimeZoneId(timeZoneId);

        return new TestingEvent
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Mode = mode,
            ManagerUserId = managerUserId,
            ApplicationsOpenAt = applicationsOpenAt,
            ApplicationsCloseAt = applicationsCloseAt,
            StartsAt = startsAt,
            EndsAt = endsAt,
            TimeZoneId = validatedTimeZoneId,
            RequiresFeedback = requiresFeedback,
            ApprovalMode = approvalMode,
            TenantId = tenantId,
            RecurrenceSeriesId = recurrenceSeriesId,
            RecurrenceOccurrence = recurrenceOccurrence,
            RecurrenceFrequency = recurrenceFrequency,
            RecurrenceInterval = recurrenceInterval,
            RecurrenceDaysOfWeek = recurrenceDaysOfWeek,
            RecurrenceEndsAt = recurrenceEndsAt,
            RecurrenceOccurrenceCount = recurrenceOccurrenceCount,
        };
    }

    public void Update(
        string name,
        string? description,
        TestingEventMode mode,
        TestingEventApprovalMode approvalMode,
        DateTime applicationsOpenAt,
        DateTime applicationsCloseAt,
        DateTime startsAt,
        DateTime endsAt,
        bool requiresFeedback,
        string timeZoneId = "UTC")
    {
        if (Status is TestingEventStatus.Active or TestingEventStatus.Completed or TestingEventStatus.Cancelled)
            throw new InvalidOperationException("Active or terminal events cannot be edited.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Event name is required.", nameof(name));
        if (applicationsCloseAt <= applicationsOpenAt) throw new ArgumentException("Application window must end after it opens.");
        if (startsAt < applicationsCloseAt) throw new ArgumentException("Event must start after applications close.");
        if (endsAt <= startsAt) throw new ArgumentException("Event must end after it starts.");
        var validatedTimeZoneId = ValidateTimeZoneId(timeZoneId);

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Mode = mode;
        ApprovalMode = approvalMode;
        ApplicationsOpenAt = applicationsOpenAt;
        ApplicationsCloseAt = applicationsCloseAt;
        StartsAt = startsAt;
        EndsAt = endsAt;
        TimeZoneId = validatedTimeZoneId;
        RequiresFeedback = requiresFeedback;
        Touch();
    }

    private static string ValidateTimeZoneId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException("A valid time zone is required.", nameof(timeZoneId));
        var normalized = timeZoneId.Trim();
        if (normalized.Length > 100)
            throw new ArgumentException("The time zone identifier cannot exceed 100 characters.", nameof(timeZoneId));
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalized);
            return normalized;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException("A valid time zone is required.", nameof(timeZoneId));
        }
        catch (InvalidTimeZoneException)
        {
            throw new ArgumentException("A valid time zone is required.", nameof(timeZoneId));
        }
    }
    public void OpenApplications()
    {
        if (Status != TestingEventStatus.Draft) throw new InvalidOperationException("Only draft events can open applications.");
        EnsureConfigurationCanFreeze();
        ConfigurationFrozenAt = SystemClock.UtcNow;
        Status = TestingEventStatus.ApplicationsOpen;
        Touch();
    }

    public void Configure(
        string generalRules,
        string candidateInstructions,
        string testerInstructions,
        QuestionnaireSchema projectApplicationSchema,
        QuestionnaireSchema testerRegistrationSchema)
    {
        EnsureDraftConfiguration();
        TestingEventTemplateRevision.EnsureContent(generalRules, candidateInstructions, testerInstructions);
        GeneralRules = generalRules.Trim();
        CandidateInstructions = candidateInstructions.Trim();
        TesterInstructions = testerInstructions.Trim();
        ProjectApplicationSchemaJson = projectApplicationSchema.ToJson();
        TesterRegistrationSchemaJson = testerRegistrationSchema.ToJson();
        Touch();
    }

    public void ConfigureFromTemplate(TestingEventTemplateRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        EnsureDraftConfiguration();
        if (TenantId != revision.TenantId)
            throw new InvalidOperationException("Template revision must belong to the event tenant.");
        SourceTemplateId = revision.TemplateId;
        SourceTemplateRevisionId = revision.Id;
        Mode = revision.DefaultMode;
        ApprovalMode = revision.DefaultApprovalMode;
        RequiresFeedback = revision.DefaultRequiresFeedback;
        Configure(
            revision.GeneralRules,
            revision.CandidateInstructions,
            revision.TesterInstructions,
            revision.ProjectApplicationSchema,
            revision.TesterRegistrationSchema);
    }

    private void EnsureDraftConfiguration()
    {
        if (Status != TestingEventStatus.Draft || ConfigurationFrozenAt.HasValue)
            throw new InvalidOperationException("Event configuration can only be edited while the event is a draft.");
    }

    private void EnsureConfigurationCanFreeze()
    {
        if (string.IsNullOrWhiteSpace(GeneralRules) ||
            string.IsNullOrWhiteSpace(CandidateInstructions) ||
            string.IsNullOrWhiteSpace(TesterInstructions))
            throw new InvalidOperationException("General rules and candidate/tester instructions are required.");
        if (string.IsNullOrWhiteSpace(ProjectApplicationSchemaJson) || string.IsNullOrWhiteSpace(TesterRegistrationSchemaJson))
            throw new InvalidOperationException("Valid project application and tester registration schemas are required.");
        QuestionnaireSchema.FromJson(ProjectApplicationSchemaJson);
        QuestionnaireSchema.FromJson(TesterRegistrationSchemaJson);
    }

    public void CloseApplications()
    {
        if (Status != TestingEventStatus.ApplicationsOpen) throw new InvalidOperationException("Applications are not open.");
        Status = TestingEventStatus.ApplicationsClosed;
        Touch();
    }

    public void Schedule()
    {
        if (Status != TestingEventStatus.ApplicationsClosed)
            throw new InvalidOperationException("Only events with closed applications can be scheduled.");
        Status = TestingEventStatus.Scheduled;
        Touch();
    }

    public void Activate()
    {
        if (Status != TestingEventStatus.Scheduled)
            throw new InvalidOperationException("Only scheduled events can become active.");
        Status = TestingEventStatus.Active;
        Touch();
    }

    public void Complete()
    {
        if (Status != TestingEventStatus.Active)
            throw new InvalidOperationException("Only active events can be completed.");
        Status = TestingEventStatus.Completed;
        Touch();
    }

    public void Cancel(string reason)
    {
        if (Status is TestingEventStatus.Completed or TestingEventStatus.Cancelled)
            throw new InvalidOperationException("Completed or cancelled events cannot be cancelled.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A cancellation reason is required.", nameof(reason));
        Status = TestingEventStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledAt = SystemClock.UtcNow;
        Touch();
    }

    public void SetReminderOverride(int[]? daysBefore)
    {
        ReminderDaysBeforeOverride = daysBefore is { Length: > 0 }
            ? string.Join(',', daysBefore.Where(day => day is > 0 and <= 30).Distinct().OrderBy(day => day))
            : null;
        Touch();
    }

    public void MarkReminderSent(int daysBefore)
    {
        var sent = SentReminderDaysCsv.ToList();
        if (!sent.Contains(daysBefore))
        {
            sent.Add(daysBefore);
            SentReminderDays = string.Join(',', sent.OrderBy(day => day));
            Touch();
        }
    }

    private List<int> SentReminderDaysCsv => SentReminderDays
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part => int.TryParse(part, out var day) ? day : 0)
        .Where(day => day > 0)
        .ToList();

    public bool HasReminderBeenSent(int daysBefore) => SentReminderDaysCsv.Contains(daysBefore);

    public void ConfigureLearning(
        Guid courseId,
        Guid? cohortId,
        Guid learningActivityId,
        TestingLearningCompletionRequirement requirement)
    {
        if (courseId == Guid.Empty || learningActivityId == Guid.Empty)
            throw new ArgumentException("Course and learning activity are required.");
        if (Status is TestingEventStatus.Active or TestingEventStatus.Completed or TestingEventStatus.Cancelled)
            throw new InvalidOperationException("Active or terminal events cannot change their Learning configuration.");
        const TestingLearningCompletionRequirement supported =
            TestingLearningCompletionRequirement.Attendance |
            TestingLearningCompletionRequirement.FeedbackSubmitted |
            TestingLearningCompletionRequirement.ProjectPresented;
        if (requirement == TestingLearningCompletionRequirement.None ||
            (requirement & ~supported) != TestingLearningCompletionRequirement.None)
            throw new ArgumentOutOfRangeException(
                nameof(requirement), requirement, "At least one supported Learning requirement is required.");
        CourseId = courseId;
        CohortId = cohortId;
        LearningActivityId = learningActivityId;
        LearningCompletionRequirement = requirement;
        Touch();
    }
}

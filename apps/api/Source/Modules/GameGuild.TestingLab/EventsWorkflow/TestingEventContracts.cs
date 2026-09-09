using System.Text.Json.Serialization;
using GameGuild.CQRS;

namespace GameGuild.TestingLab;

public enum TestingEventRecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly,
}

public sealed record TestingEventRecurrenceRequest(
    TestingEventRecurrenceFrequency Frequency,
    int Interval,
    IReadOnlyList<DayOfWeek>? DaysOfWeek,
    DateTime? EndsAt,
    int? OccurrenceCount);

public sealed record TestingEventProjection(
    Guid Id,
    string Name,
    string? Description,
    TestingEventMode Mode,
    TestingEventApprovalMode ApprovalMode,
    TestingEventStatus Status,
    Guid ManagerUserId,
    DateTime ApplicationsOpenAt,
    DateTime ApplicationsCloseAt,
    DateTime StartsAt,
    DateTime EndsAt,
    bool RequiresFeedback,
    TestingLearningCompletionRequirement LearningCompletionRequirement,
    Guid? CourseId,
    Guid? CohortId,
    Guid? LearningActivityId,
    Guid? TenantId,
    int SlotCount,
    int ApplicationCount,
    Guid? RecurrenceSeriesId = null,
    int? RecurrenceOccurrence = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    TestingEventRecurrenceFrequency? RecurrenceFrequency = null,
    int? RecurrenceInterval = null,
    IReadOnlyList<DayOfWeek>? RecurrenceDaysOfWeek = null,
    DateTime? RecurrenceEndsAt = null,
    int? RecurrenceOccurrenceCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    TestingEventConfigurationProjection? Configuration = null,
    string TimeZoneId = "UTC");

public sealed record TestingEventConfigurationProjection(
    Guid? SourceTemplateId,
    Guid? SourceTemplateRevisionId,
    string GeneralRules,
    string CandidateInstructions,
    string TesterInstructions,
    QuestionnaireSchema ProjectApplicationSchema,
    QuestionnaireSchema TesterRegistrationSchema,
    DateTime? FrozenAt);

public sealed record TestingEventSlotProjection(
    Guid Id,
    Guid EventId,
    Guid? LocationId,
    TestingEventMode Mode,
    DateTime StartsAt,
    DateTime EndsAt,
    int? MaxTesters,
    int? MaxProjects,
    string? CampusName,
    string? RoomName,
    string? MeetingUrl,
    int ApprovedProjectCount,
    int RegisteredTesterCount);

public sealed record PublicTestingEventSlotProjection(
    Guid Id,
    Guid EventId,
    TestingEventMode Mode,
    DateTime StartsAt,
    DateTime EndsAt,
    int? MaxTesters,
    int? MaxProjects,
    string? CampusName,
    string? RoomName,
    int ApprovedProjectCount,
    int RegisteredTesterCount)
{
    public int? AvailableTesterCount => MaxTesters.HasValue
        ? Math.Max(0, MaxTesters.Value - RegisteredTesterCount)
        : null;

    public int? AvailableProjectCount => MaxProjects.HasValue
        ? Math.Max(0, MaxProjects.Value - ApprovedProjectCount)
        : null;
}

public sealed record PublicTestingEventProjection(
    Guid Id,
    string Name,
    string? Description,
    TestingEventMode Mode,
    TestingEventApprovalMode ApprovalMode,
    TestingEventStatus Status,
    DateTime ApplicationsOpenAt,
    DateTime ApplicationsCloseAt,
    DateTime StartsAt,
    DateTime EndsAt,
    bool RequiresFeedback,
    int ApplicationCount,
    IReadOnlyList<PublicTestingEventSlotProjection> Slots,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    TestingEventConfigurationProjection? Configuration = null,
    string TimeZoneId = "UTC");

public sealed record CreateTestingEventCommand(
    string Name,
    string? Description,
    TestingEventMode Mode,
    TestingEventApprovalMode ApprovalMode,
    DateTime ApplicationsOpenAt,
    DateTime ApplicationsCloseAt,
    DateTime StartsAt,
    DateTime EndsAt,
    bool RequiresFeedback,
    TestingEventRecurrenceRequest? Recurrence = null,
    Guid? TemplateRevisionId = null,
    ConfigureTestingEventRequest? Configuration = null,
    string TimeZoneId = "UTC") : ICommand<Result<TestingEventProjection>>;

public sealed record ConfigureTestingEventCommand(
    Guid EventId,
    string GeneralRules,
    string CandidateInstructions,
    string TesterInstructions,
    QuestionnaireSchema ProjectApplicationSchema,
    QuestionnaireSchema TesterRegistrationSchema) : ICommand<Result<TestingEventProjection>>;

public sealed record UpdateTestingEventCommand(
    Guid EventId,
    string Name,
    string? Description,
    TestingEventMode Mode,
    TestingEventApprovalMode ApprovalMode,
    DateTime ApplicationsOpenAt,
    DateTime ApplicationsCloseAt,
    DateTime StartsAt,
    DateTime EndsAt,
    bool RequiresFeedback,
    string TimeZoneId = "UTC") : ICommand<Result<TestingEventProjection>>;

public sealed record DeleteTestingEventCommand(Guid EventId) : ICommand<Result<bool>>;

public sealed record ArchiveTestingEventCommand(Guid EventId) : ICommand<Result<bool>>;

public sealed record RestoreTestingEventCommand(Guid EventId) : ICommand<Result<bool>>;

public sealed record OpenTestingEventApplicationsCommand(Guid EventId) : ICommand<Result<TestingEventProjection>>;

public sealed record CloseTestingEventApplicationsCommand(Guid EventId) : ICommand<Result<TestingEventProjection>>;

public sealed record ScheduleTestingEventCommand(Guid EventId) : ICommand<Result<TestingEventProjection>>;

public sealed record ActivateTestingEventCommand(Guid EventId) : ICommand<Result<TestingEventProjection>>;

public sealed record CompleteTestingEventCommand(Guid EventId) : ICommand<Result<TestingEventProjection>>;

public sealed record CancelTestingEventCommand(Guid EventId, string Reason) : ICommand<Result<TestingEventProjection>>;

public sealed record ConfigureTestingEventLearningCommand(
    Guid EventId,
    Guid CourseId,
    Guid? CohortId,
    Guid LearningActivityId,
    TestingLearningCompletionRequirement Requirement) : ICommand<Result<TestingEventProjection>>;

public sealed record ConfigureTestingEventLearningRequest(
    Guid CourseId,
    Guid? CohortId,
    Guid LearningActivityId,
    TestingLearningCompletionRequirement Requirement);

public sealed record CreateTestingEventSlotCommand(
    Guid EventId,
    TestingEventMode Mode,
    DateTime StartsAt,
    DateTime EndsAt,
    int? MaxTesters,
    int? MaxProjects,
    string? CampusName,
    string? RoomName,
    string? MeetingUrl,
    Guid? LocationId = null) : ICommand<Result<TestingEventSlotProjection>>;

public sealed record UpdateTestingEventSlotCommand(
    Guid EventId,
    Guid SlotId,
    TestingEventMode Mode,
    DateTime StartsAt,
    DateTime EndsAt,
    int? MaxTesters,
    int? MaxProjects,
    string? CampusName,
    string? RoomName,
    string? MeetingUrl,
    Guid? LocationId = null) : ICommand<Result<TestingEventSlotProjection>>;

public sealed record DeleteTestingEventSlotCommand(Guid EventId, Guid SlotId) : ICommand<Result<bool>>;

public sealed record GetTestingEventQuery(Guid EventId) : IQuery<Result<TestingEventProjection>>;

public sealed record GetTestingEventsQuery(
    TestingEventStatus? Status = null,
    int Skip = 0,
    int Take = 50) : IQuery<Result<IReadOnlyList<TestingEventProjection>>>;

public sealed record GetArchivedTestingEventsQuery(
    int Skip = 0,
    int Take = 50) : IQuery<Result<IReadOnlyList<TestingEventProjection>>>;

public sealed record GetPublicTestingEventsQuery(
    int Skip = 0,
    int Take = 50) : IQuery<Result<IReadOnlyList<PublicTestingEventProjection>>>;

public sealed record GetPublicTestingEventQuery(Guid EventId) : IQuery<Result<PublicTestingEventProjection>>;

public sealed record GetTestingEventSlotsQuery(Guid EventId) : IQuery<Result<IReadOnlyList<TestingEventSlotProjection>>>;

public sealed record TestingEventCommitteeMemberProjection(
    Guid Id,
    Guid EventId,
    Guid UserId,
    string UserName,
    string UserEmail,
    bool IsChair,
    bool IsActive);

public sealed record AddTestingEventCommitteeMemberCommand(
    Guid EventId,
    Guid UserId,
    bool IsChair) : ICommand<Result<TestingEventCommitteeMemberProjection>>;

public sealed record RemoveTestingEventCommitteeMemberCommand(
    Guid EventId,
    Guid UserId) : ICommand<Result<bool>>;

public sealed record GetTestingEventCommitteeQuery(
    Guid EventId) : IQuery<Result<IReadOnlyList<TestingEventCommitteeMemberProjection>>>;

public sealed record CreateTestingEventRequest(
    string Name,
    string? Description,
    TestingEventMode Mode,
    TestingEventApprovalMode ApprovalMode,
    DateTime ApplicationsOpenAt,
    DateTime ApplicationsCloseAt,
    DateTime StartsAt,
    DateTime EndsAt,
    bool RequiresFeedback,
    TestingEventRecurrenceRequest? Recurrence = null,
    Guid? TemplateRevisionId = null,
    ConfigureTestingEventRequest? Configuration = null,
    string TimeZoneId = "UTC");

public sealed record ConfigureTestingEventRequest(
    string GeneralRules,
    string CandidateInstructions,
    string TesterInstructions,
    QuestionnaireSchema ProjectApplicationSchema,
    QuestionnaireSchema TesterRegistrationSchema);

public sealed record UpdateTestingEventRequest(
    string Name,
    string? Description,
    TestingEventMode Mode,
    TestingEventApprovalMode ApprovalMode,
    DateTime ApplicationsOpenAt,
    DateTime ApplicationsCloseAt,
    DateTime StartsAt,
    DateTime EndsAt,
    bool RequiresFeedback,
    string TimeZoneId = "UTC");

public sealed record UpsertTestingEventSlotRequest(
    TestingEventMode Mode,
    DateTime StartsAt,
    DateTime EndsAt,
    int? MaxTesters,
    int? MaxProjects,
    string? CampusName,
    string? RoomName,
    string? MeetingUrl,
    Guid? LocationId = null);
public sealed record CancelTestingEventRequest(string Reason);

public sealed record AddTestingEventCommitteeMemberRequest(Guid UserId, bool IsChair);

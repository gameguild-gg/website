
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;
using System.Text.Json;
using GameGuild.Learning.Courses;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Represents an assessment (quiz, assignment, project, or peer/self review) within a course.
/// </summary>
public class Assessment : EntityBase
{
    public Guid CourseId { get; private set; }
    public Guid? ContentId { get; private set; } // Optional: linked to specific content
    public Guid? AssessmentGroupId { get; private set; }
    public Guid? GroupSetId { get; private set; }
    public Guid? RubricId { get; private set; }
    public AssessmentGroup? AssessmentGroup { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public AssessmentType Type { get; private set; }
    public ScoreValue MaxScore { get; private set; } = ScoreValue.FromUnits(10_000);
    public ScoreValue PassingScore { get; private set; } = ScoreValue.Zero;
    public int? TimeLimitMinutes { get; private set; }
    public int MaxAttempts { get; private set; } = 1;
    public bool IsRequired { get; private set; }
    public int Order { get; private set; }
    public DateTime? AvailableFrom { get; private set; }
    public DateTime? AvailableUntil { get; private set; }
    public DateTime? DueAt { get; private set; }
    public bool AllowLateSubmissions { get; private set; }
    public DateTime? LateSubmissionDeadline { get; private set; }
    public SubmissionModality SubmissionModalities { get; private set; } = SubmissionModality.Text;
    public AssessmentPresentationMode PresentationMode { get; private set; } = AssessmentPresentationMode.SingleStep;
    public ReviewMethods ReviewMethods { get; private set; } = ReviewMethods.InstructorReview;
    public string? ReviewConfigurationCanonicalJson { get; private set; }
    public AttemptContributionMode? AttemptContributionMode { get; private set; }
    public ContentCompletionMode ContentCompletionMode { get; private set; } = ContentCompletionMode.OnReleaseAndPass;
    public ResultReleaseMode ResultReleaseMode { get; private set; } = ResultReleaseMode.Manual;
    public DateTime? ResultReleaseScheduledFor { get; private set; }
    public Guid? PublishedDefinitionRevisionId { get; private set; }
    public ICollection<InteractiveVideoAssessmentCue> InteractiveVideoCues { get; private set; } = new List<InteractiveVideoAssessmentCue>();

    private Assessment() { } // EF Core

    public static Assessment Create(
        Guid courseId,
        string title,
        AssessmentType type,
        ScoreValue maxScore,
        bool isRequired = true,
        Guid? assessmentGroupId = null,
        Guid? contentId = null,
        ReviewMethods reviewMethods = ReviewMethods.InstructorReview,
        string? slug = null)
    {
        ValidateMaxScore(maxScore);

        return new Assessment
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            ContentId = contentId,
            AssessmentGroupId = assessmentGroupId,
            Title = title,
            Slug = string.IsNullOrWhiteSpace(slug) ? title.ToSlugCase() : slug,
            Type = type,
            MaxScore = maxScore,
            IsRequired = isRequired,
            Order = 0,
            ReviewMethods = reviewMethods.EnsureValid(allowDraft: true)
        };
    }

    public bool IsAvailable()
    {
        return TryGetSubmissionTiming(SystemClock.UtcNow, out _);
    }

    public void SetDescription(string? description)
    {
        Description = description;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetMaxScore(ScoreValue maxScore)
    {
        ValidateMaxScore(maxScore);
        if (PassingScore.CompareTo(maxScore) > 0)
            throw new ArgumentOutOfRangeException(nameof(maxScore), "Maximum score cannot be lower than passing score.");
        MaxScore = maxScore;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetPassingScore(ScoreValue passingScore)
    {
        if (passingScore.CompareTo(MaxScore) > 0)
            throw new ArgumentOutOfRangeException(nameof(passingScore), "Passing score cannot exceed maximum score.");
        PassingScore = passingScore;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetTimeLimit(int? timeLimitMinutes)
    {
        TimeLimitMinutes = timeLimitMinutes;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetMaxAttempts(int maxAttempts)
    {
        if (maxAttempts != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "The initial grading runtime supports exactly one attempt.");
        }
        MaxAttempts = maxAttempts;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetAvailability(DateTime? availableFrom, DateTime? availableUntil)
    {
        SetDeliverySchedule(availableFrom, availableUntil, DueAt, AllowLateSubmissions, LateSubmissionDeadline);
    }

    public void SetDeliveryContract(SubmissionModality submissionModalities, AssessmentPresentationMode presentationMode)
    {
        ValidateSubmissionModalities(submissionModalities);
        if (!Enum.IsDefined(presentationMode))
        {
            throw new ArgumentOutOfRangeException(nameof(presentationMode), presentationMode, "Presentation mode is not supported.");
        }

        SubmissionModalities = submissionModalities;
        PresentationMode = presentationMode;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetDeliverySchedule(
        DateTime? availableFrom,
        DateTime? availableUntil,
        DateTime? dueAt,
        bool allowLateSubmissions,
        DateTime? lateSubmissionDeadline)
    {
        ValidateDeliverySchedule(availableFrom, availableUntil, dueAt, allowLateSubmissions, lateSubmissionDeadline);

        AvailableFrom = availableFrom;
        AvailableUntil = availableUntil;
        DueAt = dueAt;
        AllowLateSubmissions = allowLateSubmissions;
        LateSubmissionDeadline = lateSubmissionDeadline;
        UpdatedAt = SystemClock.UtcNow;
    }

    public bool TryGetSubmissionTiming(DateTime submittedAt, out bool isLate)
    {
        var timestamp = submittedAt.ToUniversalTime();
        if (AvailableFrom.HasValue && timestamp < AvailableFrom.Value)
        {
            isLate = false;
            return false;
        }

        if (AvailableUntil.HasValue && timestamp > AvailableUntil.Value)
        {
            isLate = false;
            return false;
        }

        isLate = DueAt.HasValue && timestamp > DueAt.Value;
        if (!isLate)
        {
            return true;
        }

        return AllowLateSubmissions &&
               LateSubmissionDeadline.HasValue &&
               timestamp <= LateSubmissionDeadline.Value;
    }

    public InteractiveVideoAssessmentCue AddInteractiveVideoCue(
        Guid contentId,
        string cueId,
        decimal? cuePositionSeconds = null)
    {
        var cue = InteractiveVideoAssessmentCue.Create(Id, contentId, cueId, cuePositionSeconds);
        InteractiveVideoCues.Add(cue);
        UpdatedAt = SystemClock.UtcNow;
        return cue;
    }

    public void AssignToGroup(Guid? assessmentGroupId)
    {
        AssessmentGroupId = assessmentGroupId;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void AssignToGroupSet(Guid? groupSetId)
    {
        GroupSetId = groupSetId;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void AssignRubric(Guid? rubricId)
    {
        RubricId = rubricId;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetReviewPolicy(
        ReviewMethods reviewMethods,
        string? reviewConfigurationCanonicalJson,
        AttemptContributionMode? attemptContributionMode,
        ContentCompletionMode contentCompletionMode,
        ResultReleaseMode resultReleaseMode,
        DateTime? resultReleaseScheduledFor)
    {
        ReviewMethods = reviewMethods.EnsureValid(allowDraft: true);
        if (resultReleaseMode == ResultReleaseMode.Scheduled && !resultReleaseScheduledFor.HasValue)
            throw new ArgumentException("Scheduled release requires a UTC instant.", nameof(resultReleaseScheduledFor));
        if (resultReleaseMode != ResultReleaseMode.Scheduled && resultReleaseScheduledFor.HasValue)
            throw new ArgumentException("A release instant is only valid for scheduled release.", nameof(resultReleaseScheduledFor));

        ReviewConfigurationCanonicalJson = GradingContractValidator.NormalizeReviewConfiguration(
            ReviewMethods,
            reviewConfigurationCanonicalJson);
        AttemptContributionMode = attemptContributionMode;
        ContentCompletionMode = contentCompletionMode;
        ResultReleaseMode = resultReleaseMode;
        ResultReleaseScheduledFor = resultReleaseScheduledFor?.ToUniversalTime();
        UpdatedAt = SystemClock.UtcNow;
    }

    public int GetRequiredPeerReviewCount()
    {
        if (!ReviewMethods.HasFlag(ReviewMethods.PeerReview)) return 0;
        if (string.IsNullOrWhiteSpace(ReviewConfigurationCanonicalJson)) return 1;
        using var document = JsonDocument.Parse(ReviewConfigurationCanonicalJson);
        return document.RootElement.TryGetProperty("peer", out var peer) &&
               peer.TryGetProperty("reviewsRequiredPerSubmission", out var count) &&
               count.TryGetInt32(out var value) && value > 0
            ? value
            : 1;
    }

    public void PublishRevision(Guid revisionId, int expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("Assessment version is stale.");
        if (revisionId == Guid.Empty) throw new ArgumentException("Revision ID is required.", nameof(revisionId));
        PublishedDefinitionRevisionId = revisionId;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void UnpublishRevision(Guid expectedRevisionId, int expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("Assessment version is stale.");
        if (PublishedDefinitionRevisionId is null)
            throw new InvalidOperationException("Assessment does not have an active revision.");
        if (PublishedDefinitionRevisionId != expectedRevisionId)
            throw new InvalidOperationException("The active revision changed before unpublish.");
        PublishedDefinitionRevisionId = null;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Update(
        string? title,
        string? description,
        bool clearDescription,
        ScoreValue? maxScore,
        ScoreValue? passingScore,
        int? timeLimitMinutes,
        bool clearTimeLimitMinutes,
        int? maxAttempts,
        bool? isRequired,
        DateTime? availableFrom,
        bool clearAvailableFrom,
        DateTime? availableUntil,
        bool clearAvailableUntil,
        Guid? contentId = null,
        bool clearContentId = false,
        Guid? assessmentGroupId = null,
        bool clearAssessmentGroupId = false,
        SubmissionModality? submissionModalities = null,
        AssessmentPresentationMode? presentationMode = null,
        DateTime? dueAt = null,
        bool clearDueAt = false,
        bool? allowLateSubmissions = null,
        DateTime? lateSubmissionDeadline = null,
        bool clearLateSubmissionDeadline = false,
        ReviewMethods? reviewMethods = null,
        Guid? groupSetId = null,
        bool clearGroupSetId = false,
        string? reviewConfigurationCanonicalJson = null,
        AttemptContributionMode? attemptContributionMode = null,
        ContentCompletionMode? contentCompletionMode = null,
        ResultReleaseMode? resultReleaseMode = null,
        DateTime? resultReleaseScheduledFor = null,
        string? slug = null)
    {
        if (title != null) Title = title;
        if (!string.IsNullOrWhiteSpace(slug)) Slug = slug;
        if (clearDescription) Description = null;
        else if (description is not null) Description = description;
        var nextMaxScore = maxScore ?? MaxScore;
        ValidateMaxScore(nextMaxScore);
        MaxScore = nextMaxScore;
        PassingScore = passingScore ?? PassingScore;
        if (PassingScore.CompareTo(MaxScore) > 0)
            throw new ArgumentOutOfRangeException(nameof(passingScore), "Passing score cannot exceed maximum score.");
        if (clearTimeLimitMinutes) TimeLimitMinutes = null;
        else if (timeLimitMinutes.HasValue) TimeLimitMinutes = timeLimitMinutes;
        if (maxAttempts.HasValue) SetMaxAttempts(maxAttempts.Value);
        if (isRequired.HasValue) IsRequired = isRequired.Value;
        var nextDueAt = clearDueAt ? null : dueAt ?? DueAt;
        var nextLateSubmissionDeadline = clearLateSubmissionDeadline
            ? null
            : lateSubmissionDeadline ?? LateSubmissionDeadline;
        SetDeliverySchedule(
            clearAvailableFrom ? null : availableFrom ?? AvailableFrom,
            clearAvailableUntil ? null : availableUntil ?? AvailableUntil,
            nextDueAt,
            allowLateSubmissions ?? AllowLateSubmissions,
            nextLateSubmissionDeadline);
        // The content link is immutable once set: content-created assessments
        // can never be unlinked or re-pointed at different content.
        if (ContentId.HasValue &&
            (clearContentId || (contentId.HasValue && contentId.Value != ContentId.Value)))
        {
            throw new ArgumentException(
                "Assessment cannot be unlinked from its content. Content link is permanent once set.");
        }
        if (clearContentId) ContentId = null;
        else if (contentId.HasValue) ContentId = contentId.Value;
        if (clearAssessmentGroupId) AssessmentGroupId = null;
        else if (assessmentGroupId.HasValue) AssessmentGroupId = assessmentGroupId.Value;
        if (submissionModalities.HasValue || presentationMode.HasValue)
        {
            SetDeliveryContract(
                submissionModalities ?? SubmissionModalities,
                presentationMode ?? PresentationMode);
        }
        if (reviewMethods.HasValue || reviewConfigurationCanonicalJson is not null || attemptContributionMode.HasValue ||
            contentCompletionMode.HasValue || resultReleaseMode.HasValue || resultReleaseScheduledFor.HasValue)
            SetReviewPolicy(
                reviewMethods ?? ReviewMethods,
                reviewConfigurationCanonicalJson ?? ReviewConfigurationCanonicalJson,
                attemptContributionMode ?? AttemptContributionMode,
                contentCompletionMode ?? ContentCompletionMode,
                resultReleaseMode ?? ResultReleaseMode,
                resultReleaseScheduledFor ?? ResultReleaseScheduledFor);
        if (clearGroupSetId) GroupSetId = null;
        else if (groupSetId.HasValue) GroupSetId = groupSetId.Value;
        UpdatedAt = SystemClock.UtcNow;
    }

    private static void ValidateSubmissionModalities(SubmissionModality submissionModalities)
    {
        const SubmissionModality supported = SubmissionModality.Text |
                                               SubmissionModality.File |
                                               SubmissionModality.Url |
                                               SubmissionModality.Code |
                                               SubmissionModality.Media |
                                               SubmissionModality.Project |
                                               SubmissionModality.StructuredAnswer;
        if (submissionModalities == SubmissionModality.None || (submissionModalities & ~supported) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(submissionModalities), submissionModalities, "At least one supported submission modality is required.");
        }
    }

    private static void ValidateMaxScore(ScoreValue maxScore)
    {
        if (maxScore.CompareTo(ScoreValue.Zero) <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxScore), "Maximum score must be greater than zero.");
        }
    }

    private static void ValidateDeliverySchedule(
        DateTime? availableFrom,
        DateTime? availableUntil,
        DateTime? dueAt,
        bool allowLateSubmissions,
        DateTime? lateSubmissionDeadline)
    {
        if (availableFrom.HasValue && availableUntil.HasValue && availableUntil.Value < availableFrom.Value)
        {
            throw new ArgumentException("Assessment availability end must be on or after availability start.", nameof(availableUntil));
        }

        if (dueAt.HasValue && availableFrom.HasValue && dueAt.Value < availableFrom.Value)
        {
            throw new ArgumentException("Assessment due date must be on or after availability start.", nameof(dueAt));
        }

        if (dueAt.HasValue && availableUntil.HasValue && dueAt.Value > availableUntil.Value)
        {
            throw new ArgumentException("Assessment due date must be on or before availability end.", nameof(dueAt));
        }

        if (allowLateSubmissions && !dueAt.HasValue)
        {
            throw new ArgumentException("A due date is required when late submissions are allowed.", nameof(dueAt));
        }

        if (allowLateSubmissions && !lateSubmissionDeadline.HasValue)
        {
            throw new ArgumentException("Late submission deadline is required when late submissions are allowed.", nameof(lateSubmissionDeadline));
        }

        if (lateSubmissionDeadline.HasValue && (!allowLateSubmissions || !dueAt.HasValue || lateSubmissionDeadline.Value <= dueAt.Value))
        {
            throw new ArgumentException("Late submission deadline must be after the due date when late submissions are allowed.", nameof(lateSubmissionDeadline));
        }

        if (lateSubmissionDeadline.HasValue && availableUntil.HasValue && lateSubmissionDeadline.Value > availableUntil.Value)
        {
            throw new ArgumentException("Late submission deadline must be on or before availability end.", nameof(lateSubmissionDeadline));
        }
    }
}

/// <summary>
/// Groups graded activities into weighted gradebook buckets for a course.
/// </summary>
public class AssessmentGroup : EntityBase
{
    public Guid CourseId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public PercentValue WeightPercent { get; private set; } = PercentValue.Zero;
    public int Order { get; private set; }

    private AssessmentGroup() { } // EF Core

    public static AssessmentGroup Create(
        Guid courseId,
        string name,
        PercentValue weightPercent,
        int order = 0,
        string? description = null)
    {
        ValidateName(name);
        ValidateWeight(weightPercent);

        return new AssessmentGroup
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = name.Trim(),
            Description = NormalizeDescription(description),
            WeightPercent = weightPercent,
            Order = order
        };
    }

    public void Update(string? name, string? description, PercentValue? weightPercent, int? order)
    {
        if (name != null)
        {
            ValidateName(name);
            Name = name.Trim();
        }

        if (description != null)
        {
            Description = NormalizeDescription(description);
        }

        if (weightPercent.HasValue)
        {
            ValidateWeight(weightPercent.Value);
            WeightPercent = weightPercent.Value;
        }

        if (order.HasValue)
        {
            Order = order.Value;
        }

        UpdatedAt = SystemClock.UtcNow;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Assessment group name is required.", nameof(name));
        }
    }

    private static void ValidateWeight(PercentValue weightPercent)
    {
        if (weightPercent.CompareTo(PercentValue.Zero) < 0 || weightPercent.CompareTo(PercentValue.Hundred) > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weightPercent), "Weight percent must be between 0 and 100.");
        }
    }

    private static string? NormalizeDescription(string? description)
    {
        var normalized = description?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}

/// <summary>
/// Represents a student's submission/attempt at an assessment
/// </summary>
public class AssessmentSubmission : EntityBase
{
    public Guid AssessmentId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? CourseGroupId { get; private set; }
    public int AttemptNumber { get; private set; }
    public ScoreValue? Score { get; private set; }
    public bool? Passed { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? GradedAt { get; private set; }
    public Guid? GradedBy { get; private set; }
    public string? Feedback { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public bool IsLate { get; private set; }
    public SubmissionModality SubmittedModalities { get; private set; }
    public string? TextPayload { get; private set; }
    public string? FilePayload { get; private set; }
    public string? UrlPayload { get; private set; }
    public string? CodePayload { get; private set; }
    public string? MediaPayload { get; private set; }
    public string? ProjectPayload { get; private set; }
    public string? RubricScoresPayload { get; private set; }

    private AssessmentSubmission() { } // EF Core

    public static AssessmentSubmission Start(Guid assessmentId, Guid enrollmentId, Guid userId, int attemptNumber)
    {
        return new AssessmentSubmission
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            EnrollmentId = enrollmentId,
            UserId = userId,
            AttemptNumber = attemptNumber,
            StartedAt = SystemClock.UtcNow,
            Status = SubmissionStatus.InProgress
        };
    }

    public void SetPayload(SubmitAssessmentRequest payload, SubmissionModality allowedModalities)
    {
        if (Status != SubmissionStatus.InProgress)
        {
            throw new InvalidOperationException("Submission payload can only be changed while in progress.");
        }

        var submittedModalities = SubmissionModality.None;
        var textPayload = NormalizePayload(payload.TextPayload, SubmissionModality.Text, ref submittedModalities);
        var filePayload = NormalizeBoundedPayload(payload.FilePayload, SubmissionModality.File, ref submittedModalities, nameof(payload.FilePayload));
        var urlPayload = NormalizeUrlPayload(payload.UrlPayload, SubmissionModality.Url, ref submittedModalities, nameof(payload.UrlPayload));
        var codePayload = NormalizePayload(payload.CodePayload, SubmissionModality.Code, ref submittedModalities);
        var mediaPayload = NormalizeUrlPayload(payload.MediaPayload, SubmissionModality.Media, ref submittedModalities, nameof(payload.MediaPayload));
        var projectPayload = NormalizeBoundedPayload(payload.ProjectPayload, SubmissionModality.Project, ref submittedModalities, nameof(payload.ProjectPayload));
        if (submittedModalities == SubmissionModality.None)
        {
            throw new ArgumentException("At least one submission payload is required.", nameof(payload));
        }

        if ((submittedModalities & ~allowedModalities) != 0)
        {
            throw new ArgumentException("Submission payload contains a modality that is not accepted by this assessment.", nameof(payload));
        }

        TextPayload = textPayload;
        FilePayload = filePayload;
        UrlPayload = urlPayload;
        CodePayload = codePayload;
        MediaPayload = mediaPayload;
        ProjectPayload = projectPayload;
        SubmittedModalities = submittedModalities;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Submit(bool isLate = false)
    {
        Submit(isLate, SystemClock.UtcNow);
    }

    public void Submit(bool isLate, DateTime submittedAt)
    {
        if (Status != SubmissionStatus.InProgress)
        {
            throw new InvalidOperationException("Only an in-progress submission can be submitted.");
        }

        SubmittedAt = submittedAt;
        IsLate = isLate;
        Status = isLate ? SubmissionStatus.Late : SubmissionStatus.Submitted;
        UpdatedAt = submittedAt;
    }

    internal void StampCourseGroup(Guid courseGroupId)
    {
        CourseGroupId = courseGroupId;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Grade(ScoreValue score, ScoreValue passingScore, ScoreValue maxScore, Guid? gradedBy = null, string? feedback = null)
    {
        if (maxScore.CompareTo(ScoreValue.Zero) <= 0 || passingScore.CompareTo(maxScore) > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxScore), "Assessment score bounds are invalid.");
        }

        if (score.CompareTo(ScoreValue.Zero) < 0 || score.CompareTo(maxScore) > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between zero and the assessment maximum.");
        }

        GradeCore(score, passingScore, gradedBy, feedback);
    }

    public void Grade(ScoreValue score, ScoreValue passingScore, ScoreValue maxScore, Guid? gradedBy, string? feedback, string? rubricScores)
    {
        Grade(score, passingScore, maxScore, gradedBy, feedback);
        RubricScoresPayload = rubricScores;
        UpdatedAt = SystemClock.UtcNow;
    }

    private void GradeCore(ScoreValue score, ScoreValue passingScore, Guid? gradedBy, string? feedback)
    {
        if (Status is not (SubmissionStatus.Submitted or SubmissionStatus.Late))
        {
            throw new InvalidOperationException("Only submitted submissions can be graded.");
        }

        Score = score;
        Passed = score.CompareTo(passingScore) >= 0;
        GradedAt = SystemClock.UtcNow;
        GradedBy = gradedBy;
        Feedback = feedback;
        Status = SubmissionStatus.Graded;
        UpdatedAt = SystemClock.UtcNow;
    }

    private static string? NormalizePayload(string? payload, SubmissionModality modality, ref SubmissionModality submittedModalities)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        submittedModalities |= modality;
        return payload;
    }

    private static string? NormalizeUrlPayload(
        string? payload,
        SubmissionModality modality,
        ref SubmissionModality submittedModalities,
        string parameterName)
    {
        var normalized = NormalizeBoundedPayload(payload, modality, ref submittedModalities, parameterName);
        if (normalized != null && (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
                                   (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException("Payload URL must be an absolute HTTP or HTTPS URL.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeBoundedPayload(
        string? payload,
        SubmissionModality modality,
        ref SubmissionModality submittedModalities,
        string parameterName)
    {
        var normalized = NormalizePayload(payload, modality, ref submittedModalities);
        if (normalized?.Length > 2048)
        {
            throw new ArgumentException("Payload cannot exceed 2048 characters.", parameterName);
        }

        return normalized;
    }

}

public enum AssessmentType
{
    Quiz = 0,
    Assignment = 2,
    Project = 3,
    PeerReview = 4,
    SelfAssessment = 5
}

public enum SubmissionStatus
{
    InProgress = 0,
    Submitted = 1,
    Graded = 2,
    Returned = 3,
    Late = 4
}

using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Learning.Grading.Contracts;


namespace GameGuild.Learning.Courses;

/// <summary>
/// Represents a user's interaction with program content, tracking progress and completion
/// </summary>
[Table("content_interactions")]
[Index(nameof(UserId))]
[Index(nameof(ContentId))]
[Index(nameof(ProgramUserId))]
[Index(nameof(IsCompleted))]
[Index(nameof(StartedAt))]
[Index(nameof(CompletedAt))]
[Index(nameof(TenantId))]
public class ContentInteraction : EntityBase
{
    /// <summary>
    /// User ID
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Content ID
    /// </summary>
    [Required]
    public Guid ContentId { get; set; }

    /// <summary>
    /// Program enrollment ID
    /// </summary>
    [Required]
    public Guid ProgramUserId { get; set; }

    /// <summary>
    /// Whether the content has been completed
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public PercentValue? ProgressPercentage { get; set; } = PercentValue.Zero;

    /// <summary>
    /// Time spent on this content in minutes
    /// </summary>
    public int? TimeSpentMinutes { get; set; } = 0;

    /// <summary>
    /// Exact accumulated active time. TimeSpentMinutes remains as a compatibility projection.
    /// </summary>
    public int TimeSpentSeconds { get; set; }

    /// <summary>
    /// When the user started this content
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the user completed this content
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Current status of the interaction
    /// </summary>
    public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;

    /// <summary>
    /// When the user submitted their work (for assignments)
    /// </summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>
    /// First time the user accessed this content
    /// </summary>
    public DateTime? FirstAccessedAt { get; set; }

    /// <summary>
    /// Last access timestamp
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Completion percentage (0-100) - alias for ProgressPercentage
    /// </summary>
    [NotMapped]
    public PercentValue CompletionPercentage
    {
        get => ProgressPercentage ?? PercentValue.Zero;
        set => ProgressPercentage = value;
    }

    /// <summary>
    /// Submission data (JSON format for assignments/activities)
    /// </summary>
    public string? SubmissionData { get; set; }

    /// <summary>
    /// Number of attempts (for quizzes/assignments)
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Best score achieved
    /// </summary>
    public ScoreValue? BestScore { get; set; }

    /// <summary>
    /// User notes/annotations for this content
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Bookmarked position (for video/text content)
    /// </summary>
    public string? BookmarkPosition { get; set; }

    // Navigation Properties
    /// <summary>
    /// User
    /// </summary>
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Program content
    /// </summary>
    public virtual ProgramContent Content { get; set; } = null!;

    /// <summary>
    /// Program enrollment
    /// </summary>
    public virtual ProgramUser ProgramUser { get; set; } = null!;

    /// <summary>
    /// Activity grades for this interaction
    /// </summary>
    public virtual ICollection<ActivityGrade> ActivityGrades { get; set; } = new List<ActivityGrade>();

    /// <summary>
    /// Fine-grained lesson and video interaction timeline.
    /// </summary>
    public virtual ICollection<ContentInteractionEvent> Events { get; set; } = new List<ContentInteractionEvent>();

    // Computed Properties
    /// <summary>
    /// Whether this interaction is global (tenant-independent)
    /// </summary>
    public override bool IsGlobal => TenantId == null;

    /// <summary>
    /// Whether the content has been started
    /// </summary>
    public bool IsStarted => StartedAt.HasValue;

    /// <summary>
    /// Whether the content is in progress
    /// </summary>
    public bool IsInProgress => IsStarted && !IsCompleted;

    /// <summary>
    /// Time since last access in days
    /// </summary>
    public int? DaysSinceLastAccess => LastAccessedAt.HasValue
        ? (SystemClock.UtcNow - LastAccessedAt.Value).Days
        : null;

    /// <summary>
    /// Duration of engagement (completed - started)
    /// </summary>
    public TimeSpan? EngagementDuration => StartedAt.HasValue && CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    // Domain Methods
    /// <summary>
    /// Starts the content interaction
    /// </summary>
    public void Start()
    {
        if (!StartedAt.HasValue)
        {
            StartedAt = SystemClock.UtcNow;
        }
        UpdateLastAccess();
    }

    /// <summary>
    /// Updates progress percentage
    /// </summary>
    public void UpdateProgress(PercentValue percentage)
    {
        if (IsCompleted)
        {
            Status = ProgressStatus.Completed;
            ProgressPercentage = PercentValue.Hundred;
            UpdateLastAccess();
            return;
        }

        ProgressPercentage = percentage;

        // Auto-complete if 100%
        if (ProgressPercentage == PercentValue.Hundred && !IsCompleted)
        {
            Complete();
        }

        UpdateLastAccess();
    }

    /// <summary>
    /// Completes the content interaction
    /// </summary>
    public void Complete()
    {
        IsCompleted = true;
        Status = ProgressStatus.Completed;
        CompletedAt ??= SystemClock.UtcNow;
        ProgressPercentage = PercentValue.Hundred;
        UpdateLastAccess();
    }

    /// <summary>
    /// Updates last access timestamp
    /// </summary>
    public void UpdateLastAccess()
    {
        LastAccessedAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Records time spent on content
    /// </summary>
    public void AddTimeSpent(int minutes)
    {
        if (minutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "Time spent must be positive.");
        }

        AddTimeSpentSeconds(checked(minutes * 60));
    }

    /// <summary>
    /// Records exact active time while maintaining the legacy whole-minute value.
    /// </summary>
    public void AddTimeSpentSeconds(int seconds)
    {
        if (seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "Time spent must be positive.");
        }

        TimeSpentSeconds = checked(TimeSpentSeconds + seconds);
        TimeSpentMinutes = TimeSpentSeconds / 60;
        UpdateLastAccess();
    }

    /// <summary>
    /// Increments attempt count (for assessments)
    /// </summary>
    public void RecordAttempt(ScoreValue? score = null)
    {
        AttemptCount++;

        if (score.HasValue && (!BestScore.HasValue || score.Value.CompareTo(BestScore.Value) > 0))
        {
            BestScore = score;
        }

        UpdateLastAccess();
    }

    /// <summary>
    /// Sets a bookmark position
    /// </summary>
    public void SetBookmark(string position)
    {
        BookmarkPosition = position;
        UpdateLastAccess();
    }

    /// <summary>
    /// Adds or updates user notes
    /// </summary>
    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        UpdateLastAccess();
    }

    /// <summary>
    /// Resets progress (for retaking content)
    /// </summary>
    public void Reset()
    {
        IsCompleted = false;
        CompletedAt = null;
        ProgressPercentage = PercentValue.Zero;
        TimeSpentMinutes = 0;
        TimeSpentSeconds = 0;
        AttemptCount = 0;
        BestScore = null;
        BookmarkPosition = null;
        UpdateLastAccess();
    }

}

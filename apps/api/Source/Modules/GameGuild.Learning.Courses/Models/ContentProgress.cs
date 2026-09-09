using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Learning.Grading.Contracts;
// using ContentEntity = GameGuild.Modules.Contents.Models.Content; // Module not yet implemented


namespace GameGuild.Learning.Courses;

/// <summary> Tracks user progress through individual content items in a program </summary>
[Table("content_progress")]
[Index(nameof(UserId), nameof(ContentId), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(ContentId))]
[Index(nameof(CompletionStatus))]
[Index(nameof(CompletedAt))]
public class ContentProgress : EntityBase {
  /// <summary> Foreign key to the User entity </summary>
  [Required]
  public Guid UserId { get; set; }

  /// <summary> Navigation property to the User entity </summary>
  [ForeignKey(nameof(UserId))]
  public virtual User User { get; set; } = null!;

  /// <summary> Foreign key to the Content entity </summary>
  [Required]
  public Guid ContentId { get; set; }

  /// <summary> Navigation property to the ProgramContent entity </summary>
  [ForeignKey(nameof(ContentId))]
  public virtual ProgramContent? Content { get; set; }

  /// <summary> Program enrollment this progress belongs to </summary>
  [Required]
  public Guid ProgramEnrollmentId { get; set; }

  /// <summary> Navigation property to the Program Enrollment </summary>
  [ForeignKey(nameof(ProgramEnrollmentId))]
  public virtual ProgramEnrollment ProgramEnrollment { get; set; } = null!;

  /// <summary> Content completion status </summary>
  public ContentCompletionStatus CompletionStatus { get; set; } = ContentCompletionStatus.NotStarted;

  /// <summary> Progress percentage for this content item (0-100) </summary>
  public PercentValue ProgressPercentage { get; set; } = PercentValue.Zero;

  /// <summary> When the user first accessed this content </summary>
  public DateTime? FirstAccessedAt { get; set; }

  /// <summary> When the user last accessed this content </summary>
  public DateTime? LastAccessedAt { get; set; }

  /// <summary> When the content was completed </summary>
  public DateTime? CompletedAt { get; set; }

  /// <summary> Time spent on this content (in seconds) </summary>
  public int TimeSpentSeconds { get; set; }

  /// <summary> Score/grade for this content (if applicable) </summary>
  public ScoreValue? Score { get; set; }

  /// <summary> Maximum possible score for this content </summary>
  public ScoreValue? MaxScore { get; set; }

  /// <summary> Number of attempts made on this content </summary>
  public int Attempts { get; set; }

  /// <summary> Additional progress data (JSON) For storing activity-specific progress data </summary>
  [Column(TypeName = "jsonb")]
  public string? ProgressData { get; set; }

  /// <summary> Mark content as accessed </summary>
  public void MarkAsAccessed() {
    if (FirstAccessedAt == null) {
      FirstAccessedAt = SystemClock.UtcNow;

      if (CompletionStatus == ContentCompletionStatus.NotStarted) { CompletionStatus = ContentCompletionStatus.InProgress; }
    }

    LastAccessedAt = SystemClock.UtcNow;
    Touch();
  }

  /// <summary> Mark content as completed </summary>
  public void MarkAsCompleted(ScoreValue? score = null, ScoreValue? maxScore = null) {
    CompletionStatus = ContentCompletionStatus.Completed;
    CompletedAt = SystemClock.UtcNow;
    ProgressPercentage = PercentValue.Hundred;

    if (score.HasValue) Score = score.Value;
    if (maxScore.HasValue) MaxScore = maxScore.Value;

    MarkAsAccessed();
  }

  /// <summary> Update progress percentage </summary>
  public void UpdateProgress(PercentValue progressPercentage) {
    ProgressPercentage = progressPercentage;

    if (ProgressPercentage == PercentValue.Hundred && CompletionStatus != ContentCompletionStatus.Completed) {
      CompletionStatus = ContentCompletionStatus.Completed;
      CompletedAt = SystemClock.UtcNow;
    }
    else if (ProgressPercentage.CompareTo(PercentValue.Zero) > 0 && CompletionStatus == ContentCompletionStatus.NotStarted) {
      CompletionStatus = ContentCompletionStatus.InProgress;
    }

    MarkAsAccessed();
  }

  /// <summary> Add time spent on this content </summary>
  public void AddTimeSpent(int seconds) {
    TimeSpentSeconds += seconds;
    MarkAsAccessed();
  }

  /// <summary> Increment attempt counter </summary>
  public void IncrementAttempts() {
    Attempts++;
    MarkAsAccessed();
  }
}

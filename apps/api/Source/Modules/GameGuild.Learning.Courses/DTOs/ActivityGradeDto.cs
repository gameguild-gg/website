using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary> DTO for ActivityGrade responses - avoids circular references for Swagger/OpenAPI </summary>
public class ActivityGradeDto {
  public Guid Id { get; set; }

  public Guid ContentInteractionId { get; set; }

  public Guid? GraderProgramUserId { get; set; }

  public ScoreValue? Points { get; set; }

  public ScoreValue? MaxPoints { get; set; }

  public PercentValue? PercentageScore { get; set; }

  public string? Feedback { get; set; }

  public string? GradingDetails { get; set; }

  public DateTime GradedAt { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  // Simplified nested objects to avoid circular references
  public ContentInteractionSummaryDto? ContentInteraction { get; set; }

  public GraderSummaryDto? Grader { get; set; }

  // Computed properties for convenience
  public bool IsPassingGrade => PercentageScore.HasValue &&
                                PercentageScore.Value.CompareTo(PercentValue.FromPercentage("70")) >= 0;

  public string? GradePercentage => PercentageScore.HasValue ? $"{PercentageScore.Value}%" : null;

  public bool HasFeedback { get => !string.IsNullOrEmpty(Feedback); }

  public bool HasGradingDetails { get => !string.IsNullOrEmpty(GradingDetails); }
}

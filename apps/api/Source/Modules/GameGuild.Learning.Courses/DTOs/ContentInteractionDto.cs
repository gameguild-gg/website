using System.Text.Json.Serialization;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary> DTO for ContentInteraction responses - avoids circular references for Swagger/OpenAPI </summary>
public class ContentInteractionDto {
  public Guid Id { get; set; }

  public Guid ProgramUserId { get; set; }

  public Guid ContentId { get; set; }

  public ProgressStatus Status { get; set; }

  public string? SubmissionData { get; set; }

  public PercentValue CompletionPercentage { get; set; }

  public int? TimeSpentMinutes { get; set; }

  public int TimeSpentSeconds { get; set; }

  public DateTime? FirstAccessedAt { get; set; }

  public DateTime? LastAccessedAt { get; set; }

  public DateTime? CompletedAt { get; set; }

  public DateTime? SubmittedAt { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  // Simplified nested objects to avoid circular references
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public ContentSummaryDto? Content { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public ProgramUserSummaryDto? ProgramUser { get; set; }

  // Computed properties for convenience
  public bool IsSubmitted { get => SubmittedAt.HasValue; }

  public bool IsCompleted { get => Status == ProgressStatus.Completed; }

  public bool CanModify { get => !IsSubmitted; }

  public int DurationInMinutes { get => TimeSpentMinutes ?? 0; }

  public int DurationInSeconds { get => TimeSpentSeconds; }
}

using System.ComponentModel.DataAnnotations;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary> DTO for creating new activity grades </summary>
public sealed record CreateActivityGradeDto(
  [Required] Guid ContentInteractionId,
  [Required] Guid GraderProgramUserId,
  ScoreValue Points,
  ScoreValue MaxPoints,
  string? Feedback = null,
  string? GradingDetails = null) {
  public Guid ContentInteractionId { get; init; } = ContentInteractionId;

  public Guid GraderProgramUserId { get; init; } = GraderProgramUserId;

  public ScoreValue Points { get; init; } = Points;

  public ScoreValue MaxPoints { get; init; } = MaxPoints;

  public string? Feedback { get; init; } = Feedback;

  public string? GradingDetails { get; init; } = GradingDetails;
}

using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// DTO for updating existing activity grades
/// </summary>
public sealed record UpdateActivityGradeDto(
  ScoreValue? Points = null,
  ScoreValue? MaxPoints = null,
  string? Feedback = null,
  string? GradingDetails = null) {
  public ScoreValue? Points { get; init; } = Points;

  public ScoreValue? MaxPoints { get; init; } = MaxPoints;

  public string? Feedback { get; init; } = Feedback;

  public string? GradingDetails { get; init; } = GradingDetails;
}

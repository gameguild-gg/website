using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

public sealed record UpdateProgramDto {
  public string? Title { get; init; }
  public string? Description { get; init; }
  public string? Metadata { get; init; }
  public string? Slug { get; init; }
  public string? Thumbnail { get; init; }
  public string? VideoShowcaseUrl { get; init; }
  public int? EstimatedHours { get; init; }
  public ContentVisibility? Visibility { get; init; }
  public ProgramCategory? Category { get; init; }
  public ProgramDifficulty? Difficulty { get; init; }
  public string? SkillsRequired { get; init; }
  public string? SkillsProvided { get; init; }
  public Guid? CreatorId { get; init; }
  public EnrollmentStatus? EnrollmentStatus { get; init; }
  public int? MaxEnrollments { get; init; }
  public DateTime? EnrollmentDeadline { get; init; }
  public PercentValue? PassingScore { get; init; }
  public bool ClearMaxEnrollments { get; init; }
  public bool ClearEnrollmentDeadline { get; init; }
}

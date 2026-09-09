using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

public sealed record ContentProgressDto(Guid ContentId, string Title, ProgressStatus Status, PercentValue CompletionPercentage, DateTime? FirstAccessedAt, DateTime? LastAccessedAt, DateTime? CompletedAt) {
  public Guid ContentId { get; init; } = ContentId;

  public string Title { get; init; } = Title;

  public ProgressStatus Status { get; init; } = Status;

  public PercentValue CompletionPercentage { get; init; } = CompletionPercentage;

  public DateTime? FirstAccessedAt { get; init; } = FirstAccessedAt;

  public DateTime? LastAccessedAt { get; init; } = LastAccessedAt;

  public DateTime? CompletedAt { get; init; } = CompletedAt;
}

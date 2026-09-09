
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

public sealed record UpdateProgressRequest(Guid ProgramUserId, Guid ContentId, PercentValue CompletionPercentage) {
  public Guid ProgramUserId { get; init; } = ProgramUserId;

  public Guid ContentId { get; init; } = ContentId;

  public PercentValue CompletionPercentage { get; init; } = CompletionPercentage;
}
